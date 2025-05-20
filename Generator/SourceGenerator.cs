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
        private readonly ILogger? logger = Environment.GetEnvironmentVariable("RENUM_CODE_GENERATION_FILE_LOGGING") != null 
            ? new FileLogger() 
            : null;

        public void Initialize(GeneratorInitializationContext context)
        {
            logger?.Log("\nInitialize context");
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
            logger?.Log("\nInitialize finished");
        }

        public void Execute(GeneratorExecutionContext context)
        {
            logger?.Log("\n\nExecute context received");

            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            logger?.Log("Starting Execution");

            foreach (var candidate in receiver.Candidates)
            {
                logger?.Log($"Processing candidate: {candidate.Identifier.Text}");

                var model = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
                var symbol = ModelExtensions.GetDeclaredSymbol(model, candidate);
                if (!(symbol is INamedTypeSymbol structSymbol)) {
                    logger?.Log("Candidate is not a named type symbol, skipping");
                    continue;
                }

                var reEnumAttr = structSymbol.GetAttributes().FirstOrDefault(
                    attr => attr.AttributeClass?.ToDisplayString() == "REnum.REnumAttribute"
                );
                if (reEnumAttr == null) {
                    logger?.Log("Candidate does not have REnumAttribute, skipping");
                    continue;
                }

                logger?.Log("Found valid REnum struct");

                List<INamedTypeSymbol> variants = structSymbol.GetAttributes()
                    .Where(attr => attr.AttributeClass?.ToDisplayString() == "REnum.REnumFieldAttribute")
                    .Select(attr => attr.ConstructorArguments.FirstOrDefault().Value)
                    .OfType<INamedTypeSymbol>()
                    .ToList();

                logger?.Log($"Found {variants.Count} variants");

                List<string?> emptyFields = structSymbol.GetAttributes()
                    .Where(attr => attr.AttributeClass?.ToDisplayString() == "REnum.REnumFieldEmptyAttribute")
                    .Select(attr => attr.ConstructorArguments[0].Value?.ToString())
                    .ToList();

                logger?.Log($"Found {emptyFields.Count} empty fields");

                string unionName = structSymbol.Name;
                string ns = structSymbol.ContainingNamespace.ToDisplayString();
                const string kindEnum = "Kind";

                logger?.Log($"Generating code for {ns}.{unionName}");

                var sb = new StringBuilder();

                sb.AppendLine("#nullable enable");
                sb.AppendLine($"namespace {ns} {{");
                sb.AppendLine($"public partial struct {unionName} {{");

                // enum
                sb.AppendLine($"    public enum {kindEnum} {{");
                foreach (var variant in variants)
                    sb.AppendLine($"        {variant.Name},");
                foreach (var emptyField in emptyFields)
                    sb.AppendLine($"        {emptyField},");
                sb.AppendLine("    }");

                sb.AppendLine($"    private readonly {kindEnum} _kind;");

                var fields = new Dictionary<INamedTypeSymbol, string>();

                foreach (var variant in variants)
                {
                    string name = $"_{variant.Name.ToLower()}";
                    fields[variant] = name;
                    sb.AppendLine($"    private readonly {variant.ToDisplayString()}? {name};");
                }

                logger?.Log("Generated enum and fields");

                // constructors
                sb.AppendLine($"private {unionName}(");
                sb.AppendLine($"{kindEnum} kind");
                sb.AppendLine(variants.Count > 0 ? "," : "");
                for (int i = 0; i < variants.Count; i++)
                {
                    var variant = variants[i];
                    var typeName = variant.ToDisplayString();
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();

                    string comma = i < variants.Count - 1 ? "," : "";
                    sb.AppendLine($"{typeName}? {fieldLower} = null{comma}");
                }
                sb.AppendLine("){");
                sb.AppendLine("_kind = kind;");
                for (int i = 0; i < variants.Count; i++)
                {
                    var variant = variants[i];
                    var argName = variant.Name;
                    var argLower = argName.ToLower();
                    var fieldName = fields[variant];

                    sb.AppendLine($"{fieldName} = {argLower};");
                }
                sb.AppendLine("}");

                logger?.Log("Generated constructor");

                foreach (var variant in variants)
                {
                    var typeName = variant.ToDisplayString();
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();

                    string kind = $"{kindEnum}.{fieldName}";

                    sb.AppendLine(
                        $"    public static {unionName} From{fieldName}({typeName} value) => new {unionName}({kind}, {fieldLower}: value);"
                    );
                }

                foreach (var emptyField in emptyFields)
                {
                    string kind = $"{kindEnum}.{emptyField}";
                    sb.AppendLine($"    public static {unionName} {emptyField}() => new {unionName}({kind});");
                }

                logger?.Log("Generated factory methods");

                // matchers
                foreach (var variant in variants)
                {
                    var typeName = variant.ToDisplayString();
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();

                    sb.AppendLine($"    public bool Is{fieldName}(out {typeName}? value) {{");
                    sb.AppendLine($"        value = _{fieldLower};");
                    sb.AppendLine($"        return _kind == {kindEnum}.{fieldName};");
                    sb.AppendLine("    }");
                }

                foreach (var emptyField in emptyFields)
                {
                    sb.AppendLine($"    public bool Is{emptyField}() => _kind == {kindEnum}.{emptyField};");
                }

                logger?.Log("Generated matchers");

                // Match method
                sb.AppendLine("    public T Match<TCtx, T>(");
                sb.AppendLine($"        TCtx ctx,");
                for (int i = 0; i < variants.Count; i++)
                {
                    var variant = variants[i];
                    var typeName = variant.ToDisplayString();
                    var fieldName = variant.Name;
                    var comma = i < variants.Count - 1 || emptyFields.Count > 0 ? "," : "";
                    sb.AppendLine($"        System.Func<TCtx, {typeName}, T> on{fieldName}{comma}");
                }
                for (int i = 0; i < emptyFields.Count; i++)
                {
                    var emptyField = emptyFields[i];
                    var fieldName = emptyField;
                    var comma = i < emptyFields.Count - 1 ? "," : "";
                    sb.AppendLine($"        System.Func<TCtx, T> on{fieldName}{comma}");
                }
                sb.AppendLine("    )");
                sb.AppendLine("    {");
                sb.AppendLine("        return _kind switch");
                sb.AppendLine("        {");
                foreach (var variant in variants)
                {
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();
                    sb.AppendLine(
                        variant.IsValueType
                            ? $"            {kindEnum}.{fieldName} => on{fieldName}(ctx, _{fieldLower}!.Value),"
                            : $"            {kindEnum}.{fieldName} => on{fieldName}(ctx, _{fieldLower}),"
                    );
                }
                foreach (var emptyField in emptyFields)
                {
                    var fieldName = emptyField;
                    sb.AppendLine($"            {kindEnum}.{fieldName} => on{fieldName}(ctx),");
                }
                sb.AppendLine("            _ => throw new System.InvalidOperationException()");
                sb.AppendLine("        };");
                sb.AppendLine("    }");

                logger?.Log("Generated Match with context");

                // Match method without TCtx
                sb.AppendLine("    public T Match<T>(");
                for (int i = 0; i < variants.Count; i++)
                {
                    var variant = variants[i];
                    var typeName = variant.ToDisplayString();
                    var fieldName = variant.Name;
                    var comma = i < variants.Count - 1 || emptyFields.Count > 0 ? "," : "";
                    sb.AppendLine($"        System.Func<{typeName}, T> on{fieldName}{comma}");
                }
                for (int i = 0; i < emptyFields.Count; i++)
                {
                    var emptyField = emptyFields[i];
                    var fieldName = emptyField;
                    var comma = i < emptyFields.Count - 1 ? "," : "";
                    sb.AppendLine($"        System.Func<T> on{fieldName}{comma}");
                }
                sb.AppendLine("    )");
                sb.AppendLine("    {");
                sb.AppendLine("        return _kind switch");
                sb.AppendLine("        {");
                foreach (var variant in variants)
                {
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();
                    sb.AppendLine(
                        variant.IsValueType
                            ? $"            {kindEnum}.{fieldName} => on{fieldName}(_{fieldLower}!.Value),"
                            : $"            {kindEnum}.{fieldName} => on{fieldName}(_{fieldLower}),"
                    );
                }
                foreach (var emptyField in emptyFields)
                {
                    var fieldName = emptyField;
                    sb.AppendLine($"            {kindEnum}.{fieldName} => on{fieldName}(),");
                }
                sb.AppendLine("            _ => throw new System.InvalidOperationException()");
                sb.AppendLine("        };");
                sb.AppendLine("    }");

                logger?.Log("Generated Match without context");

                // ToString
                sb.AppendLine("    public override string ToString() => _kind switch");
                sb.AppendLine("    {");
                foreach (var variant in variants)
                {
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();
                    sb.AppendLine($"        {kindEnum}.{fieldName} => _{fieldLower}?.ToString() ?? \"null\",");
                }
                foreach (var emptyField in emptyFields)
                {
                    var fieldName = emptyField;
                    sb.AppendLine($"        {kindEnum}.{fieldName} => \"{fieldName}\",");
                }
                sb.AppendLine("        _ => \"<invalid>\"");
                sb.AppendLine("    };");

                logger?.Log("Generated ToString");

                // Equals (typed)
                sb.AppendLine($"    public bool Equals({unionName} other)");
                sb.AppendLine("    {");
                sb.AppendLine("        if (_kind != other._kind) return false;");
                foreach (var variant in variants)
                {
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();
                    var nullable = variant.IsValueType ? "?" : "";
                    sb.AppendLine($"        if (_kind == {kindEnum}.{fieldName})");
                    sb.AppendLine(
                        $"            return System.Collections.Generic.EqualityComparer<{variant.ToDisplayString()}{nullable}>.Default.Equals(_{fieldLower}, other._{fieldLower});"
                    );
                }
                foreach (var emptyField in emptyFields)
                {
                    var fieldName = emptyField;
                    sb.AppendLine($"        if (_kind == {kindEnum}.{fieldName})");
                    sb.AppendLine("            return true;");
                }
                sb.AppendLine("        return false;");
                sb.AppendLine("    }");

                logger?.Log("Generated typed Equals");

                // Equals (object override)
                sb.AppendLine("    public override bool Equals(object? obj) => obj is " + unionName + " other && Equals(other);");

                // GetHashCode
                sb.AppendLine("    public override int GetHashCode()");
                sb.AppendLine("    {");
                sb.AppendLine("        return _kind switch");
                sb.AppendLine("        {");
                foreach (var variant in variants)
                {
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();
                    sb.AppendLine($"            {kindEnum}.{fieldName} => System.HashCode.Combine((int)_kind, _{fieldLower}),");
                }
                foreach (var emptyField in emptyFields)
                {
                    var fieldName = emptyField;
                    sb.AppendLine($"            {kindEnum}.{fieldName} => (int)_kind,");
                }
                sb.AppendLine("            _ => 0");
                sb.AppendLine("        };");
                sb.AppendLine("    }");

                logger?.Log("Generated GetHashCode");

                // Equality operators
                sb.AppendLine($"    public static bool operator ==({unionName} left, {unionName} right) => left.Equals(right);");
                sb.AppendLine($"    public static bool operator !=({unionName} left, {unionName} right) => !(left == right);");

                sb.AppendLine("}"); // struct
                sb.AppendLine("}"); // namespace

                logger?.Log("Generated equality operators");

                context.AddSource($"{unionName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
                logger?.Log($"Added source file {unionName}.g.cs");
            }

            logger?.Log("Finished Execute");
        }


        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<StructDeclarationSyntax> Candidates { get; } = new List<StructDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is StructDeclarationSyntax structDecl &&
                    structDecl.AttributeLists.Count > 0 &&
                    structDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    Candidates.Add(structDecl);
                }
            }
        }

        private interface ILogger
        {
            void Log(string message);
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
    }
}