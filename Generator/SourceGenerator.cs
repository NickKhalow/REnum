#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace REnumSourceGenerator
{
    [Generator]
    public class REnumSourceGenerator : ISourceGenerator
    {
        private readonly ILogger? logger;
        private readonly Mode mode;

        public REnumSourceGenerator() : this(
            Environment.GetEnvironmentVariable("RENUM_CODE_GENERATION_FILE_LOGGING") != null
                ? new FileLogger()
                : null,
            Mode.OnFlight
        )
        {
        }

        public REnumSourceGenerator(ILogger? logger, Mode mode)
        {
            this.logger = logger;
            this.mode = mode;
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            logger?.Log("\nInitialize context");
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
            logger?.Log("\nInitialize finished");
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                ExecuteInternal(context);
            }
            catch (Exception e)
            {
                logger?.Log($"Exception occured: {e}");
            }
        }

        private void ExecuteInternal(GeneratorExecutionContext context)
        {
            logger?.Log("\n\nExecute context received");

            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            logger?.Log("Starting Execution");

            int processedCandidates = 0;
            int errorCandidates = 0;

            foreach (var candidate in receiver.Candidates)
                try
                {
                    ProcessCandidate(context, candidate);
                    processedCandidates++;
                }
                catch (Exception e)
                {
                    errorCandidates++;
                    logger?.Log($"Exception occured on candidate processing: {e}");
                }

            logger?.Log(
                $"Finished Execute, processed candidates {processedCandidates}, error candidates {errorCandidates}");
        }

        private void ProcessCandidate(GeneratorExecutionContext context, StructDeclarationSyntax? candidate)
        {
            logger?.Log($"Processing candidate: {candidate.Identifier.Text}");

            var model = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
            var symbol = ModelExtensions.GetDeclaredSymbol(model, candidate);
            if (!(symbol is INamedTypeSymbol structSymbol))
            {
                logger?.Log("Candidate is not a named type symbol, skipping");
                return;
            }

            var reEnumAttr = structSymbol.GetAttributes()
                .FirstOrDefault(attr =>
                    attr.AttributeClass?.ToDisplayString() == "REnum.REnumAttribute"
                    || attr.AttributeClass?.ToDisplayString() == "REnum"
                );
            if (reEnumAttr == null)
            {
                logger?.Log("Candidate does not have REnumAttribute, skipping");
                return;
            }

            logger?.Log("Found valid REnum struct");

            // Get the enum underlying type from the attribute (defaults to int)
            string enumBaseType = "int";
            if (reEnumAttr.ConstructorArguments.Length > 0 && reEnumAttr.ConstructorArguments[0].Value is int enumTypeValue)
            {
                enumBaseType = enumTypeValue switch
                {
                    0 => "int",      // EnumUnderlyingType.Int
                    1 => "byte",     // EnumUnderlyingType.Byte
                    2 => "sbyte",    // EnumUnderlyingType.SByte
                    3 => "short",    // EnumUnderlyingType.Short
                    4 => "ushort",   // EnumUnderlyingType.UShort
                    5 => "uint",     // EnumUnderlyingType.UInt
                    6 => "long",     // EnumUnderlyingType.Long
                    7 => "ulong",    // EnumUnderlyingType.ULong
                    _ => "int"
                };
            }


            AttributeData? reEnumPregeneratedAttr = structSymbol.GetAttributes()
                .FirstOrDefault(attr =>
                    attr.AttributeClass?.ToDisplayString() == "REnum.REnumPregeneratedAttribute"
                    || attr.AttributeClass?.ToDisplayString() == "REnumPregenerated"
                );

            switch (mode)
            {
                case Mode.Pregenerate when reEnumPregeneratedAttr == null:
                    logger?.Log("Candidate is not mark for pregeneration, skipping for Pregenerate mode");
                    return;
                case Mode.OnFlight when reEnumPregeneratedAttr != null:
                    logger?.Log("Candidate is mark for pregeneration, skipping for OnFlight mode");
                    return;
            }

            List<FieldInfo> variants = structSymbol.GetAttributes()
                .Where(attr =>
                    attr.AttributeClass?.ToDisplayString() == "REnum.REnumFieldAttribute"
                    || attr.AttributeClass?.ToDisplayString() == "REnumField"
                )
                .Select(attr =>
                {
                    if (attr.ConstructorArguments.IsEmpty)
                    {
                        TextSpan span = attr.ApplicationSyntaxReference.Span;
                        ReadOnlySpan<char> raw = attr.ApplicationSyntaxReference.SyntaxTree
                            .ToString()
                            .AsSpan().Slice(span.Start, span.Length);

                        int start = raw.IndexOf('(') + 1;
                        raw = raw.Slice(start);
                        start = raw.IndexOf('(') + 1;
                        raw = raw.Slice(start);

                        int end = raw.IndexOf(')');
                        raw = raw.Slice(0, end);
                        string output = raw.ToString()!;

                        return new FieldInfo(output, output);
                    }

                    object? value = attr.ConstructorArguments.FirstOrDefault().Value;
                    if (value is INamedTypeSymbol namedTypeSymbol)
                    {
                        // Check if there's a custom name provided (second argument)
                        string enumMemberName = namedTypeSymbol.Name;
                        if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is string customName && !string.IsNullOrEmpty(customName))
                        {
                            enumMemberName = customName;
                        }

                        return new FieldInfo(enumMemberName, namedTypeSymbol.ToDisplayString());
                    }

                    return null;
                })
                .Where(e => e != null)
                .ToList();

            logger?.Log($"Found {variants.Count} variants");

            List<string?> emptyFields = structSymbol.GetAttributes()
                .Where(attr =>
                    attr.AttributeClass?.ToDisplayString() == "REnum.REnumFieldEmptyAttribute"
                    || attr.AttributeClass?.ToDisplayString() == "REnumFieldEmpty"
                )
                .Select(attr =>
                {
                    if (attr.ConstructorArguments.IsEmpty)
                    {
                        TextSpan span = attr.ApplicationSyntaxReference.Span;
                        ReadOnlySpan<char> raw = attr.ApplicationSyntaxReference.SyntaxTree
                            .ToString()
                            .AsSpan().Slice(span.Start, span.Length);

                        int start = raw.IndexOf('"') + 1;
                        raw = raw.Slice(start);

                        int end = raw.IndexOf('"');
                        raw = raw.Slice(0, end);

                        string output = raw.ToString()!;
                        return output;
                    }

                    return attr.ConstructorArguments[0].Value?.ToString();
                })
                .ToList();

            logger?.Log($"Found {emptyFields.Count} empty fields");

            string unionName = structSymbol.Name;
            string ns = structSymbol.ContainingNamespace.ToDisplayString();
            const string kindEnum = "Kind";

            logger?.Log($"Generating code for {ns}.{unionName}");

            var sb = new StringBuilder();

            sb.AppendLine("#nullable enable");

            // copy namespaces
            IEnumerable<string> usingsList = candidate.SyntaxTree.GetCompilationUnitRoot().Usings.Select(u => u.ToString());
            foreach (string u in usingsList)
            {
                sb.AppendLine(u);
            }

            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial struct {unionName} : System.IEquatable<{unionName}>");
            sb.AppendLine("    {");

            // enum
            sb.AppendLine($"        public enum {kindEnum} : {enumBaseType}");
            sb.AppendLine("        {");
            foreach (var variant in variants)
                sb.AppendLine($"            {variant.Name},");
            foreach (var emptyField in emptyFields)
                sb.AppendLine($"            {emptyField},");
            sb.AppendLine("        }");

            sb.AppendLine($"        private readonly {kindEnum} _kind;");

            var fields = new Dictionary<FieldInfo, string>();

            foreach (var variant in variants)
            {
                string name = $"_{variant.Name.ToLower()}";
                fields[variant] = name;
                sb.AppendLine($"        private readonly {variant.ToDisplayString()} {name};");
            }

            logger?.Log("Generated enum and fields");

            // constructors
            sb.AppendLine($"        private {unionName}(");
            string kindParam = $"            {kindEnum} kind";
            if (variants.Count > 0)
            {
                sb.AppendLine($"{kindParam},");
            }
            else
            {
                sb.AppendLine(kindParam);
            }

            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                var typeName = variant.ToDisplayString();
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();

                string comma = i < variants.Count - 1 ? "," : "";
                sb.AppendLine($"            {typeName} {fieldLower} = default{comma}");
            }

            sb.AppendLine("        )");
            sb.AppendLine("        {");
            sb.AppendLine("            _kind = kind;");
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                var argName = variant.Name;
                var argLower = argName.ToLower();
                var fieldName = fields[variant];

                sb.AppendLine($"            {fieldName} = {argLower};");
            }

            sb.AppendLine("        }");

            logger?.Log("Generated constructor");

            foreach (var variant in variants)
            {
                var typeName = variant.ToDisplayString();
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();

                string kind = $"{kindEnum}.{fieldName}";

                sb.AppendLine(
                    $"        public static {unionName} From{fieldName}({typeName} value) => new {unionName}({kind}, {fieldLower}: value);"
                );
            }

            foreach (var emptyField in emptyFields)
            {
                string kind = $"{kindEnum}.{emptyField}";
                sb.AppendLine($"        public static {unionName} {emptyField}() => new {unionName}({kind});");
            }

            logger?.Log("Generated factory methods");

            // matchers
            foreach (var variant in variants)
            {
                var typeName = variant.ToDisplayString();
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();

                sb.AppendLine($"        public bool Is{fieldName}(out {typeName}? value)");
                sb.AppendLine("        {");
                sb.AppendLine($"            value = _{fieldLower};");
                sb.AppendLine($"            return _kind == {kindEnum}.{fieldName};");
                sb.AppendLine("        }");
            }

            foreach (var emptyField in emptyFields)
            {
                sb.AppendLine($"        public bool Is{emptyField}() => _kind == {kindEnum}.{emptyField};");
            }

            logger?.Log("Generated matchers");

            // Match method
            sb.AppendLine("        public T Match<TCtx, T>(");
            sb.AppendLine($"            TCtx ctx,");
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                var typeName = variant.ToDisplayString();
                var fieldName = variant.Name;
                var comma = i < variants.Count - 1 || emptyFields.Count > 0 ? "," : "";
                sb.AppendLine($"            System.Func<TCtx, {typeName}, T> on{fieldName}{comma}");
            }

            for (int i = 0; i < emptyFields.Count; i++)
            {
                var emptyField = emptyFields[i];
                var fieldName = emptyField;
                var comma = i < emptyFields.Count - 1 ? "," : "";
                sb.AppendLine($"            System.Func<TCtx, T> on{fieldName}{comma}");
            }

            sb.AppendLine("        )");
            sb.AppendLine("        {");
            sb.AppendLine("            return _kind switch");
            sb.AppendLine("            {");
            foreach (var variant in variants)
            {
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();
                sb.AppendLine(
                    $"                {kindEnum}.{fieldName} => on{fieldName}(ctx, _{fieldLower}),"
                );
            }

            foreach (var emptyField in emptyFields)
            {
                var fieldName = emptyField;
                sb.AppendLine($"                {kindEnum}.{fieldName} => on{fieldName}(ctx),");
            }

            sb.AppendLine("                _ => throw new System.InvalidOperationException()");
            sb.AppendLine("            };");
            sb.AppendLine("        }");

            logger?.Log("Generated Match with context");

            // Match method without TCtx
            sb.AppendLine("        public T Match<T>(");
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                var typeName = variant.ToDisplayString();
                var fieldName = variant.Name;
                var comma = i < variants.Count - 1 || emptyFields.Count > 0 ? "," : "";
                sb.AppendLine($"            System.Func<{typeName}, T> on{fieldName}{comma}");
            }

            for (int i = 0; i < emptyFields.Count; i++)
            {
                var emptyField = emptyFields[i];
                var fieldName = emptyField;
                var comma = i < emptyFields.Count - 1 ? "," : "";
                sb.AppendLine($"            System.Func<T> on{fieldName}{comma}");
            }

            sb.AppendLine("        )");
            sb.AppendLine("        {");
            sb.AppendLine("            return _kind switch");
            sb.AppendLine("            {");
            foreach (var variant in variants)
            {
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();
                sb.AppendLine(
                    $"                {kindEnum}.{fieldName} => on{fieldName}(_{fieldLower}),"
                );
            }

            foreach (var emptyField in emptyFields)
            {
                var fieldName = emptyField;
                sb.AppendLine($"                {kindEnum}.{fieldName} => on{fieldName}(),");
            }

            sb.AppendLine("                _ => throw new System.InvalidOperationException()");
            sb.AppendLine("            };");
            sb.AppendLine("        }");

            logger?.Log("Generated Match without context");

            sb.AppendLine("        public void Match<TCtx>(");
            sb.AppendLine($"            TCtx ctx,");
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                var typeName = variant.ToDisplayString();
                var fieldName = variant.Name;
                var comma = i < variants.Count - 1 || emptyFields.Count > 0 ? "," : "";
                sb.AppendLine($"            System.Action<TCtx, {typeName}> on{fieldName}{comma}");
            }

            for (int i = 0; i < emptyFields.Count; i++)
            {
                var emptyField = emptyFields[i];
                var fieldName = emptyField;
                var comma = i < emptyFields.Count - 1 ? "," : "";
                sb.AppendLine($"            System.Action<TCtx> on{fieldName}{comma}");
            }

            sb.AppendLine("        )");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (_kind)");
            sb.AppendLine("            {");
            foreach (var variant in variants)
            {
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();
                sb.AppendLine(
                    $"                case {kindEnum}.{fieldName}: on{fieldName}(ctx, _{fieldLower}); break;"
                );
            }

            foreach (var emptyField in emptyFields)
            {
                var fieldName = emptyField;
                sb.AppendLine($"                case {kindEnum}.{fieldName}: on{fieldName}(ctx); break;");
            }

            sb.AppendLine("                default: throw new System.InvalidOperationException();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            logger?.Log("Generated void Match with context");

            // Match void without context
            sb.AppendLine("        public void Match(");
            for (int i = 0; i < variants.Count; i++)
            {
                var variant = variants[i];
                var typeName = variant.ToDisplayString();
                var fieldName = variant.Name;
                var comma = i < variants.Count - 1 || emptyFields.Count > 0 ? "," : "";
                sb.AppendLine($"            System.Action<{typeName}> on{fieldName}{comma}");
            }

            for (int i = 0; i < emptyFields.Count; i++)
            {
                var emptyField = emptyFields[i];
                var fieldName = emptyField;
                var comma = i < emptyFields.Count - 1 ? "," : "";
                sb.AppendLine($"            System.Action on{fieldName}{comma}");
            }

            sb.AppendLine("        )");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (_kind)");
            sb.AppendLine("            {");
            foreach (var variant in variants)
            {
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();
                sb.AppendLine(
                    $"                case {kindEnum}.{fieldName}: on{fieldName}(_{fieldLower}); break;"
                );
            }

            foreach (var emptyField in emptyFields)
            {
                var fieldName = emptyField;
                sb.AppendLine($"                case {kindEnum}.{fieldName}: on{fieldName}(); break;");
            }

            sb.AppendLine("                default: throw new System.InvalidOperationException();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");

            logger?.Log("Generated void Match without context");

            // ToString
            sb.AppendLine("        public override string ToString() => _kind switch");
            sb.AppendLine("        {");
            foreach (var variant in variants)
            {
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();
                sb.AppendLine($"            {kindEnum}.{fieldName} => _{fieldLower}.ToString() ?? \"null\",");
            }

            foreach (var emptyField in emptyFields)
            {
                var fieldName = emptyField;
                sb.AppendLine($"            {kindEnum}.{fieldName} => \"{fieldName}\",");
            }

            sb.AppendLine("            _ => \"<invalid>\"");
            sb.AppendLine("        };");

            logger?.Log("Generated ToString");

            // Equals (typed)
            sb.AppendLine($"        public bool Equals({unionName} other)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_kind != other._kind) return false;");
            foreach (var variant in variants)
            {
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();
                sb.AppendLine($"            if (_kind == {kindEnum}.{fieldName})");
                sb.AppendLine(
                    $"                return System.Collections.Generic.EqualityComparer<{variant.ToDisplayString()}>.Default.Equals(_{fieldLower}, other._{fieldLower});"
                );
            }

            foreach (var emptyField in emptyFields)
            {
                var fieldName = emptyField;
                sb.AppendLine($"            if (_kind == {kindEnum}.{fieldName})");
                sb.AppendLine("                return true;");
            }

            sb.AppendLine("            return false;");
            sb.AppendLine("        }");

            logger?.Log("Generated typed Equals");

            // Equals (object override)
            sb.AppendLine("        public override bool Equals(object? obj) => obj is "
                          + unionName
                          + " other && Equals(other);");

            // GetHashCode
            sb.AppendLine("        public override int GetHashCode()");
            sb.AppendLine("        {");
            sb.AppendLine("            return _kind switch");
            sb.AppendLine("            {");
            foreach (var variant in variants)
            {
                var fieldName = variant.Name;
                var fieldLower = fieldName.ToLower();
                sb.AppendLine(
                    $"                {kindEnum}.{fieldName} => System.HashCode.Combine((int)_kind, _{fieldLower}),");
            }

            foreach (var emptyField in emptyFields)
            {
                var fieldName = emptyField;
                sb.AppendLine($"                {kindEnum}.{fieldName} => (int)_kind,");
            }

            sb.AppendLine("                _ => 0");
            sb.AppendLine("            };");
            sb.AppendLine("        }");

            logger?.Log("Generated GetHashCode");

            // Equality operators
            sb.AppendLine(
                $"        public static bool operator ==({unionName} left, {unionName} right) => left.Equals(right);");
            sb.AppendLine(
                $"        public static bool operator !=({unionName} left, {unionName} right) => !(left == right);");

            sb.AppendLine("    }"); // struct
            sb.AppendLine("}"); // namespace

            logger?.Log("Generated equality operators");

            context.AddSource($"{unionName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            logger?.Log($"Added source file {unionName}.g.cs");
        }


        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<StructDeclarationSyntax> Candidates { get; } = new List<StructDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is StructDeclarationSyntax structDecl
                    && structDecl.AttributeLists.Count > 0
                    && structDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    Candidates.Add(structDecl);
                }
            }
        }

        public interface ILogger
        {
            void Log(string message);
        }

        public class ConsoleLogger : ILogger
        {
            public void Log(string message)
            {
                Console.WriteLine(message);
            }
        }

        private class FileLogger : ILogger
        {
            public void Log(string message)
            {
                File.AppendAllText(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        $"renum-log-{Process.GetCurrentProcess().ProcessName}-{Process.GetCurrentProcess().Id}.txt"
                    ),
                    message + "\n"
                );
            }
        }

        private class FieldInfo
        {
            private readonly string typeName;
            public string Name { get; }

            public FieldInfo(string name, string typeName)
            {
                this.typeName = typeName;
                Name = name;
            }

            public string ToDisplayString()
            {
                return typeName;
            }
        }

        public enum Mode
        {
            OnFlight,
            Pregenerate
        }
    }
}