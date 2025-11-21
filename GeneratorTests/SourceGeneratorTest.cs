using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using REnumSourceGenerator;

namespace REnum.GeneratorTests;

public class SourceGeneratorTest
{
    [Test]
    public void InvalidEnumUnderlyingType_ProducesWarning()
    {
        // Create a compilation with invalid enum underlying type
        string sourceCode = @"
using REnum;

namespace TestNamespace
{
    [REnum((EnumUnderlyingType)99)] // Invalid value
    public partial struct TestEnum
    {
    }
}";

        var compilation = CreateCompilation(sourceCode);
        var generator = new REnumSourceGenerator.REnumSourceGenerator();

        // Run the generator
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Get diagnostics from the generator run result
        var runResult = driver.GetRunResult();
        var generatorDiagnostics = runResult.Diagnostics;

        // Look for warning in generator diagnostics
        var warning = generatorDiagnostics.FirstOrDefault(d => d.Id == "RENUM001");

        // Assert that we have the warning
        Assert.That(warning, Is.Not.Null, "Expected RENUM001 warning to be reported");
        Assert.That(warning!.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        Assert.That(warning.GetMessage(), Does.Contain("Invalid enum underlying type value '99'"));
        Assert.That(warning.GetMessage(), Does.Contain("Defaulting to 'int'"));
    }

    [Test]
    public void ValidEnumUnderlyingType_NoWarning()
    {
        // Create a compilation with valid enum underlying type
        string sourceCode = @"
using REnum;

namespace TestNamespace
{
    [REnum(EnumUnderlyingType.Byte)] // Valid value
    [REnumFieldEmpty(""Empty"")]
    public partial struct TestEnum
    {
    }
}";

        var compilation = CreateCompilation(sourceCode);
        var generator = new REnumSourceGenerator.REnumSourceGenerator();

        // Run the generator
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Get diagnostics from the generator run result
        var runResult = driver.GetRunResult();
        var generatorDiagnostics = runResult.Diagnostics;

        // Assert that we don't have the RENUM001 warning
        var warning = generatorDiagnostics.FirstOrDefault(d => d.Id == "RENUM001");
        Assert.That(warning, Is.Null, "Did not expect RENUM001 warning for valid enum type");
    }

    private static Compilation CreateCompilation(string source)
    {
        // Get the REnum assembly reference (contains both attributes and EnumUnderlyingType)
        var renumAssembly = typeof(REnumAttribute).Assembly;
        var renumReference = MetadataReference.CreateFromFile(renumAssembly.Location);

        // Create the syntax tree
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Get references for basic .NET types
        var references = new List<MetadataReference>
        {
            renumReference,
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        // Add System.Runtime if available
        var systemRuntime = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (systemRuntime != null)
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntime.Location));
        }

        // Add netstandard reference for source generator compatibility
        var netstandardAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "netstandard");
        if (netstandardAssembly != null)
        {
            references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
        }

        // Create the compilation
        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
    }
}
