"""Runs Ghidra headless on the local project of Siedler1.exe. See README.md in this folder.

Usage: python3 -I headless.py natives | import | update | dump [index-only]

Run from the root of the repository or of one of its git worktrees. The Ghidra project and the dump lie in
re-local/ of the main checkout, which every worktree shares; the scripts and the program index come from the
current tree. Needs analyzeHeadless on PATH (mise puts it there) and, for import, SIEDLER_DIR.
"""

import os
import shutil
import subprocess
import sys

ROOT = os.getcwd()


def main_checkout():
    """The main checkout: the parent of git's common dir. The current folder outside git or without git."""
    try:
        found = subprocess.run(
            ["git", "rev-parse", "--path-format=absolute", "--git-common-dir"],
            capture_output=True, text=True,
        )
    except OSError:
        return ROOT
    common = found.stdout.strip()
    if found.returncode != 0 or not common:
        return ROOT
    return os.path.realpath(os.path.dirname(common))


MAIN = main_checkout()
PROJECT = os.path.join(MAIN, "re-local", "ghidra")
DUMP = os.path.join(MAIN, "re-local", "dump")
SCRIPTS = os.path.join(ROOT, "tools", "GhidraDump")
INDEX = os.path.join(ROOT, "docs", "rules", "program-index.md")
APPLY_INDEX = ["-postScript", "CreateMissingFunctions.java", "-postScript", "ApplyIndexNames.java", INDEX]


def fail(message):
    sys.exit(f"headless.py: {message}")


def headless():
    path = shutil.which("analyzeHeadless")
    if path is None:
        fail("analyzeHeadless is not on PATH; run `mise install` and run this through `mise run`")
    return path


def refuse_locked():
    """Stops before a folder is touched while a run holds the project. Ghidra keeps Siedler1.lock beside Siedler1.gpr
    while a run has the project open (ProjectLocator.getProjectLockFile) and deletes it when the run ends."""
    lock = os.path.join(PROJECT, "Siedler1.lock")
    if os.path.exists(lock):
        fail(f"the Ghidra project is open in another run ({lock} exists); wait for it to end, "
             "or delete the lock when no Ghidra has the project open")


def run(args, cwd=None):
    sys.exit(subprocess.call(args, cwd=cwd))


def analyze(tool, *args):
    run([tool, PROJECT, "Siedler1", *args, "-scriptPath", SCRIPTS])


def main(argv):
    if not argv:
        fail(__doc__.strip().splitlines()[2])
    mode, rest = argv[0], argv[1:]
    if mode == "natives":
        support = os.path.dirname(headless())
        gradle = shutil.which("gradle") or fail("gradle is not on PATH")
        run([gradle, "buildNatives"], cwd=os.path.join(support, "gradle"))
    elif mode == "import":
        game = os.environ.get("SIEDLER_DIR") or fail("set SIEDLER_DIR in mise.local.toml")
        tool = headless()
        refuse_locked()
        os.makedirs(PROJECT, exist_ok=True)
        analyze(tool, "-import", os.path.join(game, "Siedler1.exe"), "-overwrite", *APPLY_INDEX)
    elif mode == "update":
        analyze(headless(), "-process", "Siedler1.exe", "-noanalysis", *APPLY_INDEX)
    elif mode == "dump":
        index_only = rest == ["index-only"]
        if rest and not index_only:
            fail(f"unknown dump mode: {' '.join(rest)}")
        tool = headless()
        refuse_locked()
        if not index_only:
            shutil.rmtree(DUMP, ignore_errors=True)
        os.makedirs(DUMP, exist_ok=True)
        extra = ["index-only"] if index_only else []
        analyze(
            tool, "-process", "Siedler1.exe", "-noanalysis", "-readOnly",
            "-postScript", "DumpProgram.java", DUMP, *extra,
            "-postScript", "StateFields.java", DUMP,
        )
    else:
        fail(f"unknown mode: {mode}")


if __name__ == "__main__":
    main(sys.argv[1:])
