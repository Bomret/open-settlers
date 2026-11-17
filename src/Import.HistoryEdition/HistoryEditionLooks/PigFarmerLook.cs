using OpenSettlers.Domain;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>The pig farmer's look (<c>0x43ff56</c>): the work's constant, the pig's for the step that takes it out, and the walk's.</summary>
/// <param name="tables">The tables of the exe: the pig farmer's adds.</param>
sealed class PigFarmerLook(RuleTables tables) : FoodHutLook<PigFarmer>(tables);
