using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// No static field or static auto-property initializer and no static constructor of a test class may reach
// TestProgram.Tables() or TestProgram.OpenInstall(). Without the install, the Assert.Inconclusive they throw then fires
// during type initialization, where MSTest reports it as a TypeInitializationException that fails every test of the
// class. A lambda, local function, or method group that runs only when called later is allowed. Build the value inside
// the test instead, for example through an expression-bodied accessor that caches it.
//
// The check flags a call qualified by TestProgram or by a `using` alias of it, a bare call under
// `using static TestProgram`, and a call of a static method or a read of a static property declared in the same file
// whose body or getter reaches the install, through any chain of such members. A bare member name resolves through
// the types around it, a qualified one through its receiver where that is a type of the same file. A method name counts
// when any overload of it reaches the install. A name in a type position or inside nameof() counts as no read, but a
// helper property sharing its name with a type is flagged where that type is the receiver (`Map.Create()`).
//
// The check reads the syntax of each file alone, so it misses a helper declared in another file or another part of a
// partial class, a `global using` in another file, a helper reached through an instance or a receiver that is no type
// of the file, a delegate stored in a static field and invoked in an initializer, and a local function, lambda, or
// method group that a static initializer, static constructor, or helper calls in place.
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
    public void AnInitializerReadingAStaticPropertyThatReachesTheInstallIsFlagged()
    {
        var source = """
            class A
            {
                private static Map? _map;
                private static Map Map => _map ??= MapViewState.Open(TestProgram.Tables(), "1", 1).Map!;
                private static readonly int Width = Map.Width;
                static Map Generated => MapViewState.Open(TestProgram.Tables(), "1", 1).Map!;
                private static readonly Map Copy = Generated;
                static Install Game
                {
                    get { return TestProgram.OpenInstall(); }
                }
                private static readonly Install Opened = A.Game;
            }
            """;

        CollectionAssert.AreEqual(new[] { 5, 7, 12 }, Lines(source).ToArray());
    }

    [TestMethod]
    public void AHelperReachingTheInstallThroughAnotherHelperIsFlagged()
    {
        var source = """
            class A
            {
                static RuleTables Rules => TestProgram.Tables();
                static Map Generated() => MapViewState.Open(Rules, "1", 1).Map!;
                static Map Wrapped { get { return Generated(); } }
                private static readonly Map Map = Wrapped;
            }
            """;

        CollectionAssert.AreEqual(new[] { 6 }, Lines(source).ToArray());
    }

    [TestMethod]
    public void AHelperPropertyNameInATypePositionOrUnevaluatedIsNotFlagged()
    {
        var source = """
            class A
            {
                private static Map? _map;
                private static Map Map => _map ??= MapViewState.Open(TestProgram.Tables(), "1", 1).Map!;
                private static readonly Lazy<Map> Later = new Lazy<Map>(() => Map);
                private static readonly Type Kind = typeof(Map);
                private static readonly Map? None = default(Map);
                private static readonly object? Cast = (Map?)null;
                private static readonly string Named = nameof(Map);
                private static readonly Settings Options = new Settings { Map = 1 };
            }
            """;

        CollectionAssert.AreEqual(Array.Empty<int>(), Lines(source).ToArray());
    }

    [TestMethod]
    public void AHelperOfAnotherSameFileTypeIsMatchedByItsReceiver()
    {
        var source = """
            class A
            {
                internal static Map Generated() => MapViewState.Open(TestProgram.Tables(), "1", 1).Map!;
            }
            class B
            {
                internal static Map Generated() => MapViewState.Make("1", 1).Map!;
                private static readonly Map Own = Generated();
                private static readonly Map Other = B.Generated();
                private static readonly Map Install = A.Generated();
            }
            """;

        CollectionAssert.AreEqual(new[] { 10 }, Lines(source).ToArray());
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
    public void ACallThroughATypeAliasOfTestProgramIsFlagged()
    {
        var source = """
            using TP = OpenSettlers.Tests.TestProgram;
            class A
            {
                private static readonly RuleTables Rules = TP.Tables();
            }
            """;

        CollectionAssert.AreEqual(new[] { 4 }, Lines(source).ToArray());
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
                private static readonly Lazy<Map> Later = new(() => Generated());
                private static readonly Lazy<Map> Group = new(Generated);
                private static readonly Func<RuleTables> Local;
                static A()
                {
                    Local = () => TestProgram.Tables();
                    RuleTables Deferred() => TestProgram.Tables();
                }
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

    // A static method or property of a type of the source, and the code that runs when it is called or read: the
    // method's body, the property's expression body or getter.
    private sealed record Member(string Type, string Name, bool IsProperty, SyntaxNode Body);

    // How the source names TestProgram: whether `using static` imports it, and the receivers that name it, its own name
    // and each alias a using directive gives it.
    private sealed record TestProgramNames(bool Bare, HashSet<string> Receivers);

    // The 1-based line of the '=' of every static field or static auto-property initializer, and of the name of every
    // static constructor, in the source that reaches the install outside a lambda or local function: by calling it, or
    // by calling a static method or reading a static property of the source that reaches it, through any number of them.
    private static IEnumerable<int> Lines(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var imports = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(directive => directive.Name is { } name && Name(name) == "TestProgram")
            .ToList();
        var testProgram = new TestProgramNames(
            imports.Any(directive => directive.StaticKeyword != default),
            imports
                .Select(directive => directive.Alias?.Name.Identifier.Text)
                .OfType<string>()
                .Append("TestProgram")
                .ToHashSet()
        );
        var types = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Select(type => type.Identifier.Text)
            .ToHashSet();
        var members = root.DescendantNodes()
            .OfType<MemberDeclarationSyntax>()
            .Where(IsStatic)
            .Select(Declared)
            .OfType<Member>()
            .ToList();

        // Grow the helpers until no further member reaches the install through one of them.
        var helpers = new Dictionary<(string Type, string Name), bool>();
        bool Reaches(SyntaxNode node) => Runs(node, testProgram, types, helpers);
        while (members.FirstOrDefault(m => !helpers.ContainsKey((m.Type, m.Name)) && Reaches(m.Body)) is { } helper)
        {
            helpers[(helper.Type, helper.Name)] = helper.IsProperty;
        }

        return root.DescendantNodes()
            .Where(node => Anchor(node) is not null && Reaches(node))
            .Select(node => Anchor(node)!.Value.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    // The static method or property the declaration declares with the code it runs, null for any other declaration.
    private static Member? Declared(MemberDeclarationSyntax declaration) =>
        (declaration.Parent as TypeDeclarationSyntax)?.Identifier.Text is not { } type
            ? null
            : declaration switch
            {
                MethodDeclarationSyntax method when (method.Body ?? (SyntaxNode?)method.ExpressionBody) is { } body =>
                    new Member(type, method.Identifier.Text, false, body),
                PropertyDeclarationSyntax property
                    when (
                        property.ExpressionBody
                        ?? property
                            .AccessorList?.Accessors.Where(a => a.IsKind(SyntaxKind.GetAccessorDeclaration))
                            .Select(a => a.Body ?? (SyntaxNode?)a.ExpressionBody)
                            .FirstOrDefault()
                    )
                        is { } body => new Member(type, property.Identifier.Text, true, body),
                _ => null,
            };

    // Whether the node, outside the body of a lambda or local function, calls the install or calls or reads a helper.
    private static bool Runs(
        SyntaxNode node,
        TestProgramNames testProgram,
        HashSet<string> types,
        Dictionary<(string Type, string Name), bool> helpers
    ) =>
        node.DescendantNodesAndSelf(child =>
                child is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax) && !IsNameOf(child)
            )
            .Any(child =>
                child is InvocationExpressionSyntax call && IsInstallCall(call, testProgram)
                || child is SimpleNameSyntax name && IsHelperUse(name, types, helpers)
            );

    // Whether the name calls a helper method or reads a helper property. A name in a type position, a named argument,
    // or an object initializer's member names no helper. A qualified name resolves through its receiver where that is
    // a type of the source and resolves to nothing otherwise; a bare name resolves through the types around it. A
    // method name not called is a method group, which runs nothing yet.
    private static bool IsHelperUse(
        SimpleNameSyntax name,
        HashSet<string> types,
        Dictionary<(string Type, string Name), bool> helpers
    )
    {
        if (
            SyntaxFacts.IsInTypeOnlyContext(name)
            || name.Parent is NameColonSyntax or MemberBindingExpressionSyntax or QualifiedNameSyntax
            || name.Parent is AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } assignment
                && assignment.Left == name
        )
        {
            return false;
        }

        var qualified = name.Parent is MemberAccessExpressionSyntax access && access.Name == name;
        var owners = qualified
            ? Name(((MemberAccessExpressionSyntax)name.Parent!).Expression) is { } receiver && types.Contains(receiver)
                ? [receiver]
                : Array.Empty<string>()
            : name.Ancestors().OfType<TypeDeclarationSyntax>().Select(type => type.Identifier.Text);
        var use = qualified ? (ExpressionSyntax)name.Parent! : name;

        return owners.Any(owner =>
            helpers.TryGetValue((owner, name.Identifier.Text), out var isProperty)
            && (isProperty || use.Parent is InvocationExpressionSyntax invocation && invocation.Expression == use)
        );
    }

    // Whether the node is a nameof(...) expression, which evaluates nothing.
    private static bool IsNameOf(SyntaxNode node) =>
        node is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } };

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

    // Whether the call is TestProgram.Tables() or TestProgram.OpenInstall(): qualified by TestProgram or an alias of it,
    // or, where the source imports TestProgram with `using static`, bare.
    private static bool IsInstallCall(InvocationExpressionSyntax call, TestProgramNames testProgram) =>
        InstallCalls.Contains(Name(call.Expression))
        && call.Expression switch
        {
            MemberAccessExpressionSyntax access => testProgram.Receivers.Contains(Name(access.Expression) ?? ""),
            SimpleNameSyntax => testProgram.Bare,
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
