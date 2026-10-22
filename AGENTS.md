# Open Settlers

An open-source engine for Die Siedler (Blue Byte, 1993) on Godot 4 with C#. It ships no game content and
reads the files of a Die Siedler install the user owns. See `README.md` for layout and usage.

## Where to read

- Domain terms: `CONTEXT.md`.
- Decisions: `docs/adr/`. Invariants: `docs/invariants.md`.
- Milestone plan: `docs/roadmap.md`.
- Layout, requirements, and how to get an install: `README.md`.

## Game install

The path of the local install is in `CLAUDE.local.md`, a gitignored file per checkout. Read it there,
and ask the user for the path when the file is missing.

Investigate the game's logic, rules, and file formats in the `SIED.EXE` of that install with REA (the
`reverse-engineer-anything` skill and the `rea` MCP tools) before you build a rule or a reader. Do not
guess a rule or copy one from a fan source or another reimplementation. Record each recovered rule in our
own words with its address as evidence (INV-0001). Mark a rule that the program does not confirm as
**inferred** or **stand-in**.

## Agent skills

### Slice store
Codeberg Issues on `bomret/open-settlers`, through the `tea` login for `https://codeberg.org`. See `docs/agents/issue-tracker.md`.

### Code host
Codeberg (`bomret/open-settlers`) through `tea`; scopes open as draft pull requests into `main`. See `docs/agents/code-management.md`.

### Documentation
In-repo `AGENTS.md` hierarchy, controlled writing standard, American English. See `docs/agents/documentation.md`.

### Knowledge base
Codeberg wiki of `bomret/open-settlers`, staged locally and sent by `/superdev:publish-kb`. See `docs/agents/knowledge-base.md`.

### Coding
C# style rules in `docs/agents/CODING.md`; the patterns this repo uses in `docs/agents/CODING_PATTERNS.md`.

### Toolchain
.NET SDK 10 (net8.0) and Godot 4.7.2 .NET; build with `dotnet build OpenSettlers.csproj`. Format with CSharpier 1.3.0, a local tool, through `dotnet csharpier format .`. See `docs/agents/toolchain.md`.

### Verification
Full: format check, unit tests, and build; focused: unit tests. Arms the merge green-gate. The first `dotnet tool restore` needs NuGet access. See `docs/agents/verification.md`.

### Deployment
Posture `manual`, no targets yet. See `docs/agents/deployment.md`.
