using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace OpenSettlers.Generators;

/// <summary>
/// Writes <c>JobTablesRegistry</c> (ADR 0016), which builds every tables module of the compilation that declares
/// <c>OpenSettlers.Simulation.JobTables&lt;TJob&gt;</c>, and lists the job and state names the modules state with
/// <c>JobNameAttribute</c> and <c>StateNameAttribute</c>. A module with no accessible parameterless constructor, a second module for
/// one job, and two modules that name one job or one state are build errors.
/// </summary>
[Generator]
public sealed class JobTablesGenerator : IIncrementalGenerator
{
    private const string TablesMetadataName = "OpenSettlers.Simulation.JobTables`1";

    private static readonly DiagnosticDescriptor NoConstructor = new(
        "TAB001",
        "A job tables module has no constructor",
        "The job tables module '{0}' has no accessible parameterless constructor",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor SecondModule = new(
        "TAB002",
        "A job has two tables modules",
        "The job tables modules '{0}' and '{1}' are both for the job '{2}'",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor SharedJobName = new(
        "TAB003",
        "Two job tables modules name one job",
        "The job tables modules '{0}' and '{1}' both name the job '{2}'",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor SharedStateName = new(
        "TAB004",
        "Two job tables modules name one state",
        "The job tables modules '{0}' and '{1}' both name the state {2}",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var modules = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (syntax, token) => Describe(syntax, token)
            )
            .Where(static module => module is not null)
            .Select(static (module, _) => module!)
            .Collect();
        context.RegisterSourceOutput(modules, static (output, found) => Generate(output, found));
    }

    private static Module? Describe(GeneratorSyntaxContext syntax, System.Threading.CancellationToken token)
    {
        var declaration = (ClassDeclarationSyntax)syntax.Node;
        var tables = syntax.SemanticModel.Compilation.GetTypeByMetadataName(TablesMetadataName);
        if (
            tables is null
            || tables.Locations.All(location => !location.IsInSource)
            || syntax.SemanticModel.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol type
            || type.IsAbstract
        )
        {
            return null;
        }

        INamedTypeSymbol? job = null;
        for (var parent = type.BaseType; parent is not null && job is null; parent = parent.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(parent.OriginalDefinition, tables))
            {
                job = parent.TypeArguments[0] as INamedTypeSymbol;
            }
        }

        if (job is null)
        {
            return null;
        }

        var jobName = Attributes(type, "JobNameAttribute", tables)
            .Select(attribute => Name(attribute, 0))
            .FirstOrDefault();
        var states = Attributes(type, "StateNameAttribute", tables)
            .Select(attribute => new Named((int)attribute.ConstructorArguments[0].Value!, Name(attribute, 1)))
            .ToArray();
        return new Module(
            type.ToDisplayString(),
            job.ToDisplayString(),
            tables.ContainingNamespace.ToDisplayString(),
            type.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Length == 0
                && constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
            ),
            jobName,
            new Sequence<Named>(states),
            Place.Of(declaration.Identifier.GetLocation())
        );
    }

    private static IEnumerable<AttributeData> Attributes(INamedTypeSymbol type, string name, INamedTypeSymbol tables) =>
        type.GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass is { } attributeClass
                && attributeClass.Name == name
                && attributeClass.ContainingNamespace.ToDisplayString() == tables.ContainingNamespace.ToDisplayString()
            );

    // The name and the evidence of an attribute: its two arguments from the index <paramref name="first"/>.
    private static Text Name(AttributeData attribute, int first) =>
        new(
            (string)attribute.ConstructorArguments[first].Value!,
            (string)attribute.ConstructorArguments[first + 1].Value!
        );

    private static void Generate(SourceProductionContext output, ImmutableArray<Module> found)
    {
        if (found.Length == 0)
        {
            return;
        }

        // A partial module has a declaration per part: the first part by file and position stands for it.
        var candidates = found
            .OrderBy(module => module.Name, System.StringComparer.Ordinal)
            .ThenBy(module => module.Place.Path, System.StringComparer.Ordinal)
            .ThenBy(module => module.Place.Span.Start)
            .GroupBy(module => module.Name)
            .Select(parts => parts.First())
            .OrderBy(module => module.Job, System.StringComparer.Ordinal)
            .ThenBy(module => module.Name, System.StringComparer.Ordinal)
            .ToList();

        var kept = new List<Module>();
        var jobs = new Dictionary<string, Module>();
        var jobNames = new Dictionary<string, Module>();
        var states = new Dictionary<int, Module>();
        foreach (var module in candidates)
        {
            if (!module.HasConstructor)
            {
                output.ReportDiagnostic(Diagnostic.Create(NoConstructor, module.Place.ToLocation(), module.Name));
                continue;
            }

            if (jobs.TryGetValue(module.Job, out var other))
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(SecondModule, module.Place.ToLocation(), other.Name, module.Name, module.Job)
                );
                continue;
            }

            if (module.JobName is { } jobName && jobNames.TryGetValue(jobName.Name, out other))
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(SharedJobName, module.Place.ToLocation(), other.Name, module.Name, jobName.Name)
                );
                continue;
            }

            var clash = module.States.Items.FirstOrDefault(state => states.ContainsKey(state.Code));
            if (clash is not null)
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(
                        SharedStateName,
                        module.Place.ToLocation(),
                        states[clash.Code].Name,
                        module.Name,
                        clash.Code
                    )
                );
                continue;
            }

            jobs[module.Job] = module;
            if (module.JobName is { } name)
            {
                jobNames[name.Name] = module;
            }

            foreach (var state in module.States.Items)
            {
                states[state.Code] = module;
            }

            kept.Add(module);
        }

        output.AddSource("JobTablesRegistry.g.cs", SourceText.From(Write(found[0].Namespace, kept), Encoding.UTF8));
    }

    private static string Write(string ns, List<Module> modules)
    {
        var text = new StringBuilder();
        text.AppendLine("// <auto-generated/>");
        text.AppendLine("#nullable enable");
        text.AppendLine($"namespace {ns};");
        text.AppendLine();
        text.AppendLine(
            "/// <summary>The tables modules of the jobs and the names they state, written from the module types.</summary>"
        );
        text.AppendLine("internal static class JobTablesRegistry");
        text.AppendLine("{");
        text.AppendLine(
            "    /// <summary>A new instance of each tables module, in the order of their job types' names.</summary>"
        );
        text.AppendLine(
            "    internal static global::System.Collections.Generic.IReadOnlyList<IJobTables> Modules() =>"
        );
        text.AppendLine("    [");
        foreach (var module in modules)
        {
            text.AppendLine($"        new global::{module.Name}(),");
        }

        text.AppendLine("    ];");
        text.AppendLine();
        text.AppendLine(
            "    /// <summary>The job names the modules state: the job's number, the name, and the evidence.</summary>"
        );
        text.AppendLine("    internal static readonly (int Code, string Name, string Evidence)[] JobNames =");
        text.AppendLine("    [");
        foreach (var module in modules.Where(module => module.JobName is not null))
        {
            text.AppendLine(
                $"        (global::{module.Job}.Number, {Literal(module.JobName!.Name)}, {Literal(module.JobName.Evidence)}),"
            );
        }

        text.AppendLine("    ];");
        text.AppendLine();
        text.AppendLine(
            "    /// <summary>The state names the modules state: the state's code, the name, and the evidence.</summary>"
        );
        text.AppendLine("    internal static readonly (int Code, string Name, string Evidence)[] StateNames =");
        text.AppendLine("    [");
        foreach (var state in modules.SelectMany(module => module.States.Items).OrderBy(state => state.Code))
        {
            text.AppendLine($"        ({state.Code}, {Literal(state.Text.Name)}, {Literal(state.Text.Evidence)}),");
        }

        text.AppendLine("    ];");
        text.AppendLine("}");
        return text.ToString();
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    /// <summary>Where a declaration's name stands, kept as values so that a model of it compares equal across compilations.</summary>
    private sealed record Place(string Path, TextSpan Span, LinePositionSpan Lines)
    {
        public static Place Of(Location location)
        {
            var line = location.GetLineSpan();
            return new Place(line.Path, location.SourceSpan, line.Span);
        }

        public Location ToLocation() => Location.Create(Path, Span, Lines);
    }

    /// <summary>A name with its evidence.</summary>
    private sealed record Text(string Name, string Evidence);

    /// <summary>A state the module names.</summary>
    private sealed record Named(int Code, Text Text);

    /// <summary>A list that compares by its items, so that a model holding it compares equal across compilations.</summary>
    private sealed class Sequence<T>(T[] items) : System.IEquatable<Sequence<T>>
    {
        public T[] Items { get; } = items;

        public bool Equals(Sequence<T>? other) => other is not null && Items.SequenceEqual(other.Items);

        public override bool Equals(object? obj) => Equals(obj as Sequence<T>);

        public override int GetHashCode() =>
            Items.Aggregate(17, (hash, item) => hash * 31 + (item?.GetHashCode() ?? 0));
    }

    /// <summary>What the generator keeps of a module: its type, its job type, its names, and where it stands.</summary>
    private sealed record Module(
        string Name,
        string Job,
        string Namespace,
        bool HasConstructor,
        Text? JobName,
        Sequence<Named> States,
        Place Place
    );
}
