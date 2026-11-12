using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace OpenSettlers.Generators;

/// <summary>Where a declaration's name stands, kept as values so that a model of it compares equal across compilations.</summary>
internal sealed record Place(string Path, TextSpan Span, LinePositionSpan Lines)
{
    public static Place Of(Location location)
    {
        var line = location.GetLineSpan();
        return new Place(line.Path, location.SourceSpan, line.Span);
    }

    public Location ToLocation() => Location.Create(Path, Span, Lines);
}
