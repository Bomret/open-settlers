using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// One pass of the original's main loop for the item tables (ADR 0005): the clock step, the scan that clears the retry bits
/// (<c>0x428728</c>), the objects' turn, the phase step (<c>0x4291c2</c>), then the slices of the flag table (<c>0x46414d</c>,
/// the carriers' requests only), the building table (<c>0x42cb2d</c>), and the settler table (<c>0x42f3d9</c>). The original's
/// other passes (the players' timers, the sound) are not ported.
/// </summary>
public sealed class MainPass(
    GameClock clock,
    RuleTables tables,
    Map map,
    SettlerTable settlers,
    BuildingTable buildings,
    GameHistory history,
    Causes causes,
    TablePass tablePass,
    CarrierBehavior carriers,
    WoodcutterBehavior woodcutter,
    Removal removal,
    CastleRoster roster
)
{
    // The phases of a pass cycle (docs/rules/timing.md). **Inferred** for a game with no computer opponent: the
    // original sets 33 where no player's start parameter at +0x1d6 is from 1 to 11 (0x42055e).
    private const int PhaseCount = 33;

    // The phases from this one on run no item table.
    private const int TablePhases = 32;

    /// <summary>The phase the last pass ran, 0 to 32.</summary>
    public int Phase { get; private set; }

    /// <summary>
    /// The building the next scan of the retry bits starts at: the scan runs over a window of buildings from a cursor that
    /// wraps at the end of the table.
    /// </summary>
    public int ScanCursor { get; private set; }

    /// <summary>
    /// Whether each pass gives every settler that walks, waits out a step, steps through a door, cuts, or searches
    /// (the activities of the states 2, 3, 4, 5, 0xe, 0x10, 0x11, 0x19) one more turn after the slices, as the original's view
    /// pass (<c>0x42f4f1</c>) does for the settlers in view. Off, a settler takes a turn only in its slice, every 16 or 17
    /// passes, as in the original's passes without a view.
    /// </summary>
    public bool EveryPassTurns { get; init; }

    /// <summary>Runs one pass.</summary>
    public void Run()
    {
        clock.Advance();
        ClearRetryBits();
        carriers.ClearFlagRetryBits();
        woodcutter.ObjectPass();
        Phase = (Phase + 1) % PhaseCount;
        if (Phase < TablePhases)
        {
            tablePass.Run(
                Phase,
                () => carriers.FlagPass(Phase),
                (slot, building) =>
                {
                    if (building.IsDemolishing)
                    {
                        removal.Burn(slot, building);
                    }
                    else
                    {
                        roster.HandleBuilding(building);
                    }
                }
            );
        }

        if (!EveryPassTurns)
        {
            return;
        }

        // 0x42f4f1 gives each settler in view one more turn every pass; the engine gives it to every settler in an activity
        // the view draws in motion, so the walk on the screen does not stall between the slice turns and the simulation
        // does not depend on the view. The other activities keep the slice turns alone.
        for (var slot = 0; slot < settlers.Count; slot++)
        {
            if (
                settlers[slot].Activity
                is Walks
                    or WalksRoad
                    or Door
                    or WaitsOutStep
                    or HandsOver
                    or WalksFree
                    or Cuts
                    or SearchesHome
            )
            {
                tablePass.Visit(slot);
            }
        }
    }

    // 0x428728: each pass clears the request-failed mark of a window of buildings from a cursor that wraps at the end of
    // the table, and stops after 10 buildings that had the mark set. A request that found no worker is made again
    // when its building's turn comes after that.
    private void ClearRetryBits()
    {
        var count = buildings.Count;
        if (count == 0)
        {
            return;
        }

        if (count <= ScanCursor)
        {
            ScanCursor = 0;
        }

        var window = Math.Min(count - ScanCursor, tables.ScanWindow(map.Size.Value));
        var left = 10;
        for (var i = 0; i < window; i++)
        {
            var building = buildings[ScanCursor];
            var had = building.RequestFailed;
            building.RequestFailed = false;
            if (had)
            {
                history.RecordBuilding(ScanCursor, EventType.RetryBitsCleared, causes.OfBuilding(ScanCursor));
            }

            if (had && --left == 0)
            {
                return;
            }

            ScanCursor++;
        }
    }
}
