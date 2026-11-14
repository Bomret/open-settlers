using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
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
            return new Declaration(type.ToDisplayString(), jobNamespace, job.Name, true, true, null, null, null, place);
        }

        if (!IsJob(type, job) || type.IsAbstract || type.Name == FallbackName)
        {
            return null;
        }

        var first = Constant(type, "Number");
        var last = type.GetMembers("LastNumber").Any() ? Constant(type, "LastNumber") : first;
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
            place
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

        output.AddSource("Job.Of.g.cs", SourceText.From(Write(job, kept), Encoding.UTF8));
    }

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

    static string Write(Declaration job, List<Declaration> jobs)
    {
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
        Place Place
    );
}
