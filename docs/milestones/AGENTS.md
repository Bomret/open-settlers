# Milestone scopes

Each file in this folder lists where in `Siedler1.exe` one milestone of the [roadmap](../roadmap.md) starts
its investigation. The roadmap owns each milestone's goal, runnable mode, and done condition. A scope file
is the input to that milestone's `/superdev:plan` interview.

- [M3 First loop](m3-first-loop.md)
- [M4 Construction](m4-construction.md)
- [M5 Food](m5-food.md)
- [M6 Mining and metal](m6-mining-and-metal.md)
- [M7 Military and territory](m7-military-and-territory.md)
- [M8 Computer opponent](m8-computer-opponent.md)
- [M9 Free-play release](m9-free-play-release.md)

## Leads

A scope file holds leads: routines, data, and fields that a survey of the local dump
(`tools/GhidraDump/README.md`) ties to the milestone. Each lead has one of these marks:

- **known:** the [program index](../rules/program-index.md) names it. The scope file links to the index
  and does not restate its role.
- **likely:** callers, callees, or fields point clearly to the role.
- **guess:** the role is a first reading and can be wrong.

A lead is not a rule. When an investigation settles a lead, record it in the program index and the rule
notes ([Rule notes](../rules/AGENTS.md#program-index)) and remove it from the scope file. A scope file
holds addresses and our own words only ([INV-0001](../invariants.md#inv-0001)).

Offsets without context are offsets into the game state block. "Player `+0x..`" is an offset into a
player block, and "view `+0x..`" one into a view record (see the program index, "Structures and data").
