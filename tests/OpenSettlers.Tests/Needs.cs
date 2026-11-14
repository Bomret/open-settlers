namespace OpenSettlers.Tests;

/// <summary>The <c>[TestCategory]</c> names of tests that need the install or <c>analyzeHeadless</c>; the full and focused commands of <c>mise.toml</c> filter them out, and its integration command runs only them.</summary>
static class Needs
{
    public const string Install = "Install";
    public const string Ghidra = "Ghidra";
}
