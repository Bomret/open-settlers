using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>The baker's look (<c>0x440112</c>): the baking's constant, the loaf's for the step that takes the bread out, and the walk's.</summary>
/// <param name="tables">The tables of the exe: the baker's adds.</param>
sealed class BakerLook(RuleTables tables) : FoodHutLook<Baker>(tables);
