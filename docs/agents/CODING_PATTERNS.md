# Patterns (LLM Reference)

Target audience: LLMs working in this repo. The folder layout is in `README.md` (Layout).

## Namespace

`OpenSettlers.<Folder>`, one namespace per folder of `src/`. For example, `OpenSettlers.Formats`. Tests use
`OpenSettlers.Tests`.

## Simulation layers

The simulation is three class-library assemblies under `src/` that depend only inward (INV-0008, ADR 0013):

- `Domain/` (`OpenSettlers.Domain`): the typed model. The map, flag table, road network, building and settler
  tables, `Job`, `Activity`, `RuleTables`, and the value types they use. It references no other project.
- `Application/` (`OpenSettlers.Simulation`): `Game`, the coordinator, the `ICommandHandler` of each command, the
  `CommandQueue`, the history, and the rules as behaviors (`CarrierBehavior`, `GoodsTransport`, `Removal`,
  `WalkHomeBehavior`, `WoodcutterBehavior`, `SettlerMovement`). It references the domain. A behavior takes its parts
  through its constructor; `GameParts` wires them, and `GameFactory` builds them.
- `Adapters/Simulation/` (`OpenSettlers.Simulation`): the edge. Importers (`ProgramTablesImporter`, `MapImporter`,
  `MapGenerator`, `Layout`), exporters (`GameDigest`, `SnapshotExporter`, `FlagEncoder`, `SettlerEncoder`,
  `BuildingEncoder`, `MapLayerExporter`), and `GameFactory`. It references the domain and the application.

Place new code by what it does: a rule goes in the application, a type that states what a rule reads goes in the
domain, and a conversion from or to the original's bytes goes in the adapters.

## Typed vocabulary

Rules read and write typed state: `Settler` with a `Job` and one `Activity`, `Flag` with its `FlagSide`s and
`WaitingGood`s, `Road`, `Good`, `BuildingType`, `Terrain`, `MapObject`, `ObjectClass`, `Direction`, and `Heading`.
Never read a packed byte, a bit mask, a map layer, or a bare code in the domain or the application. The original's
codes sit at the edge: an importer turns them into the typed model and an exporter turns the typed model back, so
`Game.Digest()` and the events keep the original's encoding. A field with no recovered meaning stays a named raw
field, so the exporters lose nothing. A test of a rule asserts typed state; only a test of an importer or exporter
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
  is `partial` because the Godot source generator requires it. That use is the only exception.
- Do not hard-code a value that the install defines.
