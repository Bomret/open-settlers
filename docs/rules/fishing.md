# Fishing

This note covers how the fisher works. It states which building and which job he belongs to, how he picks a node at a shore, and how he walks to it. It states how he casts, how a cast takes a fish from the lake detail, and how he lays the fish on the flag. He works as the woodcutter does ([Woodcutting](woodcutting.md)), with three differences: his own search, an arrival that starts a cast, and a catch instead of a log. Addresses and routine names are in the [Program index](program-index.md#simulation). The settlers and their walk to the building are in [Settlers](settlers.md). The worker's request is in [Construction](construction.md#the-finished-building-asks-for-its-worker). The lake details the fisher takes from are in [Woodcutting](woodcutting.md#the-object-pass).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) starts a game as the woodcutter's scenarios do. The game has a finished building of type 1 at a flag, on a map with a lake east of the castle. The verb runs the original's own routines for each pass. The verb's `ld=` field lists the lake details (byte 0 of record B) that change in a pass.

The engine runs the same commands. It matches these values in two games (`tests/OpenSettlers.Tests/fish-walks.txt`):

- the fisher's item after every change of his state, his node, his timers, and his counters;
- every object byte and every lake detail that changes;
- the building's bytes and the flag's goods where they change;
- the castle's count of fish;
- the generator's words.

The first game (`fisher-lake`) runs 3000 passes. The fisher finds a node, casts seven times at a lake detail of 1, and catches a fish at the seventh. The detail falls to 0. He lays the fish on the flag at pass 1928, and the fish reaches the castle at pass 2875, so the castle's count of fish is 1. The second game (`fisher-full-flag`) lays eight logs on the flag before the fish comes. The fish is lost, and nothing else changes.

The third game (`fisher-dry-lake`) is the first game with a lake detail of 0 written into the nodes x 20 to 30, y 0 to 12 before the pass 940 (the verb's `ld@PASS,X0,Y0,X1,Y1,VALUE` field of the SET list). The fisher casts ten times from pass 954 on, and no cast draws, catches, or lowers a detail. The engine matches the generator's words at every line while its casts draw nothing, so no cast takes a draw. The words do change inside the casting state, at the passes 1449, 1515, 1631, 1664, 1779, 1895, 1994, and 2010. Those are draws of the lake-detail pass for other nodes of the map, the same passes that print `ld=` lines elsewhere. He turns back at pass 2076 with nothing.

The removal of his building matches the original in three games (`removals.txt`: `fisher-inside`, `fisher-fishing`, `fisher-with-fish`).

## The building and the job

**Status:** confirmed for the request and the work, **inferred** for the name.

The handler of building type 1 is at `0x42e0b8`, entry 1 of the table at `0x42cd1d`. It asks for a settler of job 11 with the tool of stock word `0x24`, which is good 18 ([Food buildings](food-buildings.md#the-jobs)). The work of job 11, described below, casts at the lake details and lays good 1 on the flag. No text of the install names the job. So the name fisher, and the name fish for good 1, are inferred from the work (INV-0006).

The castle starts with one settler of job 11, in the slot 15 of the first game. The request takes him first. A building asked for later takes a settler without a job and a tool from the stock ([Settlers](settlers.md#the-request)).

## The start at the building

**Status:** confirmed; **emulated**.

At the door the worker is in state 4. He runs the routine of his job through the table at `0x43ca54`. For job 11 that routine is `0x43d14d`. Where the settler is not sent home, byte 11, the mode, is not `0xfe`. Then the routine empties the building's node of the settler's slot and takes state `0x1f`. A settler sent home goes on at `0x43cd06`. The routine sets no request class, so the building asks for no good.

## Choosing a node

**Status:** confirmed; **emulated**.

State `0x1f` is at `0x433f71`. When the timer has run out, the worker takes a draw of the game's generator. From the draw he takes an entry of the spiral table, `((draw & 0xfc) + 4) / 4`. That entry is from 1 to 64, half the woodcutter's range. The entry is counted from the node he stands on, which is the building's.

The node of that entry is his goal where all of these hold:

- The object byte of the node, without its bit 7, is 0. No object stands there.
- The flag byte of the node, without its bits 6 and 7, is 0. No road leaves it.
- The node is a shore. It is a shore where the east test or the west test holds:
  - East test (`0x434022`): the lower triangle of the node's tile is water, and the upper triangle of the tile north-west of the node is not.
  - West test (`0x434056`): the lower triangle of the tile west of the node is water, and the upper triangle of the tile north of the node is not.

A triangle is water where its terrain code is below 4 ([Map form](map-form.md)). The checks read bits 2 and 3 of the nibble.

Where one check fails, 100 is added to the timer, and he draws again. The draws go on until that sum wraps the timer, which ends the turn. The woodcutter's miss adds 400.

**No other check:** a node where another settler stands is a goal as well. So is a node the fisher does not own.

On a hit, the vector in bytes 11 and 12 is the goal's offset from the building less one each way. Bytes 13 and 14 hold the negated vector, which is the way back. Byte 15 holds `0x10`, the state of the free walk. The free walk comes after the step from the building onto its flag, which is state 7 at `0x43e02e`, as the woodcutter's.

## The walk out

**Status:** confirmed; **emulated**.

State `0x10` is the free walk ([Woodcutting](woodcutting.md#the-walk)), with no check of its own. At its end the arrival of job 11 takes over. The routine at `0x437ddd` jumps by the job, and entry 11 goes to `0x437e53`, which jumps to `0x438232`.

## The arrival at the node

**Status:** confirmed; **emulated**.

The arrival has byte 13 not at `0x80`. Then the way back in bytes 13 and 14 becomes the vector in bytes 11 and 12. What follows depends on the node he stands on, which another settler or a builder may have changed since the search:

- Where no road leaves the node and it is still a shore, he casts. Bytes 13, 14, and 15 are cleared, the timer is `0x2ff`, and the state is `0x20`. The frame, byte 1, is `0x84` where the east test holds, else `0x83` where the west test holds. The east test comes first.
- Otherwise byte 13 is set to `0x80`, the way-back mark. Bytes 14 and 15 are cleared and the timer is 0. He walks back at once, still in state `0x10`.

## Casting

**Status:** confirmed; **emulated**.

State `0x20` is at `0x433e05`. It acts when the timer has run out. It takes turns in a loop. A turn ends the loop where the timer wraps.

1. Where byte 14, the catch, is not 0, or byte 15, the casts, is 10, he turns back. Byte 13 is `0x80`, byte 15 is 0, the timer is 0, and the state is `0x10`. Byte 14 stays as it is.
2. Byte 13, the turns, goes up by one.
3. On an even turn, 2 is taken off the frame and `0x300` is added to the timer.
4. On an odd turn he casts. A cast picks a water node, takes a draw where that node holds a lake detail, and adds one to byte 15. Then 2 is added to the frame and `0x80` to the timer.

The water node of a cast is the west neighbour of the fisher's node where the frame is `0x83`, and the east neighbour where it is `0x84`. Where that neighbour has no bit 6 in its flag byte, the node is the one below it instead (`0x433e47`, `0x433e72`). The node below the west neighbour is the node south of the fisher, and the node below the east neighbour is the node south-east of him.

The cast reads the lake detail of the water node, byte 0 of its record B, as a byte. Where the detail is 0, the cast draws nothing and catches nothing. Where it is not 0, the cast takes a draw of the generator. The draw's low six bits are compared with the detail plus 4, as a byte. Where the bits are below it, the detail falls by one and byte 14 is 1. So the chance of a catch is the detail plus 4 in 64, and a detail never falls below 0, because a cast with a detail of 0 takes nothing. A detail of 252 or more adds up to a byte below 4, and a cast there catches in fewer than 4 draws of 64.

The frame is `0x83` or `0x84` at each cast, because the even turn takes back the 2 the cast added. A fisher turns back after 10 casts at the latest, 20 turns.

## Back at the flag

**Status:** confirmed; **emulated**.

The way back is the free walk of state `0x10`, with the vector in bytes 11 and 12 and the mark `0x80` in byte 13. At its end the arrival of job 11 reads byte 13 as `0x80` (`0x438232`). Then one of three things happens:

- With byte 14 negative, he goes home as any settler (`0x4385ab`, [Settlers](settlers.md#walking-home)).
- With byte 14 not 0, `Flag_PutGood` (`0x436b46`, [Goods](goods.md)) lays good 1, the fish, on the first empty place of the flag (the call at `0x43837f`). A flag with no empty place takes nothing, and the fish is gone. The fisher goes on with no check.
- Byte 11 is then 0 and `0x43c7c2` takes him into the building. From there he searches for the next node.

The fish goes over the roads like any good. Its class is 0, first request word ([Food buildings](food-buildings.md#the-goods)). No building of this milestone asks for it, so it goes to the castle, whose count of fish goes up by one.

## Removal

**Status:** confirmed; **emulated**.

Taking his building down sends the settlers inside it home (`0x461b77`, [Removal](removal.md)). The fisher takes the generic ways of any settler of a hut:

- Inside, he is on the building's node. He takes state `0x1c` and walks home.
- At the shore or on the way, he is not on the building's node. The removal gives him state `0x19`, the search for the nearest flag, with bytes 13, 14, and 15 as they were. The walk home follows.
- With a fish on the way back, the fish goes with him. Nothing lays it on the flag or counts it, and the castle's count of fish does not change.

## Drawing

**Status:** the figure words are **confirmed** for the walk by the draw run of the `job11` recording (`settler-draws.txt`). The words of the cast with no catch (`0x2c80`) are read from the code and seen in a play run (`play/fisher.play` on `layouts/fisher.jsonc`): at the shore, frame `0x84`, the figure holds a rod out over the water. The words with a catch are read from the code and seen in a second play run (`play/fisher-catch.play`, on the map of seed `4455544685788642`, size 1, whose generated lake holds lake details): at the shore with byte 14 set (frame `0x86`, the figure byte from 0x80) the figure holds the rod with a fish hanging at its end (`0x2d80`), on the way back with the fish (frame 34, `0x2f00`) the fish is at his side, and the fish lies on his flag.

The figure handler of job 11 is at `0x43fed8`, reached by the stub at `0x43f79c`. It adds a value to the figure byte:

- `0x2c00` for a byte below 0x80, the walk (`0x43ff4b`).
- `0x2f00` for a byte below 0x80 on the way back with a fish, where byte 13 is `0x80`, byte 14 is 1, and the state is `0x10` (`0x43ff40`). The fish is part of the figure.
- `0x2c80` for a byte from 0x80, the cast (`0x43ff0f`).
- `0x2d80` for a byte from 0x80 where byte 14 is 1, a fish caught (`0x43ff1a`).

The bytes other than `0x80`, `0x87`, `0x88`, and `0x8f` also ask for a sound (`0x44fe13`), which the engine leaves out. The fish on the flag is drawn as every good on a flag is ([Goods](goods.md)).

## In the engine

`src/Application/FisherRules.cs`, the rules module of the fisher (`JobRules<Fisher>`), holds `Start`, `SearchWater`, `Arrive`, and `Fish`. The shared states are `HutWork`'s: the door, the step out, the free walk, and the entry into the building. `HutWork.ObjectPass` moves the lake details.

The fisher's work is the activities `LooksForWater`, `StepsOntoFlag`, `WalksFree`, `Fishes`, `EntersBuilding`, and `Door`. A free walk out to the node has the purpose `ToWater`, and the way back has `BackToHut`, whose `Carrying` is the catch. The events are `search-water` and `fish`, with the cause `fishing`. The lake detail is the `LakeDetail` of the node.

**Departure:** the original reads the byte of record B at the water node whether that node holds a lake detail, a deposit, or a link to a flag or a building. The engine reads the lake detail and the deposit. A water node that links to a flag or a building reads 0, so the engine catches nothing there. The original would count the link down. No scenario here has a fisher cast at such a node.
