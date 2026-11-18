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
/// Writes <c>Job.Of</c>, the number-to-job lookup of the domain (ADR 0016), from the sealed job types of the compilation that
/// declares <c>OpenSettlers.Domain.Job</c>. A job type declares its number as <c>public const int Number</c>; a job that spans
/// several numbers also declares <c>public const int LastNumber</c> and takes the offset from <c>Number</c> as its one
/// constructor argument. <c>UnportedJob</c> takes every other number. A job type that is not sealed, two jobs with one number,
/// a number that is not a constant, and a <c>LastNumber</c> below the <c>Number</c> are build errors.
/// </summary>
[Generator]
public sealed class JobLookupGenerator : IIncrementalGenerator
{
    const string JobMetadataName = "OpenSettlers.Domain.Job";
    const string FallbackName = "UnportedJob";

    static readonly DiagnosticDescriptor NotSealed = new(
        "JOB001",
        "A job type is not sealed",
        "The job type '{0}' is not sealed",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor SharedNumber = new(
        "JOB002",
        "Two jobs have one number",
        "The job types '{0}' and '{1}' have the number {2} in common",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor NotConstant = new(
        "JOB003",
        "A job's number is not a constant",
        "The job type '{0}' declares no 'public const int {1}' the generator can read",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor BackwardRange = new(
        "JOB004",
        "A job's last number is below its number",
        "The job type '{0}' declares a LastNumber {2} below its Number {1}",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor SharedName = new(
        "JOB005",
        "Two jobs have one stable name",
        "The job types '{0}' and '{1}' have the stable name '{2}' in common",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor Nameless = new(
        "JOB006",
        "A job has no stable name",
        "The job type '{0}' declares no JobName for {1}",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor MalformedName = new(
        "JOB007",
        "A job's stable name is malformed",
        "The job type '{0}' has the stable name '{1}', which holds anything other than lowercase letters, digits, and hyphens",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    static readonly DiagnosticDescriptor StrayName = new(
        "JOB008",
        "A job's stable name attribute stands for no level",
        "The job type '{0}' declares a JobName with the level {1}, which repeats a level or lies outside the job's levels",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // The transform reduces each declaration to an equatable model, so the output reruns only where a job type changes.
        var declarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) =>
                    node
                        is TypeDeclarationSyntax { BaseList: not null }
                            or TypeDeclarationSyntax { Identifier.ValueText: "Job" },
                static (syntax, token) => Describe(syntax, token)
            )
            .Where(static declaration => declaration is not null)
            .Select(static (declaration, _) => declaration!)
            .Collect();
        context.RegisterSourceOutput(declarations, static (output, found) => Generate(output, found));
    }

    static Declaration? Describe(GeneratorSyntaxContext syntax, System.Threading.CancellationToken token)
    {
        var declaration = (TypeDeclarationSyntax)syntax.Node;
        var job = syntax.SemanticModel.Compilation.GetTypeByMetadataName(JobMetadataName);
        if (
            job is null
            || job.Locations.All(location => !location.IsInSource)
            || syntax.SemanticModel.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol type
        )
        {
            return null;
        }

        var place = Place.Of(declaration.Identifier.GetLocation());
        var jobNamespace = job.ContainingNamespace.ToDisplayString();
        if (SymbolEqualityComparer.Default.Equals(type, job))
        {
            return new Declaration(
                type.ToDisplayString(),
                jobNamespace,
                job.Name,
                true,
                true,
                null,
                null,
                null,
                place,
                new Sequence<NameEntry>([])
            );
        }

        if (!IsJob(type, job) || type.IsAbstract || type.Name == FallbackName)
        {
            return null;
        }

        var first = Constant(type, "Number");
        var last = type.GetMembers("LastNumber").Any() ? Constant(type, "LastNumber") : first;
        var names = type.GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass is { Name: "JobNameAttribute" } attributeClass
                && attributeClass.ContainingNamespace.ToDisplayString() == jobNamespace
            )
            .Select(attribute => new NameEntry(
                attribute
                    .NamedArguments.Where(pair => pair.Key == "Level")
                    .Select(pair => pair.Value.Value is int level ? level : -1)
                    .DefaultIfEmpty(-1)
                    .First(),
                attribute.ConstructorArguments.Length > 0
                    ? attribute.ConstructorArguments[0].Value as string ?? ""
                    : "",
                attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as string ?? "" : ""
            ))
            .ToArray();
        return new Declaration(
            type.ToDisplayString(),
            jobNamespace,
            job.Name,
            false,
            type.IsSealed,
            first,
            last,
            first is null ? "Number"
                : last is null ? "LastNumber"
                : null,
            place,
            new Sequence<NameEntry>(names)
        );
    }

    static void Generate(SourceProductionContext output, ImmutableArray<Declaration> declarations)
    {
        var job = declarations.FirstOrDefault(declaration => declaration.IsJob);
        if (job is null)
        {
            return;
        }

        // A partial job type has a declaration per part: the first part by file and position stands for it.
        var candidates = declarations
            .Where(declaration => !declaration.IsJob)
            .OrderBy(declaration => declaration.Name, System.StringComparer.Ordinal)
            .ThenBy(declaration => declaration.Place.Path, System.StringComparer.Ordinal)
            .ThenBy(declaration => declaration.Place.Span.Start)
            .GroupBy(declaration => declaration.Name)
            .Select(parts => parts.First())
            .ToList();

        var jobs = new List<Declaration>();
        foreach (var candidate in candidates)
        {
            if (!candidate.IsSealed)
            {
                output.ReportDiagnostic(Diagnostic.Create(NotSealed, candidate.Place.ToLocation(), candidate.Name));
            }
            else if (candidate.First is null || candidate.Last is null)
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(NotConstant, candidate.Place.ToLocation(), candidate.Name, candidate.Unread)
                );
            }
            else if (candidate.Last < candidate.First)
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(
                        BackwardRange,
                        candidate.Place.ToLocation(),
                        candidate.Name,
                        candidate.First,
                        candidate.Last
                    )
                );
            }
            else
            {
                jobs.Add(candidate);
            }
        }

        jobs.Sort(
            (a, b) =>
                a.First == b.First ? string.CompareOrdinal(a.Name, b.Name) : a.First!.Value.CompareTo(b.First!.Value)
        );
        var kept = new List<Declaration>();
        foreach (var candidate in jobs)
        {
            // The kept jobs have disjoint ranges in ascending order, and the candidate starts at or after the last kept one's start,
            // so the candidate's first number is the shared one where the ranges overlap.
            if (kept.Count > 0 && candidate.First <= kept[kept.Count - 1].Last)
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(
                        SharedNumber,
                        candidate.Place.ToLocation(),
                        kept[kept.Count - 1].Name,
                        candidate.Name,
                        candidate.First
                    )
                );
                continue;
            }

            kept.Add(candidate);
        }

        // Each job, and each level of a job that spans several numbers, declares a stable name with its evidence: a lowercase name no
        // other job has. A job that fails here is left out of the output, as a job with a bad number is.
        var named = new List<(Declaration Job, NameEntry[] Names)>();
        var owners = new Dictionary<string, string>();
        foreach (var candidate in kept.OrderBy(declaration => declaration.Name, System.StringComparer.Ordinal))
        {
            var ranged = candidate.First != candidate.Last;
            var levels = ranged ? candidate.Last!.Value - candidate.First!.Value + 1 : 1;
            var entries = new NameEntry[levels];
            var valid = true;
            var seen = new HashSet<int>();
            foreach (var item in candidate.Names.Items)
            {
                var wanted = ranged ? item.Level >= 0 && item.Level < levels : item.Level == -1;
                if (!wanted || !seen.Add(item.Level))
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(StrayName, candidate.Place.ToLocation(), candidate.Name, item.Level)
                    );
                    valid = false;
                }
            }

            for (var level = 0; level < levels; level++)
            {
                var entry = candidate.Names.Items.FirstOrDefault(item => item.Level == (ranged ? level : -1));
                if (entry is null)
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(
                            Nameless,
                            candidate.Place.ToLocation(),
                            candidate.Name,
                            ranged ? $"level {level}" : "the job"
                        )
                    );
                    valid = false;
                }
                else if (entry.Name.Length == 0 || !entry.Name.All(IsNameCharacter))
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(MalformedName, candidate.Place.ToLocation(), candidate.Name, entry.Name)
                    );
                    valid = false;
                }
                else if (owners.TryGetValue(entry.Name, out var other))
                {
                    output.ReportDiagnostic(
                        Diagnostic.Create(SharedName, candidate.Place.ToLocation(), other, candidate.Name, entry.Name)
                    );
                    valid = false;
                }
                else
                {
                    owners[entry.Name] = candidate.Name;
                    entries[level] = entry;
                }
            }

            if (valid)
            {
                named.Add((candidate, entries));
            }
        }

        output.AddSource("Job.Of.g.cs", SourceText.From(Write(job, named), Encoding.UTF8));
    }

    static bool IsNameCharacter(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';

    static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    static bool IsJob(INamedTypeSymbol type, INamedTypeSymbol job)
    {
        for (var parent = type.BaseType; parent is not null; parent = parent.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(parent, job))
            {
                return true;
            }
        }

        return false;
    }

    static int? Constant(INamedTypeSymbol type, string name) =>
        type.GetMembers(name).OfType<IFieldSymbol>().FirstOrDefault() is { IsConst: true, ConstantValue: int value }
            ? value
            : null;

    static string Write(Declaration job, List<(Declaration Job, NameEntry[] Names)> named)
    {
        var jobs = named.Select(entry => entry.Job).OrderBy(entry => entry.First).ToList();
        var text = new StringBuilder();
        text.AppendLine("// <auto-generated/>");
        text.AppendLine("#nullable enable");
        text.AppendLine($"namespace {job.JobNamespace};");
        text.AppendLine();
        text.AppendLine($"public abstract partial record {job.JobName}");
        text.AppendLine("{");
        text.AppendLine(
            "    /// <summary>The job of a number: the job type that owns it, or an <see cref=\"UnportedJob\"/>.</summary>"
        );
        text.AppendLine($"    public static {job.JobName} Of(int code) => code switch");
        text.AppendLine("    {");
        foreach (var entry in jobs)
        {
            text.AppendLine(
                entry.First == entry.Last
                    ? $"        {entry.First} => new global::{entry.Name}(),"
                    : $"        >= {entry.First} and <= {entry.Last} => new global::{entry.Name}(code - {entry.First}),"
            );
        }

        text.AppendLine($"        _ => new {FallbackName}(code),");
        text.AppendLine("    };");
        text.AppendLine();
        text.AppendLine(
            "    /// <summary>Whether a job type owns the number, so that an <see cref=\"UnportedJob\"/> may not hold it.</summary>"
        );
        text.AppendLine("    internal static bool IsPorted(int code) =>");
        text.Append("        ");
        text.Append(
            jobs.Count == 0
                ? "false"
                : "code is "
                    + string.Join(
                        " or ",
                        jobs.Select(entry =>
                            entry.First == entry.Last ? $"{entry.First}" : $">= {entry.First} and <= {entry.Last}"
                        )
                    )
        );
        text.AppendLine(";");

        // Every job and every level of a ranged job, in the order of the job types' names: the type name first, the level next.
        var instances = new List<(string Creation, string Pattern, NameEntry Entry)>();
        foreach (var (declaration, names) in named)
        {
            for (var level = 0; level < names.Length; level++)
            {
                var ranged = declaration.First != declaration.Last;
                instances.Add(
                    (
                        ranged ? $"new global::{declaration.Name}({level})" : $"new global::{declaration.Name}()",
                        ranged ? $"global::{declaration.Name} {{ Level: {level} }}" : $"global::{declaration.Name}",
                        names[level]
                    )
                );
            }
        }

        text.AppendLine();
        text.AppendLine(
            "    /// <summary>The job's stable name; empty for an <see cref=\"UnportedJob\"/>, which has none.</summary>"
        );
        text.AppendLine("    public string Name => this switch");
        text.AppendLine("    {");
        foreach (var (_, pattern, entry) in instances)
        {
            text.AppendLine($"        {pattern} => {Literal(entry.Name)},");
        }

        text.AppendLine($"        {FallbackName} => \"\",");
        text.AppendLine("        _ => throw new global::System.InvalidOperationException(),");
        text.AppendLine("    };");
        text.AppendLine();
        text.AppendLine(
            "    /// <summary>The evidence for the job's stable name; empty for an <see cref=\"UnportedJob\"/>.</summary>"
        );
        text.AppendLine("    public string NameEvidence => this switch");
        text.AppendLine("    {");
        foreach (var (_, pattern, entry) in instances)
        {
            text.AppendLine($"        {pattern} => {Literal(entry.Evidence)},");
        }

        text.AppendLine($"        {FallbackName} => \"\",");
        text.AppendLine("        _ => throw new global::System.InvalidOperationException(),");
        text.AppendLine("    };");
        text.AppendLine();
        text.AppendLine("    /// <summary>The job with the stable name; null for a name no job has.</summary>");
        text.AppendLine($"    public static {job.JobName}? Named(string name) => name switch");
        text.AppendLine("    {");
        foreach (var (creation, _, entry) in instances)
        {
            text.AppendLine($"        {Literal(entry.Name)} => {creation},");
        }

        text.AppendLine("        _ => null,");
        text.AppendLine("    };");
        text.AppendLine();
        text.AppendLine(
            "    /// <summary>Every job that has a stable name, in the order of the job types' names and their levels.</summary>"
        );
        text.AppendLine(
            $"    public static global::System.Collections.Generic.IReadOnlyList<{job.JobName}> Ported {{ get; }} ="
        );
        text.AppendLine("    [");
        foreach (var (creation, _, _) in instances)
        {
            text.AppendLine($"        {creation},");
        }

        text.AppendLine("    ];");
        text.AppendLine("}");
        return text.ToString();
    }

    /// <summary>
    /// What the generator keeps of a declaration: <c>Job</c> itself, or a type derived from it. <c>First</c> and <c>Last</c> are
    /// null where the type declares no constant for them; <c>Unread</c> names the member that failed to read (<c>Number</c> first).
    /// </summary>
    sealed record Declaration(
        string Name,
        string JobNamespace,
        string JobName,
        bool IsJob,
        bool IsSealed,
        int? First,
        int? Last,
        string? Unread,
        Place Place,
        Sequence<NameEntry> Names
    );

    /// <summary>A <c>JobName</c> attribute: the level it names (-1 for a job of one number), the name, and the evidence.</summary>
    sealed record NameEntry(int Level, string Name, string Evidence);
}
