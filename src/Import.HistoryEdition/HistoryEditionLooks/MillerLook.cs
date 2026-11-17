using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>The miller's look (<c>0x4400d3</c>): the grinding's constant, the sack's for the step that takes the flour out, and the walk's.</summary>
/// <param name="tables">The tables of the exe: the miller's adds.</param>
sealed class MillerLook(RuleTables tables) : FoodHutLook<Miller>(tables);
