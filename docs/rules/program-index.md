# Program index

This index records where routines and data lie in `Siedler1.exe`. Read it before you open the program in
REA. It names places and states their role in one line. The rules themselves are in the rule notes that
it links to.

- Build and address notation: [Rule notes](AGENTS.md#reference-build).
- The game code band and the platform layer: [Game code](game-code.md).
- The local dump for searching the whole program: [Ghidra dump](../../tools/GhidraDump/README.md).

The index holds addresses and our own words only ([INV-0001](../invariants.md#inv-0001)). Decompiler
output, REA databases, snapshots, and evidence bundles stay on the local machine.

## Conventions

A routine row has these fields:

- **Address:** the entry of the routine.
- **Name:** our name, in the form `Subsystem_Verb`. Data names use the form `Subsystem_Noun`. REA takes
  the routine names from this index (see [Rule notes](AGENTS.md#program-index)).
- **Role:** one line.
- **Status:** **confirmed** when the code was read and shows the role, **inferred** when the role
  follows from callers, strings, or names but the code was not read, **unknown** when the role is open.
- **Layer:** **game** inside the game code band, **platform** outside it.
- **Anchor:** the string, save field, constant, import, or caller that leads to the routine.

A site is an address inside a routine where a specific thing happens. Topics list sites. The address
table lists only routines and data.

The game code passes a register and state block in `EDI` (see [Game code](game-code.md)). This index
calls it the EDI block. The offset `+0x34` of the EDI block holds a pointer to the game state block.

## By topic

### Timing

Note: [Timing](timing.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x405fe0` | `Speed_SetFactor` | Stores the speed factor and sets the timer rate to match. | confirmed | platform | Writes `Speed_Factor` and `Timer_RateHz`. |
| `0x406c90` | `Timer_ThreadMain` | Timer thread; calls `Timer_GameEntry` at the timer rate. | confirmed | platform | Reads `Timer_RateHz`. |
| `0x479cb2` | `Timer_GameEntry` | The game code's timer entry point; calls `Timer_Handler`. | confirmed | game | Called by `Timer_ThreadMain`. |
| `0x479cc0` | `Timer_Handler` | Counts the call, adds the clock step to the game clock, runs pointer input. | confirmed | game | Called by `Timer_GameEntry`. |
| `0x41e710` | `Game_StartClock` | Sets the clock step to 2.0 when a game starts. | confirmed | game | Writer of the clock step. |
| `0x425a10` | `Main_Loop` | The main loop. | confirmed | game | Calls `Main_ComputeElapsed`. |
| `0x426abe` | `Main_HandleKeys` | Handles the current key code; calls the pause on `P`. | inferred | game | Called by `Main_Loop`; holds the pause site `0x426adf`. |
| `0x426dfc` | `Main_ComputeElapsed` | Computes the whole clock units elapsed since the previous pass. | confirmed | game | Called by `Main_Loop`. |
| `0x425c9c` | `Timer_WaitNextCall` | Resets the interrupt counter and waits for the next timer call. | confirmed | game | No known caller. |
| `0x45772e` | `Game_Pause` | Sets the clock step to 0. | confirmed | game | Called by `Main_HandleKeys`. |
| `0x457751` | `Game_Resume` | Restores the clock step. | confirmed | game | Called by `Main_HandleKeys`. |

| Address | Name | Role | Status | Layer |
| --- | --- | --- | --- | --- |
| `0x67d608` | `Timer_RateHz` | Timer calls per second; 50 at program start. | confirmed | platform |
| `0x674048` | `Speed_Factor` | Game-speed factor: 1, 2, 4, or 8, plus two values that make the timer run without waiting. | confirmed | platform |

The game clock is a field of the game state block (see "Game state block").

Sites:

- `0x4168c7`, `0x4168d5`: set the two speed-factor values that make the timer thread run without waiting.
- `0x426adf`: the main loop's key handling calls the pause on the `P` key code.
- `0x425a44`: the main loop subtracts the elapsed units from a 6000-unit countdown.

### Input

Note: [Timing](timing.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x47a4c3` | `Input_UpdatePointer` | Updates one player's pointer; timing thresholds scale with `Speed_Factor`. Changed by the History Edition. | inferred | game | Called twice by `Timer_Handler`. |

### Random generator and map start

Note: [Map start](map-start.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x466ade` | `Rng_Step` | Advances the random generator by one step. | confirmed | game | Reads the generator state at state `+0x212` to `+0x216`. |
| `0x4216e9` | `MapGen_Generate` | Seeds the generator from the map seed and runs the generation passes. | confirmed | game | Called from the game start at `0x41eeb9`. |
| `0x41ee3b` | `FreeGame_NewSeed` | Draws a new free-game seed when the free-game screen opens. | confirmed | game | Calls `Rng_Step`. |

Sites:

- `0x4425a1`, `0x47a374`: the generator step, inline.
- `0x467831`: the seed is shown as four 12-bit groups.
- `0x467ee1`: edited groups are written back into the seed.
- `0x468b3d`: a free game masks the seed words into the map seed.
- `0x4688f4`: a mission takes the map seed from its record.
- `0x41eeb9`: the game start calls `MapGen_Generate`.

### Save and load

The save and load code of the game calls the platform's file routines. The save game format is in "Save
format".

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x45f755` | `Save_WriteArchiveIndex` | Creates `ARCHIV.DS` in the `[SP]` folder and writes 160 bytes from state `+0xd8` into it. | confirmed | game | String `[SP]/ARCHIV.DS`. |
| `0x45fa13` | `Save_WriteGame` | Builds the first record of a save game in the save buffer and writes it. | confirmed | game | Save fields at `0x45fbf8` and `0x45fc29`. |
| `0x4600a8`, `0x46010d`, `0x4601f4`, `0x4602db`, `0x4603c2` | — | Write records 2 to 6 of a save game, in this order, through `Save_WriteRecord`. | inferred | game | Called in sequence from `0x4507b4` to `0x4507d0`. |
| `0x46049a` | `Save_WriteRecord` | Writes the save buffer to the open file; the EDI block gives the length. | confirmed | game | Calls `File_Write`. |
| `0x45f7d1` | `Load_LoadSlot` | Opens the save game file of the current slot and runs the six load routines in order. Entry inferred from the return before it. | inferred | game | Calls `File_Open` and `Load_ReadGame`. |
| `0x4604ed` | `Load_ReadGame` | Reads the first record of a save game and copies its fields back into the game state. | confirmed | game | Load fields at `0x4606bc` and `0x4606ee`. |
| `0x460bba`, `0x460c28`, `0x460d5a`, `0x460e8c`, `0x460fbe` | — | Read records 2 to 6 of a save game, in this order, through `Load_ReadRecord`. | inferred | game | Called in sequence from `0x45f83c` to `0x45f868`. |
| `0x4610e3` | `Load_ReadRecord` | Reads from the open file into the save buffer. | inferred | game | Calls `File_Read`. |
| `0x461136` | `Mem_CopyBytes` | Copies a count of bytes between two pointers in the EDI block. | confirmed | game | Called by every save and load routine. |
| `0x407750` | `File_Create` | Opens a file for writing. | inferred | platform | Imports `CreateFileW`; called by the save code. |
| `0x4076d0` | `File_Open` | Opens a file for reading. | inferred | platform | Imports `CreateFileW`; called by `Load_LoadSlot`. |
| `0x407800` | `File_Write` | Writes to the open file. | confirmed | platform | Imports `WriteFile`. |
| `0x407870` | `File_Read` | Reads from the open file. | confirmed | platform | Imports `ReadFile`. |
| `0x4077e0` | `File_Close` | Closes the open file. | confirmed | platform | Imports `CloseHandle`. |

Sites:

- `0x45fbf8`: save writes the game clock. `0x45fc29`: save writes the generator state.
- `0x4606bc`: load reads the game clock. `0x4606ee`: load reads the generator state.
- `0x45074c`: the save code calls `Save_WriteArchiveIndex`. `0x450769`: it builds the save file name.
  `0x45078d`: it calls `File_Create`. `0x4507ad` to `0x4507d0`: it calls the six save routines in
  order. Ghidra assigns this code to no function, and its entry is unknown.

### Files and configuration

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x401090` | — | References the `[SP]` folder prefix. | unknown | platform | String `[SP]`. |
| `0x406fb0` | `Sound_LoadFile` | Opens a numbered sound file in `snd/` with the C runtime's file calls. | inferred | platform | String `snd/%02d.SND`. |

| Address | Name | Role | Status | Layer |
| --- | --- | --- | --- | --- |
| `0x67d614` | `Loca_FileTable` | Table of the language files in `loca/`: an archive and a text file for each of English, German, and French. | inferred | platform |

Sites:

- `0x4015ac`: references the string `game.ini`.
- `0x40fc40`: references the registry key `Control Panel\Mouse`.

### Game code band

Note: [Game code](game-code.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x41da10` | — | First routine of the game code band. | unknown | game | Band comparison. |
| `0x47b4f9` | — | Last routine of the game code band. | unknown | game | Band comparison. |

## Structures and data

### Game state block

The EDI block holds a pointer to the game state block at `+0x34`. Offsets below are offsets into the game
state block.

| Offset | Name | Size | Role | Status |
| --- | --- | --- | --- | --- |
| `+0x64`, `+0x68`, `+0x6c`, `+0x70` | `State_SaveBlocks` | 4 bytes each | Pointers to the four areas that a save game carries in full. The save copies each from 128 bytes before the pointer. | confirmed |
| `+0x74` | `State_SaveBuffer` | 4 bytes | Pointer to the save buffer. | confirmed |
| `+0xd8` | `State_ArchiveIndex` | 160 bytes | The content of `ARCHIV.DS`. | confirmed |
| `+0x1f8` | `Save_Slot` | 2 bytes | The save slot. Save and load put its digit into the save file name. | confirmed |
| `+0x202` | `Clock_Game` | 4 bytes | The game clock: fixed point, 16 fraction bits. | confirmed |
| `+0x212`, `+0x214`, `+0x216` | `Rng_State` | 2 bytes each | The random generator's three state words. | confirmed |

### Save format

**Status:** confirmed by `Save_WriteGame` and `Load_ReadGame`, except where marked.

A save game is the file `SAVEn.DS` in the `[SP]` folder, where `n` is the digit of `Save_Slot`. The save
code takes the name from the string `[SP]/SAVE-.DS` at `0x674886` and puts the digit in place of the `-`.
The file holds six records, written and read in a fixed order. **Inferred:** the records follow each
other without a gap.

The first record has 34762 bytes:

| Save offset | Size | Content |
| --- | --- | --- |
| `0x0` | 250 | Header: single fields copied from the game state block. |
| `0xfa` | 8628 | Area 1, from `State_SaveBlocks` at `+0x64`. |
| `0x22ae` | 8628 | Area 2, from `+0x68`. |
| `0x4462` | 8628 | Area 3, from `+0x6c`. |
| `0x6616` | 8628 | Area 4, from `+0x70`. |

Header fields with a known role:

| Save offset | Size | State offset | Name |
| --- | --- | --- | --- |
| `0x4c` | 4 | `+0x202` | `Clock_Game` |
| `0x54`, `0x56`, `0x58` | 2 each | `+0x212`, `+0x214`, `+0x216` | `Rng_State` |

The other header fields copy single fields of the game state block. Their roles are unknown.

## By address

| Address | Name | Topic |
| --- | --- | --- |
| `0x401090` | — | Files and configuration |
| `0x405fe0` | `Speed_SetFactor` | Timing |
| `0x406c90` | `Timer_ThreadMain` | Timing |
| `0x406fb0` | `Sound_LoadFile` | Files and configuration |
| `0x4076d0` | `File_Open` | Save and load |
| `0x407750` | `File_Create` | Save and load |
| `0x4077e0` | `File_Close` | Save and load |
| `0x407800` | `File_Write` | Save and load |
| `0x407870` | `File_Read` | Save and load |
| `0x41da10` | — | Game code band |
| `0x41e710` | `Game_StartClock` | Timing |
| `0x41ee3b` | `FreeGame_NewSeed` | Random generator and map start |
| `0x4216e9` | `MapGen_Generate` | Random generator and map start |
| `0x425a10` | `Main_Loop` | Timing |
| `0x425c9c` | `Timer_WaitNextCall` | Timing |
| `0x426abe` | `Main_HandleKeys` | Timing |
| `0x426dfc` | `Main_ComputeElapsed` | Timing |
| `0x45772e` | `Game_Pause` | Timing |
| `0x457751` | `Game_Resume` | Timing |
| `0x45f755` | `Save_WriteArchiveIndex` | Save and load |
| `0x45f7d1` | `Load_LoadSlot` | Save and load |
| `0x45fa13` | `Save_WriteGame` | Save and load |
| `0x46049a` | `Save_WriteRecord` | Save and load |
| `0x4604ed` | `Load_ReadGame` | Save and load |
| `0x4610e3` | `Load_ReadRecord` | Save and load |
| `0x461136` | `Mem_CopyBytes` | Save and load |
| `0x466ade` | `Rng_Step` | Random generator and map start |
| `0x479cb2` | `Timer_GameEntry` | Timing |
| `0x479cc0` | `Timer_Handler` | Timing |
| `0x47a4c3` | `Input_UpdatePointer` | Input |
| `0x47b4f9` | — | Game code band |
| `0x674048` | `Speed_Factor` | Timing |
| `0x67d608` | `Timer_RateHz` | Timing |
| `0x67d614` | `Loca_FileTable` | Files and configuration |

## Unexplored

Ghidra finds 7232 functions in `Siedler1.exe` and 429 of them in the game code band. The game code
builds no stack frames, so Ghidra misses some of its routines, and 429 is a lower bound. This index names
20 routines in the band and lists 12 more without a name.

Anchors not yet followed:

- Save records 2 to 6: which part of the state each holds, and its size.
- The entry of the save code around `0x45074c` to `0x4507d0`, and what calls it and `Load_LoadSlot`.
- The roles of the header fields in the save format.
- The 22 further callees of `Main_Loop` and the callee `0x479e54` of `Timer_Handler`.
- The callers of `Rng_Step` between `0x421d71` and `0x424a9e`: inferred to be map generation passes.
- The asset readers of the game code. Few strings are referenced from the band, so file names are weak
  anchors there.
