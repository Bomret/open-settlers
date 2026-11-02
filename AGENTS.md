# Open Settlers

An open-source engine for Die Siedler (Blue Byte, 1993) on Godot 4 with C#. It ships no game content and
reads the files of a Die Siedler install the user owns. See `README.md` for layout and usage.

## Where to read

- Domain terms: `CONTEXT.md`.
- Decisions: `docs/adr/`. Invariants: `docs/invariants.md`.
- Milestone plan: `docs/roadmap.md`; per-milestone scopes: `docs/milestones/`.
- Rules recovered from the game's program: `docs/rules/` (start at `docs/rules/AGENTS.md`). Where
  routines and data lie in the program: `docs/rules/program-index.md`.
- Layout, requirements, and how to get an install: `README.md`.

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

## Agent skills

### Slice store
GitHub Issues on `Bomret/open-settlers`, through `gh api` with `GITHUB_TOKEN`. See `docs/agents/issue-tracker.md`.

### Code host
GitHub (`Bomret/open-settlers`) through `gh`; scopes open as draft pull requests into `main`. See `docs/agents/code-management.md`.

### Documentation
In-repo `AGENTS.md` hierarchy, controlled writing standard, American English. See `docs/agents/documentation.md`.

### Knowledge base
GitHub wiki of `Bomret/open-settlers`, staged locally and sent by `/superdev:publish-kb` through git. See `docs/agents/knowledge-base.md`.

### Coding
C# style rules in `docs/agents/CODING.md`; the patterns this repo uses in `docs/agents/CODING_PATTERNS.md`.

### Toolchain
.NET SDK 10 (net8.0), Godot 4.7.2 .NET, CSharpier 1.3.0, pinned in `mise.toml`. See `docs/agents/toolchain.md`.

### Verification
Full: format check, unit tests, and build; focused: unit tests. See `docs/agents/verification.md`.

### Deployment
Posture `manual`, no targets yet. See `docs/agents/deployment.md`.
