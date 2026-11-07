# Windows checks: the map generator and the elapsed-units probe

The operator runs two checks of the original in the Windows VM:

- The map generator check. ADR 0008 asks for a run of the original in Windows under a debugger that
  confirms the emulator's values for at least one seed and every map size. Its results go in
  `tests/OpenSettlers.Tests/windows-run.txt`.
- The elapsed-units probe (`elapsed-probe.ps1`, "The elapsed-units probe" below). It tallies the clock
  units of the main loop's passes for the measurement in
  [Timing](../../docs/rules/timing.md#the-frame-wait).

Nothing the original wrote goes into the repository: dumps and the probe's summary stay outside it, and
only the short generator words and the digests are recorded (INV-0001).

## What you need

- An Intel or AMD Windows machine. On Windows on ARM the Ubisoft layer (`uplay_r1`, `UbiServices`) does not
  tolerate a debugger: launched under a debugger the process exits and the launcher restarts it outside, and
  attached the game UI freezes and later shuts down (operator run, 2026-11-02, WinDbg/cdb x86 in a Windows on
  ARM VM; module base `0x00a70000`, and breakpoints at the relocated addresses never hit).
- The History Edition `Siedler1.exe`, the one the emulator harness reads, and a debugger for 32-bit programs
  (x32dbg or WinDbg; the commands of both are listed below). The addresses below are those of the program's
  image at its link base `0x400000`. Check the module base in the debugger. If it differs, add the
  difference to every address below.
- Python 3 in the VM, or copy the dump out and run `digest.py` on the host.

## Addresses

| Address | What |
| --- | --- |
| `0x6740a2` | the game state block |
| `0x674290` | the map seed words, 3 words (`+0x1ee`); the free game masks the screen seed into them at `0x468b3d` |
| `0x674404` | the free-game size value, a word (`+0x362`); 1, 2, or 3 |
| `0x674264`, `0x674266` | the map width and height in tiles, words (`+0x1c2`, `+0x1c4`) |
| `0x6740c6` | the pointer to the map buffer, a dword (`+0x24`) |
| `0x6742b4`, `0x6742b6`, `0x6742b8` | the generator words, 3 words (`+0x212`, `+0x214`, `+0x216`) |
| `0x6740ee` | the resource type 1 total, a dword (`+0x4c`) |
| `0x4685bc` | `StartScreen_StartGame`; its size doubling ends at `0x468620` |
| `0x4216e9` | `MapGen_Generate`, entry |
| `0x421808` | the instruction after `MapGen_Generate`'s call of `MapGen_CountResource1`: the end of the last pass |

The emulator harness reads the generator words and the map buffer at `0x421808`, so the check reads them
there as well. `Minimap_Build` runs after that point and the game goes on to change the buffer, so a read
taken later does not compare.

## Debugger commands

| Step | x32dbg | WinDbg |
| --- | --- | --- |
| breakpoint at an address | `bp <address>` | `bp <address>` |
| continue | `run` (F9) | `g` |
| read 3 words | dump pane at the address | `dw <address> L3` |
| read a dword | dump pane at the address | `dd <address> L1` |
| write a word | edit in the dump pane | `ew <address> <value>` |
| save the map buffer | dump pane, Binary, Save to file | `.writemem <file> <address> L<length>` |

## Step 0: the live screen check (slice 01)

1. Start the game and open the free-game page.
2. Type `1234567812345678` as the seed and press Enter. The display shows the result only after Enter: before
   it the field echoes the typed text (`1234 5678 1234 5678`).
3. The page must show `2731 4582 2731 4582`. Record what it shows, whether or not it matches.

Done: the page showed `2731 4582 2731 4582` (Windows run, 2026-11-02, no debugger), as the emulator shows.

If it differs, report it as a finding with the screen text, and carry on with the steps below.

## The run, for each size value 1, 2, and 3

Do the run once for each size value. Seed A, the screen seed `1234567812345678`, is the required seed. A
run for each of seed B and seed C is optional, for more coverage.

1. In the debugger set a breakpoint at `0x4216e9` (`MapGen_Generate`) and another at `0x421808`.
2. Start the game and open the free-game page. Type the seed `1234567812345678` (seed A) and press Enter.
3. Write the size value N (1, 2, or 3) as a word at `0x674404`, for example in the dump pane of the debugger.
   The free-game page shows `MAPSIZE : 3` (operator run, 2026-11-02). This procedure has no recovered page
   control for the size, so the memory write is the way to pick the size under the debugger.
4. Start the game. The program stops at `0x4216e9`.
5. Read the three words at `0x674290`. For seed A they must be `6359 A0D2 B4FA`. Read the words at `0x674264`
   and `0x674266`: width and height must be `32 32` for N = 1, `64 32` for N = 2, and `64 64` for N = 3. A
   different value is a finding: name the size and stop that run.
   For seed B or C overwrite the three words at `0x674290` with the map seed words of the table below, and
   continue. The generator copies them at its start, as the emulator harness does.
6. Continue to the breakpoint at `0x421808`.
7. Read and write down:
   - the three generator words at `0x6742b4`, `0x6742b6`, `0x6742b8`, as 4 hex digits each (`W0,W1,W2`);
   - the dword at `0x6740ee`, as a decimal number (the total);
   - the dword at `0x6740c6`, the address of the map buffer.
8. Dump the map buffer: from the address of step 7, `height * 8 * width` bytes, that is 8192 bytes for N = 1,
   16384 for N = 2, and 32768 for N = 3. In x32dbg: go to the address in the dump pane, select the range
   (right-click, Binary, Edit or "Select" with the length), then right-click, Binary, Save to file. In WinDbg:
   `.writemem <file> <address> L<length>`.
   Keep the file outside the repository.
9. Compute the digests:

   ```
   python3 -I tools/WindowsCheck/digest.py <dump-file> N <seed words> <W0,W1,W2> <total>
   ```

   For example `python3 -I tools/WindowsCheck/digest.py map1.bin 1 6359,A0D2,B4FA 7590,F842,468D 476`. The
   script prints one row that starts `Windows run | `. It computes the FNV-1a 64 of each layer (flags,
   height, terrain, lake, object, resource) from the layout of `docs/rules/map-form.md`.
10. Compare the row with the expected row below. They must be equal.

## Expected rows

The rows are those of `tests/OpenSettlers.Tests/map-digests.txt` after the pass `CountResource1`. The
emulator and the engine agree on each. Each row holds: size, map seed, pass, the three generator words, the
digests of the layers flags, height, terrain, lake, object, resource, and the total.

Seed A (screen seed `1234567812345678`, map seed `6359,A0D2,B4FA`):

```
1 6359,A0D2,B4FA CountResource1 7590,F842,468D C4955AB79A5B76A5 F590D3FA53C8256C 9EFB835932603E54 FA43598ED0E932C1 887F8C4E2A0BBD62 FA43598ED0E932C1 total=476
2 6359,A0D2,B4FA CountResource1 D623,CD45,3DE8 BAD0A490BA6556E5 1794F6E46AB6AC3A 273A415F1704A6F0 E1BA44074D0D8731 FF965E44CB249146 E1BA44074D0D8731 total=88
3 6359,A0D2,B4FA CountResource1 A251,BD57,EE83 4F1CC358ED5178E5 264258BFF1B12D6A 9532022A17F7BADF DDC03A9A38E34F84 295B6BCC8377F484 DDC03A9A38E34F84 total=956
```

Seed B (map seed `1234,5678,9ABC`, optional):

```
1 1234,5678,9ABC CountResource1 DDEC,FD2A,75D1 56A7F8AA25F62CE5 C49BB77D644D3468 FC353C1663797F1D D35377DA4FDF6DA9 0D6146EDCFC93B19 D35377DA4FDF6DA9 total=0
2 1234,5678,9ABC CountResource1 983F,D09C,BEC9 D165CB21E1E59965 2D6083B23F71E88D 86799BFC4C5DC2EA F8F52FAF9A689D7B CAD42072BA5C4670 F8F52FAF9A689D7B total=712
3 1234,5678,9ABC CountResource1 D441,EE20,1791 F543870E11050EE5 B3D354A96D6F8259 7F353C6D853382FC AB8B3A05B4254649 0258B277633D5B6E AB8B3A05B4254649 total=1060
```

Seed C (map seed `FFFF,0001,8001`, optional):

```
1 FFFF,0001,8001 CountResource1 A201,E1F8,2A76 D094EE2D1B7A51E5 C57F4D4041F9B8DE 7D66AF4E14510B1D 750FA7E8B07F5F2C C77F4B4E025632FF 750FA7E8B07F5F2C total=0
2 FFFF,0001,8001 CountResource1 A762,3E9A,9676 AA405A00D051C7A5 97B52A5A953D5D9A 284621CE8ABB5C05 DDDDAF85AD0BC9F9 0816D8E5F315E4B7 DDDDAF85AD0BC9F9 total=64
3 FFFF,0001,8001 CountResource1 3B7B,9987,8150 0E8B3D39192A9365 DD02A7E93355A470 1EB9D75C42B0303D 311BE0E76D4778E6 7C0590BC2E350E8D 311BE0E76D4778E6 total=1644
```

The generator words after the seeding, before any pass, are `0186,6FFD,D2BA` for seed A, `3D2C,4FF3,6620`
for seed B, and `0003,E001,0001` for seed C. They are read at `0x421749`, the first entry of `END_ADDRESS`
(see Mismatch).

## Recording the results

Append each row `digest.py` printed, unedited, to `tests/OpenSettlers.Tests/windows-run.txt`, one row per
seed and size. The test `WindowsRunMatchesTheEmulator` compares each row with the emulator's row.

## A mismatch

A mismatch is a finding. Record neither a changed value nor an adjusted row. Report the size, the seed, the
value you read, the value expected, and the layer whose digest differs. To name the pass, set a breakpoint
at the end of each pass listed in `END_ADDRESS` of `tools/GhidraDump/EmulateMapStart.java` and read the
generator words and a dump there: the first pass whose words or digests differ from the `map-digests.txt` row
of that pass is the pass the finding names.

## What to send back

- The result of step 0: the text the free-game page shows after typing `1234567812345678`.
- For each size 1, 2, and 3 with seed A (and each optional run): the seed, the size, the width and height
  read at step 5, the map seed words read at step 5, the generator words, the total, and the row `digest.py`
  printed.
- For a mismatch: the details of the section above.
- Not the dump files and nothing else of the original's output.

## The elapsed-units probe

`elapsed-probe.ps1` polls the game state block of a running `Siedler1.exe` and tallies the elapsed clock
units of each main-loop pass, grouped by the game-speed factor. It reads the original's memory and sets no
breakpoint, so it needs no debugger.

### What you need

- A running game of the History Edition `Siedler1.exe` in the Windows VM. The probe waits for the process
  to start.
- Windows PowerShell or PowerShell 7, run as the same user as the game.

### How to run it

```
powershell -ExecutionPolicy Bypass -File tools\WindowsCheck\elapsed-probe.ps1 [-Seconds <n>] [-Out <path>]
```

In PowerShell 7, run the same command with `pwsh` in place of `powershell`.

`-Seconds` is how long the probe polls, 900 by default; it also stops when the game exits. `-Out` is the
summary file, `$env:TEMP\elapsed-summary.txt` by default. The probe rewrites the summary every 5 seconds and
once at the end. Keep the summary outside the repository, and send back only the counts of the summary.

### The fields it reads

The probe finds the EDI block at `0x674000` plus the difference between the module base and `0x400000`.
The fields are those of [the program index](../../docs/rules/program-index.md):

| Field | What the probe takes from it |
| --- | --- |
| EDI block `+0x34` | the pointer to the game state block |
| EDI block `+0x48` (`Speed_Factor`, `0x674048`) | the speed factor that groups the counts |
| game state `+0x1c8` bit 3 (`Game_Flags`) | a game runs; the probe tallies nothing while it is clear |
| game state `+0x1fe` (`Clock_Step`) | 0 marks a paused pass |
| game state `+0x26c`, `+0x286` (`Sim_Phase`, `Sim_PhaseCount`) | the phase steps between two polls |
| game state `+0x284` (`Clock_Elapsed`) | the elapsed units of the pass |
| game state `+0x28a` | the copy of `Clock_ElapsedTotal`, the units of the last phase cycle |

### The summary

The probe compares each poll with the previous one and counts only polls in which `Sim_Phase` changed.
The phase steps between two polls are the change of `Sim_Phase`, modulo `Sim_PhaseCount`.

- `paused passes`: polls that saw the phase change while the clock step was 0, at any speed.
- `elapsed units per pass`: for each value of `Clock_Elapsed`, the number of polls that saw the phase
  advance by exactly 1 with the clock running.
- `extra phase steps`: for each poll that saw the phase advance by more than 1 with the clock running, the
  steps beyond the first. A pass of phases 33 to 48 that ran several phases adds to it, and so does a pass
  the poller missed; the count does not tell the two apart.
- `cycle totals (phase count:units)`: for each poll that saw the phase wrap, the phase count and the value
  at `+0x28a`, with how often that pair occurred.
