# Invariant register

### INV-0001

Class: legal
Concepts: Install, Asset
Statement: The repository contains no bytes taken from an install and no code transcribed from the game's program; tests build their fixtures in code, and rules recovered from the program are described in our own words. A test may hold a short sequence of values observed in a run of the original, or a digest of its output, with the seed and the origin of each; it never holds a dump of the original's output.
Breaks: silently
Check: `RepositoryContentTests` fails on a file with the extension of an install's file types; no cheap test can tell a copied byte sequence or a transcribed routine from an original one, so reviewers check that fixtures are built in code and that recovered rules are restated, not transcribed.

### INV-0002

Class: architecture
Concepts: Simulation, Tick
Statement: The simulation draws randomness only from its own seeded generator, never from wall-clock time, frame timing, the iteration order of an unordered collection, or floating-point arithmetic whose result can differ between platforms; the same seed and the same inputs give the same state after the same number of ticks.
Breaks: silently
Check: none — no simulation code exists yet; the first milestone with a simulation adds a test that runs the same seed and inputs twice and compares the states.

### INV-0003

Class: fidelity
Concepts: Simulation, Rule
Statement: The engine's random generator produces the same sequence as the original's for the same seed, and a map generated from a seed is the same map the original generates from it.
Breaks: silently
Check: none — the generators do not exist yet; the map milestone adds tests that compare generated sequences with values observed in the original and generated maps with digests of the original's maps (INV-0001).

### INV-0004

Class: architecture
Concepts: Simulation
Statement: The simulation code references no Godot type, so its rules are tested without the engine.
Breaks: silently
Check: the test build — the test project compiles the Godot-free folders of `src/` directly, so a Godot reference there breaks it (`docs/agents/CODING_PATTERNS.md`, "Godot boundary").
