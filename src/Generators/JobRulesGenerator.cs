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
/// <c>JobParts</c>, and a second module for one job, are build errors.
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
                static (node, _) =>
                    node
                        is ClassDeclarationSyntax { BaseList: not null }
                            or ClassDeclarationSyntax { Identifier.ValueText: "JobRules" },
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
        var rules = syntax.SemanticModel.Compilation.GetTypeByMetadataName(RulesMetadataName);
        var parts = syntax.SemanticModel.Compilation.GetTypeByMetadataName(PartsMetadataName);
        if (
            rules is null
            || rules.Locations.All(location => !location.IsInSource)
            || syntax.SemanticModel.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol type
        )
        {
            return null;
        }

        var place = Place.Of(declaration.Identifier.GetLocation());
        if (SymbolEqualityComparer.Default.Equals(type, rules))
        {
            return new Declaration(
                type.ToDisplayString(),
                rules.ContainingNamespace.ToDisplayString(),
                true,
                null,
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
            if (parent.IsGenericType && SymbolEqualityComparer.Default.Equals(parent.OriginalDefinition, rules))
            {
                var constructed =
                    parts is not null
                    && type.InstanceConstructors.Any(constructor =>
                        constructor.Parameters.Length == 1
                        && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, parts)
                        && constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
                    );
                return new Declaration(
                    type.ToDisplayString(),
                    rules.ContainingNamespace.ToDisplayString(),
                    false,
                    parent.TypeArguments[0].ToDisplayString(),
                    constructed,
                    place
                );
            }
        }

        return null;
    }

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
            if (module.HasConstructor)
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
    /// job it rules and whether it declares the <c>JobParts</c> constructor.
    /// </summary>
    private sealed record Declaration(
        string Name,
        string Namespace,
        bool IsRules,
        string? Job,
        bool HasConstructor,
        Place Place
    );
}
