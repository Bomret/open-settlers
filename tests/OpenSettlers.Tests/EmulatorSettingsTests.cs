using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// Each command of the emulator harness starts from its own verb's settings (INV-0018): defaultSettings of
// tools/GhidraDump/EmulateMapStart.java, which runs at the start of each command, sets every field a verb can set, and
// clears every list or array a final field holds. The check reads the script as text and needs neither Ghidra nor the install.
[TestClass]
public sealed class EmulatorSettingsTests
{
    // A field of the script's class: a declaration at the class's indent, with its modifiers, its name, and its initializer.
    static readonly Regex Field = new(
        @"^ {4}(?<modifiers>(?:(?:private|protected|public|static|final|transient|volatile)\s+)*)[\w.\[\]]+(?:<[^;=()]*>)?(?:\[\])*\s+(?<name>\w+)\s*(?:=\s*(?<initializer>[^;]*))?;\s*$",
        RegexOptions.Multiline
    );

    [TestMethod]
    public void DefaultSettingsSetsEveryFieldOfTheScriptThatACommandCanChange()
    {
        var source = File.ReadAllText(PythonTool.InRepo("tools", "GhidraDump", "EmulateMapStart.java"))
            .Replace("\r", "");
        var start = source.IndexOf("void defaultSettings() {", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "EmulateMapStart.java declares defaultSettings");
        var body = source[start..source.IndexOf("\n    }", start, StringComparison.Ordinal)];

        // A command can change a field that is not final, and the contents of a final field that holds an object it made, such
        // as a list or an array. emu is the emulator, made new for each command before defaultSettings runs.
        var fields = Field
            .Matches(source)
            .Where(found =>
                !found.Groups["modifiers"].Value.Contains("static")
                && (
                    !found.Groups["modifiers"].Value.Contains("final")
                    || found.Groups["initializer"].Value.StartsWith("new ", StringComparison.Ordinal)
                )
            )
            .Select(found => found.Groups["name"].Value)
            .Where(name => name != "emu")
            .ToArray();
        Assert.IsTrue(fields.Length > 0, "the script declares fields a command can change");

        // defaultSettings sets a field, clears its collection, or fills its array.
        var unset = fields
            .Where(name => !Regex.IsMatch(body, $@"\b{name}\s*=[^=]|\b{name}\.clear\(\)|Arrays\.fill\(\s*{name}\b"))
            .ToArray();
        Assert.AreEqual(0, unset.Length, "defaultSettings does not set " + string.Join(", ", unset));
    }
}
