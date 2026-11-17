using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenSettlers.Import.ContentSet;

/// <summary>
/// A value of a rule file with the file and the key it was read at. Every fault it raises is an <see cref="InvalidDataException"/> of the
/// form <c>&lt;file&gt;: &lt;key&gt;: &lt;reason&gt;</c>, as the manifest's are. The key is the path from the file's root: names joined with
/// <c>.</c>, and <c>[i]</c> for the entry <c>i</c> of a list.
/// </summary>
sealed class RuleNode(string file, string key, JsonElement value)
{
    /// <summary>Whether the value is a list.</summary>
    public bool IsList => value.ValueKind == JsonValueKind.Array;

    /// <summary>Whether the value is text.</summary>
    public bool IsText => value.ValueKind == JsonValueKind.String;

    /// <summary>The fault of this value.</summary>
    public InvalidDataException Fault(string reason) => new($"{file}: {(key.Length == 0 ? "$" : key)}: {reason}");

    /// <summary>
    /// The members of an object that holds exactly the keys <paramref name="required"/> and, besides, any of <paramref name="optional"/>.
    /// A repeated key, a missing key, and a key the object does not know are each a fault at that key.
    /// </summary>
    public IReadOnlyDictionary<string, RuleNode> Fields(
        IEnumerable<string> required,
        IEnumerable<string>? optional = null
    )
    {
        var members = Members();
        var known = required.Concat(optional ?? []).ToHashSet();
        var fields = members.ToDictionary(member => member.Key, member => member.Node);
        foreach (var name in required.Where(name => !fields.ContainsKey(name)))
        {
            throw new InvalidDataException($"{file}: {Join(name)}: missing");
        }

        foreach (var member in members.Where(member => !known.Contains(member.Key)))
        {
            throw member.Node.Fault("unknown key");
        }

        return fields;
    }

    /// <summary>The members of an object in file order. A repeated key is a fault at that key.</summary>
    public IReadOnlyList<(string Key, RuleNode Node)> Members()
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Fault("not an object");
        }

        var members = new List<(string, RuleNode)>();
        var seen = new HashSet<string>();
        foreach (var property in value.EnumerateObject())
        {
            var node = new RuleNode(file, Join(property.Name), property.Value);
            if (!seen.Add(property.Name))
            {
                throw node.Fault("duplicate key");
            }

            members.Add((property.Name, node));
        }

        return members;
    }

    /// <summary>The entries of a list: exactly <paramref name="count"/> of them where it is given, else at most <paramref name="max"/>.</summary>
    public IReadOnlyList<RuleNode> Items(int? count = null, int max = int.MaxValue)
    {
        if (!IsList)
        {
            throw Fault("not a list");
        }

        var length = value.GetArrayLength();
        if (count is { } expected && length != expected)
        {
            throw Fault($"holds {length} entries, expected {expected}");
        }

        if (length > max)
        {
            throw Fault($"holds {length} entries, at most {max}");
        }

        return [.. value.EnumerateArray().Select((item, at) => new RuleNode(file, $"{key}[{at}]", item))];
    }

    /// <summary>The whole number the value is, from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public int Int(int min, int max)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw Fault("not a whole number");
        }

        return number >= min && number <= max ? number : throw Fault($"{number} is outside {min} to {max}");
    }

    /// <summary>
    /// The numbers from <paramref name="min"/> to <paramref name="max"/> the value names: one number, or a list <c>[first, last]</c>
    /// that runs from <c>first</c> to <c>last</c>. A range that runs backwards is a fault.
    /// </summary>
    public (int First, int Last) Range(int min, int max)
    {
        var bounds = IsList ? Items(2) : new[] { this, this };
        var (first, last) = (bounds[0].Int(min, max), bounds[1].Int(min, max));

        return last < first ? throw Fault($"the range {first} to {last} runs backwards") : (first, last);
    }

    /// <summary>The text the value is.</summary>
    public string Text() => IsText ? value.GetString()! : throw Fault("not text");

    /// <summary>The term the value's text names in <paramref name="terms"/>; <paramref name="what"/> names the kind of term for the fault.</summary>
    public T Term<T>(IReadOnlyDictionary<string, T> terms, string what)
    {
        var text = Text();

        return terms.TryGetValue(text, out var term)
            ? term
            : throw Fault($"unknown {what} \"{text}\"; the known ones are {string.Join(", ", terms.Keys)}");
    }

    string Join(string name) => key.Length == 0 ? name : $"{key}.{name}";
}
