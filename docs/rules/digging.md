# The digger

This note covers how a large building site asks for a digger. It states how he levels the ground of the site's node and its six neighbours to the site's height in state 8. It also states how the build-site evaluator treats a large site that waits for him. Addresses and routine names are in the [Program index](program-index.md#castle-and-build-sites). The request for a worker is in [The builder](construction.md#the-worker-request). The site's placement is in [Build sites](build-sites.md#placing-a-building). The materials the site asks for after the levelling are in [Materials for a building site](construction-materials.md#the-building-passs-turn-of-a-site).

**Emulated.** The file `tests/OpenSettlers.Tests/construction-digger.txt` holds the output of the harness verb `construct:` (`tools/GhidraDump/README.md`). The game has a sawmill's site, a large type, on ground that is not level. The castle's own digger is the settler of slot 13 with job 2. He walks out to the site at pass 292. He works from pass 342 to pass 1695 in 11 digs that change 11 heights. He walks home by pass 1959.

`ConstructionDiggerTests` runs the engine through the same commands. These values match the fixture:

- The digger's 16 bytes after every pass.
- The heights of the site's seven nodes after every pass.
- The site's bytes at the passes the fixture lists.

The harness lists the heights a pass changed (`h=`). The fixture also holds the evaluator's verdict on every node of the castle's land while the site waits (`v@0,0`). It holds the verdict twice. The first is on the ground as it is. The second is with the ground west of the site levelled to a lower height by `l@x,y,H` clicks.

## The large site waits for the ground

**Status:** confirmed, and emulated.

A large type under construction reaches its handler `0x42d328` (`Building_HandleLargeConstruction`) first. While the progress word is not 0, the handler goes on at `0x42d55f`, the request for the builder ([The builder](construction.md#the-request-for-a-builder)). While the progress word is 0, the routine does these steps in order:

1. It returns at once where the status has a worker inside or on the way, which are bits 6 and 7. The digger has the ground.
2. It tests the node and its six neighbours. Each must hold the height the site was given. That height is the word at 14 of the building item, which is the evaluator's level at the placement. Where all seven nodes do, the progress word becomes 1 and the routine goes on at `0x42d55f`.
3. Otherwise, where the request-failed bit (bit 2) is set, it returns. The retry scan of the main pass ([Timing](timing.md)) clears the bit a few passes later.
4. Otherwise it asks the castle for a digger (`0x42d52a`).

The request in step 4 names job 2 and the tool in the stock word `0x20` of the storage item. That tool is good 16, because the word is halved, as for the other workers. The request is the one of [The builder](construction.md#the-worker-request). A request can fail in two ways. It finds no waiting settler of the job, or it finds no jobless settler with a tool in the stock. Then it sets the request-failed bit. So a site with no digger and no shovel waits and asks again.

The castle's first settlers include one digger, in slot 13. So the first large site gets him. A second site gets a jobless settler and a shovel from the stock. A small site starts with the progress word 1 and never reaches the test.

**Not ported:** a branch of step 4 for a player whose player-block byte `+0x163` has bit 6 set (`0x42d4e9`). There only the types 2, 4, and 0x11 ask for a digger. Any other type sets the request-failed bit at once. The meaning of that bit is not recovered, and the players of M4 do not set it. The engine takes the branch of the clear bit.

The names of the digger and his tool good are **inferred** from the job number the request names (INV-0006). No other evidence names them.

## The start (`0x43cd9d`)

**Status:** confirmed, and emulated.

The start routine of job 2 is entry 2 of the job start table `0x43ca54`. It runs where the digger has stepped through the door of the site's node ([Settlers](settlers.md#the-walk)). With byte 11 at `0xfe`, it goes into the storage, as every job's does.

Otherwise he takes state 8 and the work's bytes:

- Byte 11, the pass, is 15.
- Byte 12 is the site's height. It is the low byte of the word at 14 of the building at his node.
- Byte 13, the node of the pass, is 6.
- Byte 14, the phase, is 1.

His frame, timer, and stamp stay as the door's step left them. Status bit 6 of the site was set when he entered.

## State 8 (`0x43e45e`)

**Status:** confirmed, and emulated for every turn of a run of 1350 passes.

The digger makes sixteen *passes*. Byte 11 holds the pass number, which counts down from 15 to 0. A pass visits the seven nodes by a *node index* in byte 13, which counts down from 6 to 0. The index names these nodes:

- 6 is the node east of the site's node.
- 5 is south-east.
- 4 is south.
- 3 is west.
- 2 is north-west.
- 1 is north.
- 0 is the site's node itself.

The jump tables at `0x43e4d6`, `0x43ebca`, and `0x43e8c3` add the neighbour's offset by the index. The `Direction` of the engine is 6 minus the index. The first pass starts at index 5, so the east neighbour is left out of pass 15 only.

**The height a pass names.** The pass's height is the site's height plus a delta by the pass number. The byte table `0x43ebba` holds the deltas. They are -1 for pass 0, +1 for pass 1, -2 for pass 2, +2 for pass 3, and so on. They end at -8 for pass 14 and +8 for pass 15. A sum that is not a height skips the pass. A sum is not a height below 0 or above 31.

A node is worked in the pass where it holds exactly the pass's height. An odd pass lowers it by one. An even pass raises it by one. After passes 15 down to 0, every node within 8 of the site's height stands at it. Each of the 16 heights meets the nodes once. A node more than 8 above or below the site's height is never named, and stays.

**The phases.** Each turn of state 8 takes the elapsed time off the timer, as every waiting state does ([Settlers](settlers.md#the-walk)). Once the timer has run out, the digger works while the work leaves the timer negative. The work is a loop over the phase in byte 14. The phase falls by one on each round. The phase a round finds, before it falls, says what the round does:

- **0:** the node of the pass was busy. He looks again at the node the index names, from the node he stands on. A settler still standing on it makes him wait, with timer `0x7f`, and the phase stays 0. Otherwise he steps onto it as in phase 1.
- **1:** the next node. The index falls by one. Past 0 it becomes 6 and the pass number falls by one. Past pass 0 the digger is done. The routine then compares the height of the node with the pass's height.
  - The site's node that holds the height takes phase 2 and the dig's figure.
  - A neighbour that holds the height and stands free takes the step onto it. The node is his. The frame is `4 + 9 * direction` plus the height of the new node less the old one's, taken as a byte. The phase becomes 3. The time of the step goes onto the timer, from the step time table `0x43f248`.
  - A neighbour that holds the height and has a settler on it makes him wait. The frame is `0x57` less the index, the timer is `0x7f`, and the phase is 0.
  - A node that does not hold the height is skipped in no time. The loop goes on to the next node within the same turn.
- **2:** the dig. The node he stands on changes by one. An odd pass lowers it, and an even pass raises it. On the site's node, the work goes on at once with phase 1. On a neighbour, he steps back onto the site's node. He uses the opposite direction and the frame by the same rule, with no check for a settler there. The phase stays 1.
- **3:** the dig's figure. The frame is `0x57` in an odd pass and `0x58` in an even one. The timer gets `0x17f`. The phase is then 2.

The loop goes on at once where adding a time to the timer does not carry out of the word. A time added to a timer that ran negative ends the turn where it brings the timer back above 0.

**Emulated:** the digger of the fixture works 1350 passes. A node takes a step onto it, the dig's figure, the dig, and the step back. Each takes a few turns of a settler, and a turn comes every 16 or 17 passes. The sixteen passes find only 11 changes to make on that ground.

**Emulated** (`digger-busy-node`): a carrier stands on the site's flag, which is its south-east neighbour, when the pass names that node. That gives the frame `0x52`, which is `0x57` less the index 5. It gives the timer `0x7f` and the phase 0. The digger steps onto the node at his next turn, once the carrier has left it.

**Not ported:** the repaint of the changed node (`0x45086a`). It marks the tiles around the node dirty for the view and changes no rule.

A frame from 128 on reads a word past the step time table in the program. That happens at a height difference of 5 or more downhill on the east step, or a larger sum. The engine reads 0 there.

## The end

**Status:** confirmed, and emulated.

After pass 0, the building at the digger's node, the site, changes. It takes the progress word 1 and loses status bit 6 and its worker slot. The digger takes the bytes of a walk to the storage. Byte 11 is `0xfe`, bytes 12 to 14 are 0, and byte 15 is 2. He falls into the step onto the flag (`0x43e02e`, [Woodcutting](woodcutting.md#the-start-at-the-hut)). He walks home and enters the castle.

The next building pass sends the site through `0x42d55f` to ask for its builder, because its progress word is now 1. So the builder starts only after the levelling.

**Emulated:** the builder is requested at the first building pass after the digger's end. The progress word is 1 at pass 1695, and the builder's item exists at pass 1715.

## The digger's figure

**Status:** confirmed from the code and by a draw run. The figure frames are the walk's and the dig's two.

His figure handler is stub 2 of `0x43f744`, at `0x43fa87`. It adds `0x300` to the figure byte for a byte below `0x80`. It adds `0x380` from `0x80`, with a sound request at the frames `0x83` and `0x84`. Every frame of state 8 is below `0x80`, so only the walk's add draws him. `RuleTables.DiggerFigureAdds` holds both adds, read from `0x43fae9` and `0x43fade`.

The draw of the walk and of the dig is the settlers' ([Settlers](settlers.md#drawing-the-walk-leads)). `SettlerFigures` draws the activity `Digs` for job 2. It lifts him by the height of the node he stands on.

**Confirmed** by a draw run of the `digger` recording of `settler-draws.txt` on a `construct:` game. The run draws his walk out, his steps onto the nodes, and the dig, as the engine draws him.

The `open-settlers` content set draws him with the `settler/digger/*` images (`tools/open-settlers-art.py`). It draws the dig as the "cuts" image.

## A site that waits, in the build-site evaluator

**Status:** confirmed, and emulated.

The evaluator judges a node for a large building from the heights of the 12 nodes of its second ring. They may differ by less than 9 ([Build sites](build-sites.md)). The routine also folds in the third ring, which has 18 nodes (`0x44c151` to `0x44c204` in `0x44bd72`).

A node of the third ring contributes its target height to the lowest and the highest height of the range. The target height is the word at 14. The node contributes where all of these hold:

- It holds the map object code 3, a large building.
- Its building is under construction.
- Its progress word is still 0, so the site waits for its digger.

A large site that has started contributes nothing. A finished large building contributes nothing either. **Emulated:** the verdict sweep `sweep-site-started` of `tests/OpenSettlers.Tests/construction-digger.txt` covers this. It runs before pass 1960 of `digger-sawmill`, where the site has started and the digger is home. It matches the engine on every node with the node state 4 to 7.

A large site taken down while it waited still contributes. It contributes the clock that its take-down put into that word ([Removal](removal.md#taking-a-building-site-down)).

**Emulated:** take a site of height 15 at 14,8, with the ground west of it levelled to 5. The node 12,9 has the site in its third ring. It reads the site 3, where without the site it reads 4 at height 5. `BuildSite.Evaluate` takes the building table to read the site. The flag button and the building placement hand it over.

## In the engine

`DiggerBehavior` (`src/Application/DiggerBehavior.cs`) holds the start routine and state 8. `Start` is registered with `WoodcutterBehavior.RegisterStart` for `Job.Digger`. The activity `Digs` has `Pass`, `Target`, `Node`, and `Phase`, which keep the bytes 11 to 14 for the exporters.

`SitePass.Turn` asks for the digger through `CastleRoster.AskForWorker`, with `RuleTables.DiggerRequest`. `ProgramTablesImporter` reads that table from `0x42d52a`. `EventType.Digs` and `Levelled` record the digger's turns and his end. `BuildSite.Evaluate` has the third-ring term. The tests on made-up tables are `DiggerTests` and `SitePassTests`.
