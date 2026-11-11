# The digger

How a large building site asks for a digger, how he levels the ground of the site's node and its six neighbours to the site's height in
state 8, and how the build-site evaluator treats a large site that waits for him. Addresses and routine names are in the
[Program index](program-index.md#castle-and-build-sites); the request for a worker is in [The builder](construction.md#the-worker-request),
the site's placement in [Build sites](build-sites.md#placing-a-building), and the materials the site asks for after the levelling in
[Materials for a building site](construction-materials.md#the-building-passs-turn-of-a-site).

**Emulated.** `tests/OpenSettlers.Tests/construction-digger.txt` holds the output of the harness verb `construct:` (`tools/GhidraDump/README.md`)
for a game whose sawmill's site (a large type) stands on ground that is not level: the castle's own digger (the settler of the slot 13, job 2)
walks out to the site at the pass 292, works from the pass 342 to the pass 1695 in 11 digs that change 11 heights, and walks home by the pass 1959.
`ConstructionDiggerTests` runs the engine through the same commands: the digger's 16 bytes after every pass, the heights of the site's seven nodes
after every pass, and the site's bytes at the passes the fixture lists match. The harness lists the heights a pass changed (`h=`). The fixture also holds
the evaluator's verdict on every node of the castle's land while the site waits (`v@0,0`), once on the ground as it is and once with the ground west of
the site levelled to a lower height by `l@x,y,H` clicks.

## The large site waits for the ground

**Status:** confirmed, and emulated.

A large type under construction reaches its handler `0x42d328` (`Building_HandleLargeConstruction`) first. While the progress word is not 0 it goes on at
`0x42d55f`, the request for the builder ([The builder](construction.md#the-request-for-a-builder)). While it is 0 the routine:

1. returns at once where the status has a worker inside or on the way (bits 6 and 7): the digger has the ground;
2. tests the node and its six neighbours: each must hold the height the site was given (the word at 14 of the building item, the evaluator's
   level at the placement). Where all seven do, the progress word becomes 1 and the routine goes on at `0x42d55f`;
3. otherwise, where the request-failed bit (bit 2) is set, returns: the retry scan of the main pass ([Timing](timing.md)) clears the bit a few passes later;
4. otherwise asks the castle for a digger (`0x42d52a`): job 2 and the tool in the stock word `0x20` of the storage item, which is the good 16 (the word
   halved, as for the other workers). A request that finds neither a waiting settler of the job nor a jobless settler with a tool in the stock sets the
   request-failed bit, so a site with no digger and no shovel waits and asks again. The request is the one of [The builder](construction.md#the-worker-request).

The castle's 20 first settlers hold one digger (the slot 13), so the first large site gets him; a second gets a jobless settler and a shovel from the stock.
A small site starts with the progress word 1 and never reaches the test.

**Not ported:** a branch of step 4 for a player whose player-block byte `+0x163` has bit 6 set (`0x42d4e9`): there only the types 2, 4, and 0x11 ask for a digger and
any other sets the request-failed bit at once. The bit's meaning is not recovered (the players of M4 do not set it); the engine takes the branch of the clear bit.
The names of the digger and his tool good are **inferred** from the job number the request names (INV-0006); no other evidence names them.

## The start (`0x43cd9d`)

**Status:** confirmed, and emulated.

The start routine of job 2 (entry 2 of the job start table `0x43ca54`) runs where the digger has stepped through the door of the site's node
([Settlers](settlers.md#the-walk)). With byte 11 `0xfe` it goes into the storage as every job's does. Otherwise he takes state 8 and the work's bytes:
byte 11 (the pass) 15, byte 12 the site's height (the low byte of the word at 14 of the building at his node), byte 13 (the node of the pass) 6, byte 14 (the
phase) 1. His frame, timer, and stamp stay as the door's step left them. Status bit 6 of the site was set when he entered.

## State 8 (`0x43e45e`)

**Status:** confirmed, and emulated for every turn of a run of 1350 passes.

The digger makes sixteen *passes*, the pass number counting down from 15 to 0 in byte 11. A pass visits the seven nodes by a *node index* in byte 13, counting
down from 6 to 0: the index 6 is the node east of the site's node, 5 south-east, 4 south, 3 west, 2 north-west, 1 north, and 0 the site's node itself (the jump tables
at `0x43e4d6`, `0x43ebca`, and `0x43e8c3` add the neighbour's offset by the index; the direction of the engine's `Direction` is 6 minus the index). The first pass starts at
the index 5: the east neighbour is left out of the pass 15 only.

**The height a pass names.** The pass's height is the site's height plus a delta by the pass number (the byte table `0x43ebba`): -1 for the pass 0, +1 for 1, -2 for 2,
+2 for 3, and so on to -8 for the pass 14 and +8 for the pass 15. A sum that is not a height (below 0 or above 31) skips the pass. A node is worked in the pass where it holds exactly the pass's
height: an odd pass lowers it by one, an even pass raises it by one, so after the passes 15 down to 0 every node within 8 of the site's height stands at it, and each of the 16
heights meets the nodes once. A node more than 8 above or below the site's height is never named, and stays.

**The phases.** Each turn of state 8 takes the elapsed time off the timer as every waiting state does ([Settlers](settlers.md#the-walk)); once it has run out the digger works while the work leaves the timer
negative. The work is a loop over the phase in byte 14, which falls by one on each round:

The phase a round finds (before it falls) says what the round does:

- **0**: the node of the pass was busy. He looks again at the node the index names, from the node he stands on: a settler still standing on it makes him wait (timer `0x7f`, the phase stays 0); else he steps onto it as below.
- **1**: the next node. The index falls by one (past 0 it becomes 6 and the pass number falls by one; past the pass 0 the digger is done). The routine then compares the height of the node with the pass's. The site's node that holds it takes the phase 2 and the dig's figure (below). A neighbour that holds it and stands free takes the step onto it: the node
  is his, the frame is `4 + 9 * direction` plus the height of the new node less the old one's (taken as a byte), the phase becomes 3, and the time of the step (the step time table `0x43f248`) goes onto the timer. A neighbour that holds it and has a settler on it makes him wait
  (frame `0x57` less the index, timer `0x7f`, phase 0). A node that does not hold it is skipped in no time: the loop goes on to the next node within the same turn.
- **2**: the dig. The node he stands on changes by one (lowered in an odd pass, raised in an even pass). On the site's node the work goes on at once with the phase 1; on a neighbour he steps back onto the site's node (the opposite direction, the frame by the same rule, no check for a settler there), and the phase stays 1.
- **3**: the dig's figure: the frame `0x57` in an odd pass and `0x58` in an even one, and `0x17f` on the timer. The phase is then 2.

The loop goes on at once where adding a time to the timer does not carry out of the word; a time added to a timer that ran negative ends the turn where it brings the timer back above 0.

**Emulated:** the digger of the fixture works 1350 passes: a node takes a step onto it, the dig's figure, the dig, and the step back, each a few turns of a settler (a turn comes every 16 or 17 passes), and the sixteen passes find only 11 changes to make on that ground.

**Not ported:** the repaint of the changed node (`0x45086a`, which marks the tiles around the node dirty for the view and changes no rule). A frame from 128 on (a height difference of 5 or more downhill on the east step, or a larger sum) reads a word past the step time table in the program; the engine reads 0 there.

## The end

**Status:** confirmed, and emulated.

After the pass 0 the building at the digger's node (the site) takes the progress word 1, loses status bit 6 and its worker slot, and the digger takes the bytes of a walk to the storage (byte 11 `0xfe`, bytes 12 to 14 0, byte 15 2) and falls into the step onto the flag (`0x43e02e`, [Woodcutting](woodcutting.md#the-start-at-the-hut)); he walks home and enters the castle. The next building pass sends the site, whose progress word is now 1, through `0x42d55f` to ask for its builder, so
the builder starts only after the levelling. **Emulated:** the builder is requested at the first building pass after the digger's end (the progress word 1 at the pass 1695, the builder's item at the pass 1715).

## The digger's figure

**Status:** confirmed from the code; the figure frames are the walk's and the dig's two.

His figure handler (stub 2 of `0x43f744`, `0x43fa87`) adds `0x300` to the figure byte for a byte below `0x80` and `0x380` from `0x80` (with a sound request at the frames `0x83` and `0x84`). Every frame of state 8 is below `0x80`, so only the
walk's add draws him; `RuleTables.DiggerFigureAdds` holds both, read from `0x43fae9` and `0x43fade`. The draw of the walk and of the dig is the settlers' ([Settlers](settlers.md#drawing-the-walk-leads)): `SettlerFigures` draws the activity `Digs` for job 2, lifted by the height of the node he stands on. **Inferred:** the
draw of the dig (no draw run of the program holds a digger). The `open-settlers` content set draws him with the `settler/digger/*` images (`tools/open-settlers-art.py`), the dig as the "cuts" image.

## A site that waits, in the build-site evaluator

**Status:** confirmed, and emulated.

The evaluator judges a node for a large building from the heights of the 12 nodes of its second ring: they may differ by less than 9 ([Build sites](build-sites.md)). The routine also folds in the third ring (18 nodes, `0x44c151` to `0x44c204` in `0x44bd72`):
a node of the ring that holds the map object code 3 (a large building) whose building is under construction and whose progress word is still 0 (a site that waits for its digger) contributes its target height (the word at 14) to the lowest and the highest height of the range. A large site that has started, and a finished large building, contribute nothing. A large site taken down while it waited still contributes, with the clock its take-down put into that word ([Removal](removal.md#taking-a-building-site-down)).
**Emulated:** with a site of the height 15 at 14,8 and the ground west of it levelled to 5, the node 12,9 (the site lies in its third ring) reads the site 3 where without the site it reads 4 at the height 5. `BuildSite.Evaluate` takes the building table to read the site; the flag button and the building placement hand it.

## In the engine

`DiggerBehavior` (`src/Application/DiggerBehavior.cs`) is the start routine (`Start`, registered with `WoodcutterBehavior.RegisterStart` for `Job.Digger`) and state 8; the activity `Digs` (`Pass`, `Target`, `Node`, `Phase`) keeps the bytes 11 to 14 for the exporters. `SitePass.Turn` asks for the digger (`CastleRoster.AskForWorker`, with
`RuleTables.DiggerRequest`, read from `0x42d52a` by `ProgramTablesImporter`). `EventType.Digs` and `Levelled` record the digger's turns and his end. `BuildSite.Evaluate` has the third-ring term. The tests on made-up tables are `DiggerTests` and `SitePassTests`.
