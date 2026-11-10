using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// No static field or static auto-property initializer of a test class may call TestProgram.Tables() or
// TestProgram.OpenInstall(). Without the install, the Assert.Inconclusive they throw then fires during type
// initialization, where MSTest reports it as a TypeInitializationException that fails every test of the class.
// Build the value inside the test instead, for example through an expression-bodied accessor that caches it.
[TestClass]
public sealed class InstallInitializerTests
{
    private static readonly Regex InstallCall = new(@"\bTestProgram\s*\.\s*(Tables|OpenInstall)\s*\(");
    private static readonly Regex Container = new(@"\b(class|struct|record|interface|enum|namespace)\b");
    private static readonly Regex Static = new(@"\bstatic\b");

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

    // The 1-based line of the '=' of every static field or static auto-property initializer in the source that calls
    // the install outside a lambda.
    private static IEnumerable<int> Lines(string source)
    {
        var code = Code(source);
        var offenders = new List<int>();
        var frames = new Stack<Member>();
        frames.Push(new Member(0));

        for (var i = 0; i < code.Length; i++)
        {
            var member = frames.Peek();
            switch (code[i])
            {
                case '(' or '[':
                    member.Depth++;
                    break;
                case ')' or ']':
                    member.Depth--;
                    break;
                case '{' when member.Depth == 0 && member.Initializer < 0 && Container.IsMatch(member.Header(code, i)):
                    frames.Push(new Member(i + 1));
                    break;
                case '{':
                    member.Depth++;
                    break;
                case '}' when member.Depth == 0:
                    frames.Pop();
                    if (frames.Count == 0)
                    {
                        return offenders;
                    }

                    frames.Peek().Reset(i + 1);
                    break;
                case '}':
                    member.Depth--;
                    if (member.Depth == 0 && member.Initializer < 0 && NextCode(code, i + 1) != '=')
                    {
                        member.Reset(i + 1);
                    }

                    break;
                case '=' when member.Depth == 0 && member.Initializer == -1:
                    if (i + 1 < code.Length && code[i + 1] == '>')
                    {
                        member.Initializer = -2;
                    }
                    else if (!IsOperator(code, i))
                    {
                        member.Initializer = i;
                        member.IsStatic = Static.IsMatch(member.Header(code, i));
                    }

                    break;
                case ';' when member.Depth == 0:
                    if (member.Initializer >= 0 && member.IsStatic && CallsTheInstall(code, member.Initializer, i))
                    {
                        offenders.Add(LineOf(code, member.Initializer));
                    }

                    member.Reset(i + 1);
                    break;
            }
        }

        return offenders;
    }

    private sealed class Member(int start)
    {
        public int Start = start;
        public int Depth;

        // The index of the member's initializer '=', -1 while none was seen, -2 once the member is expression-bodied.
        public int Initializer = -1;
        public bool IsStatic;

        public string Header(string code, int end) => code[Start..end];

        public void Reset(int start)
        {
            Start = start;
            Depth = 0;
            Initializer = -1;
            IsStatic = false;
        }
    }

    // Whether the '=' at the index is part of ==, !=, <=, or >=.
    private static bool IsOperator(string code, int i) =>
        (i + 1 < code.Length && code[i + 1] == '=') || (i > 0 && code[i - 1] is '=' or '!' or '<' or '>');

    private static char NextCode(string code, int from)
    {
        for (var i = from; i < code.Length; i++)
        {
            if (!char.IsWhiteSpace(code[i]))
            {
                return code[i];
            }
        }

        return '\0';
    }

    private static int LineOf(string code, int index) => code.AsSpan(0, index).Count('\n') + 1;

    // Whether the initializer between the '=' and the ';' calls the install outside the body of a lambda.
    private static bool CallsTheInstall(string code, int from, int to)
    {
        foreach (Match call in InstallCall.Matches(code[..to], from))
        {
            if (!InLambda(code, from, call.Index))
            {
                return true;
            }
        }

        return false;
    }

    // Whether the index lies in the body of a lambda that starts after `from`: after a '=>' whose bracket depth the
    // code has not left, and no ',' at that depth, before the index.
    private static bool InLambda(string code, int from, int index)
    {
        var depth = 0;
        var bodies = new Stack<int>();
        for (var i = from + 1; i < index; i++)
        {
            switch (code[i])
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth--;
                    while (bodies.Count > 0 && bodies.Peek() > depth)
                    {
                        bodies.Pop();
                    }

                    break;
                case ',':
                    while (bodies.Count > 0 && bodies.Peek() >= depth)
                    {
                        bodies.Pop();
                    }

                    break;
                case '=' when i + 1 < index && code[i + 1] == '>':
                    bodies.Push(depth);
                    i++;
                    break;
            }
        }

        return bodies.Count > 0;
    }

    // The source with every comment and every string or character literal replaced by spaces, line breaks kept.
    private static string Code(string source)
    {
        var code = new StringBuilder(source);
        var i = 0;
        while (i < source.Length)
        {
            var end = LiteralEnd(source, i);
            if (end == i)
            {
                i++;
                continue;
            }

            for (var j = i; j < end; j++)
            {
                if (code[j] != '\n')
                {
                    code[j] = ' ';
                }
            }

            i = end;
        }

        return code.ToString();
    }

    // The index after the comment or literal that starts at the index, or the index itself when none starts there.
    private static int LiteralEnd(string s, int i)
    {
        if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '/')
        {
            var end = s.IndexOf('\n', i);
            return end < 0 ? s.Length : end;
        }

        if (s[i] == '/' && i + 1 < s.Length && s[i + 1] == '*')
        {
            var end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
            return end < 0 ? s.Length : end + 2;
        }

        if (s[i] == '\'')
        {
            var j = i + 1;
            while (j < s.Length && s[j] != '\'')
            {
                j += s[j] == '\\' ? 2 : 1;
            }

            return Math.Min(j + 1, s.Length);
        }

        var quote = i;
        var verbatim = false;
        var interpolated = false;
        while (quote < s.Length && s[quote] is '$' or '@')
        {
            verbatim |= s[quote] == '@';
            interpolated |= s[quote] == '$';
            quote++;
        }

        if (quote >= s.Length || s[quote] != '"' || (quote > i && IsIdentifierPart(s, i - 1)))
        {
            return i;
        }

        var quotes = 0;
        while (quote + quotes < s.Length && s[quote + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3)
        {
            var closing = new string('"', quotes);
            var end = s.IndexOf(closing, quote + quotes, StringComparison.Ordinal);
            return end < 0 ? s.Length : end + quotes;
        }

        var k = quote + 1;
        while (k < s.Length)
        {
            if (verbatim && s[k] == '"')
            {
                if (k + 1 < s.Length && s[k + 1] == '"')
                {
                    k += 2;
                    continue;
                }

                return k + 1;
            }

            if (!verbatim && s[k] == '\\')
            {
                k += 2;
                continue;
            }

            if (!verbatim && s[k] == '"')
            {
                return k + 1;
            }

            if (interpolated && s[k] == '{')
            {
                if (k + 1 < s.Length && s[k + 1] == '{')
                {
                    k += 2;
                    continue;
                }

                k = HoleEnd(s, k + 1);
                continue;
            }

            k++;
        }

        return s.Length;
    }

    // The index after the '}' that closes the interpolation hole whose code starts at the index.
    private static int HoleEnd(string s, int i)
    {
        var depth = 0;
        while (i < s.Length)
        {
            var end = LiteralEnd(s, i);
            if (end != i)
            {
                i = end;
                continue;
            }

            if (s[i] == '{')
            {
                depth++;
            }
            else if (s[i] == '}')
            {
                if (depth == 0)
                {
                    return i + 1;
                }

                depth--;
            }

            i++;
        }

        return s.Length;
    }

    private static bool IsIdentifierPart(string s, int i) => i >= 0 && (char.IsLetterOrDigit(s[i]) || s[i] == '_');
}
