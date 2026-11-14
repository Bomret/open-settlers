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
/// <c>JobNameAttribute</c> and <c>StateNameAttribute</c>. A generic module, a module the registry cannot reach, a module with no
/// accessible parameterless constructor, a second module for
/// one job, two modules that name one job or one state, a module that names one state twice, and a module that names a state the
/// assembly's <c>CoreStateCodesAttribute</c> lists are build errors.
/// </summary>
[Generator]
public sealed class JobTablesGenerator : IIncrementalGenerator
{
    const string TablesMetadataName = "OpenSettlers.Simulation.JobTables`1";

    static readonly DiagnosticDescriptor NoConstructor = new(
        "TAB001",
        "A job tables module has no constructor",
        "The job tables module '{0}' has no accessible parameterless constructor",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor SecondModule = new(
        "TAB002",
        "A job has two tables modules",
        "The job tables modules '{0}' and '{1}' are both for the job '{2}'",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor SharedJobName = new(
        "TAB003",
        "Two job tables modules name one job",
        "The job tables modules '{0}' and '{1}' both name the job '{2}'",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor SharedStateName = new(
        "TAB004",
        "Two job tables modules name one state",
        "The job tables modules '{0}' and '{1}' both name the state {2}",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor StateNamedTwice = new(
        "TAB005",
        "A job tables module names one state twice",
        "The job tables module '{0}' names the state {1} twice",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor CoreState = new(
        "TAB006",
        "A job tables module names a state of the core",
        "The job tables module '{0}' names the state {1}, which the name table names itself",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor GenericModule = new(
        "TAB007",
        "A job tables module is generic",
        "The job tables module '{0}' is generic, or nested in a generic type, which the registry cannot build",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor UnreachableModule = new(
        "TAB008",
        "A job tables module is not reachable from the registry",
        "The job tables module '{0}' is private or protected, or nested in a type that is, so the registry cannot build it",
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
        var core = context.CompilationProvider.Select(
            static (compilation, _) =>
                new Sequence<int>([
                    .. compilation
                        .Assembly.GetAttributes()
                        .Where(attribute =>
                            attribute.AttributeClass?.ToDisplayString()
                            == "OpenSettlers.Simulation.CoreStateCodesAttribute"
                        )
                        .SelectMany(attribute => attribute.ConstructorArguments[0].Values)
                        .Select(value => (int)value.Value!),
                ])
        );
        context.RegisterSourceOutput(
            modules.Combine(core),
            static (output, found) => Generate(output, found.Left, found.Right.Items)
        );
    }

    static Module? Describe(GeneratorSyntaxContext syntax, System.Threading.CancellationToken token)
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
        var (generic, unreachable) = Reach.Of(type);
        return new Module(
            type.ToDisplayString(),
            job.ToDisplayString(),
            tables.ContainingNamespace.ToDisplayString(),
            type.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Length == 0
                && constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
            ),
            generic,
            unreachable,
            jobName,
            new Sequence<Named>(states),
            Place.Of(declaration.Identifier.GetLocation())
        );
    }

    static IEnumerable<AttributeData> Attributes(INamedTypeSymbol type, string name, INamedTypeSymbol tables) =>
        type.GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass is { } attributeClass
                && attributeClass.Name == name
                && attributeClass.ContainingNamespace.ToDisplayString() == tables.ContainingNamespace.ToDisplayString()
            );

    // The name and the evidence of an attribute: its two arguments from the index <paramref name="first"/>.
    static Text Name(AttributeData attribute, int first) =>
        new(
            (string)attribute.ConstructorArguments[first].Value!,
            (string)attribute.ConstructorArguments[first + 1].Value!
        );

    static void Generate(SourceProductionContext output, ImmutableArray<Module> found, int[] coreStates)
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
            if (module.Generic)
            {
                output.ReportDiagnostic(Diagnostic.Create(GenericModule, module.Place.ToLocation(), module.Name));
                continue;
            }

            if (module.Unreachable)
            {
                output.ReportDiagnostic(Diagnostic.Create(UnreachableModule, module.Place.ToLocation(), module.Name));
                continue;
            }

            if (!module.HasConstructor)
            {
                output.ReportDiagnostic(Diagnostic.Create(NoConstructor, module.Place.ToLocation(), module.Name));
                continue;
            }

            var twice = module.States.Items.GroupBy(state => state.Code).FirstOrDefault(group => group.Count() > 1);
            if (twice is not null)
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(StateNamedTwice, module.Place.ToLocation(), module.Name, twice.Key)
                );
                continue;
            }

            var core = module.States.Items.FirstOrDefault(state => coreStates.Contains(state.Code));
            if (core is not null)
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(CoreState, module.Place.ToLocation(), module.Name, core.Code)
                );
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

    static string Write(string ns, List<Module> modules)
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

    static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    /// <summary>A name with its evidence.</summary>
    sealed record Text(string Name, string Evidence);

    /// <summary>A state the module names.</summary>
    sealed record Named(int Code, Text Text);

    /// <summary>A list that compares by its items, so that a model holding it compares equal across compilations.</summary>
    sealed class Sequence<T>(T[] items) : System.IEquatable<Sequence<T>>
    {
        public T[] Items { get; } = items;

        public bool Equals(Sequence<T>? other) => other is not null && Items.SequenceEqual(other.Items);

        public override bool Equals(object? obj) => Equals(obj as Sequence<T>);

        public override int GetHashCode() =>
            Items.Aggregate(17, (hash, item) => hash * 31 + (item?.GetHashCode() ?? 0));
    }

    /// <summary>
    /// What the generator keeps of a module: its type, its job type, whether it declares the constructor the registry calls, whether
    /// it or a type it is nested in is generic, whether it or a type it is nested in is private or protected, its names, and where it
    /// stands.
    /// </summary>
    sealed record Module(
        string Name,
        string Job,
        string Namespace,
        bool HasConstructor,
        bool Generic,
        bool Unreachable,
        Text? JobName,
        Sequence<Named> States,
        Place Place
    );
}
