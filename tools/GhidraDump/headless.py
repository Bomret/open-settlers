"""Runs Ghidra headless on the local project of Siedler1.exe. See README.md in this folder.

Usage: python3 -I headless.py natives | import | update | dump [index-only]

Run from the repository root. Needs analyzeHeadless on PATH (mise puts it there) and, for import,
SIEDLER_DIR.
"""

import os
import shutil
import subprocess
import sys

ROOT = os.getcwd()
PROJECT = os.path.join(ROOT, "re-local", "ghidra")
DUMP = os.path.join(ROOT, "re-local", "dump")
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
        os.makedirs(PROJECT, exist_ok=True)
        analyze(tool, "-import", os.path.join(game, "Siedler1.exe"), "-overwrite", *APPLY_INDEX)
    elif mode == "update":
        analyze(headless(), "-process", "Siedler1.exe", "-noanalysis", *APPLY_INDEX)
    elif mode == "dump":
        index_only = rest == ["index-only"]
        if rest and not index_only:
            fail(f"unknown dump mode: {' '.join(rest)}")
        tool = headless()
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
