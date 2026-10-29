# Patterns (LLM Reference)

Target audience: LLMs working in this repo. The folder layout is in `README.md` (Layout).

## Namespace

`OpenSettlers.<Folder>`, one namespace per folder of `src/`. For example, `OpenSettlers.Formats`. Tests use
`OpenSettlers.Tests`.

## Godot boundary

Keep logic that needs no engine in Godot-free folders of `src/`. The test project compiles the source files
of those folders directly (a `<Compile Include>` per folder in `OpenSettlers.Tests.csproj`), so the tests
run without Godot. A `using Godot` in such a folder breaks the test build.

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
an `ArgumentOutOfRangeException` from a palette lookup. Any other exception is a bug: let it propagate. The
asset browser (`AssetBrowserView`) catches whatever escapes `AssetCheck.Run`, prints `asset check crashed` to stderr, and exits 1.

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
