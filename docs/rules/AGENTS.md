# Rule notes

This folder records the rules that the project recovers from the game's program. Each file covers one
topic. The engine reproduces these rules ([ADR 0003](../adr/0003-rule-fidelity-not-state-identity.md)).

## Reference build

Every address in this folder refers to this one build of `Siedler1.exe`, the program of The Settlers
History Edition ([ADR 0004](../adr/0004-history-edition-is-the-reference-install.md)):

- File size: 3023280 bytes.
- SHA-256: `0113d8b31268a5848e6827da32a970d7c6a0aab33ff28cc8f11a974b3679dbd5`.
- Version: the file carries no version resource.

A different build can move every address. Before you cite an address, check the SHA-256 of your
`Siedler1.exe` against the value above.

## Address notation

An address is a virtual address in the loaded image, at the image base `0x400000` that the file
declares. Write it in lowercase hexadecimal with the `0x` prefix, for example `0x41da10`. This is the
notation that REA shows. Do not cite a file offset.

## Contents of a rule note

Each rule note holds:

- The rule, in our own words.
- One or more addresses as evidence.
- A status: **confirmed** when the code at the address shows the rule, **inferred** when the rule
  follows from the code but no single address shows it, or **stand-in** when the engine supplies a value
  that the program does not confirm.
- The mark **platform layer** when the evidence lies outside the game code (`CONTEXT.md`). The platform
  layer is new Windows code, so such a rule may differ from the DOS original.

A rule note never holds decompiler output, assembly, byte sequences, or byte patterns from the program
([INV-0001](../invariants.md#inv-0001)). Describe what the code does, and cite where.

## Program index

[Program index](program-index.md) records where routines and data lie in `Siedler1.exe`, under our
own names. The index links to the rule notes and does not restate the rules.

Investigate the program in this order:

1. Read the program index.
2. Search and read the local dump in `re-local/dump` (below).
3. Use REA only for a question that the dump cannot answer yet.

When an investigation names or explains a routine, a datum, or a save field, add or update its row in
the topic tables and take its area out of "Unexplored". Then rebuild the index's generated sections, the
"By address" table and the coverage line, with
`python3 -I tools/GhidraDump/program_index.py docs/rules/program-index.md re-local/dump`. Do both in
the same commit as the rule note. Never edit a generated section by hand.

REA opens `Siedler1.exe` directly and does not read the Ghidra project in `re-local/ghidra`. Apply
what REA finds, such as a missing function, a function's boundaries, a type, or a comment, to the
Ghidra project as you go.

Never edit the dump by hand: the next dump run overwrites it. A dump run is costly. Write the dump
again only when all of these hold:

- The implementation of the scope is done. Never run it during an investigation or a slice, or only
  because the program was opened or a finding was made.
- The Ghidra project holds a change that alters what the dump says about code: a function created, a
  function's boundaries fixed, or a type or signature that changes the decompiled code. Names and
  comments alone do not count, because the program index carries them.
- Later work will read the dump again. Otherwise the change waits for the next dump run that is due
  anyway.
- The user agreed to this run, after being told the changes that make it necessary.

When the run must change only the tables of the dump, write it in index-only mode
(`tools/GhidraDump/README.md`). Import the program into a new project only when `re-local/ghidra` holds
none: the import replaces the project and loses every change applied to it.

REA's Ghidra provider keeps names only while the program stays open in REA, and it names only routines
that it found: use `annotate_native_function`, since it rejects `set_address_name` and
`set_addresses_names`. To read REA output with our names, set the index names again in each new REA
session.

To search the whole program, use the local dump in `re-local/dump` (`tools/GhidraDump/README.md`):
`functions.tsv` and `strings.tsv` find a routine by string, caller, or callee, and `state_fields.tsv`
finds the routines that read or write a field of the game state. The dump carries the index names.
When the dump is missing, build it before you investigate.

## Topics

- [Game code](game-code.md): where the game code lies inside `Siedler1.exe`.
- [Timing](timing.md): the timer, the game clock, and how the main loop advances the simulation.
- [Map start](map-start.md): the random generator, the free-game seed, and where the seed enters the
  map generation.
