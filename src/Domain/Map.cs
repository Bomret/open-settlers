using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The map: a grid of <see cref="Node"/> values in row-major order. The map wraps at its edges: a column or row outside
/// the grid is the one on the other side. A map comes from an importer of the adapters (the generator's layers or a
/// layout) and a game changes it.
/// </summary>
/// <remarks>A map of the size with every node empty.</remarks>
/// <param name="size">The size of the map.</param>
public sealed class Map(MapSize size)
{
    readonly Node[] _nodes = new Node[size.Width * size.Height];

    /// <summary>The size the map was made for.</summary>
    public MapSize Size { get; } = size;

    /// <summary>
    /// The random generator as the generation leaves it. The game's random generator continues from it, as the
    /// original's does (<c>docs/rules/woodcutting.md</c>). A map made by hand has the generator of all zeros.
    /// </summary>
    public RandomGenerator Generator { get; set; }

    /// <summary>The sum of the amounts of resource type 1 (<c>MapGen_CountResource1</c>); 0 before that pass.</summary>
    public int Resource1Total { get; set; }

    /// <summary>The number of nodes.</summary>
    public int Count => _nodes.Length;

    /// <summary>The objects on the map in row-major node order: every node that holds one.</summary>
    public IReadOnlyList<PlacedObject> Objects
    {
        get
        {
            var objects = new List<PlacedObject>();
            for (var index = 0; index < _nodes.Length; index++)
            {
                if (_nodes[index].Object.Exists)
                {
                    objects.Add(
                        new PlacedObject
                        {
                            X = index % Size.Width,
                            Y = index / Size.Width,
                            Object = _nodes[index].Object,
                        }
                    );
                }
            }

            return objects;
        }
    }

    /// <summary>The resource deposits on the map in row-major node order: every node whose resource type is not 0.</summary>
    public IReadOnlyList<ResourceDeposit> Deposits
    {
        get
        {
            var deposits = new List<ResourceDeposit>();
            for (var index = 0; index < _nodes.Length; index++)
            {
                if (_nodes[index].DepositType != 0)
                {
                    deposits.Add(
                        new ResourceDeposit
                        {
                            X = index % Size.Width,
                            Y = index / Size.Width,
                            Type = _nodes[index].DepositType,
                            Amount = _nodes[index].DepositAmount,
                        }
                    );
                }
            }

            return deposits;
        }
    }

    /// <summary>The index of a node; the column and the row wrap at the edges.</summary>
    public int Index(int x, int y) => (y & (Size.Height - 1)) * Size.Width + (x & (Size.Width - 1));

    /// <summary>The node at an index. The reference changes the map.</summary>
    public ref Node At(int index) => ref _nodes[index];

    /// <summary>The node in a column and a row, which wrap at the edges. The reference changes the map.</summary>
    public ref Node At(int x, int y) => ref _nodes[Index(x, y)];
}
