using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace OpenSettlers.Generators;

/// <summary>
/// Writes the registries of the job looks (ADR 0016, INV-0010), one for each graphics source, from the classes of the compilation
/// that declare <c>OpenSettlers.MapView.HistoryEditionLook&lt;TJob&gt;</c> or <c>OpenSettlers.MapView.OpenSettlersLook&lt;TJob&gt;</c>.
/// A registry builds each look of its source in the order of the looks' full type names. A History Edition look needs a constructor
/// that takes the <c>RuleTables</c> alone and an Open Settlers look a constructor without parameters. A look without its
/// constructor, a second look for one job under one source, a generic look, and a look the registry cannot reach are build errors.
/// </summary>
[Generator]
public sealed class JobLooksGenerator : IIncrementalGenerator
{
    const string TablesMetadataName = "OpenSettlers.Domain.RuleTables";

    static readonly DiagnosticDescriptor NoConstructor = new(
        "LOOK001",
        "A job look has no constructor the registry can call",
        "The job look '{0}' declares no constructor that {1}",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor SecondLook = new(
        "LOOK002",
        "Two job looks draw one job under one source",
        "The job looks '{0}' and '{1}' both draw the job '{2}' under the {3}",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    /// <summary>
    /// A graphics source: its look type (the generic base's metadata name and the name the registry lists), how a look is built, and
    /// the registry it gets. A look of a source that takes the tables is built from the <c>RuleTables</c> alone, else from nothing.
    /// </summary>
    sealed record Source(
        string Label,
        string LookMetadataName,
        string LookType,
        string Registry,
        string Signature,
        bool TakesTables
    )
    {
        public string Parameter => TakesTables ? "global::OpenSettlers.Domain.RuleTables tables" : "";

        public string Arguments => TakesTables ? "tables" : "";
    }

    // The History Edition source comes first: its registry is written first.
    static readonly Source[] Sources =
    [
        new(
            "History Edition source",
            "OpenSettlers.MapView.HistoryEditionLook`1",
            "HistoryEditionLook",
            "HistoryEditionLookRegistry",
            "takes the RuleTables alone",
            true
        ),
        new(
            "Open Settlers source",
            "OpenSettlers.MapView.OpenSettlersLook`1",
            "OpenSettlersLook",
            "OpenSettlersLookRegistry",
            "takes no parameters",
            false
        ),
    ];

    static readonly DiagnosticDescriptor GenericLook = new(
        "LOOK003",
        "A job look is generic",
        "The job look '{0}' is generic, or nested in a generic type, which the registry cannot build",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor UnreachableLook = new(
        "LOOK004",
        "A job look is not reachable from the registry",
        "The job look '{0}' is private or protected, or nested in a type that is, so the registry cannot build it",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The transform reduces each declaration to an equatable model, so the output reruns only where a look changes.
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

    static Declaration? Describe(GeneratorSyntaxContext syntax, System.Threading.CancellationToken token)
    {
        var declaration = (ClassDeclarationSyntax)syntax.Node;
        if (syntax.SemanticModel.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol type)
        {
            return null;
        }

        var compilation = syntax.SemanticModel.Compilation;
        var tables = compilation.GetTypeByMetadataName(TablesMetadataName);
        var place = Place.Of(declaration.Identifier.GetLocation());
        foreach (var source in Sources)
        {
            var look = compilation.GetTypeByMetadataName(source.LookMetadataName);
            if (look is null || look.Locations.All(location => !location.IsInSource))
            {
                continue;
            }

            var ns = look.ContainingNamespace.ToDisplayString();
            if (SymbolEqualityComparer.Default.Equals(type, look))
            {
                return new Declaration(
                    type.ToDisplayString(),
                    ns,
                    source.Registry,
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
                continue;
            }

            for (var parent = type.BaseType; parent is not null; parent = parent.BaseType)
            {
                if (parent.IsGenericType && SymbolEqualityComparer.Default.Equals(parent.OriginalDefinition, look))
                {
                    var constructed = type.InstanceConstructors.Any(constructor =>
                        constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                        && (
                            source.TakesTables
                                ? tables is not null
                                    && constructor.Parameters.Length == 1
                                    && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, tables)
                                : constructor.Parameters.Length == 0
                        )
                    );
                    var (generic, unreachable) = Reach.Of(type);
                    return new Declaration(
                        type.ToDisplayString(),
                        ns,
                        source.Registry,
                        false,
                        parent.TypeArguments[0].ToDisplayString(),
                        constructed,
                        generic,
                        unreachable,
                        place
                    );
                }
            }
        }

        return null;
    }

    static void Generate(SourceProductionContext output, ImmutableArray<Declaration> declarations)
    {
        foreach (var source in Sources)
        {
            var look = declarations.FirstOrDefault(declaration =>
                declaration.IsLook && declaration.Source == source.Registry
            );
            if (look is null)
            {
                continue;
            }

            // A partial look has a declaration per part: the first part by file and position stands for it.
            var modules = declarations
                .Where(declaration => !declaration.IsLook && declaration.Source == source.Registry)
                .OrderBy(declaration => declaration.Name, System.StringComparer.Ordinal)
                .ThenBy(declaration => declaration.Place.Path, System.StringComparer.Ordinal)
                .ThenBy(declaration => declaration.Place.Span.Start)
                .GroupBy(declaration => declaration.Name)
                .Select(parts => parts.First())
                .ToList();

            var drawn = new Dictionary<string, Declaration>();
            var kept = new List<Declaration>();
            foreach (var module in modules)
            {
                if (drawn.TryGetValue(module.Job!, out var first))
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(
                            SecondLook,
                            module.Place.ToLocation(),
                            first.Name,
                            module.Name,
                            module.Job,
                            source.Label
                        )
                    );
                    continue;
                }

                drawn.Add(module.Job!, module);
                if (module.Generic)
                {
                    output.ReportDiagnostic(Diagnostic.Create(GenericLook, module.Place.ToLocation(), module.Name));
                }
                else if (module.Unreachable)
                {
                    output.ReportDiagnostic(Diagnostic.Create(UnreachableLook, module.Place.ToLocation(), module.Name));
                }
                else if (module.HasConstructor)
                {
                    kept.Add(module);
                }
                else
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(NoConstructor, module.Place.ToLocation(), module.Name, source.Signature)
                    );
                }
            }

            output.AddSource(
                source.Registry + ".g.cs",
                SourceText.From(Write(source, look.Namespace, kept), Encoding.UTF8)
            );
        }
    }

    static string Write(Source source, string ns, List<Declaration> modules)
    {
        var text = new StringBuilder();
        text.AppendLine("// <auto-generated/>");
        text.AppendLine("#nullable enable");
        text.AppendLine($"namespace {ns};");
        text.AppendLine();
        text.AppendLine(
            $"/// <summary>The registry of the looks of the {source.Label}, in the order of their full type names.</summary>"
        );
        text.AppendLine($"internal static class {source.Registry}");
        text.AppendLine("{");
        text.AppendLine("    /// <summary>Builds every look of the source.</summary>");
        text.AppendLine(
            $"    public static global::System.Collections.Generic.IReadOnlyList<{source.LookType}> Build({source.Parameter}) =>"
        );
        text.AppendLine($"        new {source.LookType}[]");
        text.AppendLine("        {");
        foreach (var module in modules)
        {
            text.AppendLine($"            new global::{module.Name}({source.Arguments}),");
        }

        text.AppendLine("        };");
        text.AppendLine("}");
        return text.ToString();
    }

    /// <summary>
    /// What the generator keeps of a declaration: the look type of a source itself, or a concrete class derived from it with the job it
    /// draws, whether it declares the constructor the registry calls, whether it or a type it is nested in is generic, and whether it or
    /// a type it is nested in is private or protected, which the registry cannot build. <c>Source</c> is the registry of its graphics
    /// source.
    /// </summary>
    sealed record Declaration(
        string Name,
        string Namespace,
        string Source,
        bool IsLook,
        string? Job,
        bool HasConstructor,
        bool Generic,
        bool Unreachable,
        Place Place
    );
}
