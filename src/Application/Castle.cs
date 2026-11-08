namespace OpenSettlers.Simulation;

/// <summary>A player's castle: the node it stands on and the height its site was levelled to.</summary>
/// <param name="X">The node's column; the castle's flag stands one column east and one row south of it.</param>
/// <param name="Y">The node's row.</param>
/// <param name="Height">The height the castle's node and its six neighbors were set to.</param>
public readonly record struct Castle(int X, int Y, int Height);
