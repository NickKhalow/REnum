using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// dotnet run -- <inputDir> <outputDir>
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: renum-cli <inputDir> <outputDir>\nor\ndotnet run -- <inputDir> <outputDir>");
    return 1;
}

var inputDir = Path.GetFullPath(args[0]);
var outputDir = Path.GetFullPath(args[1]);

if (Directory.Exists(outputDir))
{
    Directory.Delete(outputDir, recursive: true);
}

Directory.CreateDirectory(outputDir);

var csFiles = Directory.GetFiles(inputDir, "*.cs", SearchOption.AllDirectories);
if (csFiles.Length == 0)
{
    Console.Error.WriteLine($"No .cs files found in {inputDir}");
    return 1;
}

var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
var syntaxTrees = csFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), parseOptions, f));

var compilation = CSharpCompilation.Create(
    assemblyName: "RenumGenTemp",
    syntaxTrees: syntaxTrees,
    references: MakeRefs().Append(MetadataReference.CreateFromFile(typeof(object).Assembly.Location)).ToList(),
    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
);

var generator = new REnumSourceGenerator.REnumSourceGenerator(
    new REnumSourceGenerator.REnumSourceGenerator.ConsoleLogger(),
    REnumSourceGenerator.REnumSourceGenerator.Mode.Pregenerate
);
GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);

var runResult = driver.GetRunResult();
var genResult = runResult.Results.Single(r => r.Generator is REnumSourceGenerator.REnumSourceGenerator);

if (genResult.GeneratedSources.IsEmpty)
{
    Console.WriteLine("No files were generated");
    return 0;
}

foreach (var source in genResult.GeneratedSources)
{
    var filePath = Path.Combine(outputDir, source.HintName);
    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
    File.Delete(filePath);
    File.WriteAllText(filePath, source.SourceText.ToString());
    Console.WriteLine($"Wrote: {filePath}");
}

// Print any generator diagnostics
foreach (var d in diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning))
    Console.WriteLine(d.ToString());

return 0;

static List<MetadataReference> MakeRefs()
{
    List<MetadataReference> refs =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location), // System.Private.CoreLib
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location), // System.Linq
        MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location), // System.Runtime
        MetadataReference.CreateFromFile(typeof(Console).Assembly.Location), // System.Console
        MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
        // required attrs else can be added later

        // the assembly with the attrs
        MetadataReference.CreateFromFile(typeof(REnum.REnumAttribute).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(REnum.REnumFieldEmptyAttribute).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(REnum.REnumFieldAttribute).Assembly.Location)
    ];

    return refs;
}