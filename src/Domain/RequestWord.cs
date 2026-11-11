namespace OpenSettlers.Domain;

/// <summary>
/// One of the two request words of a flag item, each a byte of classes with a rank byte after it (<c>docs/rules/construction-materials.md</c>):
/// the first is at <c>0x42</c> with its rank at <c>0x43</c>, the second at <c>0x44</c> with its rank at <c>0x45</c>.
/// </summary>
public enum RequestWord
{
    /// <summary>The word at <c>0x42</c>.</summary>
    First,

    /// <summary>The word at <c>0x44</c>.</summary>
    Second,
}
