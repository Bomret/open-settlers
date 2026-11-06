"""Runs the emulator harness EmulateMapStart.java on Siedler1.exe from the install. See README.md.

Usage: python3 -I emulate.py [--game <dir>] <command>...

Each command is one of the forms EmulateMapStart.java lists, for example rng:0000,0000,0000. Run from the
repository root. The install is the --game folder or SIEDLER_DIR, as for the game itself. The script imports
Siedler1.exe into a project in a temporary folder, without analysis, and removes the project afterwards: it
keeps no program bytes.

The command draw:WINDOWS:CX,CY:VERB:ARGS... (EmulateDraw.java lists its form) runs EmulateDraw.java instead. It runs
only on the reference build of Siedler1.exe (the History Edition, docs/rules/AGENTS.md, "Reference build") and
prints nothing, and exits non-zero, where the run fails, so a failed run leaves no recording. A successful run
prints the line "EMU draw install=..." that names the build, then the lines of the recording.
"""

import hashlib
import os
import shutil
import subprocess
import sys
import tempfile

SCRIPTS = os.path.join(os.getcwd(), "tools", "GhidraDump")

# The History Edition's Siedler1.exe (docs/rules/AGENTS.md, "Reference build"; ADR 0004).
REFERENCE_SHA256 = "0113d8b31268a5848e6827da32a970d7c6a0aab33ff28cc8f11a974b3679dbd5"


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
    drawing = any(command.startswith("draw:") for command in argv)
    if drawing and not all(command.startswith("draw:") for command in argv):
        fail("draw: runs alone: its recording is the whole output")
    headless = shutil.which("analyzeHeadless") or fail("analyzeHeadless is not on PATH; run through `mise run`")
    program = find_program(game)
    script = "EmulateDraw.java" if drawing else "EmulateMapStart.java"
    environment = dict(os.environ)
    environment["EMU_GAME"] = game
    install = None
    if drawing:
        with open(program, "rb") as file:
            data = file.read()
        digest = hashlib.sha256(data).hexdigest()
        if digest != REFERENCE_SHA256:
            fail(f"{program} is not the reference build of the History Edition (SHA-256 {digest})")
        install = f"EMU draw install=History Edition program=Siedler1.exe bytes={len(data)} sha256={digest}"
    with tempfile.TemporaryDirectory(prefix="emulate") as project:
        result = subprocess.run(
            [headless, project, "Emu", "-import", program, "-noanalysis", "-deleteProject",
             "-scriptPath", SCRIPTS, "-postScript", script, *argv],
            capture_output=True, text=True, env=environment,
        )
    marker = script + "> "
    lines = [line.split(marker, 1)[1].strip().removesuffix("(GhidraScript)").strip()
             for line in result.stdout.splitlines() if marker in line]
    if drawing:
        # A run counts only if it ends: a failed pass stops the script before its last line.
        if result.returncode != 0 or not any(line.startswith("EMU draw end") for line in lines):
            sys.stderr.write(result.stdout[-3000:] + result.stderr[-1000:])
            sys.exit(1)
        print(install)
    for line in lines:
        print(line)
    if not drawing and (result.returncode != 0 or not any(line.startswith("EMU ") for line in lines)):
        sys.stderr.write(result.stdout[-3000:] + result.stderr[-1000:])
        sys.exit(1)


if __name__ == "__main__":
    main(sys.argv[1:])
