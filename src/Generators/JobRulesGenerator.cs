using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace OpenSettlers.Generators;

/// <summary>
/// Writes <c>JobModules</c>, the registry of the job rules modules of the application layer (ADR 0016), from the classes of the
/// compilation that declare <c>OpenSettlers.Simulation.JobRules&lt;TJob&gt;</c>. The registry builds each module from the
/// <c>JobParts</c>, in the order of the modules' full type names. A module without a constructor that takes the
/// <c>JobParts</c>, a second module for one job, a generic module, and a module the registry cannot reach are build errors.
/// </summary>
[Generator]
public sealed class JobRulesGenerator : IIncrementalGenerator
{
    private const string RulesMetadataName = "OpenSettlers.Simulation.JobRules`1";
    private const string PartsMetadataName = "OpenSettlers.Simulation.JobParts";

    private static readonly DiagnosticDescriptor NoConstructor = new(
        "RUL001",
        "A job module has no JobParts constructor",
        "The job module '{0}' declares no constructor that takes the JobParts alone",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor GenericModule = new(
        "RUL003",
        "A job module is generic",
        "The job module '{0}' is generic, or nested in a generic type, which the registry cannot build",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor UnreachableModule = new(
        "RUL004",
        "A job module is not reachable from the registry",
        "The job module '{0}' is private or protected, or nested in a type that is, so the registry cannot build it",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor SecondModule = new(
        "RUL002",
        "Two job modules rule one job",
        "The job modules '{0}' and '{1}' both rule the job '{2}'",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The transform reduces each declaration to an equatable model, so the output reruns only where a module changes.
        var declarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (syntax, token) => Describe(syntax, token)
            )
            .Where(static declaration => declaration is not null)
            .Select(static (declaration, _) => declaration!)
            .Collect();
        context.RegisterSourceOutput(declarations, static (output, found) => Generate(output, found));
    }

    private static Declaration? Describe(GeneratorSyntaxContext syntax, System.Threading.CancellationToken token)
    {
        var declaration = (ClassDeclarationSyntax)syntax.Node;
        if (syntax.SemanticModel.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol type)
        {
            return null;
        }

        // The declaration is one of this compilation's sources, so a type that is JobRules<TJob> is declared here.
        var place = Place.Of(declaration.Identifier.GetLocation());
        if (IsRules(type))
        {
            return new Declaration(
                type.ToDisplayString(),
                type.ContainingNamespace.ToDisplayString(),
                true,
                null,
                false,
                false,
                false,
                place
            );
        }

        if (type.IsAbstract)
        {
            return null;
        }

        for (var parent = type.BaseType; parent is not null; parent = parent.BaseType)
        {
            if (parent.IsGenericType && IsRules(parent.OriginalDefinition))
            {
                var constructed = type.InstanceConstructors.Any(constructor =>
                    constructor.Parameters.Length == 1
                    && constructor.Parameters[0].Type.ToDisplayString() == PartsMetadataName
                    && constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                );
                var generic = false;
                var unreachable = false;
                for (var holder = type; holder is not null; holder = holder.ContainingType)
                {
                    generic |= holder.IsGenericType;
                    unreachable |=
                        holder.DeclaredAccessibility
                            is Accessibility.Private
                                or Accessibility.Protected
                                or Accessibility.ProtectedAndInternal;
                }

                return new Declaration(
                    type.ToDisplayString(),
                    parent.OriginalDefinition.ContainingNamespace.ToDisplayString(),
                    false,
                    parent.TypeArguments[0].ToDisplayString(),
                    constructed,
                    generic,
                    unreachable,
                    place
                );
            }
        }

        return null;
    }

    private static bool IsRules(INamedTypeSymbol type) =>
        type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName == RulesMetadataName;

    private static void Generate(SourceProductionContext output, ImmutableArray<Declaration> declarations)
    {
        var rules = declarations.FirstOrDefault(declaration => declaration.IsRules);
        if (rules is null)
        {
            return;
        }

        // A partial module has a declaration per part: the first part by file and position stands for it.
        var modules = declarations
            .Where(declaration => !declaration.IsRules)
            .OrderBy(declaration => declaration.Name, System.StringComparer.Ordinal)
            .ThenBy(declaration => declaration.Place.Path, System.StringComparer.Ordinal)
            .ThenBy(declaration => declaration.Place.Span.Start)
            .GroupBy(declaration => declaration.Name)
            .Select(parts => parts.First())
            .ToList();

        var ruled = new Dictionary<string, Declaration>();
        var kept = new List<Declaration>();
        foreach (var module in modules)
        {
            if (ruled.TryGetValue(module.Job!, out var first))
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(SecondModule, module.Place.ToLocation(), first.Name, module.Name, module.Job)
                );
                continue;
            }

            ruled.Add(module.Job!, module);
            if (module.Generic)
            {
                output.ReportDiagnostic(Diagnostic.Create(GenericModule, module.Place.ToLocation(), module.Name));
            }
            else if (module.Unreachable)
            {
                output.ReportDiagnostic(Diagnostic.Create(UnreachableModule, module.Place.ToLocation(), module.Name));
            }
            else if (module.HasConstructor)
            {
                kept.Add(module);
            }
            else
            {
                output.ReportDiagnostic(Diagnostic.Create(NoConstructor, module.Place.ToLocation(), module.Name));
            }
        }

        output.AddSource("JobModules.g.cs", SourceText.From(Write(rules, kept), Encoding.UTF8));
    }

    private static string Write(Declaration rules, List<Declaration> modules)
    {
        var text = new StringBuilder();
        text.AppendLine("// <auto-generated/>");
        text.AppendLine("#nullable enable");
        text.AppendLine($"namespace {rules.Namespace};");
        text.AppendLine();
        text.AppendLine(
            "/// <summary>The registry of the job rules modules (ADR 0016), in the order of their full type names.</summary>"
        );
        text.AppendLine("public static class JobModules");
        text.AppendLine("{");
        text.AppendLine("    /// <summary>Builds every job rules module from the shared parts.</summary>");
        text.AppendLine(
            "    public static global::System.Collections.Generic.IReadOnlyList<JobRules> Build(JobParts parts) =>"
        );
        text.AppendLine("        new JobRules[]");
        text.AppendLine("        {");
        foreach (var module in modules)
        {
            text.AppendLine($"            new global::{module.Name}(parts),");
        }

        text.AppendLine("        };");
        text.AppendLine("}");
        return text.ToString();
    }

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

    /// <summary>
    /// What the generator keeps of a declaration: <c>JobRules&lt;TJob&gt;</c> itself, or a concrete class derived from it with the
    /// job it rules, whether it declares the <c>JobParts</c> constructor, whether it or a type it is nested in is generic, and whether
    /// it or a type it is nested in is private or protected, which the registry cannot build.
    /// </summary>
    private sealed record Declaration(
        string Name,
        string Namespace,
        bool IsRules,
        string? Job,
        bool HasConstructor,
        bool Generic,
        bool Unreachable,
        Place Place
    );
}
