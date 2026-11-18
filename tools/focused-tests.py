#!/usr/bin/env python3
"""Runs the unit-test classes of every test project that a set of changed paths can affect.

Run it from the repository root, with the changed paths as arguments:

    python3 -I tools/focused-tests.py [--list] <path>...

A test project is a folder named `*.Tests` at the root, under tests/, under src/, or under a folder of src/ (TEST_GLOBS).
It runs `dotnet test` on each test project that holds a selected class, filtered to the selected classes and always to
`TestCategory!=Install&TestCategory!=Ghidra`. A whole-suite selection runs `dotnet test --solution OpenSettlers.slnx`
with the category filter, which reaches every test project. With --list it prints the selection instead and runs
nothing: one class name per line, or the line `all` for the whole unit suite. Without a path it prints a usage line on
stderr and exits 2. With --classes it prints every test class it knows, one full name per line, and runs nothing;
--classes <project folder>... limits the list to those test projects.

The selection follows from the names in the test files. A test class is a class with [TestClass] in a top-level .cs
file of a test project. A name in a comment does not count.

- A path that the names cannot reason about selects the whole unit suite: a build or toolchain file (SHARED_FILES, any
  .csproj, .props, .targets, or .slnx), a file under src/Generators/, whose source generators reach every project, and
  a .cs file of a test project that declares no test class.
- A .cs file selects every test class whose file names a type the file declares, as a whole word. The test file of a
  class names that class, so a changed test file selects its own class. A helper file of a test project or of a helper
  project (a folder named `Testing` or `*.Testing` under src/ or a folder of src/) that names such a type adds its own
  types to the names. A deleted .cs file stands for the type named as the file.
- A .cs file under src/ that selects no test class selects the whole unit suite, so a change no test names still gets a
  build. Files under src/Viewer/ are exempt: no test project compiles them, and they may select nothing.
- Any other file selects every test class whose file names its repository path or its file name. Where none does, the
  folders above it are tried, deepest first, each written as a path (`content/basic`) or as quoted segments
  (`"content", "basic"`). A Markdown (.md) file tries no folder.

Where nothing is selected, as for a prose change, it prints one line and exits 0 without a build: a filter that
matches no test makes the test host exit 8. A selected class whose tests all carry the category Install or Ghidra
leaves the filter without a test too, so the run passes --ignore-exit-code 8.

The selection follows only the names a test file holds. A test reached through a type that its file never names is
missed, so the full command stays the check before a merge.
"""

import glob
import os
import re
import subprocess
import sys

TEST_GLOBS = ("*.Tests", "tests/*.Tests", "src/*.Tests", "src/*/*.Tests")
HELPER_GLOBS = ("src/Testing", "src/*.Testing", "src/*/Testing", "src/*/*.Testing")
SOLUTION = "OpenSettlers.slnx"
CATEGORIES = "TestCategory!=Install&TestCategory!=Ghidra"
SHARED_FILES = {"Directory.Build.props", "Directory.Build.targets", "global.json", "mise.toml", ".editorconfig"}
SHARED_SUFFIXES = (".csproj", ".props", ".targets", ".slnx")
SHARED_FOLDERS = ("src/Generators/",)
COMPILED_FOLDER = "src/"
GODOT_FOLDER = "src/Viewer/"
USAGE = "usage: python3 -I tools/focused-tests.py [--list] <path>... | --classes"

DECLARATION = re.compile(r"\b(?:class|struct|interface|enum|record|delegate\s+\S+)\s+(?:class\s+|struct\s+)?([A-Za-z_]\w*)")
TEST_CLASS = re.compile(r"\[TestClass\][^{;]*?\bclass\s+([A-Za-z_]\w*)", re.S)
NAMESPACE = re.compile(r"^namespace\s+([\w.]+)\s*;", re.M)
# A string literal is matched before a comment, so a // inside a string starts none.
CODE = re.compile(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\\n])*"|//[^\n]*|/\*.*?\*/', re.S)

ALL = object()


def read(path):
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def folders(globs):
    """The folders the globs match, as repository paths in name order."""
    found = {path.replace(os.sep, "/") for pattern in globs for path in glob.glob(pattern) if os.path.isdir(path)}
    return sorted(found)


def test_projects():
    return folders(TEST_GLOBS)


def test_files():
    """Each top-level .cs file of a test project or a helper project: (repository path, text, test classes as
    (project folder, full name) pairs, which only a test project has)."""
    files = []
    tests = test_projects()
    for project in tests + folders(HELPER_GLOBS):
        tested = project in tests
        for name in sorted(os.listdir(project)):
            if not name.endswith(".cs"):
                continue
            path = project + "/" + name
            text = code(read(path))
            namespace = NAMESPACE.search(text)
            prefix = namespace.group(1) + "." if namespace else ""
            classes = [(project, prefix + found) for found in TEST_CLASS.findall(text)] if tested else []
            files.append((path, text, classes))
    return files


def changed_files(arguments):
    """The changed paths as repository paths."""
    root = os.path.realpath(os.getcwd())
    return [os.path.relpath(os.path.realpath(argument), root).replace(os.sep, "/") for argument in arguments]


def is_shared(path, test_classes):
    name = os.path.basename(path)
    if name in SHARED_FILES or name.endswith(SHARED_SUFFIXES) or path.startswith(SHARED_FOLDERS):
        return True
    in_tests = os.path.dirname(path) in test_projects()
    return in_tests and path.endswith(".cs") and not test_classes.get(path)


def word(name):
    return re.compile(r"\b" + re.escape(name) + r"\b")


def literal(text):
    return re.compile(re.escape(text))


def folder_patterns(folder):
    segments = folder.split("/")
    quoted = r'"\s*,\s*"'.join(re.escape(segment) for segment in segments)
    return [literal(folder + "/"), re.compile('"' + quoted + '"')] + ([literal(folder)] if len(segments) > 1 else [])


def code(text):
    """The text without its comments."""
    return CODE.sub(lambda found: "" if found.group(0).startswith("/") else found.group(0), text)


def declared(text):
    return set(DECLARATION.findall(code(text)))


def type_names(path, files):
    """The types a .cs file declares, and the types of the test project's helper files that name one of them."""
    names = declared(read(path)) if os.path.isfile(path) else {os.path.basename(path)[: -len(".cs")]}
    helpers = [(text, declared(text)) for _, text, classes in files if not classes]  # no test class, so a helper
    grown = True
    while grown:
        grown = False
        for text, types in helpers:
            if types - names and any(word(name).search(text) for name in names):
                names |= types
                grown = True
    return names


def matching(patterns, files):
    return {found for _, text, classes in files if any(p.search(text) for p in patterns) for found in classes}


def select(paths):
    files = test_files()
    test_classes = {path: classes for path, _, classes in files}
    selected = set()
    for path in paths:
        if is_shared(path, test_classes):
            return ALL
        if path.endswith(".cs"):
            found = matching([word(name) for name in type_names(path, files)], files)
            if not found and path.startswith(COMPILED_FOLDER) and not path.startswith(GODOT_FOLDER):
                return ALL
            selected |= found
            continue
        found = matching([literal(path), literal(os.path.basename(path))], files)
        folder = "" if path.endswith(".md") else os.path.dirname(path)
        while not found and folder:
            found = matching(folder_patterns(folder), files)
            folder = os.path.dirname(folder)
        selected |= found
    return selected


def run_commands(selected):
    """The dotnet commands that run the selection: one per test project holding a selected class, or the solution."""
    options = ["--ignore-exit-code", "8"]
    if selected is ALL:
        return [["dotnet", "test", "--solution", SOLUTION, "--filter", CATEGORIES, *options]]
    commands = []
    for project in sorted({project for project, _ in selected}):
        classes = "|".join("ClassName=" + name for found, name in sorted(selected) if found == project)
        commands.append(["dotnet", "test", "--project", project, "--filter", "(" + classes + ")&" + CATEGORIES, *options])
    return commands


def main(arguments):
    if arguments[:1] == ["--classes"]:
        projects = [path.rstrip("/").replace(os.sep, "/") for path in arguments[1:]]
        names = [
            name
            for _, _, classes in test_files()
            for project, name in classes
            if not projects or project in projects
        ]
        print("\n".join(sorted(names)))
        return 0
    listing = arguments[:1] == ["--list"]
    paths = changed_files(arguments[1:] if listing else arguments)
    if not paths:
        print(USAGE, file=sys.stderr)
        return 2
    selected = select(paths)
    if listing:
        print("all" if selected is ALL else "\n".join(sorted({name for _, name in selected})))
        return 0
    if not selected:
        print("focused-tests: no unit-test class names a changed path; nothing to run")
        return 0
    status = 0
    for command in run_commands(selected):
        status = status or subprocess.run(command).returncode
    return status


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
