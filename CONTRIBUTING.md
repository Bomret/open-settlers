# Contributing

Open Settlers is an engine for Die Siedler (Blue Byte, 1993) on Godot 4 with C#. Read `README.md` for
the layout and `AGENTS.md` for the project rules. Everyone who takes part follows the
[Code of Conduct](CODE_OF_CONDUCT.md). Report a vulnerability as `SECURITY.md` describes, not in a
public issue.

## Rules

- **No game content.** The repository contains no bytes taken from an install and no code transcribed
  from the game's program (INV-0001 in `docs/invariants.md`). Tests build their fixtures in code.
- **The install is the reference.** The reference install is The Settlers History Edition
  ([ADR 0004](docs/adr/0004-history-edition-is-the-reference-install.md)). Recover a rule or a file
  format from its `Siedler1.exe`, not from a fan source or another reimplementation. Describe each
  recovered rule in your own words in `docs/rules/`, with its address as evidence. Mark a rule that the
  program does not confirm as **inferred** or **stand-in**. `docs/rules/AGENTS.md` describes the process.
- **Code style.** Follow `docs/agents/CODING.md` and `docs/agents/CODING_PATTERNS.md`. The build enforces the rules
  that `CODING.md` lists under "What the build enforces": a violation fails `mise run check`, a plain build, and a play
  run, and names the file, line, and rule ID. `docs/agents/toolchain.md` says how to fix one. The rules follow the .NET
  SDK version that `mise.toml` pins.
- **Docs.** Update the docs that your change makes wrong. `docs/agents/documentation.md` gives the
  writing standard: American English, present tense, current state only.

## Set up

1. Install [mise](https://mise.jdx.dev).
2. Run `mise install` in the repository root. It installs the pinned tools from `mise.toml`.
3. Create the gitignored `mise.local.toml` with the path of your install:

   ```toml
   [env]
   SIEDLER_DIR = "/path/to/thesettlers"
   ```

`README.md` lists the platform requirements and how to get the install. The unit tests need no install.

## Make a change

Run every command through its mise task from the repository root. `mise tasks` lists the tasks.

- `mise run format` formats the C# code with CSharpier.
- `mise run test` runs the unit tests that need neither the install nor Ghidra.
- `mise run check` runs the full verification: format check, the unit tests that need neither the install nor Ghidra, and build. It must pass
  before you open a pull request.
- `mise run test:integration` runs the integration tests, which need the install (`SIEDLER_DIR`) and Ghidra. Run it with
  `SIEDLER_DIR` set before you open a pull request that changes what they test.

## Commits and pull requests

- Write commit messages as [Conventional Commits](https://www.conventionalcommits.org/):
  `<type>(<scope>): <summary>`, for example `fix(assets): find install files case-insensitively`.
- Report bugs and propose changes as issues on GitHub (`Bomret/open-settlers`).
- Open a pull request into `main` on GitHub. Keep one change per pull request.
- Pull requests are merged by squash. The pull request title becomes the commit message, so it follows
  Conventional Commits too.
