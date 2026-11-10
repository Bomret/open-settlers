using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// No static field or static auto-property initializer of a test class may call TestProgram.Tables() or
// TestProgram.OpenInstall(). Without the install, the Assert.Inconclusive they throw then fires during type
// initialization, where MSTest reports it as a TypeInitializationException that fails every test of the class.
// Build the value inside the test instead, for example through an expression-bodied accessor that caches it.
[TestClass]
public sealed class InstallInitializerTests
{
    private static readonly string[] InstallCalls = ["Tables", "OpenInstall"];

    private static string ProjectDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "OpenSettlers.Tests.csproj")))
        {
            dir =
                Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar))
                ?? throw new InvalidOperationException("OpenSettlers.Tests.csproj not found");
        }

        return dir;
    }

    [TestMethod]
    public void NoTestClassReachesTheInstallFromAStaticInitializer()
    {
        var project = ProjectDirectory();
        var files = Directory
            .EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(project, file))
            .Where(file => !IsBuildOutput(file))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.AreNotEqual(0, files.Count, $"No source file found under {project}");

        var offenders = files
            .SelectMany(file => Lines(File.ReadAllText(Path.Combine(project, file))).Select(line => $"{file}:{line}"))
            .ToList();

        Assert.AreEqual(
            0,
            offenders.Count,
            "Static initializers that reach the install (move the call into the test): " + string.Join(", ", offenders)
        );
    }

    [TestMethod]
    public void AWrappedInitializerIsFlaggedAtItsAssignment()
    {
        var source = """
            class A
            {
                private static readonly Map Map =
                    MapViewState.Open(
                        TestProgram.Tables(),
                        "1234567812345678",
                        1
                    ).Map!;
            }
            """;

        CollectionAssert.AreEqual(new[] { 3 }, Lines(source).ToArray());
    }

    [TestMethod]
    public void AStaticAutoPropertyInitializerIsFlagged()
    {
        var source = """
            class A
            {
                public static Install Game { get; } = TestProgram.OpenInstall();
            }
            """;

        CollectionAssert.AreEqual(new[] { 3 }, Lines(source).ToArray());
    }

    [TestMethod]
    public void EveryModifierOrderIsFlagged()
    {
        var source = """
            class A
            {
                readonly static RuleTables First = TestProgram.Tables();
                static private readonly RuleTables Second = TestProgram.Tables();
            }
            """;

        CollectionAssert.AreEqual(new[] { 3, 4 }, Lines(source).ToArray());
    }

    [TestMethod]
    public void AnInitializerCallingAStaticHelperThatReachesTheInstallIsFlagged()
    {
        var source = """
            class A
            {
                static Map Generated() => MapViewState.Open(TestProgram.Tables(), "1", 1).Map!;
                static Install Opened()
                {
                    return TestProgram.OpenInstall();
                }
                private static readonly Map Map = Generated();
                private static readonly Install Game = A.Opened();
            }
            """;

        CollectionAssert.AreEqual(new[] { 8, 9 }, Lines(source).ToArray());
    }

    [TestMethod]
    public void AStaticConstructorThatReachesTheInstallIsFlaggedAtItsName()
    {
        var source = """
            class A
            {
                static Map Generated() => MapViewState.Open(TestProgram.Tables(), "1", 1).Map!;
                private static readonly Map First;
                static A()
                {
                    First = Generated();
                }
            }
            class B
            {
                private static readonly RuleTables Second;
                static B() => Second = TestProgram.Tables();
            }
            """;

        CollectionAssert.AreEqual(new[] { 5, 13 }, Lines(source).ToArray());
    }

    [TestMethod]
    public void ABareCallUnderUsingStaticTestProgramIsFlagged()
    {
        var source = """
            using static OpenSettlers.Tests.TestProgram;
            class A
            {
                private static readonly RuleTables First = Tables();
                private static readonly Install Second = OpenInstall();
            }
            """;

        CollectionAssert.AreEqual(new[] { 4, 5 }, Lines(source).ToArray());
    }

    [TestMethod]
    public void ABareCallWithoutUsingStaticTestProgramIsNotFlagged()
    {
        var source = """
            class A
            {
                static RuleTables Tables() => TestProgram.Made();
                private static readonly RuleTables Own = Tables();
            }
            """;

        CollectionAssert.AreEqual(Array.Empty<int>(), Lines(source).ToArray());
    }

    [TestMethod]
    public void DeferredCallsAndCallTextOutsideCodeAreNotFlagged()
    {
        var source = """
            class A
            {
                // private static readonly RuleTables Commented = TestProgram.Tables();
                /* private static readonly RuleTables Blocked = TestProgram.Tables(); */
                private static readonly string Text = "TestProgram.Tables()";
                private static readonly string Verbatim = @"TestProgram.Tables() "" TestProgram.OpenInstall()";
                private static readonly string Interpolated = $"{(true ? "a" : "b")} TestProgram.Tables()";
                private static readonly Lazy<RuleTables> Deferred = new(() => TestProgram.Tables());
                private static Map? _map;
                private static Map Map => _map ??= MapViewState.Open(TestProgram.Tables(), "1", 1).Map!;
                static Map Generated() => MapViewState.Open(TestProgram.Tables(), "1", 1).Map!;
                private readonly RuleTables _instance = TestProgram.Tables();
                public static void Test(int count = 1)
                {
                    var tables = TestProgram.Tables();
                }
                public static Map Body { get { return Generated(); } }
            }
            """;

        CollectionAssert.AreEqual(Array.Empty<int>(), Lines(source).ToArray());
    }

    private static bool IsBuildOutput(string relative)
    {
        var first = relative.Split(Path.DirectorySeparatorChar)[0];
        return first is "bin" or "obj";
    }

    // The 1-based line of the '=' of every static field or static auto-property initializer, and of the name of every
    // static constructor, in the source that calls the install outside a lambda, directly or through a static method
    // of the source that calls it directly.
    private static IEnumerable<int> Lines(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var bare = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Any(directive =>
                directive.StaticKeyword != default && directive.Name is { } name && Name(name) == "TestProgram"
            );
        var helpers = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(method => IsStatic(method) && Calls(method, call => IsInstallCall(call, bare)))
            .Select(method => method.Identifier.Text)
            .ToHashSet();

        return root.DescendantNodes()
            .Where(node =>
                Anchor(node) is not null
                && Calls(node, call => IsInstallCall(call, bare) || helpers.Contains(Name(call.Expression) ?? ""))
            )
            .Select(node => Anchor(node)!.Value.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    // The token whose line names the node when it runs during type initialization: the '=' of a static field or static
    // auto-property initializer, the name of a static constructor; null for any other node.
    private static SyntaxToken? Anchor(SyntaxNode node) =>
        node switch
        {
            EqualsValueClauseSyntax clause
                when IsStatic(clause.Parent is VariableDeclaratorSyntax v ? v.Parent?.Parent : clause.Parent) =>
                clause.EqualsToken,
            ConstructorDeclarationSyntax constructor when IsStatic(constructor) => constructor.Identifier,
            _ => null,
        };

    // Whether the node is a member declared static: a field, property, method, or constructor.
    private static bool IsStatic(SyntaxNode? node) =>
        node is MemberDeclarationSyntax member && member.Modifiers.Any(SyntaxKind.StaticKeyword);

    // Whether the node holds a call the predicate accepts outside the body of a lambda or local function.
    private static bool Calls(SyntaxNode node, Func<InvocationExpressionSyntax, bool> accepts) =>
        node.DescendantNodes(child => child is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<InvocationExpressionSyntax>()
            .Any(accepts);

    // Whether the call is TestProgram.Tables() or TestProgram.OpenInstall(), qualified or, where the source imports
    // TestProgram with `using static`, bare.
    private static bool IsInstallCall(InvocationExpressionSyntax call, bool bare) =>
        InstallCalls.Contains(Name(call.Expression))
        && call.Expression switch
        {
            MemberAccessExpressionSyntax access => Name(access.Expression) == "TestProgram",
            SimpleNameSyntax => bare,
            _ => false,
        };

    // The rightmost name of a name or member access, null for any other expression.
    private static string? Name(ExpressionSyntax expression) =>
        expression switch
        {
            SimpleNameSyntax name => name.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            MemberAccessExpressionSyntax access => access.Name.Identifier.Text,
            _ => null,
        };
}
