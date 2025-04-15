using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace REnum.Generators
{
    [Generator]
    public class REnumGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            foreach (var candidate in receiver.Candidates)
            {
                var model = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(candidate);
                if (symbol is not INamedTypeSymbol structSymbol) continue;

                var reEnumAttr = structSymbol.GetAttributes().FirstOrDefault(
                    attr =>
                        attr.AttributeClass?.Name == "REnumAttribute"
                );
                if (reEnumAttr == null) continue;

                var variantAttrs = structSymbol.GetAttributes().Where(
                    attr =>
                        attr.AttributeClass?.Name == "REnumFieldAttribute"
                ).ToList();

                var variants = variantAttrs
                    .Select(attr => attr.ConstructorArguments[0].Value as INamedTypeSymbol)
                    .Where(t => t != null)
                    .ToList();

                var unionName = structSymbol.Name;
                var ns = structSymbol.ContainingNamespace.ToDisplayString();
                var kindEnum = "Kind";

                var sb = new StringBuilder();
                sb.AppendLine($"namespace {ns} {{");
                sb.AppendLine($"public partial struct {unionName} {{");

                // enum
                sb.AppendLine($"    public enum {kindEnum} {{");
                foreach (var variant in variants)
                    sb.AppendLine($"        {variant.Name},");
                sb.AppendLine("    }");

                sb.AppendLine($"    private readonly {kindEnum} _kind;");
                foreach (var variant in variants)
                    sb.AppendLine($"    private readonly {variant.ToDisplayString()}? _{variant.Name.ToLower()};");

                // constructors
                foreach (var variant in variants)
                {
                    var typeName = variant.ToDisplayString();
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();

                    sb.AppendLine(
                        $"    public static {unionName} From{fieldName}({typeName} value) => new {unionName} {{ _kind = {kindEnum}.{fieldName}, _{fieldLower} = value }};"
                    );
                }

                // matchers
                foreach (var variant in variants)
                {
                    var typeName = variant.ToDisplayString();
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();

                    sb.AppendLine($"    public bool Is{fieldName}(out {typeName} value) {{");
                    sb.AppendLine($"        value = _{fieldLower}!.Value;");
                    sb.AppendLine($"        return _kind == {kindEnum}.{fieldName};");
                    sb.AppendLine("    }");
                }

                // Match method
                sb.AppendLine("    public T Match<T>(");
                for (int i = 0; i < variants.Count; i++)
                {
                    var variant = variants[i];
                    var typeName = variant.ToDisplayString();
                    var fieldName = variant.Name;
                    var comma = i < variants.Count - 1 ? "," : "";
                    sb.AppendLine($"        Func<{typeName}, T> on{fieldName}{comma}");
                }
                sb.AppendLine("    )");
                sb.AppendLine("    {");
                sb.AppendLine("        return _kind switch");
                sb.AppendLine("        {");
                foreach (var variant in variants)
                {
                    var fieldName = variant.Name;
                    var fieldLower = fieldName.ToLower();
                    sb.AppendLine($"            {kindEnum}.{fieldName} => on{fieldName}(_{fieldLower}!.Value),");
                }
                sb.AppendLine("            _ => throw new System.InvalidOperationException()");
                sb.AppendLine("        };");
                sb.AppendLine("    }");

                sb.AppendLine("}"); // struct
                sb.AppendLine("}"); // namespace
                
                context.AddSource($"{unionName}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }


        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<StructDeclarationSyntax> Candidates { get; } = new();

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
    }
}