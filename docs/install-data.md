# Install data

This page lists the data the engine reads from a History Edition install, which reader reads it, what the
reader turns it into, and which part of the engine uses the result. [ADR 0006](adr/0006-the-engine-reads-what-siedler1-reads.md)
decides which files are read. The formats are in `docs/rules/` (linked below). The program addresses are in
`ProgramTablesImporter` and in [the program index](rules/program-index.md).

`Install` (`src/Assets/Install.cs`) opens the install folder named by `--game` or `SIEDLER_DIR`. It refuses a
folder without `Siedler1.exe`. Every reader below gets its bytes through `Install.ReadAll`, which matches each
path segment without regard to letter case.

## Overview

| Source in the install | Read by | Typed result | Used by |
|---|---|---|---|
| `Siedler1.exe`, data tables and instruction immediates | `ProgramTablesImporter` and the job tables modules (`src/Adapters/Simulation/`) | `RuleTables` (`src/Domain/RuleTables.cs`) | Simulation, map generator, seed info, History Edition graphics source |
| `loca/SPAD.PA`, palette, masks, textures, sprites | `TerrainGraphics`, `RoadGraphics`, `ObjectGraphics`, `SettlerSprites` (`src/MapView/`) | GPU layers and `ObjectImage` | `HistoryEditionSource` (`IGraphicsSource`) |
| `loca/SPAD.PA`, entry 2 (walk sequences) | `StepOffsets` (`src/MapView/`) | `StepOffsets`, and the raw bytes for `SettlerFigureTables` | Game view on the History Edition set |
| `loca/SPAD.PA`, `SPAE.PA`, `SPAF.PA`, every entry | `ArchiveEntryKind` and its subclasses (`src/Assets/`) | Previews | Asset browser and asset check |
| `loca/eng.txt`, `ger.txt`, `fre.txt` | `TextKind` | Previews | Asset browser |
| `snd/01.SND` to `snd/99.SND` | `SoundKind` | Previews | Asset browser |
| `snd/music_1.midi` | `MusicKind` | Previews | Asset browser |

Seed info reads only `Siedler1.exe`. The map view and the game view read `Siedler1.exe` and `loca/SPAD.PA`
under the History Edition content set, which includes entry 2 of `loca/SPAD.PA` for the settlers' step offsets.
Under the basic content set none of these is read: the rules and step offsets come from `content/basic/rules/`. The text, sound, and music files are read only by the asset browser and the asset check.

## `Siedler1.exe`

`ProgramTablesImporter.Import` takes the bytes of the program and maps each program address to a file offset
through the PE section headers. It checks the instructions around a value before it reads the value, and it
throws `InvalidDataException` when the bytes are not the expected program. The result is one `RuleTables`.

The importer reads these tables:

| Table | `RuleTables` member | Form in `RuleTables` | Used by |
|---|---|---|---|
| Object classes, one byte per object code | `ClassOf`, `Classes` | `ObjectClass` | Map generator (large objects), rules, `NameTable` |
| Start stock, 5 levels of 26 goods | `StartStock` | `byte[][]` | `StartStock` (adapters) |
| Step times | `StepTime`, `StepTimes` | `ushort` per step shape | Settler movement |
| Door speeds | `DoorSpeed`, `DoorSpeeds` | `byte` per building type | Settler movement at doors |
| Scan windows per map size | `ScanWindow`, `ScanWindows` | `ushort` per map size | Rules |
| Building sizes and needs | `SizeOf`, `NeedsOf` | `BuildingSize`, `BuildingNeeds` | Building placement, construction |
| Walk rows of the free walk | `WalkDirection`, `WalkRows` | Direction per row and candidate | Settler movement |
| Good kinds, road capacities | `FillCounterOf`, `RoadCapacity` | `FillCounter`, `byte` | Goods transport |
| Transport priorities, from the immediates of the player setup | `TransportPriority` | Rank per good | Goods transport |
| Plank rank base, from the immediate of the player setup | `PlankRankBase` | `byte` | Construction materials |
| Good classes and the three storage hand-out orders | `ClassOfGood`, `DistributionOrder` | `GoodClass`, `DistributionStep` | Flag pass, storage distribution |
| Worker requests of the four huts | `WorkerOf` | `WorkerRequest` | Hut work |
| Material order and blow steps of the builder | `StoneAt`, `BlowStep` | `bool`, `ushort` per building type | Builder rules |
| Figure bases, figure offsets, carried figures | `FigureBases`, `FigureOffsets`, `CarriedFigures` | `short[]` | `SettlerFigureTables`, `CarrierLook` |
| Frame rows | `FrameRows` | `sbyte[]` | `MapViewControl` (draw order) |
| Idle phases, idle figures, idle offsets of the waiting carriers | `IdlePhases`, `IdleFigures`, `IdleOffsets` | `short[]`, `sbyte[]` | `SettlerFigureTables` |
| Job data: figure adds of each job, and the site's requests for a builder and a digger | `Get<T>` per job | `FigureAdds`, `WorkerRequest` | History Edition looks, construction |

The job data comes from one tables module per job (`src/Adapters/Simulation/Jobs/*Tables.cs`, found by the
source generator, [ADR 0016](adr/0016-jobs-are-types-and-each-layer-finds-its-job-modules.md)). A module reads
the immediates of its job's figure handler. The import fails when a job declares data that no module read.

The rules read the typed accessors. The members that are raw arrays are the figure tables, the start stock,
and the tables that the importer tests compare against the program.

The map generator and the random generator are ports of the program's code (INV-0003). They read no install
data apart from the object classes.

## `loca/SPAD.PA`

The map view and the game view read the German archive only (`TerrainGraphics.ArchiveFile`). The three
archives hold the same graphics ([Graphics archives](rules/graphics-archives.md)). Each loader reads the
archive with `ArchiveFile.Read` and the entries with the readers in `src/Formats/`
([Graphics images](rules/graphics-images.md)).

| Entries | Content | Loader | Result |
|---|---|---|---|
| 2 | Walk sequences: step offsets of each walk frame | `StepOffsets` | Step offsets in map pixels, and the raw bytes for `SettlerFigureTables` |
| 3 | Game palette | Each loader | RGBA color of each palette index |
| 4 | Shadow mask of a settler | `SettlerSprites` | Shadow `ObjectImage` |
| 60 to 221 | Fill masks of the terrain triangles | `TerrainGraphics` | Mask layers |
| 230 to 256 | Strip masks of the roads | `RoadGraphics` | Mask layers |
| 260 to 292 | Fill textures of the terrain | `TerrainGraphics` | RGBA texture layers |
| 300 to 309 | Road textures | `RoadGraphics` | RGBA texture layers |
| 320 to 351 | Goods at a flag | `ObjectGraphics` | `ObjectImage` |
| 367 to 370 | Build markers | `ObjectGraphics` | `ObjectImage` |
| 630 to 645 | Water ripples | `ObjectGraphics` | `ObjectImage` |
| 1250 to 1369 and further to the castle | Map objects, flag frames, buildings, sites, scaffolds | `ObjectGraphics` (entries in `ObjectSprites`) | `ObjectImage` |
| Sprite entry + 250 | Shadow masks of the objects | `ObjectGraphics` | Shadow `ObjectImage` |
| From 1850, 2500, and 3150 | Settler figures, overlays, and second figures | `SettlerSprites`, picked by `SettlerFigureTables` | `ObjectImage` in the player's color |

`HistoryEditionSource` composes the loaders and answers the draw terms of `IGraphicsSource`
([ADR 0014](adr/0014-the-map-view-draws-through-a-graphics-source.md)). The settler images need the figure
tables of `RuleTables`. Without them the source draws no settler. `OpenSettlersSource` draws from `content/`
and reads no archive entry. In the game view, `MapViewControl` still reads entry 2 through `StepOffsets` for the
settlers' steps.

## Asset browser and asset check

The asset browser (the launch without a mode flag) and the asset check (`--check`) read every file of ADR 0006 to show and check
it. Each kind in `src/Assets/` (`AssetKind`, `ArchiveEntryKind` and its subclasses, `TextKind`, `SoundKind`,
`MusicKind`) reads the raw format with the reader in `src/Formats/` and turns it into a preview. The formats are
in [Text files](rules/text-files.md), [Sound files](rules/sound-files.md), and [Music file](rules/music-file.md).
No preview reaches the simulation or the map view.

## Where the readers sit

- `Install`, `src/Formats/`, `src/Assets/`, and `src/MapView/` are part of the Godot project
  (`OpenSettlers.csproj`). `ProgramTablesImporter` and the job tables modules are in the adapters assembly
  (`OpenSettlers.Adapters`, [ADR 0013](adr/0013-the-simulation-is-typed-and-the-original-s-bytes-stay-at-its-edge.md)).
- `ProgramTablesImporter.Import` is a static method that takes the bytes of the program. `MapViewControl` and
  `AssetBrowserView` (seed info) call it with `Install.ReadAll(Install.Program)`.
- The graphics loaders take an `Install` and read `loca/SPAD.PA` themselves. `IGraphicsSource` is the interface
  that the map view draws through. The loaders behind it have no interface of their own.
