# Castle

This note records how the program places a player's first building, the castle, what the placement writes, and
what the castle holds when it stands. Addresses and routine names are in the
[Program index](program-index.md#castle-and-build-sites); the judgment of a node is in
[Build sites](build-sites.md); the item tables are in [Item tables](item-tables.md).

## Where the castle comes from

**Status:** confirmed.

A mission start places the castle by itself. `Game_Start` (`0x41ee84`) calls `0x41eeeb` after the map exists, and
`0x41eeeb` calls the routine at `0x41f1f4` for each of the four players. That routine reads the player's start
position (two bytes at state `+0x1e6` to `+0x1ed`, a column and a row for each player), and when the column byte is
not negative it copies the position to the player block (`+0xfc`, `+0xfe`), centers the view, judges the node with
the build-site evaluator, and calls the placement routine `0x442704`.

A free game places none. `Game_SetStartParams` (`0x468717`) stores 0xff in all four position pairs for a free game,
so `0x41f1f4` skips every player, and the player has no castle until he asks for one. The click handler's build
action 5 (`0x440d23`) calls `0x442630`. That routine judges the node under the cursor with the evaluator and
places the castle only when the node state is 7 and the site is 5; otherwise it asks for the error sound and
changes nothing else. After the check it holds the same placement as `0x442704`: the two copies differ in what
they do for the view and the sound.

Until the castle stands, every other build action is out of reach: the evaluator holds a player without a
castle to the unowned nodes, and the flag button (`0x442244`) places a flag only where the site is not 0 (emulated: `flag-verdicts.txt`). The
engine refuses every command but `CommandKind.PlaceCastle` before the castle stands, and refuses a second castle.

## What the placement writes

**Status:** confirmed for the map and the tables, **inferred** for the player fields. **Emulated:** the harness
(`castle:N:S0,S1,S2:L:x,y`) ran `0x442704` on five generated maps after the evaluator judged the node; the engine's
map matches it byte for byte in the six layers of the canonical form and in the number of nodes it changes, 217
every time (`tests/OpenSettlers.Tests/castle-placements.txt`).

- It takes the first free slot of the storage table (`Table120`), the building table (`Table18`), and the flag
  table (`Table70`) and links them: the building holds the storage, the storage holds the building and the flag,
  and the flag holds the building. The player keeps the three slot numbers.
- It sets bit 0 of the player's second byte, the mark that the castle stands, and bit 3 of the third byte, and
  adds 20 to a counter of the player's (`+0x116`) that the first settlers (below) also draw on.
- The castle's node takes the object code 4 and the flag bits 0x42 (ORed in), and its resource byte takes the
  building's slot number, 0 for the first. A resource deposit or a lake detail on the node is lost.
- The flag stands one column east and one row south of the castle: the object code 1, the flag bits 0x90 (ORed
  in), and the flag's slot number in the resource byte.
- The node and its six neighbors take the height that the evaluator handed on (the low 5 bits of the height byte).
- The player owns the castle's node and every node within 8 steps (the first 217 nodes of the spiral table): the
  top three bits of each unowned node's height byte become the player's number plus 4. The routines that do it are
  `0x45e375` and its callees; the engine holds the result (`CastlePlacement`) and not their walk. A node another
  player owns is not reached by this slice, which has one player.
- 20 settlers are created in the castle, one of each of these jobs: 1 of job byte 0x10, 5 of 0x54, 3 of 0x58, and
  one each of 0x48, 0x14, 0x18, 0x1c, 0x08, 0x0c, 0x2c, 0x50, 0x50, 0x24, 0x24 (as the emulator listed the first byte of
  each new item of the settler table). **Not ported:** no settler table exists yet; the slices that send a carrier
  or a worker out of the castle take the job bytes from here. The tools they carry are taken from the stock below.

## The start stock

**Status:** confirmed for the table and the arithmetic; the level of a free game is **inferred**.

The storage item holds 26 counts of goods. The placement fills them from a table of five rows of 26 numbers: the
stock at the levels 0, 10, 20, 30, and 40. A player's start stock level (player byte `+0x162`, 0 to 49) selects
the two rows around it (rows 3 and 4 at the levels from 30, and row 4 twice from 40) and a weight of the tenths
of the level times 0x199a out of 65536. Each count is the lower row's value plus the product of the difference
and the weight divided by 65536, one more where the fraction of that division is above one half, all in
16-bit arithmetic. The engine reads the five rows from the install's `Siedler1.exe` at run time, at `0x442e8a`
through the section headers of the executable (`ProgramTables`); it holds no copy (the harness verb `stockrows`
prints them, and `ProgramTablesTests` compares a digest of the rows read with the emulator's).

The placement then takes from the result what the castle's own construction and the 20 settlers use: 7 of good 7,
2 of good 9, 1 of good 15, 4 of good 16, 1 each of goods 17 and 20, 2 of good 21, 3 of good 22, and 3 each of goods 24
and 25. **Inferred:** the vector is the difference between the interpolated rows and the stock the emulator leaves,
not read from the subtractions of `0x442704` and its callees. **Emulated:** at the levels 0, 5, 9, 10, 25, 40, and 49
the stock the emulator leaves is the interpolated row less this; the levels from 40 give one stock.

The mission start applies more changes by mission number (`+0x354`); the free game applies none.

**Where the level comes from.** The level is the free-game setting byte at state `+0x37a`, which
`FreeGame_RandomizeOpponents` (`0x4692d4`) draws: it seeds the random generator from the free-game seed with the
masks 0x5a5a, 0xa5a5, and 0xc3c3 and draws four rounds of four values for the opponents, then two pairs for the two
human-capable players; the 19th draw, scaled to 0 to 40 (`draw * 0x29 / 65536`), is the level of player 0. The
writers of `+0x37a` in the dump are this routine and the save loader. **Inferred:** the free-game screen draws the
values whenever it opens (`0x4692aa`), and no click on it changes the byte, so the level follows from the seed on
the screen. **Emulated:** the harness (`opponents:S0,S1,S2`) gave the level for three seeds, which the engine's
`StartStock.LevelOf` reproduces.

## In the engine

`Game` takes the program's tables (`ProgramTables`), the map, and a start stock level (`MapViewState.StockLevel`
from the seed). `CommandKind.PlaceCastle`
with the node in `Command.X` and `Command.Y` places the castle through `BuildSite.Evaluate` and
`CastlePlacement.Apply`; `Game.Castle` and `Game.Stock` hold the result. The game changes the map's layers it was
given. The digest holds the castle and the stock. Departures: the settlers, the three item tables' fields, the
player's flag bits, and the mission changes are not held.

**Stand-in:** the map view picks the node of a click as the node whose drawn point (lifted by its height) lies
nearest to the click (`MapPicking`). The rule of the original's cursor is not recovered, so a click near the edge
of a triangle may pick another node than the original does.
