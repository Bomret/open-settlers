using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>The butcher's look (<c>0x43ff90</c>): the work's constant, the meat's for the step that takes it out, and the walk's.</summary>
/// <param name="tables">The tables of the exe: the butcher's adds.</param>
sealed class ButcherLook(RuleTables tables) : FoodHutLook<Butcher>(tables);
