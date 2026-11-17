# 18. Each library pairs with its own tests and publishes its API

**Status:** Accepted

Amends [ADR 0017](0017-install-data-enters-through-import-ports.md).

Every library has a test project beside it: `src/<Name>/` pairs with `src/<Name>.Tests/`. A test lives in
the test project of the lowest library that reaches, through its own references, every type the test uses.
A test project references its library, and the shared test helpers in `src/Testing/`
(`OpenSettlers.Testing`), and nothing else. The Godot host pairs with `OpenSettlers.Tests/` at the
repository root, which also holds the tests of the repository itself: the code-style build, the tools, the
content set, and the test category filters.

A library grants its internals to its own test project and to no other assembly. A type or member that
another library uses is public, and is part of the library's API; the rest stays internal. A consumer
changes another library's state through that library's public API, never by writing its internals.

The Godot-free folders of the Godot project, `src/Launch/`, `src/Assets/`, and `src/MapView/`, are
libraries. `src/Viewer/` holds the Godot nodes and stays in the Godot project. Assets references Launch;
Launch does not reference Assets.

The layout lets a change name the test projects it reaches through the project graph, so a run can test
those projects and skip the rest.

## Considered options

- **One test project for every library.** Every change reaches it, so no change can skip a test, and the
  test project's grant of every library's internals hides how libraries depend on each other.
- **A test beside the library under test, with extra references.** A test of the application that loads
  tables of the install would need the application's test project to reference the History Edition
  source, which the application itself never references (ADR 0017).
- **Production grants between libraries.** They let one library write another's state, so the API a
  consumer sees is not the API a reader of the library sees.
- **Launch and Assets as one library.** It keeps their mutual reference, but breaks one library per
  folder; the reference from Launch to Assets is one helper, which moves to Assets.

## Consequences

The map view is a library that references the import ports and no source assembly, so the compiler holds it
to the ports (INV-0011). The shared helpers are one more project that is no test pair.
