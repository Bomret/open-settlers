"""Runs the emulator harness EmulateMapStart.java on Siedler1.exe from the install. See README.md.

Usage: python3 -I emulate.py [--game <dir>] <command>...

Each command is one of the forms EmulateMapStart.java lists, for example rng:0000,0000,0000. Run from the
repository root. The install is the --game folder or SIEDLER_DIR, as for the game itself. The script imports
Siedler1.exe into a project in a temporary folder, without analysis, and removes the project afterwards: it
keeps no program bytes.
"""

import os
import shutil
import subprocess
import sys
import tempfile

SCRIPTS = os.path.join(os.getcwd(), "tools", "GhidraDump")


def fail(message):
    sys.exit(f"emulate.py: {message}")


def find_program(root):
    """Finds Siedler1.exe in the install folder, ignoring case as Install.Find does."""
    for name in os.listdir(root):
        if name.lower() == "siedler1.exe":
            return os.path.join(root, name)
    fail(f"no Siedler1.exe in {root}: pass --game <dir> or set SIEDLER_DIR")


def main(argv):
    game = os.environ.get("SIEDLER_DIR")
    if argv[:1] == ["--game"]:
        game, argv = argv[1], argv[2:]
    if not game or not os.path.isdir(game):
        fail("no install folder: pass --game <dir> or set SIEDLER_DIR")
    if not argv:
        fail(__doc__.strip().splitlines()[2])
    headless = shutil.which("analyzeHeadless") or fail("analyzeHeadless is not on PATH; run through `mise run`")
    program = find_program(game)
    with tempfile.TemporaryDirectory(prefix="emulate") as project:
        result = subprocess.run(
            [headless, project, "Emu", "-import", program, "-noanalysis", "-deleteProject",
             "-scriptPath", SCRIPTS, "-postScript", "EmulateMapStart.java", *argv],
            capture_output=True, text=True,
        )
    lines = [line.split("> ", 1)[1].strip().removesuffix("(GhidraScript)").strip()
             for line in result.stdout.splitlines() if "EmulateMapStart.java> " in line]
    for line in lines:
        print(line)
    if result.returncode != 0 or not any(line.startswith("EMU ") for line in lines):
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-1000:])
        sys.exit(1)


if __name__ == "__main__":
    main(sys.argv[1:])
