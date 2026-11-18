# Invariant register

### INV-0001

Class: legal
Concepts: Install, Asset
Statement: The repository contains no bytes taken from an install and no code transcribed from the game's program; tests build their fixtures in code, and rules recovered from the program are described in our own words. The stand-in rules of the basic content set are values of the project's own making. They are never read from the program (ADR 0018). A test may hold a short sequence of values observed in a run of the original, or a digest of the original's output. The test states the seed and the origin of each sequence and each digest. A test never holds a dump of the original's output.
Breaks: silently
Check: `RepositoryContentTests` fails on a file with the extension of an install's file types among the files git would commit: the tracked files and the untracked files no ignore rule covers. It fails where git is missing or cannot list the repository. No cheap test can tell a copied byte sequence or a transcribed routine from an original one, so reviewers check three things. Fixtures are built in code. The recovered rules in `docs/rules/` are restated, not transcribed. The basic set's stand-in values are invented.

### INV-0002

Class: architecture
Concepts: Simulation, Tick, Content catalog
Statement: The simulation draws randomness only from its own seeded generator. It never draws randomness from wall-clock time, frame timing, the iteration order of an unordered collection, or floating-point arithmetic whose result can differ between platforms. The content catalog orders its entries by stable name, whatever order its source gives them in (ADR 0020). The same seed and the same inputs give the same state after the same number of ticks.
Breaks: silently
Check: `GameTests` (`TheSameSeedAndCommandsGiveTheSameDigest`, `ACommandAtAnotherTickGivesAnotherDigest`) runs the same seed and commands twice and compares `Game.Digest()`. A catalog test builds one catalog from its entries in two orders and compares the entry order.

### INV-0003

Class: fidelity
Concepts: Simulation, Rule
Statement: The engine's random generator produces the same sequence as the original game's generator for the same seed. The engine's map generator produces the same map from a seed as the original game's map generator. The original game is the game code that both releases share, as `Siedler1.exe` carries it (ADR 0004). The map rule holds under the History Edition content set. The basic set's stand-in rules give other maps (ADR 0018).
Breaks: silently
Check: `RandomGeneratorTests` and `SeedTests` compare generated sequences and parsed seeds with values observed in the original, and `MapGeneratorTests` (`EveryPassMatchesTheEmulatorForEverySizeAndSeed`) compares generated maps with digests of the original's maps (INV-0001).

### INV-0004

Class: architecture
Concepts: Simulation
Statement: The simulation code references no Godot type, so tests run its rules without Godot.
Breaks: silently
Check: the test build (`docs/agents/CODING_PATTERNS.md`, "Godot boundary").

### INV-0005

Class: fidelity
Concepts: Layout, Entry state, Settler
Statement: A layout sets a settler or a good only in an entry state: a state some routine of the original writes at the start of a behaviour, named by its address. It never sets a settler in the middle of a behaviour (a walk, a cut, a step's timer). A settler a layout places is one of the castle's own: one of its 20 first settlers, or one its castle entry names.
Breaks: silently
Check: the layout loader's tests refuse every settler or good entry that names no supported entry state, and each supported entry state's documentation names its address.

### INV-0006

Class: architecture
Concepts: Name table, Thing, Catalog entry, Stable name
Statement: Every name shown for a map object code or a settler state comes from a name-table entry that cites its evidence, and every name shown for a good, building type, or job is the stable name of its catalog entry; an original entry's name cites its evidence (ADR 0010, ADR 0020). The raw value always shows beside the name, a code with no entry shows only raw, and an entry with no original code shows its name and no raw value.
Breaks: silently
Check: `NameTableTests` checks that every entry carries its evidence and that a code with no entry gets no name. A catalog test checks that every original entry carries its evidence. Reviewers check the evidence of each new entry.

### INV-0007

Class: architecture
Concepts: Thing ID, Event, Simulation
Statement: Recording never changes the game. The thing-ID counters and the event store stay out of `Game.Digest()`, and no rule reads them to decide anything. A run of the same seed and commands gives the digests that `golden.txt` pins, whether or not anything reads the events.
Breaks: silently
Check: `GoldenTests` plays each scenario once, reads no event until the run has ended, and compares the digests at each checkpoint with the lines of `golden.txt`; reviewers check that no rule reads the store or a counter.

### INV-0008

Class: architecture
Concepts: Simulation, Domain, Adapter
Statement: The simulation's layers are separate assemblies, and each depends only inward. The domain assembly references no other project assembly. The application assembly references only the domain. The ports assembly references only the domain, and each source assembly the ports assembly and the domain. The adapter assembly references the domain, the application, and the History Edition source assembly. None of them references a Godot type. The original's packed state bytes, map layers, and bare codes appear only in the adapter assembly; the map view and every other caller read the typed model (ADR 0013). The thing readouts of the map view are the one exception: they show the original's encoding of a settler or building through `SettlerEncoder` and `BuildingEncoder` as read-only display, and no rule takes it as input.
Breaks: loudly
Check: the project references in each layer's `.csproj`. The compiler refuses a reference that points outward. Reviewers check that no packed byte or bare code of the original enters the domain or the application.

### INV-0009

Class: architecture
Concepts: Graphics source, Simulation
Statement: The simulation is independent of the graphics source. No domain, application, or adapter type references a graphics source (ADR 0014, ADR 0018).
Breaks: loudly
Check: the project references keep the graphics sources out of the simulation assemblies, and the compiler refuses a reference that points outward.

### INV-0010

Class: architecture
Concepts: Graphics source, Entry
Statement: The original's graphics encoding stays inside the History Edition source. Entry numbers, archive files, palette indexes, and the slope and figure tables of `Siedler1.exe` used to pick an image appear in no draw term and in no class of the map view outside that source (ADR 0014). The one exception is `StepOffsets`, which reads entry 2 of the graphics archive for the walk steps of the History Edition content set. The basic set brings its own walk steps (ADR 0018).
Breaks: silently
Check: `GraphicsSourceBoundaryTests` pins the classes of the map view outside the source and `StepOffsets`'s exception for the History Edition. It also pins that `OpenSettlersWalkSteps` of the basic set reads no archive entry and names no install. Reviewers check each draw term and every map view class outside the History Edition source. A test of the draw-list builders runs with no install-backed source.

### INV-0011

Class: architecture
Concepts: Import port, Graphics source, Install
Statement: The engine reaches game data only through import ports. Only the History Edition source assembly reads the install or names a type of the original's file formats. Only the Open Settlers source assembly reads the basic content set: its graphics, its stand-in rules, and its walk steps (ADR 0018). The map view, the asset check and the asset browser state (`src/Assets`), the domain, and the application name no type of a source assembly. The launch library (`src/Launch`) and the Godot host name one only where they build a source: the launch code and the main scene, `AssetBrowserView`. The rule tables source builds the content catalog, and every other layer reads it as `RuleTables.Catalog` (ADR 0020). The adapter assembly is the one exception: it names `JobTableNames` of the History Edition source, through which the name table reads the state names; a job names itself on its type and its catalog entry (ADR 0017, ADR 0020).
Breaks: loudly
Check: the format readers, the asset kinds, the graphics readers and figure tables, the program tables importer, the job tables modules, and the looks are internal to the History Edition assembly, so the compiler refuses a use from outside it. Its public types are the ones the host builds (`Install`, `HistoryEditionSource`, `HistoryEditionRuleTables`, `HistoryEditionAssets`, `StepOffsets`) and `JobTableNames`. The looks and the rule file reader are internal to the Open Settlers assembly. Its public types are the ones the host builds: `OpenSettlersSource`, `OpenSettlersRuleTables`, and `OpenSettlersWalkSteps`. `ImportPortBoundaryTests` checks that the domain and the application reference no source assembly and that the adapter assembly's metadata names no History Edition type but `JobTableNames`. The map view and `src/Assets` are libraries of their own, but the map view references the adapters, which reference the History Edition source, and `src/Assets` references the launch library, which references both sources, so the compiler does not keep either from those public types. `src/Viewer` compiles inside the Godot project, which references both source assemblies, so the compiler does not keep it from them either. `GraphicsSourceBoundaryTests` scans the map view, `src/Assets`, and the viewer classes outside the composition root for History Edition names and for content-set names (the source, the looks, their namespace, and the content set's folder). Reviewers check that no port returns a format type.

### INV-0012

Class: architecture
Concepts: Library, Test pair
Statement: Every library has a test pair: a test project beside it that references the library, `OpenSettlers.Testing`, and the helper projects (`OpenSettlers.<Name>.Testing`) of that library and of every library it references directly or through another library, and no other project. A library and the Godot host (`OpenSettlers.csproj`) reference no test project and no helper project, `OpenSettlers.Testing` included, so no test code ships. `OpenSettlers.Testing` references no library, and a helper project references only its library and the helper projects of the libraries that library references directly or through another library. Tests of the repository itself live in `OpenSettlers.Tests/` beside the Godot project (ADR 0019).
Breaks: silently
Check: `TestPairTests` in `OpenSettlers.Tests` reads every project folder under `src/`, whatever its project file is called, and fails on a library without a sibling `<Name>.Tests` project, on a test project or a helper project that reaches a project outside its library's reach (references followed transitively), on `src/Testing` reaching a library, on a library or a project outside `src/` whose folder does not end in `.Tests`, such as `OpenSettlers.csproj`, that references a test project, a helper project, or `src/Testing`, on a test or helper project with no library beside it, on a test project whose assembly name is not `OpenSettlers.<Name>.Tests`, such as another library's test assembly, on any other project, a helper project or a project outside `src/`, that builds a library's test assembly name, and on an `AssemblyName` set in a `.props` or `.targets` file. It reads project references and the assembly name by element name in any case and in any namespace. It allows no exception.
Paths: src/**, OpenSettlers.Tests/**

### INV-0013

Class: architecture
Concepts: Library, Test pair
Statement: A library grants its internals to its own test project, `OpenSettlers.<Name>.Tests`, and to no other assembly, by no route: not through an `InternalsVisibleTo` item, an `AssemblyAttribute` item, a source attribute, or a shared build file, and not through reflection that skips the access check. Another library reaches it only through its public types (ADR 0019).
Breaks: silently
Check: `TestPairTests` in `OpenSettlers.Tests` fails on a grant, written as an `InternalsVisibleTo` item, an `AssemblyAttribute` item, or a source attribute in its short or full name, that names an assembly other than `OpenSettlers.<Name>.Tests`, on a grant whose assembly it cannot read, on a grant in a `Directory.Build.props`, `Directory.Build.targets`, or any other `.props` or `.targets` file, on a grant written outside a library (a helper project, `OpenSettlers.csproj`, `OpenSettlers.Tests`, or any source file of the repository), on a `using` alias of the attribute, including a `global::` alias, and on `IgnoresAccessChecksTo`, written as an item, an `AssemblyAttribute` item, or a source attribute; item types match in any case. It scans every source file, those of `TestPairs` and `TestPairTests` included: their fixtures write the attribute names in parts. It allows no exception. Reviewers check that no test reads another library's non-public member by reflection.
Symbols: InternalsVisibleTo, IgnoresAccessChecksTo
Paths: **/*.csproj, **/*.cs, **/*.props, **/*.targets

### INV-0014

Class: architecture
Concepts: Content catalog, Catalog entry, Original code, Domain, Rule
Statement: No rule of the domain or the application reads or branches on an original code. Goods, building types, and jobs are identified by their stable names, and the facts the rules read come from their catalog entries. Only the History Edition importer and the exporters read an original code (ADR 0020). The map view's readout reads whether an entry has one (`OriginalCode`, `GoodWords.IsCodelessWord`, `BuildingWords.HasCode`, `JobWords.HasCode`), to decide whether it shows a raw value beside the name or the name alone. One exception stands: `UnportedJob.ArrivesHomeGenerically` reads the job's number, because an unported job has no catalog entry and no stable name. It is a stand-in that pins the table at 0x437dfb and retires when the job is ported.
Breaks: silently
Check: `OriginalCodeBoundaryTests` (`OpenSettlers.Tests/OriginalCodeBoundaryTests.cs`) parses `src/Domain` and `src/Application` for reads of an original code (the code members, the job lookup, and the job number names), with planted violations as positive controls. Reviewers check that a new fact of an original entry is read from its table into the entry, not derived from its code.
Symbols: GoodEntry.OriginalCode, BuildingEntry.OriginalCode, JobEntry.OriginalCode, Job.Of
Paths: src/Domain/**, src/Application/**

### INV-0015

Class: architecture
Concepts: Building site, Castle under construction, Graphics source
Statement: The view draws a building site and the castle under construction from the building's type and its progress word alone. It keeps no timer, counter, or animation state of its own for the growth, so the drawing always shows the simulation's state.
Breaks: silently
Check: `SiteStagesTests` and `SiteDrawTests` derive each drawing from a type and a progress word, with no clock and no earlier frame.
Paths: src/Import/SiteStages.cs, src/MapView/ObjectDraws.cs, src/MapView/ObjectLayerWatch.cs

### INV-0016

Class: architecture
Concepts: Draw term, Simulation
Statement: The map view reads the game and never writes it. A value the view shows that the original's draw routines write back into the game state, such as the countdown of a burning building, is derived anew for each frame and never stored. A game drawn on screen and the same game not drawn give the same digest.
Breaks: silently
Check: `ObjectDrawsTests` builds the draw list of a game with a burning building and compares `Game.Digest()` before and after.
Paths: src/MapView/**

### INV-0017

Class: architecture
Concepts: Job search, Work area, Work-area overlay
Statement: A gatherer's search range and target test exist once, in its job rules' job search. The worker's search draws from that range and tests with that test, and the work-area overlay reads the same declaration. No view keeps its own range, offset list, or target test of a job.
Breaks: silently
Check: `JobSearchTests` pins each gatherer's range against the draw mask of its loop. It pins its target test against the nodes its worker accepts. `GoldenTests` and the gatherer tests (`WoodcuttingTests`, `StonecuttingTests`, `ForestryTests`, `FisherTests`, `FarmerTests`) keep the searches' outcomes. `WorkAreaDrawsTests` checks that the overlay's marks equal the job search's spiral entries around each building. It also checks that no file under `src/MapView/` or `src/Viewer/` names the spiral. Reviewers check that no such file names a spiral entry bound or a target condition.
Paths: src/Application/*Rules.cs, src/MapView/**

### INV-0018

Class: fidelity
Concepts: Rule
Statement: A command of the emulator harness prints the same lines whatever commands ran before it in the same launch. It prints them whether the launch imports the program or reuses a kept project. The harness is `EmulateMapStart.java` and, for the `draw:` verb, `EmulateDraw.java`; a launch is one `emulate.py` call. Each command, of every verb and of a draw batch, starts from the state a launch of its own gives it. So a reference value recorded from the harness (ADR 0008) does not depend on the order or company of the commands it was recorded with. The invariant covers the commands that run. A command that fails ends a launch that runs no draw batch, and the later commands of that launch print nothing. A command of a draw batch that fails prints its failure line, and the batch runs on.
Breaks: silently
Check: a Ghidra-category test runs commands that each change a different part of the harness's state in one launch. It compares each command's lines with the lines of a launch of its own. `EmulatorLaunchTests.ACommandThatRestoresTheCachedGenerationAfterAnotherDidPrintsItsOwnLaunchLines` covers the cached map generation. `EmulatorLaunchTests.ACommandAfterACommandOfAnotherVerbRunsWithItsOwnVerbsSettingsOnly` covers the settings of a verb. `EmulatorLaunchTests.EachCommandOfADrawBatchPrintsTheLinesOfASingleDrawCommand` covers a draw batch. `EmulatorLaunchTests.ACommandAgainstAKeptProjectPrintsTheLinesOfAFreshImport` covers a kept project. `EmulatorSettingsTests.DefaultSettingsSetsEveryFieldOfTheScriptThatACommandCanChange` reads `EmulateMapStart.java`. It fails where `defaultSettings`, which runs at the start of each command, does not set a field of the script other than the emulator, or does not clear a list or an array that a final field holds. Reviewers check that a new verb keeps every setting it sets in such a field. They also check that state shared across the commands of a launch holds only what a launch of its own would compute the same.
Paths: tools/GhidraDump/**
