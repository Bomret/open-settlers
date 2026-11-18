# 19. Each library pairs with its own tests and publishes its API

**Status:** Accepted

Amends [ADR 0017](0017-install-data-enters-through-import-ports.md).

Every library has a test project beside it: `src/<Name>/` pairs with `src/<Name>.Tests/`. A test lives in
the test project of the lowest library that reaches, through its own references, every type the test uses.
A test project references its library and test helpers, and no other library. The helpers that name no
library type live in `src/Testing/` (`OpenSettlers.Testing`), which references no library. The helpers
that name a library's types live beside that library in `src/<Name>.Testing/`
(`OpenSettlers.<Name>.Testing`), which references that library and the helper projects of the libraries it
references. A test project references the helper projects of its own library. It also references those of every library
its library references, directly or through another library. The Godot host pairs with `OpenSettlers.Tests/` at the
repository root, which also holds the tests of the repository itself: the code-style build, the tools, the
content set, and the test category filters.

A library grants its internals to its own test project and to no other assembly. No route grants more.
No `InternalsVisibleTo` item, `AssemblyAttribute` item, source attribute, or shared build file names another
assembly, and no reflection skips the access check. The rule has no exception. A type or member that
another library uses is public, and is part of the library's API; the rest stays internal. A consumer
changes another library's state through that library's public API, never by writing its internals.

The Godot-free folders of the Godot project, `src/Launch/`, `src/Assets/`, and `src/MapView/`, are
libraries. `src/Viewer/` holds the Godot nodes and stays in the Godot project. Assets references Launch;
Launch does not reference Assets.

The layout lets a change name the test projects it reaches through the project graph, so a run can test
those projects and skip the rest. A helper project that referenced every library would put every test
project above every library, so helpers split by library.

## Considered options

- **One test project for every library.** Every change reaches it, so no change can skip a test, and the
  test project's grant of every library's internals hides how libraries depend on each other.
- **A test beside the library under test, with extra references.** A test of the application that loads
  tables of the install would need the application's test project to reference the History Edition
  source, which the application itself never references (ADR 0017).
- **Production grants between libraries.** They let one library write another's state, so the API a
  consumer sees is not the API a reader of the library sees.
- **One helper project for every library.** Every test project would reach every library through it, so
  every change would reach every test project.
- **Launch and Assets as one library.** It would break one library per folder. It would also put the code of Launch
  and Assets in one assembly, so Assets would no longer reference Launch.

## Consequences

The map view references the adapters, which reference the History Edition source, so the compiler does not
hold the map view to the ports, and the repository scan of INV-0011 stays. The helper projects are no
test pairs and have no tests of their own.
