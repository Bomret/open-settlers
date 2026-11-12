using System.Collections.Generic;
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
/// and a number that is not a constant are build errors.
/// </summary>
[Generator]
public sealed class JobLookupGenerator : IIncrementalGenerator
{
    private const string JobMetadataName = "OpenSettlers.Domain.Job";
    private const string FallbackName = "UnportedJob";

    private static readonly DiagnosticDescriptor NotSealed = new(
        "JOB001",
        "A job type is not sealed",
        "The job type '{0}' is not sealed",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor SharedNumber = new(
        "JOB002",
        "Two jobs have one number",
        "The job types '{0}' and '{1}' have the number {2} in common",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor NotConstant = new(
        "JOB003",
        "A job's number is not a constant",
        "The job type '{0}' declares no 'public const int Number' the generator can read",
        "OpenSettlers.Jobs",
        DiagnosticSeverity.Error,
        true
    );

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { BaseList: not null },
                static (syntax, _) => (TypeDeclarationSyntax)syntax.Node
            )
            .Collect();
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(declarations),
            static (output, pair) => Generate(output, pair.Left, pair.Right)
        );
    }

    private static void Generate(
        SourceProductionContext output,
        Compilation compilation,
        IEnumerable<TypeDeclarationSyntax> declarations
    )
    {
        var job = compilation.GetTypeByMetadataName(JobMetadataName);
        if (job is null || job.Locations.All(location => !location.IsInSource))
        {
            return;
        }

        var jobs = new List<JobType>();
        foreach (var declaration in declarations)
        {
            var model = compilation.GetSemanticModel(declaration.SyntaxTree);
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type || !IsJob(type, job))
            {
                continue;
            }

            if (type.IsAbstract || type.Name == FallbackName)
            {
                continue;
            }

            var location = declaration.Identifier.GetLocation();
            if (!type.IsSealed)
            {
                output.ReportDiagnostic(Diagnostic.Create(NotSealed, location, type.Name));
                continue;
            }

            var first = Constant(type, "Number");
            if (first is not { } number)
            {
                output.ReportDiagnostic(Diagnostic.Create(NotConstant, location, type.Name));
                continue;
            }

            var last = type.GetMembers("LastNumber").Any() ? Constant(type, "LastNumber") : number;
            if (last is not { } end)
            {
                output.ReportDiagnostic(Diagnostic.Create(NotConstant, location, type.Name));
                continue;
            }

            jobs.Add(new JobType(type.ToDisplayString(), number, end, location));
        }

        jobs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        var valid = new List<JobType>();
        foreach (var candidate in jobs)
        {
            var clash = valid.FirstOrDefault(other => other.First <= candidate.Last && candidate.First <= other.Last);
            if (clash is not null)
            {
                output.ReportDiagnostic(
                    Diagnostic.Create(SharedNumber, candidate.Location, clash.Name, candidate.Name, candidate.First)
                );
                continue;
            }

            valid.Add(candidate);
        }

        valid.Sort((a, b) => a.First.CompareTo(b.First));
        output.AddSource("Job.Of.g.cs", SourceText.From(Write(job, valid), Encoding.UTF8));
    }

    private static bool IsJob(INamedTypeSymbol type, INamedTypeSymbol job)
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

    private static int? Constant(INamedTypeSymbol type, string name) =>
        type.GetMembers(name).OfType<IFieldSymbol>().FirstOrDefault() is { IsConst: true, ConstantValue: int value }
            ? value
            : null;

    private static string Write(INamedTypeSymbol job, List<JobType> jobs)
    {
        var text = new StringBuilder();
        text.AppendLine("// <auto-generated/>");
        text.AppendLine("#nullable enable");
        text.AppendLine($"namespace {job.ContainingNamespace.ToDisplayString()};");
        text.AppendLine();
        text.AppendLine($"public abstract partial record {job.Name}");
        text.AppendLine("{");
        text.AppendLine(
            "    /// <summary>The job of a number: the job type that owns it, or an <see cref=\"UnportedJob\"/>.</summary>"
        );
        text.AppendLine($"    public static {job.Name} Of(int code) => code switch");
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
        text.AppendLine("    public static bool IsPorted(int code) =>");
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

    private sealed class JobType
    {
        public JobType(string name, int first, int last, Location location)
        {
            Name = name;
            First = first;
            Last = last;
            Location = location;
        }

        public string Name { get; }

        public int First { get; }

        public int Last { get; }

        public Location Location { get; }
    }
}
