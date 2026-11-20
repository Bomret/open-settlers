"""Runs the emulator harness EmulateMapStart.java on Siedler1.exe from the install. See README.md.

Usage: python3 -I emulate.py [--game <dir>] [--project <dir>] [--timeout <seconds>] <command>...

Each command is one of the forms EmulateMapStart.java lists, for example rng:0000,0000,0000. The commands of one call
run in one Ghidra launch, in argument order. They are independent: each prints the lines it prints in a call of its own
(INV-0018). The call exits 0 only where every command ended. EmulateMapStart.java prints "EMU command=N ended" after
the command N returns, and the script leaves these lines out of its output. A command that prints no line still ends. A
command does not end where it throws, where Ghidra stops before it, where the import fails, or where Ghidra exits
non-zero. A failed call prints nothing on stdout and exits non-zero. It writes the first command that did not end to
stderr, with its number and its text, and the tail of Ghidra's output. A draw batch is the exception, as below. The
script finds EmulateMapStart.java and EmulateDraw.java beside itself, from any folder. The install is the --game folder
or SIEDLER_DIR, as for the game itself. The script imports Siedler1.exe into a project in a temporary folder, without
analysis, and removes the project afterwards: it keeps no program bytes.

Each command runs under a bound, --timeout seconds, 600 by default: a number above 0 and at most Python's
threading.TIMEOUT_MAX, in any order among the other flags. The clock covers one step at a time and restarts at each:
Ghidra's start and the import, up to Ghidra's line "INFO  SCRIPT: ", then each command, up to its end line. A step that
runs past the bound, whether or not Ghidra prints meanwhile, fails the call: the script ends every process of the
launch, prints nothing on stdout, exits non-zero, and writes to stderr the import or the first command that did not end,
with its number and its text, the bound, and the tail of Ghidra's output. A draw batch is the exception on stdout, as
below. A call that Ctrl-C, SIGTERM, or SIGHUP stops while Ghidra runs ends the launch the same way; a call started with
SIGHUP ignored, as under nohup, keeps ignoring it. The temporary project folder goes in every case.

With --project <dir> the project lives in that folder and stays: the first call imports Siedler1.exe into it, and later
calls run the script against the imported program instead of importing again. The folder records the SHA-256 of the
imported program, and a call for a program with another SHA-256 imports it again over the old project, replacing
only Emu.gpr, Emu.rep, and program.sha256: other files in the folder stay. A folder with the marker but no Emu.gpr is
imported again. The caller removes the folder.

The command draw:WINDOWS:CX,CY:VERB:ARGS... (EmulateDraw.java lists its form) runs EmulateDraw.java instead. It runs
only on the reference build of Siedler1.exe (the History Edition, docs/rules/AGENTS.md, "Reference build") and
prints nothing, and exits non-zero, where the run fails, so a failed run leaves no recording. A successful run
prints the line "EMU draw install=..." that names the build, then the lines of the recording.

Several draw commands run as a batch in one Ghidra launch, each in a new script instance. A batch on another build prints
nothing and exits non-zero. Otherwise it prints the install line, then for each command, in argument order, the line
"EMU draw command=N ended" followed by its recording, or "EMU draw command=N failed" with no lines, N counting from 1. A
command fails where its run does not end, and where the import fails or Ghidra stops before it. A batch exits non-zero
where any command fails. Each draw command runs in a script run of its own, so Ghidra's "INFO  SCRIPT: " line that opens
it restarts the bound's clock, and the command ends at its line "EMU draw end", which restarts it too. Where a draw
command runs past the bound, it and each later command fail; stderr names it and the bound as above.
"""

import hashlib
import math
import os
import queue
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import time

PROJECT_NAME = "Emu"
DIGEST_FILE = "program.sha256"

# The per-command bound in seconds where the call passes no --timeout.
DEFAULT_TIMEOUT = 600

# Ghidra opens each script run with this line: the import before it has ended.
SCRIPT_OPENS = "INFO  SCRIPT: "

SCRIPTS = os.path.dirname(os.path.abspath(__file__))

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
    """Whether the project folder holds an import of the program with this SHA-256."""
    marker = os.path.join(project, DIGEST_FILE)
    if not (os.path.isfile(os.path.join(project, PROJECT_NAME + ".gpr")) and os.path.isfile(marker)):
        return False
    with open(marker) as file:
        return file.read().strip() == digest


def clear(project):
    """Removes the files of an earlier import: headless -import refuses a program the project already holds."""
    for name in (PROJECT_NAME + ".gpr", PROJECT_NAME + ".rep", DIGEST_FILE):
        path = os.path.join(project, name)
        if os.path.isdir(path):
            shutil.rmtree(path)
        elif os.path.exists(path):
            os.remove(path)
    os.makedirs(project, exist_ok=True)


def remove(folder):
    """Removes a folder. On Windows a file of a process that just ended can stay locked for a moment."""
    for attempt in range(20):
        try:
            shutil.rmtree(folder)
            return
        except FileNotFoundError:
            return
        except OSError:
            if attempt == 19:
                raise
            time.sleep(0.25)


def pump(stream, lines, events):
    """Reads a pipe of Ghidra to its end, so a full pipe never stalls Ghidra, and hands each line to the events."""
    for line in stream:
        lines.append(line)
        events.put(line)
    events.put(None)


def script_text(line, marker):
    """The text a script printed on a line of Ghidra's log, or None where the line is not the script's."""
    if marker not in line:
        return None
    return line.split(marker, 1)[1].strip().removesuffix("(GhidraScript)").strip()


def end(process):
    """Ends the process tree of the launch and returns once it is gone: on macOS and Linux the process group the launch
    runs in (the mise shim, analyzeHeadless, launch.sh, and java), on Windows the tree under analyzeHeadless.bat."""
    if os.name == "nt":
        subprocess.run(["taskkill", "/T", "/F", "/PID", str(process.pid)], capture_output=True)
        process.wait()
        return
    # macOS answers EPERM for a group whose members have all ended but are not yet reaped.
    try:
        os.killpg(process.pid, signal.SIGKILL)
    except (ProcessLookupError, PermissionError):
        pass
    process.wait()
    # The group outlives its leader until the system has reaped every member the kill ended.
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        try:
            os.killpg(process.pid, 0)
        except (ProcessLookupError, PermissionError):
            return
        time.sleep(0.05)


def run(headless, project, mode, script, argv, environment, bound):
    """Runs Ghidra and reads its output while it runs. The clock covers one step at a time: Ghidra's start and the
    import up to the first "INFO  SCRIPT: " line, then each command up to its end line; each such line restarts it.
    Returns the completed run and whether a step ran past the bound, in which case the launch was ended."""
    # Each draw command gets its own -postScript, so each runs in a new script instance.
    if script == "EmulateDraw.java":
        scripts = [part for command in argv for part in ("-postScript", script, command)]
    else:
        scripts = ["-postScript", script, *argv]
    command = [headless, project, PROJECT_NAME, *mode, "-scriptPath", SCRIPTS, *scripts]
    marker = script + "> "
    ends = {f"EMU command={index} ended" for index in range(1, len(argv) + 1)}
    # A process group of its own, so that end() reaches every process of the launch and nothing else.
    if os.name == "nt":
        group = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP}
    else:
        group = {"start_new_session": True}
    process = subprocess.Popen(
        command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, env=environment, **group
    )
    finished = False
    try:
        stdout, stderr, events = [], [], queue.Queue()
        readers = [
            threading.Thread(target=pump, args=(process.stdout, stdout, events), daemon=True),
            # simplicity: no one reads stderr's queue, so it holds a second copy of Ghidra's stderr, a few lines, until
            # the call ends. Where Ghidra's stderr grows large, a reader that takes no queue is the upgrade.
            threading.Thread(target=pump, args=(process.stderr, stderr, queue.Queue()), daemon=True),
        ]
        for reader in readers:
            reader.start()
        deadline = time.monotonic() + bound
        expired = False
        while True:
            try:
                line = events.get(timeout=max(0.0, deadline - time.monotonic()))
            except queue.Empty:
                expired = True
                break
            if line is None:
                break
            text = script_text(line, marker)
            if line.startswith(SCRIPT_OPENS) or (
                text is not None and (text in ends or text.startswith("EMU draw end"))
            ):
                deadline = time.monotonic() + bound
        if not expired:
            try:
                process.wait(timeout=max(0.0, deadline - time.monotonic()))
            except subprocess.TimeoutExpired:
                expired = True
        if expired:
            end(process)
        for reader in readers:
            reader.join(10)
        finished = True
    finally:
        # Ctrl-C, SIGTERM, SIGHUP, or an exception while Ghidra runs.
        if not finished:
            end(process)
    return subprocess.CompletedProcess(command, process.returncode, "".join(stdout), "".join(stderr)), expired


def script_runs(stdout, marker):
    """The script lines of each script run, in order. Ghidra opens each script run with an "INFO  SCRIPT: " line."""
    runs = []
    for line in stdout.splitlines():
        if line.startswith(SCRIPT_OPENS):
            runs.append([])
        elif marker in line and runs:
            runs[-1].append(script_text(line, marker))
    return runs


def overrun(argv, result, marker, bound, drawing):
    """The stderr line that names the step that ran past the bound: the import where no script run opened, otherwise the
    first command that did not end. A draw command ends at its "EMU draw end" line, in the script run of its own."""
    lines = result.stdout.splitlines()
    texts = [script_text(line, marker) for line in lines if marker in line]
    if drawing:
        runs = script_runs(result.stdout, marker)
        ended = [index < len(runs) and any(line.startswith("EMU draw end") for line in runs[index])
                 for index in range(len(argv))]
    else:
        ended = [f"EMU command={index + 1} ended" in texts for index in range(len(argv))]
    if texts or any(line.startswith(SCRIPT_OPENS) for line in lines):
        hung = next((index for index, done in enumerate(ended) if not done), None)
        what = "Ghidra, after the last command ended," if hung is None else f"command {hung + 1} ({argv[hung]})"
    else:
        what = "the import"
    return f"emulate.py: {what} ran past the bound of {bound:g} s\n"


def batch(commands, result, marker, install, bounded):
    """Prints the result of each draw command of a batch under its own line, in argument order, and exits non-zero where one
    fails. A command whose script run is missing, as after a failed import, a crash, or a bound, failed. bounded is the
    stderr line of a bound, or empty."""
    runs = script_runs(result.stdout, marker)
    print(install)
    failed = False
    for index in range(len(commands)):
        lines = runs[index] if index < len(runs) else []
        # A command counts only if it ends: a failed pass stops its script before its last line.
        if any(line.startswith("EMU draw end") for line in lines):
            print(f"EMU draw command={index + 1} ended")
            for line in lines:
                print(line)
        else:
            print(f"EMU draw command={index + 1} failed")
            failed = True
    if failed or result.returncode != 0:
        sys.stderr.write(bounded + result.stdout[-3000:] + result.stderr[-1000:])
        sys.exit(1)


def main(argv):
    game = os.environ.get("SIEDLER_DIR")
    shared = None
    bound = DEFAULT_TIMEOUT
    while argv[:1] in (["--game"], ["--project"], ["--timeout"]):
        if argv[0] == "--timeout":
            try:
                bound = float(argv[1])
            except (IndexError, ValueError):
                bound = math.nan
            # The waits on Ghidra take no timeout above threading.TIMEOUT_MAX. NaN fails both comparisons.
            if not 0 < bound <= threading.TIMEOUT_MAX:
                fail(
                    f"--timeout needs a number of seconds above 0 and at most {threading.TIMEOUT_MAX:g}. "
                    f"{__doc__.strip().splitlines()[2]}"
                )
        elif len(argv) < 2 or not argv[1]:
            fail(f"{argv[0]} needs a folder. {__doc__.strip().splitlines()[2]}")
        elif argv[0] == "--game":
            game = argv[1]
        else:
            shared = argv[1]
        argv = argv[2:]
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
    if drawing or shared is not None:
        with open(program, "rb") as file:
            data = file.read()
        digest = hashlib.sha256(data).hexdigest()
    if drawing:
        if digest != REFERENCE_SHA256:
            fail(f"{program} is not the reference build of the History Edition (SHA-256 {digest})")
        install = f"EMU draw install=History Edition program=Siedler1.exe bytes={len(data)} sha256={digest}"
    if shared is None:
        project = tempfile.mkdtemp(prefix="emulate")
        try:
            result, expired = run(
                headless, project, ["-import", program, "-noanalysis", "-deleteProject"], script, argv, environment, bound
            )
        finally:
            remove(project)
    else:
        if imported(shared, digest):
            result, expired = run(
                headless, shared, ["-process", os.path.basename(program), "-noanalysis"], script, argv, environment, bound
            )
        else:
            clear(shared)
            result, expired = run(headless, shared, ["-import", program, "-noanalysis"], script, argv, environment, bound)
            # The import precedes the script, so a run whose script fails still leaves the program imported.
            if "REPORT: Import succeeded" in result.stdout:
                with open(os.path.join(shared, DIGEST_FILE), "w") as file:
                    file.write(digest)
    marker = script + "> "
    bounded = overrun(argv, result, marker, bound, drawing) if expired else ""
    if bounded and not drawing:
        sys.stderr.write(bounded + result.stdout[-3000:] + result.stderr[-1000:])
        sys.exit(1)
    if drawing and len(argv) > 1:
        batch(argv, result, marker, install, bounded)
        return
    lines = [script_text(line, marker) for line in result.stdout.splitlines() if marker in line]
    if drawing:
        # A run counts only if it ends: a failed pass stops the script before its last line.
        if result.returncode != 0 or not any(line.startswith("EMU draw end") for line in lines):
            sys.stderr.write(bounded + result.stdout[-3000:] + result.stderr[-1000:])
            sys.exit(1)
        print(install)
    else:
        # A command ended where the script printed its end line. Where Ghidra exits non-zero, no command counts as ended.
        ends = [f"EMU command={index} ended" for index in range(1, len(argv) + 1)]
        ended = set(lines) if result.returncode == 0 else set()
        failed = next((index for index, end in enumerate(ends) if end not in ended), None)
        if failed is not None:
            sys.stderr.write(f"emulate.py: command {failed + 1} ({argv[failed]}) did not end\n")
            sys.stderr.write(result.stdout[-3000:] + result.stderr[-1000:])
            sys.exit(1)
        lines = [line for line in lines if line not in ends]
    for line in lines:
        print(line)


if __name__ == "__main__":
    # SIGTERM and SIGHUP unwind the call as Ctrl-C does, so the launch ends and the temporary project folder goes. Ghidra
    # runs in a session of its own, so a closed terminal hangs up only this process. SIGHUP exists only on POSIX, and a
    # call started with it ignored, as under nohup, keeps ignoring it.
    signal.signal(signal.SIGTERM, lambda number, frame: sys.exit("emulate.py: terminated"))
    if hasattr(signal, "SIGHUP") and signal.getsignal(signal.SIGHUP) != signal.SIG_IGN:
        signal.signal(signal.SIGHUP, lambda number, frame: sys.exit("emulate.py: hung up"))
    try:
        main(sys.argv[1:])
    except KeyboardInterrupt:
        sys.exit("emulate.py: interrupted")
