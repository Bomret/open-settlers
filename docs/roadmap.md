# Milestone roadmap

This roadmap orders the work of Open Settlers toward the [free-play release](../CONTEXT.md#roadmap). The
milestones M0 to M10 lead to that release. Missions (M11), music (M12), and the extensions follow it.

## How the roadmap works

After the foundation milestones M0 to M2, the game grows one production chain at a time, starting from
the smallest playable loop ([ADR 0002](adr/0002-milestones-grow-by-playable-loop.md)). Every milestone
reproduces the rules of the game code of `Siedler1.exe` under rule fidelity
([ADR 0003](adr/0003-rule-fidelity-not-state-identity.md)). Each milestone ends in a mode that a person
can run and check by hand.

The project plans each milestone as its own feature, with its own `/superdev:plan` interview and its own
feature label. The roadmap carries no progress status and no dates. The pull requests and the issue
tracker show the progress of each milestone.

The roadmap rests on these recorded decisions, invariants, and glossary terms:

- Decisions: [ADR 0002](adr/0002-milestones-grow-by-playable-loop.md),
  [ADR 0003](adr/0003-rule-fidelity-not-state-identity.md), and
  [ADR 0004](adr/0004-history-edition-is-the-reference-install.md).
- Invariants: [INV-0001](invariants.md#inv-0001) to [INV-0004](invariants.md#inv-0004).
- Glossary terms: [Rule, Simulation, and Tick](../CONTEXT.md#game-rules), and
  [Free-play release](../CONTEXT.md#roadmap).

Each of M0 to M10 states its goal and these parts:

- **Runnable mode:** what a person can run at the end of the milestone.
- **Done when:** the observable outcome that ends the milestone, and whether checking it needs an install.
- **Depends on:** the earlier milestones it builds on.
- **Investigate:** the parts of the original program that the milestone recovers rules from.

M3 to M10 link their **Investigate** part to a [scope file](milestones/AGENTS.md) that lists the leads in
`Siedler1.exe` where the investigation starts.

M11, M12, and the extensions state only their goal and what they depend on.

## Working names and M0 findings

The building and profession names in this roadmap are working names. The program confirms or replaces
each name when a milestone investigates that name.

The [rule notes](rules/AGENTS.md) record what M0 found about the program: [Timing](rules/timing.md) and
[Map start](rules/map-start.md).

## Milestones to the free-play release

### M0 Analysis access

M0 makes the game code of `Siedler1.exe`, the program of the History Edition, analyzable and records where
it lies ([ADR 0004](adr/0004-history-edition-is-the-reference-install.md)). The analysis tool REA opens the
program directly.

- **Runnable mode:** REA with `Siedler1.exe` open.
- **Done when:** the rule notes record the address band of the game code, the tick rate, and whether the
  program generates a map from a seed, each with an address in `Siedler1.exe` as evidence. Checking this
  outcome needs an install.
- **Depends on:** none.
- **Investigate:** the boundary between the game code and the platform layer, the code that starts a map,
  and the timer that drives the game loop.

### M1 Assets

M1 reads the data files of an install through the engine's own readers.

- **Runnable mode:** a headless asset check, and an asset browser that shows each asset.
- **Done when:** the asset check reads every data file of an install and reports the assets it found.
  The asset browser shows palettes, sprites, and sounds. The readers' unit tests need no install.
  Checking the asset check and the browser needs an install.
- **Depends on:** M0.
- **Investigate:** the code that opens and decodes each data file.

### M2 Map

M2 generates the map of a seed and shows its terrain.

- **Runnable mode:** a map view that takes a seed and shows the generated terrain, with a camera that
  moves over the map.
- **Done when:** the random generator and the map generator match the original
  ([INV-0003](invariants.md#inv-0003)). Tests check them against values observed in the original and
  against digests of the original's maps, never against dumps ([INV-0001](invariants.md#inv-0001)).
  These tests need no install. Checking the map view needs an install.
- **Depends on:** M0 and M1.
- **Investigate:** the random generator, the map generator, the terrain drawing, and the palette
  handling.
- **Open decision:** how the engine renders the original's palette graphics.

### M3 First loop

M3 runs the smallest playable loop on the fixed-tick simulation
([INV-0002](invariants.md#inv-0002), [INV-0004](invariants.md#inv-0004)).

- **Runnable mode:** a game on a generated map with a castle. A person builds flags, a road, and a
  woodcutter through a minimal build interface.
- **Done when:** a carrier delivers a log from the woodcutter to storage over the road. A test runs the
  same seed and inputs twice and gets the same state. The simulation tests need no install. Checking the
  game needs an install.
- **Depends on:** M2.
- **Investigate:** the tick loop, the rules for flags and roads, and carrier movement with the order of
  handovers at a flag. M3 also investigates the woodcutter's work cycle and the castle's storage. Scope: [M3 scope](milestones/m3-first-loop.md).
- **Open decision:** the interface approach, that is, how the engine builds the original's panels and
  input. M3 answers it with a stand-in: a minimal build interface of Godot controls and mouse picks on
  the `--game-view` view, which sends `Command`s to the `Game`. It does not read the original's panels. The
  original's panels and input stay open for a later milestone.

### M4 Construction

M4 adds construction: workers build buildings from materials.

- **Runnable mode:** a game where a person places building sites and the settlers build them.
- **Done when:** a digger levels a building site that a person placed. The site receives its planks and
  stones over the road and becomes a working building. The forester, the sawmill, and the stonecutter
  work. Checking the game needs an install.
- **Depends on:** M3.
- **Investigate:** the placement rules for each building size, the construction sequence, the builder
  and the digger, and the forester, sawmill, and stonecutter. Scope: [M4 scope](milestones/m4-construction.md).

### M5 Food

M5 adds the food chains.

- **Runnable mode:** a game where a person builds the food buildings.
- **Done when:** the fisher, the farm, the mill, the baker, the pig farm, and the butcher produce, and
  their goods reach storage. The original has no well ([Food buildings](rules/food-buildings.md#the-well)). Checking the game needs an install.
- **Depends on:** M4.
- **Investigate:** the production cycle of each food building and the transport priorities of goods. Scope: [M5 scope](milestones/m5-food.md).

### M6 Content catalog

M6 makes goods, building types, and jobs entries of an engine-owned content catalog. Each entry is identified
by its stable name, and the original's code is an optional attribute of it
([ADR 0020](adr/0020-content-is-a-catalog-of-named-entries-and-the-original-s-codes-stay-at-its-edge.md),
[INV-0014](invariants.md#inv-0014)).

- **Runnable mode:** the M5 game view, which plays as in M5 under both content sets.
- **Done when:** the rules read the facts of a good, a building type, or a job from its catalog entry and
  never from an original code, and a repository test fails when a rule reads one. On the original's content
  every golden digest, play script, snapshot, and readout gives the values it gave before. A test carries a
  good that the original lacks, `ai-token`, from a flag into the castle as catalog data only. Layout files
  and play scripts name goods and building types by stable name. The tests need no install. Checking that
  the game plays as before needs an install.
- **Depends on:** M5.
- **Investigate:** the tables that give each good and each building type its facts, and the routines that
  test a code where a table could give the fact. Scope: [M6 scope](milestones/m6-content-catalog.md).

### M7 Mining and metal

M7 adds the mines, the smelters, and the tools.

- **Runnable mode:** a game where a person sends geologists and builds mines, smelters, and the
  toolmaker.
- **Done when:** a geologist marks resources, and a mine that receives food produces. A smelter makes
  iron or gold, and the toolmaker makes the tools that new workers need. Checking the game needs an
  install.
- **Depends on:** M6.
- **Investigate:** the geologist, the resources in the ground, the food each mine uses, the smelters,
  and the toolmaker. Scope: [M7 scope](milestones/m7-mining-and-metal.md).

### M8 Military and territory

M8 adds knights, military buildings, and territory.

- **Runnable mode:** a game with a second party that does not act, whose military buildings a person can
  attack.
- **Done when:** knights occupy military buildings, the territory grows with each occupied building, and
  an attack captures a building of the other party. The weaponsmith equips new knights. Checking the game
  needs an install.
- **Depends on:** M7.
- **Investigate:** the military buildings, the territory borders, the knight levels and gold, the
  weaponsmith, and the combat rules. Scope: [M8 scope](milestones/m8-military-and-territory.md).

### M9 Computer opponent

M9 adds a computer opponent that builds and fights.

- **Runnable mode:** a game against a computer opponent.
- **Done when:** the computer opponent builds an economy, occupies territory, and attacks the player.
  Checking the game needs an install.
- **Depends on:** M8.
- **Investigate:** the routines that decide what the computer opponent builds and attacks. Scope: [M9 scope](milestones/m9-computer-opponent.md).
- **Open decision:** how much of the original opponent's logic the engine recovers.

### M10 Free-play release

M10 completes the [free-play release](../CONTEXT.md#roadmap).

- **Runnable mode:** the engine with its free-game start menu.
- **Done when:** a person starts a free game from the start menu and plays it to a win or a loss. The
  person saves the game and loads it again in the engine's own save format. The game plays its sound
  effects and shows the remaining original panels. Checking the game needs an install.
- **Depends on:** M9.
- **Investigate:** the free-game start menu, the win and loss conditions, the triggers of the sound
  effects, and the remaining panels. Scope: [M10 scope](milestones/m10-free-play-release.md).

## After the release

The roadmap plans this work after the free-play release:

- **M11 Missions:** the original missions. Depends on M10.
- **M12 Music:** the original music. Depends on M10.
- **Extensions:** features and rule changes that the original lacks. Depends on M11 and M12. Two parts are
  recorded as later work: the end-state mod sketch
  ([intent #366](https://github.com/Bomret/open-settlers/issues/366)) and the decision on a generic converter
  rule ([intent #367](https://github.com/Bomret/open-settlers/issues/367)).

This work leaves out the two-player split-screen mode. The roadmap defers that mode and gives it no
milestone.

## Non-goals

The roadmap excludes these goals:

- Loading save games of either release of the original game
  ([ADR 0003](adr/0003-rule-fidelity-not-state-identity.md), [ADR 0004](adr/0004-history-edition-is-the-reference-install.md))
- Reading a DOS CD install ([ADR 0006](adr/0006-the-engine-reads-what-siedler1-reads.md))
- Any rule change before M11 completes the missions
- Game data or transcribed program content in the repository ([INV-0001](invariants.md#inv-0001))
- Dates.
