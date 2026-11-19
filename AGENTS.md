# Open Settlers

An open-source engine for Die Siedler (Blue Byte, 1993) on Godot 4 with C#. It ships no game content of
the original and reads the files of a Die Siedler install the user owns. The map view and the game view draw
through a content set: the History Edition install's art and rules, or the repository's basic set, which runs
without an install (`content/`, ADR 0014, ADR 0015, ADR 0018). See `README.md` for layout and usage.

## Where to read

- Domain terms: `CONTEXT.md`.
- Decisions: `docs/adr/`. Invariants: `docs/invariants.md`.
- Milestone plan: `docs/roadmap.md`; per-milestone scopes: `docs/milestones/`.
- Rules recovered from the game's program: `docs/rules/` (start at `docs/rules/AGENTS.md`). Where
  routines and data lie in the program: `docs/rules/program-index.md`.
- Layout, requirements, and how to get an install: `README.md`. Controls of the map view and the game view:
  `docs/controls.md`.

## Where game data is read

The engine reads game data only through the import ports in `src/Import/` (ADR 0017, INV-0011). Each port is an
interface that returns dedicated types, never a format type.

- `src/Import.HistoryEdition/` reads the install (formats, assets, program tables, graphics) and implements every
  port. Its readers and tables are internal; its public types are the ones the host builds, plus `JobTableNames`
  for the adapters.
- `src/Import.OpenSettlers/` reads `content/basic/` and implements the graphics source, the rule tables port, and the walk steps port.
- A rule tables source builds the content catalog (ADR 0020) and hands it over as `RuleTables.Catalog`:
  `ProgramTablesImporter.Catalog` in the History Edition source builds the original's entries, `StandInRules` in the
  Open Settlers source builds the basic set's. The rules, the adapters, and the map view read it from the rule tables.
  The adapters take only the state names from the History Edition source, through `JobTableNames`; goods, building
  types, and jobs take theirs from the catalog.
- The map view, `src/Assets/`, the domain, and the application name no source type. The launch code and the asset
  browser build a source.

Each library has a test project beside it, `src/<Name>.Tests/`, and only that project sees the library's internals
(ADR 0019, INV-0012, INV-0013). `src/Testing/` and `src/<Name>.Testing/` hold the test helpers, and
`OpenSettlers.Tests/` beside the Godot project holds the tests of the repository itself. `src/Viewer/` has no test
project. A new Godot-free folder of the Godot project becomes a library with a test project, or its code is untested.

A new test goes in the project of the lowest library that reaches every type it uses
(`docs/agents/verification.md`, "Where a new test goes").

## Game install

The path of the local install is in `CLAUDE.local.md`, a gitignored file per checkout. Read it there,
and ask the user for the path when the file is missing.

The install is The Settlers History Edition ([ADR 0004](docs/adr/0004-history-edition-is-the-reference-install.md)).
Investigate the game's logic, rules, and file formats in the `Siedler1.exe` of that install before you
build a rule or a reader. Check the program index and the local dump first. Use REA (the
`reverse-engineer-anything` skill and the `rea` MCP tools) only for a question that the dump cannot
answer yet. Record every finding in the program index and apply it to the Ghidra project. Write the
dump again only under the conditions in `docs/rules/AGENTS.md`, "Program index". Do not
guess a rule or copy one from a fan source or another reimplementation. Record each recovered rule in our
own words with its address as evidence (INV-0001). Mark a rule that the program does not confirm as
**inferred** or **stand-in**. Mark a finding that lies only in the platform layer, outside the game code
(`CONTEXT.md`), as a platform-layer finding.

The install also holds the game's manual as PDF: `$SIEDLER_DIR/THE SETTLERS - Manual-EN.pdf`, with German
and French editions beside it (`-DE.pdf`, `-FR.pdf`). The manual is a valid source of information on the
game's rules and terms. Cite it by file and page.

Official material of the original game, such as screenshots and the reference card, is also a valid source.
Its folder on the local machine is in `CLAUDE.local.md`. Cite an image by file name and a PDF by file and page.

## Agent skills

### Slice store
GitHub Issues on `Bomret/open-settlers`, through `gh api` with `GITHUB_TOKEN`. See `docs/agents/issue-tracker.md`.

### Code host
GitHub (`Bomret/open-settlers`) through `gh`; scopes open as ready pull requests into `main`. See `docs/agents/code-management.md`.

### Documentation
In-repo `AGENTS.md` hierarchy, controlled writing standard, American English. See `docs/agents/documentation.md`.

### Knowledge base
GitHub wiki of `Bomret/open-settlers`, staged locally and sent by `/superdev:publish-kb` through git. See `docs/agents/knowledge-base.md`.

### Coding
C# style rules in `docs/agents/CODING.md`; the patterns this repo uses in `docs/agents/CODING_PATTERNS.md`.

### Toolchain
.NET SDK 10 (net8.0), Godot 4.7.2 .NET, CSharpier 1.3.0, pinned in `mise.toml`. See `docs/agents/toolchain.md`.

### Verification
Full: format check, unit tests, and build; focused: unit tests; map view behavior: a play run. See `docs/agents/verification.md`.

### Deployment
Posture `manual`, no targets yet. See `docs/agents/deployment.md`.
