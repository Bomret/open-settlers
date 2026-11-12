# Patterns (LLM Reference)

Target audience: LLMs working in this repo. The folder layout is in `README.md` (Layout).

## Namespace

`OpenSettlers.<Folder>`, one namespace per folder of `src/`. For example, `OpenSettlers.Formats`. Tests use
`OpenSettlers.Tests`. The layers are the exception: `src/Domain/` is `OpenSettlers.Domain`, and both
`src/Application/` and `src/Adapters/Simulation/` are `OpenSettlers.Simulation`.

## Simulation layers

The simulation is three class-library assemblies under `src/` that depend only inward (INV-0008, ADR 0013):

- `Domain/` (`OpenSettlers.Domain`): the typed model. The map, flag table, road network, building and settler
  tables, `Job`, `Activity`, `RuleTables`, and the value types they use. It references no other project.
- `Application/` (`OpenSettlers.Simulation`): `Game`, the coordinator, the `ICommandHandler` of each command, the
  `CommandQueue`, the history, and the rules as behaviors (`CarrierBehavior`, `GoodsTransport`, `Removal`,
  `WalkHomeBehavior`, `HutWork`, `SettlerMovement`). It references the domain. A behavior takes its parts
  through its constructor. `GameParts` holds them wired, and `GameFactory` in the adapters builds them.
- `Adapters/Simulation/` (`OpenSettlers.Simulation`): the edge. Importers (`ProgramTablesImporter`, `MapImporter`,
  `MapGenerator`, `Layout`), exporters (`GameDigest`, `SnapshotExporter`, `FlagEncoder`, `SettlerEncoder`,
  `BuildingEncoder`, `MapLayerExporter`), and `GameFactory`. It references the domain and the application.

Place new code by what it does: a rule goes in the application, a type that states what a rule reads goes in the
domain, and a conversion from or to the original's bytes goes in the adapters.

## Job modules

A job is a type (ADR 0016) and each layer holds its code for the job in a job module. A source generator in each
layer finds the modules and writes the layer's registry, so no list of jobs exists and adding a job edits no shared
file. A caller names a job by its type (`new Woodcutter()`, `is Woodcutter`), never by a static accessor or a number
constant of `Job`. Add a job as its own files, layer by layer:

1. Domain, `src/Domain/Jobs/X.cs`: `public sealed record X : Job` with `public const int Number = <n>;` (a literal) and
   `public override int Code => Number;`. Override `ArrivesHomeGenerically` where the original's table at `0x437dfb`
   gives the job a branch of its own. The data the original holds for the job (`FigureAdds` and its relatives, a
   `WorkerRequest`) is a record of the domain that the rules read through `RuleTables.Get<T>(new X())`. Declare the
   record types the job owns in `public override IReadOnlyList<Type> Data => [typeof(...)]`: the importer refuses a
   program whose tables module leaves one unread, so a job's data is never a silent default, and a test builds the same
   data with `TestProgram.MadeJobData()`. `Job.Of(int)` finds the job of a number and is generated from the sealed job
   types.
2. Application, a file of `src/Application/`: `public sealed class XRules(JobParts parts) : JobRules<X>(parts)`. Override
   the hooks the job needs (`CanStandInHut`, `Cause`, `SiteWorker`, `Register`, `Start`, `Arrive`, `Pass`,
   `Turn`); a hook left alone keeps the shared default. `SiteWorker` names the request a building site makes for the
   job's worker at a `SiteStage`, so the shared site pass names no job. A job needing a part `JobParts` lacks extends `JobParts` (and the
   `new JobParts(...)` calls of the tests) rather than reaching into a shared behavior. A module reaches the shared states of the
   workers who go out from a building (the step out, the entry, the arrival home) through `Parts.Hut` alone.
   `JobModules.Build(parts)` builds every module.
3. Adapters, `src/Adapters/Simulation/Jobs/XTables.cs`: `public sealed class XTables : JobTables<X>` with `[JobName]` and
   `[StateName]` (each carries its evidence, INV-0006) and a `Read` override that adds the job's data records from
   `Siedler1.exe`. `JobTablesRegistry` lists the modules and `NameTable` the names.
4. Map view: `src/MapView/HistoryEditionLooks/XLook.cs` with `XLook(RuleTables tables) : HistoryEditionLook<X>(tables)`
   for the History Edition source and `src/MapView/OpenSettlersLooks/XKey.cs` with
   `XKey() : OpenSettlersLook<X>("x")` for the content set (`HistoryEditionLookRegistry`, `OpenSettlersLookRegistry`).
   A job without a look draws nothing; `JobCoverageTests` fails a job that has rules and no look under a source.

A job without a module of some kind builds and keeps the shared defaults. The carrier's, the castle roster's, and the walk
home's rules stay hand-wired core. Test doubles in the test assembly are not found by the generators: a test proves
the generators on fixture jobs compiled through `CSharpGeneratorDriver` (`FixtureJobTests`).

A malformed module fails the build with a message that names the class:

| Id | Cause |
| --- | --- |
| `JOB001`–`JOB003` | A job type that is not sealed; two job types with one number; a job type with no `public const int Number` the generator can read. |
| `TAB001`–`TAB006` | A tables module with no parameterless constructor; two tables modules for one job; two that name one job; two that name one state; one that names a state twice; one that names a state of the core (`CoreStateCodes`, which `NameTable` names itself). |
| `RUL001`–`RUL004` | A rules module with no constructor that takes the `JobParts` alone; two rules modules for one job; a generic module; a module that is private or protected or nested in such a type. |
| `LOOK001`–`LOOK002` | A look with no constructor the registry can call; two looks for one job under one graphics source. |

## Typed vocabulary

Rules read and write typed state: `Settler` with a `Job` and one `Activity`, `Flag` with its `FlagSide`s and
`WaitingGood`s, `Road`, `Good`, `BuildingType`, `Terrain`, `MapObject`, `ObjectClass`, `Direction`, and `Heading`.
Never read a packed byte, a bit mask, a map layer, or a bare code in the domain or the application. The original's
codes sit at the edge: an importer turns them into the typed model and an exporter turns the typed model back, so
`Game.Digest()` and the events keep the original's encoding. A field with no recovered meaning stays a named raw
field, so the exporters lose nothing. A test of a rule asserts typed state. Only a test of an importer or exporter
asserts bytes. A rule reads what it needs through a typed accessor or a port (`IThingSnapshots`, `IRandomDraws`),
and a test builds a `RuleTables` or a flag table by hand instead of importing the install's.

## Godot boundary

Keep logic that needs no engine in Godot-free folders of `src/`. The test project compiles the source files
of those folders directly (a `<Compile Include>` per folder in `OpenSettlers.Tests.csproj`), so the tests
run without Godot. A `using Godot` in such a folder breaks the test build. The three layer assemblies are
projects the test project references, and they use the plain .NET SDK with no Godot type.

- When Godot-free code needs an engine service, pass it in as a delegate.
- Only the folders that build scenes or views reference Godot.

## Format readers

A format reader parses bytes into plain data. It does not read files or know where an asset is installed.

- Throw `InvalidDataException` for a corrupt asset. The message names what is wrong and, where known,
  the offset.

## Gaps never stop the engine

An asset that is missing or fails to parse is reported with its name and reason and then skipped. Do not
let such an exception escape a load loop or a scene build.

Such a gap is one of three cases: a file that cannot be read (`IOException`), a file that cannot be opened
(`UnauthorizedAccessException`), or corrupt data (`InvalidDataException`). The asset check and the browser
catch these three when they turn an asset into an unreadable entry. A reader may catch another exception and
rewrap it as `InvalidDataException` when its cause is corrupt data: `ArchiveEntryKind.PaletteOf` does so for
an `ArgumentOutOfRangeException` from a palette lookup. Any other exception is a bug: let it propagate. Two
entry points catch what propagates. The asset check (`AssetBrowserView.RunCheck`) catches whatever escapes
`AssetCheck.Run`, prints `asset check crashed` to stderr, and exits 1. The asset browser
(`AssetBrowserView.Guard`) catches whatever escapes `AssetBrowserState` while it selects a kind or an asset or
draws a preview, prints `asset browser crashed` to stderr, and shows `<file or kind>: crashed: <Type>: <message>`
in the preview pane, or in the window when it fails on opening. A Godot callback exception only reaches the log,
so the view catches it to show anything.

## Stand-ins

A value that the install defines is read from the install at run time. A value that the engine supplies
itself is a stand-in. Mark each stand-in with a `stand-in:` comment where it is used. The comment says what
the value replaces.

```csharp
/// The most pixels a side of a preview image holds, 2048 (stand-in: the program builds no image; the bound is
/// below the texture size Godot uploads and above every graphic of the reference install).
public const int MaxSide = 2048;
```

## Tests

Tests use MSTest (`[TestClass]`, `[TestMethod]`, `[DataRow]`). A test method name is a sentence that
states the behavior (`TheRepositoryHoldsNoFileOfAnInstallsFileTypes`).

The repository holds no bytes from an install (INV-0001). Build every fixture in code.

## Common pitfalls

- Do not split a large class into `partial` files to manage its size. Extract cohesive
  responsibilities into separate collaborator types instead. A class that derives from a Godot type
  is `partial` because the Godot source generator requires it. `Layout` is the one other `partial` class:
  `Layout.cs` parses a layout file and `Layout.Placement.cs` builds its entries in a game.
- Do not hard-code a value that the install defines.
