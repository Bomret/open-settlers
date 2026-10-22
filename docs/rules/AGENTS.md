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
own names. Read it before you open the program in REA. When an investigation names or explains a
routine, a datum, or a save field, add or update its row and take its area out of "Unexplored", in the
same commit as the rule note. Set the same names in REA with `set_addresses_names`. The index links to
the rule notes and does not restate the rules.

To search the whole program, use the local dump in `re-local/dump` (`tools/GhidraDump/README.md`):
`functions.tsv` and `strings.tsv` find a routine by string, caller, or callee. When the dump is
missing, build it before you search the program in REA.

## Topics

- [Game code](game-code.md): where the game code lies inside `Siedler1.exe`.
- [Timing](timing.md): the timer, the game clock, and how the main loop advances the simulation.
- [Map start](map-start.md): the random generator, the free-game seed, and where the seed enters the
  map generation.
