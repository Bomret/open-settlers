"""Runs the emulator harness EmulateMapStart.java on Siedler1.exe from the install. See README.md.

Usage: python3 -I emulate.py [--game <dir>] [--project <dir>] <command>...

Each command is one of the forms EmulateMapStart.java lists, for example rng:0000,0000,0000. Run from the
repository root. The install is the --game folder or SIEDLER_DIR, as for the game itself. The script imports
Siedler1.exe into a project in a temporary folder, without analysis, and removes the project afterwards: it
keeps no program bytes.

With --project <dir> the project lives in that folder and stays: the first call imports Siedler1.exe into it, and later
calls run the script against the imported program instead of importing again. The folder records the SHA-256 of the
imported program, and a call for a program with another SHA-256 imports it again over the old project, replacing only Emu.gpr, Emu.rep, and program.sha256: other files in the
folder stay. A folder with the marker but no Emu.gpr is imported again. The caller removes the folder.

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

PROJECT_NAME = "Emu"
DIGEST_FILE = "program.sha256"

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


def imported(project, digest):
    """Whether the project folder holds an import of the program with this SHA-256; clears the files of another import."""
    marker = os.path.join(project, DIGEST_FILE)
    if os.path.isfile(os.path.join(project, PROJECT_NAME + ".gpr")) and os.path.isfile(marker):
        with open(marker) as file:
            if file.read().strip() == digest:
                return True
    for name in (PROJECT_NAME + ".gpr", PROJECT_NAME + ".rep", DIGEST_FILE):
        path = os.path.join(project, name)
        if os.path.isdir(path):
            shutil.rmtree(path)
        elif os.path.exists(path):
            os.remove(path)
    os.makedirs(project, exist_ok=True)
    return False


def run(headless, project, mode, script, argv, environment):
    return subprocess.run(
        [headless, project, PROJECT_NAME, *mode, "-scriptPath", SCRIPTS, "-postScript", script, *argv],
        capture_output=True, text=True, env=environment,
    )


def main(argv):
    game = os.environ.get("SIEDLER_DIR")
    if argv[:1] == ["--game"]:
        game, argv = argv[1], argv[2:]
    shared = None
    if argv[:1] == ["--project"]:
        shared, argv = argv[1], argv[2:]
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
    environment = {**os.environ, "EMU_GAME": game}
    with open(program, "rb") as file:
        data = file.read()
    digest = hashlib.sha256(data).hexdigest()
    if drawing:
        if digest != REFERENCE_SHA256:
            fail(f"{program} is not the reference build of the History Edition (SHA-256 {digest})")
        install = f"EMU draw install=History Edition program=Siedler1.exe bytes={len(data)} sha256={digest}"
    if shared is None:
        with tempfile.TemporaryDirectory(prefix="emulate") as project:
            result = run(headless, project, ["-import", program, "-noanalysis", "-deleteProject"], script, argv, environment)
    else:
        if imported(shared, digest):
            result = run(headless, shared, ["-process", os.path.basename(program), "-noanalysis"], script, argv, environment)
        else:
            result = run(headless, shared, ["-import", program, "-noanalysis"], script, argv, environment)
            # The import precedes the script, so a run whose script fails still leaves the program imported.
            if "REPORT: Import succeeded" in result.stdout:
                with open(os.path.join(shared, DIGEST_FILE), "w") as file:
                    file.write(digest)
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
