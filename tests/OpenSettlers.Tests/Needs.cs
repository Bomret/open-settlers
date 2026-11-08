namespace OpenSettlers.Tests;

/// <summary>
/// The <c>[TestCategory]</c> names of the tests that need more than the code: <see cref="Install"/> for the
/// reference install (<c>SIEDLER_DIR</c>, <see cref="TestProgram.OpenInstall"/>, <see cref="TestProgram.Tables"/>) and
/// <see cref="Ghidra"/> for <c>analyzeHeadless</c>. The focused command in <c>mise.toml</c> filters both out by name;
/// <c>NeedsTests</c> checks that the filter names every constant here.
/// </summary>
internal static class Needs
{
    public const string Install = "Install";
    public const string Ghidra = "Ghidra";
}
