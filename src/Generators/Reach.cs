using Microsoft.CodeAnalysis;

namespace OpenSettlers.Generators;

/// <summary>Whether a generated registry can name a module type in a <c>new</c> expression.</summary>
static class Reach
{
    /// <summary>
    /// Whether <paramref name="type"/> or a type it is nested in is generic, and whether it or a type it is nested in is private or
    /// protected; the registry can build neither.
    /// </summary>
    public static (bool Generic, bool Unreachable) Of(INamedTypeSymbol type)
    {
        var generic = false;
        var unreachable = false;
        for (var holder = type; holder is not null; holder = holder.ContainingType)
        {
            generic |= holder.IsGenericType;
            unreachable |=
                holder.DeclaredAccessibility
                    is Accessibility.Private
                        or Accessibility.Protected
                        or Accessibility.ProtectedAndInternal;
        }

        return (generic, unreachable);
    }
}
