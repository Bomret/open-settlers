# Open Settlers glossary

## Game data

**Install**
A copy of Die Siedler that the player owns, on disk: the folder that holds the game's program and its
data files. The engine reads all game data from it and ships none of its own. The reference install is
The Settlers History Edition, whose program is `Siedler1.exe`.
_Avoid_: game folder, game directory, assets folder

**Import port**
An interface through which the engine reads one kind of game data, such as the rule tables, the walk
steps, the assets, or the graphics source, as dedicated types (`OpenSettlers.Import`, ADR 0017). A source
assembly implements the ports it can fill: the History Edition install every port, the content set the
graphics source.
_Avoid_: loader, reader (a reader decodes one file format inside a source assembly)

**Asset**
One loadable piece of game data in an install, such as a palette, sprite, sound, or map.
_Avoid_: resource, game file

**Archive**
A graphics file of an install, such as `loca/SPAD.PA`, that holds numbered entries. Each entry present
in it is one asset.
_Avoid_: pack, container, PA file

**Entry**
One numbered record in an archive of an install. A draw term never holds its number (INV-0010).
_Avoid_: record, slot

**Asset kind**
What an asset is, such as a palette, a sprite, a sound, or rule data. The routine of the program that
reads an asset decides its kind; nothing in the asset itself states it.
_Avoid_: asset type, format

**Rule data**
An asset whose meaning only a rule explains, such as a table the simulation reads. The milestone that
recovers the rule decodes its meaning.
_Avoid_: game data, tables

**Asset check**
The engine's headless mode that reads every asset of an install and reports each one it cannot read.
_Avoid_: validator, scan

**Asset browser**
The engine's interactive mode for picking any asset of an install and inspecting its preview.
_Avoid_: sprite viewer, asset viewer

**Preview**
The asset browser's rendering of one asset, showing that its meaning was understood: a palette as its
colors, a sprite as the game draws it, a sound as playback.
_Avoid_: thumbnail, view

**Stand-in**
A value or check that the engine supplies itself because the install does not define it or the program does not
confirm it.
_Avoid_: default, placeholder

**Seed info**
The engine's headless mode that generates the map of one seed and size and prints its layer digests and
its counts of terrain, objects, and deposits. It needs the install, whose program tables say which objects are large.
_Avoid_: map summary, map dump

**Play script**
A text file of steps, one per line, that drive the map view or the game view through its own input
handlers (keys, the mouse, a click on a tile by `click-tile`, a click on a button by `button`), take
screenshots, and check its state (in the game view also the tick count, the castle's log, plank, and stone counts, and the goods on a
flag).
_Avoid_: test script, macro

**Play run**
One run of the map view or the game view under Godot's Movie Maker that executes a play script on a fixed frame rate and
quits after its last step.
_Avoid_: replay, recording

**Layout**
A JSONC file in `layouts/` that sets up a game for the game view: a hand-drawn map (heights, terrain per
triangle, objects), a seed for the game's random generator, the castle, flags, roads, and buildings as
commands, and settlers and goods in entry states.
_Avoid_: scenario, save, fixture

**Castle contents**
The goods and settlers the castle holds when it is placed. By default these are the goods and the 20 settlers the original's castle
starts with, as `docs/rules/castle.md` records. A layout can name goods and settlers in their place.
_Avoid_: castle inventory, start roster

**Graphics source**
Where the map view gets the image for each draw term: the History Edition source, which reads the install's
archives, or the images of a content set the repository ships. The content set a run uses brings its graphics
source. A source answers a term with an RGBA image and its anchor point, or with nothing for an effect it does
not draw (ADR 0014).
_Avoid_: skin, theme, renderer, asset pack

**Draw term**
What the map view asks a graphics source to draw, named in game terms: a terrain triangle, a map object, a
settler, a road, a border post, a build marker, or a road marker, with the data that picks its image, such as a settler's job, activity, heading, and
animation step. A draw term never holds an entry number (INV-0010).
_Avoid_: sprite ID, entry, frame number

**Flame**
One image of the fire drawn over a burning building: small or large, at an offset from the building, at a phase of 0
to 7. The graphics source gives a building's flames, and the burn's countdown gives their phase (ADR 0021).
_Avoid_: fire sprite, burn mark

**Build-site overlay**
The markers the game view draws on every visible node of its player's territory, or before the castle stands, of every
castle site, to show what may be put there: a
flag, a mine, a small building, or a large building. Each marker is a build marker; the build-site evaluator's
judgment of the node picks it. It is switched on and off as a whole. Build help is the label of the game view's button
that toggles it.
_Avoid_: where-to-build overlay, placement overlay

**Job search**
What a gatherer's job rules declare about the search its worker runs from the building: the range of spiral entries
it draws from and the target test a node of that range must pass. The woodcutter, stonecutter, forester, fisher, and
farmer each have one. The worker's search and every view that shows the search read this one declaration.
_Avoid_: search radius, search config

**Work area**
The nodes a gatherer's worker searches: the spiral entries of its job search, counted from the node of its building or
building site.
_Avoid_: work radius, range, catchment

**Work-area overlay**
The marks the game view draws while its Work areas button is on: one tint mark on every node that lies in the work area
of at least one of its player's gatherer buildings or building sites, and, while a gatherer's build button is pressed,
a reach marker on every node where that building fits. It is drawn by the view as plain shapes, not through the graphics
source, and it changes nothing in the game.
_Avoid_: range overlay, heat map

**Reach marker**
The mark of the work-area overlay on a node where the gatherer building of the pressed build button fits: a filled disc
where its target test accepts at least one node of the work area that building would have there, a ring where it accepts
none.
_Avoid_: in-range marker, resource marker

**Content set**
Where a run gets its game data: its graphics source, its rule data, and its walk steps. There are two: the
History Edition set, which is the install read through the History Edition source, and the basic set, which the
repository ships in `content/basic/` with stand-in rules (ADR 0015, ADR 0018). The launch flag `--content-set`
picks one; a new folder under `content/` is not selectable.
_Avoid_: mod, pack, theme, graphics set

**Stand-in rules**
The rule data and walk steps of the basic content set: values of the project's own making that let the
engine run a game without an install. They are not the original's rules, so a seed gives another map and
another game than under the History Edition set (ADR 0018).
_Avoid_: custom rules, rule mod, fake tables

**Entry state**
A state of a settler or a good that a routine of the original writes at the start of a behaviour, such as a
woodcutter inside his hut, a carrier on his road, or a good put on a flag. A layout places settlers and
goods only in these (INV-0005).
_Avoid_: start state, initial state

**Thing**
Anything that stands on a map node: a map object, a settler, a building, a flag, or a node of a road. A thing
has a kind, a node, the raw values the engine holds for it, and a name where the name table has one.
_Avoid_: entity, item, object (a map object is one kind of thing)

**Name table**
A table of Open Settlers' own names for the original's bare codes: map object codes, settler jobs and states,
and building types. Each entry cites its evidence. A code with no entry shows raw. The object names read the
loaded class table, so `stone` names a code only where the table says class 2.
_Avoid_: lookup, code map

**Thing ID**
The name a settler, building, or flag keeps for its whole life: its kind, its slot, and a count of how often
that slot was filled, as in `settler#12.3`. A slot filled again gives the new thing a new ID. A map object
does not move, so its node serves as its ID, as in `tree@12,4`.
_Avoid_: handle, key, index

**Event**
A record of one change a rule made to a settler, building, or flag: the tick, the rule that made it, the
thing's state after the change, and its cause. An event names another thing by its ID. The game's history keeps
every event in one event store, and the state of a thing at an earlier tick is the state in its last event up
to that tick.
_Avoid_: log entry, change, history item

**Game history**
The part of a game that holds the thing IDs, the causes, and the event store (`Game.History`,
`src/Application/GameHistory.cs`). It sits beside the game state, takes no part in the digest, and no rule
reads it. It asks the snapshot exporter for a thing's state when a rule records an event.
_Avoid_: event log, journal

**Snapshot exporter**
The adapter that writes the state of a settler, building, or flag as the original's named raw values, the
form each event holds (`SnapshotExporter`, `src/Adapters/Simulation/SnapshotExporter.cs`). The history calls it
through the `IThingSnapshots` port.
_Avoid_: serializer, dumper

**Cause**
Why a change happened: the player's command that started the chain of changes (`placed` or `razed`, with
the ID of the thing commanded), or, where no command started it, the rule area that acted on its own, such
as `woodcutting`. A layout's entries give the cause `layout`.
_Avoid_: reason, trigger, source

## Game rules

**Rule**
A behavior of the original game that the engine reproduces, recovered from the game code of `Siedler1.exe`
(`docs/rules/AGENTS.md`).
_Avoid_: mechanic, logic

**RuleTables**
The tables of the program data that the rules read, as typed values (`src/Domain/RuleTables.cs`). The importer
builds them from the bytes of `Siedler1.exe`. A test builds its own.
_Avoid_: program tables

**ObjectClass**
The class the rules give an object code (0 nothing, 3 a flag, 4 to 6 buildings), read with `RuleTables.ClassOf`.
_Avoid_: object size, kind

**FillCounter**
The counter of a building's stock that a good counts against: low, high, or none (`RuleTables.FillCounterOf`).
_Avoid_: good kind

**Game code**
The part of `Siedler1.exe` that the History Edition carries over from the original DOS program: the
game's rules, its state, and its drawing.
_Avoid_: core, original code

**Domain**
The assembly of the typed model and the random generator. It references no other project assembly
(`src/Domain/`, ADR 0013).
_Avoid_: core, model layer

**Adapter**
The assembly that converts the original's packed bytes, map layers, and bare codes to and from the typed
model (`src/Adapters/`, ADR 0013). It references the domain, the application, and the History Edition source
assembly, of which it names only `JobTableNames`, for the job and state names (INV-0011).
_Avoid_: converter layer, edge

**Platform layer**
The part of `Siedler1.exe` that the History Edition adds for Windows: graphics and sound output, timing,
and input. It is new code, so a rule found only there is marked as such.
_Avoid_: wrapper, port layer

**Simulation**
The game state and the rules that advance it, independent of rendering, input devices, and frame rate.
_Avoid_: game logic, model, world

**Tick**
One fixed step of the simulation: one pass of the original's main loop at its shortest length, that is,
4 timer calls, 8 units of its game clock, 80 milliseconds at normal speed (`docs/rules/timing.md`,
ADR 0005).
_Avoid_: frame, update, turn

**Command**
A player's action that the simulation takes at a given tick, such as placing a flag or building a road.
The same seed and the same commands at the same ticks give the same game (INV-0002).
_Avoid_: input, event, order

**Flag**
A point on the map where roads meet and where goods wait to be carried on. A building stands at a flag.
_Avoid_: node, waypoint

**Flag side**
One of the six directions along which a road leaves a flag. It holds what the flag knows of that road: its length class, its
carriers, and the good that waits for it.
_Avoid_: port, slot

**Waiting good**
A good that lies on one of the eight places of a flag until a carrier takes it along a road. It has a destination flag and,
once given, the direction of the first road toward it.
_Avoid_: item, cargo

**Road**
A path between two flags, built one of six directions at a time. One carrier serves each road.
_Avoid_: edge, path, street

**Road marker**
A sign the game view shows in road mode on the end of the road being drawn and on each of its six neighbors: the end
mark on the end itself, and on each neighbor a blocked mark where no step may go, a way-back mark on the node the road
came from, or otherwise a slope mark for the height difference of that step. The original's road cursor picks each
neighbor's mark. A click on a neighbor's mark is a click on that node.
_Avoid_: cursor symbol, pointer sprite, slope icon

**Border post**
One piece of the territory border that the views draw between two neighbor nodes whose owners differ and where no road
lies: a pole on land, a post in water. The views draw it in the pass that draws the roads, so trees and buildings cover it.
_Avoid_: border marker, pole, boundary line

**Carrier**
A settler who walks one road and carries goods between its two flags.
_Avoid_: porter, transporter, worker

**Job**
What a settler is employed as, such as carrier, woodcutter, or builder, with the number the original gives it.
A settler keeps its identity when its job changes: a settler without a job takes up the carrier's job when it
takes a road. A number the engine does not port yet is an unported job.
_Avoid_: role, profession, class

**Job module**
The code one layer holds for one job, found by that layer without a list: the job and its data in the domain,
its rules in the application, its tables and names in the adapters, its look in the map view.
_Avoid_: plugin, handler, job class

**Activity**
What a settler does at one moment, with exactly the data that doing it needs: waiting in the castle, walking
along a road, searching for a tree, walking home. A settler has one activity at a time and leaves it for the
next. Each activity keeps the code of the original's settler state it stands for.
_Avoid_: settler state, mode, action

**Settler**
One of the player's people, one object for its whole life: its place on the map, its player, its timer, a job, and
one activity. Its slot in the settler table is its number and the slot of its thing ID.
_Avoid_: worker, unit, figure

**Settler supply**
A player's timer that makes one settler without a job in a storage for each period that passes. The player's settler
rate, a start setting from 0 to 40, sets the period: the higher the rate, the shorter the period. About 31 % of the new
settlers are owed as knights, and the new settler becomes one where its storage holds a sword and a shield
(`docs/rules/new-settlers.md`).
_Avoid_: reproduction, spawning, birth, population growth

**Settler cap**
The most settlers a player may hold before its settler supply stops making new ones: an eighth of the settler table,
plus the player's share of the rest by the land it owns. A player alone in a game holds the whole table.
_Avoid_: population limit, settler limit

**Building**
A building of the player's, finished or a building site: its type, its node and flag, whether its worker is inside
or on the way, and the counters of the goods it takes in. Its slot in the building table is its number. The castle
is slot 0.
_Avoid_: house, structure

**Building site**
A building that a person placed and that is not finished yet. It waits for a digger to level it (large buildings
only), for the planks and stones its type needs, and for a builder who builds it from them.
_Avoid_: construction site, site (alone), unfinished building

**Castle under construction**
The castle from its placement until it stands, while its first settler raises its progress word. It is no building
site: no digger, no builder, and no planks or stones come to it. The engine draws it through the site drawing types
(`BuildingDraw.SiteProgress`, `SitePart`, `SiteLayer`, `SiteDrawings`).
_Avoid_: castle site (a node where the castle may be placed), unfinished castle

**Request**
A building's standing ask, held at its flag, for one kind of good, with a rank that falls as goods of that kind
arrive or are on their way. A good without a destination goes to a building that the request search finds asking
for it; with no request, it goes to storage.
_Avoid_: order, demand, need

**Transport priority**
A player's rank for each kind of good. Where several goods at one flag wait for the same road, the good
with the highest rank goes next. A new game starts every player with the same default order.
_Avoid_: goods priority, transport order

## Roadmap

**Free-play release**
The first goal of the roadmap: a free game against the computer opponent on a generated map. A person
plays the game to a win or a loss. The release includes saving, loading, and sound effects.
_Avoid_: MVP, version 1.0
