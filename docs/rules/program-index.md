# Program index

This index records where routines and data lie in `Siedler1.exe`. Read it before you search the dump or
open the program in REA. It names places and states their role in one line. The rules themselves are in the rule notes that
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
  "Entry inferred" marks a routine whose entry is not a Ghidra function and follows from a preceding
  return or a jump.
- **Layer:** **game** inside the game code band, **platform** outside it.
- **Anchor:** the string, save field, constant, import, or caller that leads to the routine.

A data row's role starts with "Data:". The Ghidra project gives it a label, also for a table inside the
code section (`ApplyIndexNames.java`).

A site is an address inside a routine where a specific thing happens. Topics list sites. The address
table lists only routines and data.

The game code passes a register and state block in `EDI` (see [Game code](game-code.md)). This index
calls it the EDI block; it lies at `0x674000`. The offset `+0x34` of the EDI block holds a pointer to the
game state block, which lies at `0x6740a2`. An offset such as `+0x202` without other context is an
offset into the game state block.

Several routines in the game code only return: `0x41e8c8`, `0x424fc9`, `0x425c59`, `0x4615cd`, and
`0x4615ce`. They have no rows.

## By topic

### Program start and game flow

The platform creates a game thread, and the game thread enters the game code once. From then on, each
start routine jumps into `Main_Loop`, and `Main_Loop` leaves only by jumping to a start routine. Only
the quit request returns.

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x417c50` | `Platform_StartGameThread` | Creates the game thread at `0x417bb0` once the display mode is set. | confirmed | platform | String `DDrawApplication::DDSetCooperativeLevel`. |
| `0x417bb0` | `Platform_GameThreadMain` | Game thread: calls `Platform_RunGame`, then marks the thread done. Entry inferred. | confirmed | platform | Thread start address in `Platform_StartGameThread`. |
| `0x407480` | `Platform_RunGame` | Loads the language, allocates the game buffers, starts `Timer_ThreadMain`, fills the EDI block, and calls `Game_Entry`. | confirmed | platform | Strings `LANGUAGE`, `Unable to load language file`. |
| `0x41da10` | `Game_Entry` | Entry into the game code: sets up the EDI block and the state block pointer, derives `Memory_SizeClass`, and calls `0x41dbea`. | confirmed | game | Only caller is `Platform_RunGame`. |
| `0x41dbea` | — | Lays out the item tables, their bitmaps and capacities, the map pointer, and the save buffer; then jumps to `Game_InitOnce`. Its full role is unknown. | unknown | game | Called by `Game_Entry`. |
| `0x41e6c0` | `Game_InitOnce` | One-time start-up sequence; ends with a jump to `FreeGame_NewSeed`. | confirmed | game | Jump at `0x41e610`. |
| `0x424fca` | `Game_InitSettings` | Runs the resource loaders, seeds the random generator, and sets the defaults of the start settings. | confirmed | game | Called by `Game_InitOnce`. |
| `0x41ee84` | `Game_Start` | Start screens, or with `Game_Flags` bit 3 a new game: map geometry, `MapGen_Generate`, player placement, then `Main_Loop`. | confirmed | game | Jumped to from `Game_Restart` and `FreeGame_NewSeed`. |
| `0x41e706` | `Game_Restart` | Calls `Game_StartClock` and jumps to `Game_Start`. | confirmed | game | `Main_Loop` at `0x425a9c` and `0x425b51`. |
| `0x41f4d7` | `Game_ResumeLoaded` | Continues a game read from a save game, without map generation, then jumps to `Main_Loop`. | inferred | game | `Main_Loop` at `0x425b8f`; request set by `StartScreen_ResumeLoaded`. |
| `0x468717` | `Game_SetStartParams` | Fills the start parameters and the map seed from a mission record or from the free-game settings; returns the size class. | confirmed | game | Sites `0x4688f4`, `0x468b3d`. |
| `0x41eeeb` | — | After map generation, calls `0x41f1f4` once per player block with its start values. Inferred: places the players. | unknown | game | Called by `Game_Start`. |
| `0x42593a` | `Game_ResetCounters` | Zeroes the clock, the statistics, and other counters at a new game. | confirmed | game | Called by `Game_Start`. |
| `0x42055e` | — | Sets up the player blocks at a new game and sets `Sim_PhaseCount`; with a player block at `+0x30` of the EDI block it calls `Player_SetTransportPriorities` among other routines that fill default orders. Its full role is unknown. | unknown | game | Called by `Game_Start`. |
| `0x445570` | `Player_SetTransportPriorities` | Writes the default transport priority of each good 1 to 26 into the player block of `+0x30` of the EDI block, at `-0x55 + good`: for each good, in steps of 8 bytes, a load of the rank as a constant and its store, so the rank of good g is the byte at `0x445571 + 8 * (g - 1)`. The ranks are 1 to 26, each once ([Goods](goods.md#transport-priorities)). | confirmed | game | Called by `0x42055e`. |
| `0x467c03` | `StartScreen_HandleClick` | Click handler of the start screens; dispatches 42 actions through the table at `0x468148`. | inferred | game | Called by `0x440b01`. |
| `0x467547` | `StartScreen_ShowSeed` | Cuts the seed at `+0x364` to `+0x369` into four overlapping 12-bit groups and shows each through `StartScreen_FormatSeedGroup` ([Map start](map-start.md)). | confirmed | game | Only caller is `0x4679e2`. |
| `0x4678c7` | `StartScreen_FormatSeedGroup` | Writes a 12-bit group as four digits, bits 11 to 9, 8 to 6, 5 to 3, and 2 to 0, each plus `1`, into the text at `0x674a27`. | confirmed | game | Called by `StartScreen_ShowSeed`. |
| `0x466b94` | `StartScreen_SwitchPage` | Moves a requested start-screen page into the current page and runs its handler. | confirmed | game | `Main_Loop` while `Game_Flags` bit 4 is set. |
| `0x4685bc` | `StartScreen_StartGame` | Action 38: sets the start parameters and the map size, then requests a new game. Entry inferred. | confirmed | game | Slot `0x468278` of the table at `0x468148`. |
| `0x4684b6` | `StartScreen_ResumeLoaded` | Action 40: resets the countdowns and requests the resume of a loaded game. Entry inferred. | confirmed | game | Slot `0x468288`. |
| `0x468577` | `StartScreen_OpenLoad` | Action 39: opens the archive window in load mode. Entry inferred. | inferred | game | Slot `0x468280`. |
| `0x4694e0` | `Game_RequestQuit` | Action 11: requests the end of the main loop. Entry inferred. | confirmed | game | Slot `0x4681a0`. |
| `0x448418` | `Game_ConfirmEnd` | Panel action 169: asks for confirmation while the countdown runs; otherwise ends the game. Entry inferred. | inferred | game | Slot `0x446bd4` of the table at `0x44668c`. |
| `0x448436` | `Game_EndToStartScreen` | Panel action 171: advances the mission number in mission mode and requests the start screens. Entry inferred. | confirmed | game | Slot `0x446be4`. |
| `0x41e958` | `StartScreen_PlayLogo` | Runs the start-up logo sequence: ten draw steps, each followed by a frame present and `StartScreen_WaitOrSkip`, and returns early when that reports input. | inferred | game | Called by `Game_InitSettings`; draws entries 40 and 41 in palette 3998. |
| `0x41ea82` | `StartScreen_LogoStep1` | Draws entry 40 through `0x41ea59`, then entry 41 at 144,56. | confirmed | game | First step of `StartScreen_PlayLogo`. |
| `0x41edea` | `StartScreen_LogoStepLast` | Redraws entry 40 alone by a call of `0x41ea59`. | confirmed | game | Last step of `StartScreen_PlayLogo`. |
| `0x41edf0` | `StartScreen_DrawList` | Draws the list at `0x674824` (entry, x, y words, ended by a negative entry) to the surface record `0x6744b6` through `Gfx_DrawSprite`. | confirmed | game | Called by `Game_InitSettings`; entries 42 to 57. |
| `0x445643` | `Player_SetGoodsOrder` | Writes a default order of the 26 goods into player `+0x60` to `+0x79`. | confirmed | game | Called by `0x42055e`. |
| `0x445716` | `Player_SetDefaults1` | Writes default words into player `+0x140` to `+0x146`; their role is open. | confirmed | game | Called by `0x42055e`. |
| `0x445752` | `Player_SetDefaults2` | Writes default words into player `+0x148` to `+0x150`; their role is open. | confirmed | game | Called by `0x42055e`. |
| `0x44579c` | `Player_SetDefaults3` | Writes default words into player `+0x152` to `+0x15a`; their role is open. | confirmed | game | Called by `0x42055e`. |
| `0x466cc7` | `StartScreen_BuildPage` | Builds the start and load screen pages. | inferred | game | Only caller is `0x4679e2`; calls `0x406730`, `0x467b9f`. |
| `0x46736c` | — | Lays out part of the lists and text of the start screen. | unknown | game | Only caller is `StartScreen_ShowSeed`; calls `0x451111`. |
| `0x4679a3` | — | Part of the start screen layout. | unknown | game | Only caller is `StartScreen_ShowSeed`. |
| `0x468ba9` | — | Resets a start-screen page. | inferred | game | Called at `0x420452`, by `Load_LoadSlot`, `0x4692aa`, `0x4694f8`. |
| `0x468c1c` | — | Resets a start-screen page. | inferred | game | Called by `Load_LoadSlot` and `0x4694f8`. |
| `0x4694f8` | — | An action of the start screens; calls `0x468ba9` and `0x468c1c`. | inferred | game | Entry of the table at `0x468148`. |

Sites:

- `0x425a9c`, `0x425b51`, `0x425b8f`: `Main_Loop` handles the requests new game, end of game, and resume
  of a loaded game. `0x425b12`: it returns on the quit request.
- `0x468702`, `0x4484cf`, `0x468508`, `0x4694e3`: the actions set the request bits 1, 2, 3, and 0.
- `0x46813a`: the start-screen action dispatch. `0x44667e`: the panel action dispatch (255 slots at
  `0x44668c`).
- `0x467841`, `0x46786b`, `0x467899`, `0x4678c1`: the four calls that hand one 12-bit seed group each in EDI `+8` to `StartScreen_FormatSeedGroup` (in `StartScreen_ShowSeed`).
- `0x467941`: the end of the four character stores (in `StartScreen_FormatSeedGroup`).
- `0x467e12` to `0x467f39`: the parse of the 16 typed seed characters into the three seed words; `0x467f49`: the error exit for a character below `1` or above `8` (in `StartScreen_HandleClick`).
- `0x4685cf`: the start of the map size loop; `0x4685da` to `0x468603`: doubles the columns, then the rows, then the columns for each step of the size value above 1; `0x468620`: the end of the doubling (in `StartScreen_StartGame`).
- `0x468ba8`: the return after the seed mask code at `0x468b3d` stores `Map_Seed` (in `Game_SetStartParams`).

### Main loop

Note: [Timing](timing.md).

`Main_Loop` reads and clears `Game_Request`, then runs one pass. The order of a pass: the mode switches,
`Main_ComputeElapsed`, the start screens or the simulation and input, the views and panels, the frame
wait, and the key handling.

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x425a10` | `Main_Loop` | The main loop. | confirmed | game | Jumped to from every start routine. |
| `0x426dfc` | `Main_ComputeElapsed` | Computes the whole clock units elapsed since the previous pass, counts down the game countdowns, and posts their messages. | confirmed | game | Called by `Main_Loop`. |
| `0x426abe` | `Main_HandleKeys` | Handles the current key code; calls the pause on `P`. | inferred | game | Called by `Main_Loop`. |
| `0x45d718` | `Frame_WaitAndPresent` | Waits until `Timer_CallCount` is at least 4 above its value at the previous frame, then presents the frame. | confirmed | game | Called by `Main_Loop`. |
| `0x406c60` | `Frame_Handshake` | Releases one semaphore and waits on another. | confirmed | platform | Imports `ReleaseSemaphore`, `WaitForSingleObject`. |
| `0x406880` | `Frame_Present` | Hands the screen buffer to the platform output. | inferred | platform | Called by `Frame_WaitAndPresent`. |
| `0x4080e0` | `Palette_Present` | Takes the palette from `Frame_Present` and shifts each packed color left by 2 bits, so a shown component is the 6-bit value times 4 ([Graphics images](graphics-images.md)). Not read in full. | inferred | platform | Called by `Frame_Present` and `0x4067e0`. |
| `0x425cb9` | `Stats_RecordHistory` | Every 1500 clock units, records values for the four players in ring buffers. | inferred | game | Called by `Main_Loop`. |
| `0x42639a` | — | Every 6000 clock units, advances the counter at `+0x32e`, which wraps after 120 values. | unknown | game | Called by `Stats_RecordHistory`. |
| `0x44cb6e` | `Msg_RunTimers` | Counts down each player's timed messages by the elapsed units and posts the expired ones. | confirmed | game | Called by `Main_Loop` and the start routines. |
| `0x44cb8f` | `Msg_RunPlayerTimers` | The part of `Msg_RunTimers` for one player: counts the timed messages down by the elapsed units and posts each that goes below zero ([Timing](timing.md)). | inferred | game | Only caller is `Msg_RunTimers`. |
| `0x431c31` | `Msg_Post` | Writes a message into the first free of 64 slots of a player block. | confirmed | game | Called by `Main_ComputeElapsed`. |
| `0x426350` | `Stats_SumRing` | Adds the 120 bytes of a ring buffer into a sum. | confirmed | game | Called by `Stats_RecordHistory`. |
| `0x42637e` | — | Clears `+0x5e`, sets byte `+0x381` to `0xff`, and falls through into `0x42639a`. | confirmed | game | Called by `Stats_RecordHistory`. |
| `0x426664` | `Stats_Scale` | Fixed-point scaling of a value by a factor derived from a halved word plus `0x800`; never returns 0 for a non-zero input. | inferred | game | Called by `Stats_RecordHistory`. |
| `0x4266d7` | `Stats_ComputeShares` | Turns four values into their shares of the sum in percent (halving all while the sum exceeds `0xfffe`). | confirmed | game | Called by `Stats_RecordHistory`. |
| `0x446f54` | — | Toggles a game state and restarts the map: calls the map start routines and both palette loads (`0x4471f9`, `0x447200`). | unknown | game | Only caller is `Main_HandleKeys`, so a key reaches it. |
| `0x44d19c` | — | Short helper of the message timers. | unknown | game | Only caller is `Msg_RunPlayerTimers`; [M9](../milestones/m9-free-play-release.md). |
| `0x44d1c0` | `Msg_DrawIcon` | Draws the message icon (raw images 1782 to 1785, site `0x44d24f`) through `Gfx_DrawSprite`. | inferred | game | Called by `Msg_RunPlayerTimers` and `Panel_HandleClick`. |
| `0x44d263` | — | 13-byte helper of the message timers. | unknown | game | Only caller is `Msg_RunPlayerTimers`. |
| `0x44d270` | `Msg_DrawIconAlt` | Draws the message icon (raw images 1782 to 1785, site `0x44d2e8`) through `Gfx_DrawSprite`; sibling of `Msg_DrawIcon`. | inferred | game | Only caller is `Msg_RunPlayerTimers`. |

Sites:

- `0x425a44`: on a new game, `Main_Loop` sets `Game_Countdown` to 6000 and the two notice countdowns at
  `+0x17e` and `+0x182`. `0x426e8b`: `Main_ComputeElapsed` subtracts the elapsed units from
  `Game_Countdown`.
- `0x45d71b` to `0x45d755`: the frame wait of `Frame_WaitAndPresent`.
- `0x425c08`: `Main_Loop` waits until `Timer_WaitFlag` is 0. `0x479e1c`: `Timer_Handler` clears it.

### Timing

Note: [Timing](timing.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x405fe0` | `Speed_SetFactor` | Stores the speed factor and sets the timer rate to match. | confirmed | platform | Writes `Speed_Factor` and `Timer_RateHz`. |
| `0x406c90` | `Timer_ThreadMain` | Timer thread; calls `Timer_GameEntry` at the timer rate. | confirmed | platform | Reads `Timer_RateHz`. |
| `0x479cb2` | `Timer_GameEntry` | The game code's timer entry point; sets `EDI` to the EDI block and calls `Timer_Handler`. | confirmed | game | Called by `Timer_ThreadMain`. |
| `0x479cc0` | `Timer_Handler` | Counts the call, adds the clock step to the game clock, reads the pointer devices, runs pointer input and sound. | confirmed | game | Called by `Timer_GameEntry`. |
| `0x41e710` | `Game_StartClock` | Sets the clock step to 2.0. | confirmed | game | Called by `Game_InitOnce`, `Game_Restart`, `Game_ResumeLoaded`. |
| `0x425c9c` | `Timer_WaitNextCall` | Resets the interrupt counter and waits for the next timer call. | confirmed | game | No known caller. |
| `0x45772e` | `Game_Pause` | Saves the clock step and sets it to 0. | confirmed | game | Called by `Main_HandleKeys`. |
| `0x457751` | `Game_Resume` | Restores the clock step. | confirmed | game | Called by `Main_HandleKeys`. |

| Address | Name | Role | Status | Layer |
| --- | --- | --- | --- | --- |
| `0x67d608` | `Timer_RateHz` | Timer calls per second; 50 at program start. | confirmed | platform |
| `0x674048` | `Speed_Factor` | Game-speed factor: 1, 2, 4, or 8, plus two values that make the timer run without waiting. | confirmed | platform |

The clock fields are in the game state block (see "Game state block").

Sites:

- `0x4168c7`, `0x4168d5`: set the two speed-factor values that make the timer thread run without waiting.
- `0x426adf`: the main loop's key handling calls the pause on the `P` key code.
- `0x426f91`: the countdown of each view record fires when it reaches 0 or goes below (in `Main_ComputeElapsed`).

### Input

Note: [Timing](timing.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x47a4c3` | `Input_UpdatePointer` | Updates one player's pointer; timing thresholds scale with `Speed_Factor`. Changed by the History Edition. It sets bit 3 of the view's flag byte at `+1` while the second pointer button is down (the second button word of the platform layer's pointer state) and clears it on release; `Input_BufferKey` clears it, with the pointer's click bits, on a key press (`0x426dc7`). No other code in the game band writes that bit. | inferred | game | Called twice by `Timer_Handler`. See [Roads](roads.md#a-flag-on-a-road). |
| `0x427062` | `Input_ApplyPointerMotion` | Moves the accumulated pointer motion into the EDI block and runs the input handlers of each view. | confirmed | game | Called by `Main_Loop`. |
| `0x4069f0` | `Input_ReadPointerA` | Reads the first pointer device's position into the EDI block. | confirmed | platform | Called by `Timer_Handler`. |
| `0x406a30` | `Input_ReadPointerB` | The same for the second device. | confirmed | platform | Called by `Timer_Handler`. |
| `0x406b20` | `Input_ReadButtons` | Sets the button words and adds a device's motion to the EDI block. | confirmed | platform | Reached from `Timer_Handler` through `0x406bd0` and `0x406be0`. |
| `0x406980` | `Key_ReadCode` | Takes the next key from the key buffer and writes its code to the EDI block. | confirmed | platform | Called by `Main_Loop`, `Main_HandleKeys`. |
| `0x430ee9` | `Player_SwapInSecondView` | Swaps a second view's fields into player 1's block. | confirmed | game | Called by input and panel code when `+0x37e` bit 6 is set. |
| `0x430fd3` | `Player_SwapOutSecondView` | The reverse of `Player_SwapInSecondView`. | confirmed | game | Same callers. |
| `0x426b57` | `Input_BufferKey` | Puts a key code into the text buffer at `+0x23a` (count `+0x238`, size `+0x23e`); code `0xfc` deletes the last; with bit 2 of `+0x1ca` only the digits 1 to 8 are taken. | confirmed | game | Called by `Main_HandleKeys`. |

### Sound

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x479e54` | `Sound_UpdateChannels` | Counts down four sound channels and starts the requested sounds. | inferred | game | Called by `Timer_Handler`. |
| `0x47a32d` | `Sound_Play` | Picks a sound variant with an inline step of the random generator and starts it. | confirmed | game | Called by `Sound_UpdateChannels`; site `0x47a374`. |
| `0x44fd5f` | `Sound_Request` | Inserts a sound code into a view's four-slot request list. | inferred | game | 17 callers. |
| `0x428964` | `Sound_RunAmbient` | For each view, may request a random ambient sound. | inferred | game | Called by `0x4286d8`; calls `Rng_Step` through `0x428985`. |
| `0x407130` | `Sound_Start` | Scales the volume and starts playback. | inferred | platform | Called by `Sound_Play`. |
| `0x406fb0` | `Sound_LoadFile` | Tries the sound file numbers 1 to 99 in `snd/`, reads each file present whole, and passes it to `Sound_CreateBuffer`. | confirmed | platform | String `snd/%02d.SND`; called by `Platform_RunGame`. |
| `0x40b8c0` | `Sound_CreateBuffer` | Describes a sound to DirectSound as 8-bit mono PCM at 8403 Hz and copies the whole file into the buffer. | confirmed | platform | Called by `Sound_LoadFile`. |

Note: [Sound files](sound-files.md).

### Music

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x419a60` | `Music_ApplyOption` | When the music option changes, stops the player or reads `snd/music_1.midi` whole, loads it, and starts the stream. | confirmed | platform | String `snd/music_1.midi`; called by `Timer_ThreadMain`. |
| `0x415130` | `Music_LoadFile` | Checks the header and track tags of the music file in memory and records the format, division, and each track's start and length. | confirmed | platform | Called by `Music_ApplyOption`. |
| `0x415940` | `Music_ReadVarLen` | Reads a variable-length number of a track, 7 bits per byte while bit 7 is set; fails at the end of the track or on a finished track. | confirmed | platform | Called by `Music_LoadFile` and `0x415730`. |
| `0x415390` | `Music_Stop` | Stops and closes the MIDI stream. | inferred | platform | Calls `midiStreamStop` and `midiStreamClose`. |
| `0x415480` | `Music_SetVolume` | Caps the stored volume option at 100 and sends it, scaled to 127, to every MIDI channel as a volume controller message. | inferred | platform | Calls `midiOutShortMsg`; called at `0x419b7b`, in `Music_ApplyOption`, and at `0x406fa8`, in `Music_SetVolumeFromEdi`. |
| `0x406fa0` | `Music_SetVolumeFromEdi` | Passes the first word of the EDI block to `Music_SetVolume` as the volume. | inferred | platform | Calls `Music_SetVolume` at `0x406fa8`; called at `0x425c89` and `0x452226`. |

Note: [Music file](music-file.md).

### Graphics archives

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x405e00` | `Archive_Load` | Takes the length of the archive file, allocates a buffer of that size, and reads the whole file into it; returns the buffer, which is the archive base. | confirmed | platform | Called by `Platform_RunGame`; calls `Archive_ReadFile`. |
| `0x405d70` | `Archive_ReadFile` | Opens a file in binary mode and reads the requested number of bytes, or the whole file, into a buffer. | confirmed | platform | Called by `Archive_Load`. |
| `0x407290` | `Archive_FillAliases` | Copies the size and offset of source rows over the rows that repeat them: 255 rows in four kinds of block. | confirmed | game | Called by `Platform_RunGame`; data `Archive_Base`. |
| `0x405f60` | `Archive_Free` | Frees the archive buffer, or each entry's own buffer first when the header flag is 1. | confirmed | platform | Called by `Platform_RunGame` at the end. |
| `0x6812ec` | `Archive_Base` | Data: pointer to the loaded archive; an entry lies at the base plus its row's offset, and offset 0 means no entry. | confirmed | platform | Written once in `Platform_RunGame`; read at `0x406120`, `0x406610`, `0x4071c0`, `0x4073d0`. |
| `0x4073d0` | `Archive_FindEntry` | Finds one entry: stores the archive base plus the offset of the row whose number is in the EDI block's field `+0x8` into the field `+0x24`, and stores 0 when the row's offset is 0. | confirmed | platform | Called at `0x41de6e`, in the routine at `0x41dbea` (row 40), and at `0x41e8f6`, in `RuleData_Load`; data `Archive_Base`. |
| `0x41e8e0` | `RuleData_Load` | Finds entry 2, skips its first 4 bytes, byte-swaps the next 200 dwords in place, and stores the address after the skipped bytes in the game state block's field `+0xb0`. | confirmed | game | Called by `Game_InitSettings`; read through `+0xb0` by `0x43f537`. |

Note: [Graphics archives](graphics-archives.md).

### Graphics images

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x4071c0` | `Palette_Load` | Reads the 768 bytes of the archive entry whose row number is in `CX` as 256 colors of three bytes, reduces each component to 6 bits, stores them in `Palette_Current`, and forces color 0 to black. | confirmed | platform | Called by `Palette_LoadGameFlagged`, `Palette_LoadGame`, and `Platform_RunGame`; data `Archive_Base`. |
| `0x407250` | `Palette_LoadGameFlagged` | Loads the palette of entry 3, then sets a flag at `0x6812d8`. | confirmed | platform | Called at `0x4471f9` and `0x447200`. |
| `0x407400` | `Palette_LoadGame` | Loads the palette of entry 3 by a jump into `Palette_Load`. | confirmed | platform | Called at `0x4250a8` and `0x45229d`. |
| `0x407410` | `Palette_LoadTitle` | Loads the palette of entry 3997 by a jump into `Palette_Load`. | confirmed | platform | Called at `0x451ee9`, in the title scene. |
| `0x6816d0` | `Palette_Current` | Data: the current palette, 256 colors of three bytes with 6-bit components. | confirmed | platform | Written by `Palette_Load`; read by `Frame_Present`. |
| `0x405730` | `Gfx_BlitRaw` | Copies the width x height palette indices of a raw image to the surface row by row, clipped to the clip rectangle, with no transparency. | confirmed | platform | Called by `Gfx_DrawSprite`, which hands it the entry's address and the position. |
| `0x405590` | `Gfx_BlitSprite` | Draws a transparent sprite: per row, runs of a skip byte, a count byte, and that many palette indices copied to the surface, a count of 0 ending the row; clipped to the clip rectangle. | confirmed | platform | Called by `Gfx_DrawSpriteTransparent`, `Gfx_DrawSpriteGrowing`, `Gfx_DrawSpriteNoOffset`, and `Gfx_DrawSettler`. |
| `0x405880` | `Gfx_BlitSpriteOneColor` | Draws the same stream as `Gfx_BlitSprite` but writes one color value for every drawn pixel and skips the stored indices. | confirmed | platform | Called by `Gfx_DrawSpriteOneColor` and `Gfx_DrawSettler`. |
| `0x4062e0` | `Gfx_DrawSpriteTransparent` | Loads the clip rectangle of the target surface and calls `Gfx_BlitSprite` with the entry and the position of the EDI block. | confirmed | platform | Called at `0x4286b8`, `0x44dace`, `0x44dec8`, `0x44fd59`. |
| `0x406490` | `Gfx_DrawSpriteOneColor` | Loads the clip rectangle and calls `Gfx_BlitSpriteOneColor` with the entry, the position, and the color of the EDI block. | confirmed | platform | Called at `0x4512da` and `0x45130f`. |
| `0x406350` | `Gfx_DrawSpriteGrowing` | Draws a transparent sprite through `Gfx_BlitSprite` with its top moved by a fraction of its height, which the EDI block holds, and the top clipped to the clip rectangle; the entry is the base 1250 plus a script index. | inferred | platform | Called at `0x44ff45`. See [Graphics images](graphics-images.md). |
| `0x406590` | `Gfx_DrawSpriteNoOffset` | Draws a transparent sprite through `Gfx_BlitSprite` with the entry's two offsets set to 0 for the call and restored after; the entry is the base 1250 plus a script index. | confirmed | platform | Called at `0x44db12`. See [Graphics images](graphics-images.md). |
| `0x406610` | `Gfx_DrawSettler` | Draws a settler: the figure from the base 1850 through `Gfx_BlitSprite`, the overlay of the same index plus 2500 through `Gfx_BlitSpriteOneColor` in the player color (40 hex plus four times the player), then, where a second figure is set, an entry from the base 3150 through `Gfx_BlitSprite`, moved by header bytes 0 and 1 of the first figure as signed offsets. | confirmed | platform | Called at `0x450189`. See [Graphics images](graphics-images.md). |
| `0x44fff3` | `Gfx_DrawSettlerRecords` | Reads the draw records of an object (flags, figure word, position). For each, it loads the pair of words at 4 times the high byte of the figure word from the figure bases at `0x4501a1`. It loads the pair at 4 times the low byte from the figure offsets at `0x4503c5`. The `lea` instructions are at `0x4500fb` and `0x450128`. Both addresses are odd, so the words are read unaligned. It adds the offsets and calls `Gfx_DrawSettler`. | confirmed | game | Called at `0x44d3ae`. See [Settlers](settlers.md#drawing-the-walk-leads). |
| `0x44ffe7` | `Render_DrawRowSettlers` | Takes the next row list pointer of the view (`+0x92`, through `+0x2c` of the EDI block) and falls into `0x44fff3`, which draws the row's settler records. `0x44d3ae` calls it once for each row of the view, after the row's tiles. | confirmed | game | Called by `0x44d3ae`. Run by the `draw:` verb. |
| `0x43f744` | `Settler_FigureStubs` | Data-like code: 8 bytes for each job (`jmp rel32`), reached by `0x43f537` from byte 0 bits 2 to 6; each jumps to the job's figure handler, which adds the job's constant to the figure byte. | confirmed | game | Stub 0 to `0x43f8ae` (carrier), stub 5 to `0x43fb6b` (woodcutter), stub 11 to `0x43fed8`. |
| `0x43f8ae` | `Carrier_FigureWord` | The carrier's figure handler: in state 3 or 0xe with a good in byte 11 (not 0) it adds the 16-bit word at `0x43f4b7` plus 2 * good (`0x43f904`) to the figure word; it adds nothing else, so the word is the figure byte plus that word. | confirmed | game | Reached by the stub at `0x43f744`. The table at `0x43f4b7` has 27 words, the high byte the row of the figure bases (log: `0x1400`). [Drawing a carrier with a good](goods.md#drawing-a-carrier-with-a-good). |
| `0x43fb6b` | `Woodcutter_FigureWord` | The woodcutter's figure handler: a figure byte below 0x80 gets `0x0b00` (`0x43fc16`), or `0x1000` (`0x43fc0b`) on the way back with the log (byte 13 0x80, byte 14 1, state 0x10); a byte from 0x80 gets `0x0e80` (`0x43fbe5`). | confirmed | game | Reached by the stub at `0x43f76c`. Screenshots of `play/woodcutter-figures.play`. |
| `0x4511d5` | `Text_DrawGlyphs` | Draws a string of characters as one-color sprites: for each character, a shadow glyph (entry plus 809) in the shadow color when one is set, then the glyph (entry plus 749) in the text color; steps 8 pixels per character. | inferred | game | Entered by jumps at `0x45103e`, `0x4510a3`, `0x45110c`, `0x451171`, `0x4511d3`, the ends of the five sites that start at `0x450fde`, `0x451043`, `0x4510a8`, `0x451111`, `0x451173`; calls `Gfx_DrawSpriteOneColor`. |
| `0x405b80` | `Gfx_BlitShadow` | Draws a mask to the surface, clipped, storing palette index `0x80` on every covered pixel with no blending. | confirmed | platform | Called at `0x4061fa`, in `Gfx_DrawShadow`, and at `0x4062c9`, in the second shadow routine at `0x406210`, which `0x44ff2a` calls with an object's number plus 1500 (reached from `0x44fcd0`, from `0x44fd2f` by the jump at `0x44e56a`, and by a third path that reads the table at `0x44fcfe` (at `0x44fc4f`) after the call at `0x44fc21` and jumps to `0x44fee2` at `0x44fc70`). |
| `0x4061a0` | `Gfx_DrawShadow` | Sets the clip rectangle and draws the mask whose entry number is in the draw block's third field through `Gfx_BlitShadow`. | confirmed | platform | Called at `0x44daa4` (the entry is an object's number plus 1500, in `Gfx_DrawObjectSprite`, which about 38 sites reach) and at `0x45007d` (entry 4). |
| `0x44da4d` | `Gfx_DrawObjectSprite` | Draws an object: scales the position field by 8 and moves the position by 16 and 8, draws the mask of the entry plus 1500 through `Gfx_DrawShadow`, then the sprite of the entry plus 1250 through `Gfx_DrawSpriteTransparent`. | confirmed | game | Called at `0x44d6e8` and `0x44dcad`, in the routines at `0x44d5b4` and `0x44db26`; a scan of the code band finds 38 call or jump sites, not all read. See [Graphics archives](graphics-archives.md). |
| `0x44de9a` | `Gfx_DrawSpriteBase320` | Moves the position by 16 and 8 and draws the sprite of the entry plus 320 through `Gfx_DrawSpriteTransparent`. | confirmed | game | 39 call or jump sites, listed with the entries they reach in [Graphics archives](graphics-archives.md#entries-no-routine-reads); four are tail jumps. |
| `0x44fd4a` | `Gfx_DrawSpriteBase320Low` | Moves the position by 8 and 9 and draws the sprite of the entry plus 320 through `Gfx_DrawSpriteTransparent`. | confirmed | game | Only caller is `0x45b526`. |
| `0x405b40` | `Gfx_FillTextured` | Hands the mask and the texture entry to `Gfx_BlitTextured`. | confirmed | platform | Called at `0x40618c`, in `Gfx_DrawTexturedFill`. |
| `0x406120` | `Gfx_DrawTexturedFill` | Sets the clip rectangle, finds the texture entry from the draw block's fourth field, and fills the mask of its third field with it. | confirmed | platform | Called at `0x4279a9`, `0x427b0d`, and `0x428363`, in the routines at `0x4278be`, `0x427a25`, and `0x428008`, `0x428105`, `0x4281cc`, which form the mask (60 plus an index, 141 plus an index, or a base of 234, 243, or 252 plus a height difference) and the texture (260 or 300 plus an offset). |
| `0x427642` | `Terrain_DrawTiles` | Draws the terrain triangles of the view rows with the routines at `0x4278be` and `0x427a25` and the tables of [Terrain drawing](graphics-images.md#terrain-drawing). | confirmed | game | Called by `0x4275d6`. |
| `0x42f7ea` | — | Works out the screen position of the node at `+0xfc` and `+0xfe` of the view's record relative to the view origin, wrapping at the map size. Its full role is unknown. | unknown | game | Called by `View_HandleClick`, `0x427133`, and `0x440b01`. |
| `0x406420` | `Gfx_DrawSpriteOverColor8` | Sets the clip rectangle from the target surface and draws the entry of the draw block through `Gfx_SpriteOverColor8`. | confirmed | platform | Called at `0x44ffcd`, in `0x44ff59`. |
| `0x405d00` | `Gfx_SpriteOverColor8` | Passes the surface, the position, and the entry's address to `Gfx_BlitSpriteOverColor8`. | confirmed | platform | Called at `0x40647a`, in `Gfx_DrawSpriteOverColor8`. |
| `0x44ff59` | `Gfx_DrawPhaseSprite` | Picks one of the 16 entries 630 to 645 from the tile record's address (`& 0x3c`, doubled, `^ 0xa8`) plus the counter at the game state block's `+0x206`, shifted right 3 and masked to 4 bits, and draws it through `Gfx_DrawSpriteOverColor8`. | confirmed | game | Called by `0x44d5b4`; the add of 630 is at `0x44ffc7`. See [Map objects](map-objects.md#water). |
| `0x674824` | `StartScreen_SpriteList` | Data: a list of words (entry, x, y) ended by a negative entry, the images 42 to 57 that `0x41edf0` draws through `Gfx_DrawSprite`; game state `+0x782`. | confirmed | game | Read by `0x41edf0` (called at `0x424fca`). |

| Address | Name | Role | Status | Layer |
| --- | --- | --- | --- | --- |
| `0x4279d4` | `Render_TriangleVariantsUpper` | Data: 81 bytes, by height-difference index, the texture variant (a multiple of 4, `0xff` none) of an upper triangle. | confirmed | game |
| `0x427b38` | `Render_TriangleVariantsLower` | Data: the same 81 bytes for a lower triangle. | confirmed | game |
| `0x427b89` | `Render_TerrainTextures` | Data: 128 bytes, 8 for each terrain code, the texture number minus 260 by variant. | confirmed | game |
| `0x44e114` | `Gfx_SpriteLists` | Data: 25 inline sprite lists in the code band, entries 0 or 8, read by the site `0x44e0d1`. | inferred | game |
| `0x44f225` | `Gfx_BuildingPairTable` | Data: 128 byte pairs inline in the code band (up to `0x44f325`); first bytes 162 to 173, read with an even index by the eight sites from `0x44ee56`. | confirmed | game |
| `0x44fd16` | `Gfx_ObjectNumberTable` | Data: inline byte table in the code band of object numbers (152 to 193) for the shadow path through `0x44fcd0` to `0x44ff2a`; sibling of the table at `0x44fcfe`. | confirmed | game |
| `0x44d87d` | `Render_WaitingCarrierPhases` | Data: in the code band, 16 words, the phase of a waiting carrier's figure by the tile record's address. | confirmed | game |
| `0x44d89d` | `Render_WaitingCarrierFigures` | Data: in the code band, 128 figure words of a waiting carrier, in the figure rows 0 and `0x88`; read at the phase plus the clock. | confirmed | game |
| `0x674923` | `Render_WaitingCarrierOffsets` | Data: 64 pairs of signed bytes, x then y, one for each value of the six road direction bits of a tile's record A byte 0. Only `Render_AddWaitingCarrier` reads it. | confirmed | game |
| `0x6749a3` | `Render_WaitingCarriers` | Data: the list of draw records `Render_DrawRowTiles` builds for one row; `Render_DrawViewRows` draws it through `Gfx_DrawSettlerRecords` right after the row's settler records (`0x44d4c1`, `0x44d56e`). | confirmed | game |
| `0x4501a1` | `Settler_FigureBases` | Data: in the code band, 8 words for each job (137 pairs): the figure base in the entries from 1850 and a second figure from 3150. | confirmed | game |
| `0x4503c5` | `Settler_FigureOffsets` | Data: in the code band, two words for each figure byte, added to both entry fields by `Gfx_DrawSettlerRecords` at `0x450128` to `0x45014c`. | confirmed | game |

Note: [Graphics images](graphics-images.md).

Sites:

- `0x40669a`, `0x4066bf`, `0x4066d2`, `0x4066e4`: `add 0x9c4` forms the overlay entry (figure plus 2500); the call at `0x4066bf` draws it through `Gfx_BlitSpriteOneColor` in the player color; `0x4066d2` and `0x4066e4` read header bytes 0 and 1 of the figure as signed offsets for the second figure (in `Gfx_DrawSettler`).
- `0x4075ac`: loads the palette of entry 3998 through `Palette_Load`, before the first screens (in `Platform_RunGame`).
- `0x41ea7c`, `0x41eaaa`: draw entry 40 and entry 41 of the logo (in `0x41ea59` and `StartScreen_LogoStep1`).
- `0x41ee33`: draws each entry of the list at `0x674824`, entries 42 to 57 (in `StartScreen_DrawList`).
- `0x420d97`: the draw call of each list piece, entries 1780 to 1805 (in `View_DrawFrameList`).
- `0x42105f` to `0x4213e9`: the draws of entries 600 to 603; `0x421251` to `0x42136d`: the draws of entries 3880 to 3882 (in `View_DrawFrameBorders`).
- `0x421412` to `0x42152a`: the draws of entries 660 to 663 (in `View_DrawPanelFrames`).
- `0x425926`: draws image 42 plus an index of 0 to 15 through `Gfx_DrawSprite`, in palette 3 (in a code piece after `Game_InitSettings` that it jumps out to and back from; no Ghidra function).
- `0x44d157`: draws raw image 1750 plus a byte through `Gfx_DrawSprite` (in `Msg_RunPlayerTimers`).
- `0x44d24f`: draws raw images 1782 to 1785 through `Gfx_DrawSprite` (in `Msg_DrawIcon`, `0x44d1c0`).
- `0x44d2e8`: draws raw images 1782 to 1785 through `Gfx_DrawSprite` (in `Msg_DrawIconAlt`, `0x44d270`).
- `0x44da9e`, `0x44dac8`: forms the shadow entry (index plus 1500), then the sprite entry (index plus 1250) (in `Gfx_DrawObjectSprite`).
- `0x44dba2`, `0x44dbf2`, `0x44dc42`, `0x44dcfd`, `0x44dd4d`, `0x44dd9d`, `0x44dded`, `0x44de3d`: call `Gfx_DrawSpriteBase320` with an object byte masked to 0 to 31, entries 320 to 351 (in `Render_DrawFlag`).
- `0x44de86`: draws raw image 870 plus a tile index through `Gfx_DrawSprite` (in `0x44de56`).
- `0x44e0d1`: calls `Gfx_DrawSpriteBase320` with a sprite-list entry (0 or 8, from the 25 inline lists at `0x44e114`) plus a phase of 0 to 7 plus 136, entries 456 to 471. Lies in code after `Gfx_DrawSpriteBase320` and before `0x44e329` that no Ghidra function covers; the containing routine's entry was not established (REA rizin unavailable).
- `0x44ff24`, `0x44ff3f`: forms the shadow entry (index plus 1500, drawn by `0x406210` at `0x44ff2a`), then the sprite entry (index plus 1250, drawn by `Gfx_DrawSpriteGrowing` at `0x44ff45`). Code not covered by a Ghidra function, between `Sound_Request` and `Gfx_DrawPhaseSprite`; entry `0x44fee2` (jumped to at `0x44fc70`) is a candidate routine, not verified.
- `0x45006a`: stores shadow entry 4 for the call to `Gfx_DrawShadow` at `0x45007d`; `0x450128` to `0x45014c`: adds a word of the table `Settler_FigureOffsets` to both entry fields (in `Gfx_DrawSettlerRecords`).
- `0x450ada` (in `0x450a8f`), `0x450f2f` (in `0x450d85`), `0x450fc5` (in `0x450f6e`): call `Gfx_DrawSpriteBase320` with a run-time list word or an object byte at `+0x101`; not bounded. The three routines are called from `Render_DrawViewOverlay` (`0x450a56`, `0x450ae7`) and lack rows.
- `0x452394`, `0x452400`, `0x452443`: draw raw images 1, 5 to 11, and 15 to 21 through `Gfx_DrawSprite` under palette 3997 (title scene). Code not covered by a Ghidra function; containing routine not established.
- `0x45cf10`: draws raw images 660 to 663; `0x45cf57` to `0x45d02c`: 670 to 677 (in `Panel_CopyViewToScreen`, `0x45ce7b`).
- `0x467bd4`: draws the raw image of a tile index plus 870 through `Gfx_DrawSprite` (in `0x467ba4`).

### Views, panels, and drawing

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x440aae` | `Ui_HandleViews` | For each view, runs the start-screen click handler or the in-game click and panel handlers. | inferred | game | Called by `Main_Loop`. |
| `0x44591d` | `Panel_HandleClick` | Hit-tests a panel's buttons and dispatches the action through the table at `0x44668c`. | inferred | game | Called by `0x440c24` and `StartScreen_HandleClick`. |
| `0x451355` | `Panel_Update` | For each view, runs the handler of the requested or current panel screen. | inferred | game | Called by `Main_Loop`. |
| `0x45ce5a` | `Panel_CopyToScreen` | For each view, copies the panel into the view's screen. | inferred | game | Called by `Main_Loop`. |
| `0x44d38d` | `Render_DrawMapView` | For each view, draws the map row by row. | inferred | game | Called by `Main_Loop` in a game. |
| `0x44d3ae` | `Render_DrawViewRows` | Walks the rows of one view two at a time and runs `Render_DrawRowTiles` for each. | confirmed | game | Called by `Render_DrawMapView`. See [Map objects](map-objects.md). |
| `0x44d77e` | `Render_AddWaitingCarrier` | Appends a draw record (the low two bits of record B byte 1, the figure word, x, y) to the list at `Render_WaitingCarriers` for a tile whose record B byte 1 has bit 7 set. Where bit 6 of the tile's record A byte 0 is set, the figure word is `0x203` and the position the tile's (x the column counter times 8, y the row position less 4 times the height). Otherwise it takes a phase word from `Render_WaitingCarrierPhases` by bits 2 to 5 of the tile record's address (`0x44d832`), adds the clock (`+0x206`), shifts right 2, masks with `0xfe`, and takes that byte offset in `Render_WaitingCarrierFigures` as the figure word (`0x44d85d`). It then adds the two signed bytes of `Render_WaitingCarrierOffsets` at twice the low six bits of record A byte 0 to the position (`0x44d9b2`). | confirmed | game | Called at `0x44d5ef` by `Render_DrawRowTiles`. [Carriers](carriers.md#drawing). |
| `0x44d5b4` | `Render_DrawRowTiles` | Draws the tiles of one row from west to east. For each tile it first reads byte 1 of record B (at the state's `+0x36` from the tile's record) and calls `Render_AddWaitingCarrier` where bit 7 is set (`0x44d5ef`). Then, for the object code in byte 3 of record A: no sprite for 0, other routines for 1 to 7, the animated or still sprite through `Gfx_DrawObjectSprite` from 8 on. It starts the list at `Render_WaitingCarriers` and ends it with `0xffff`. | confirmed | game | Called by `Render_DrawViewRows`. See [Map objects](map-objects.md). |
| `0x450a35` | `Render_DrawViewOverlay` | For each view, draws sprite lists over the map. | inferred | game | Called by `Main_Loop` in a game. |
| `0x44d2fc` | — | For each view, copies a rectangle from the surface at `+0x108` into the view's screen. | unknown | game | Called by `Main_Loop` in a game. |
| `0x424d87` | `Minimap_Build` | Writes one byte per tile from terrain and height to the buffer at `+0x8c`. | inferred | game | Called by `MapGen_Generate` and `Game_ResumeLoaded`. |
| `0x406500` | `Gfx_CopyRect` | Copies a rectangle between two surfaces. | confirmed | platform | Called by panel and view code. |
| `0x4060b0` | `Gfx_DrawSprite` | Finds a raw image by its entry number and draws it opaque to the surface through `Gfx_BlitRaw`. | confirmed | platform | 55 call sites in 13 callers. See [Graphics images](graphics-images.md). |
| `0x420cc5` | `View_DrawFrameList` | Sets bit 0 of `+0x383`, picks one of the four piece lists of `View_FramePieceLists` by `Game_Flags` bit 7 and `Game_ModeFlags` bit 2 (surface record `0x674506` or `0x6744b6`), and draws each entry, x, y triple through `Gfx_DrawSprite` until a negative entry. | confirmed | game | Called by `0x41e6c0`, `Game_Start`, `0x41f4d7`, and `0x447200`; entries 1780 to 1805. |
| `0x420d9f` | `View_FramePieceLists` | Data: four lists at `0x420d9f`, `0x420e19`, `0x420eab`, `0x420f25` of entry, x, y words ended by a negative entry; one view or two, each on either surface record. | confirmed | game | Selected by `View_DrawFrameList`. |
| `0x421021` | `View_DrawFrameBorders` | Draws the screen frame pieces 600 to 603 at fixed positions, with a variant when `Game_Flags` bit 7 is set and extra pieces 3880 to 3882 when `Game_ModeFlags` bit 2 is clear. | confirmed | game | Called by `0x420fff`. |
| `0x4213ef` | `View_DrawPanelFrames` | Draws pieces 660 to 663 into the surface records `0x67457e` and `0x674592`. | confirmed | game | Called by `0x420fff`. |
| `0x43fed8` | `Settler_FigureJob11` | The figure handler of job 11, the settler of the type 1 hut: adds `0x2c00`, `0x2f00`, `0x2c80`, or `0x2d80` to the figure byte. | confirmed | game | Reached by the stub at `0x43f79c` from `0x43f537`. The walk is confirmed by the `job11` recording of the `draw:` verb. The other adds are read from the code only. |
| `0x441062` | `Msg_HandleOpenClick` | While the game runs, opens the current message (`0x4415c0`, then `0x441ba2`); on some panel screens (`0x17` to `0x1a`, `0x22`, `0x23`, `0x25`) asks for sound 4 instead. | inferred | game | Called by `View_HandleClick`. |
| `0x44126d` | `Msg_StepNext` | Steps the player's message index (`+0x172`, up to `0x40`) and reads the next slot; sound 4 where a second view swap blocks it. | inferred | game | Called by `View_HandleClick`. |
| `0x4415c0` | `Msg_Open` | Reads the first message slot of the player block (`+0x1df4`) and sets the view up by its type. | inferred | game | Called by `0x441062`. |
| `0x441987` | `Panel_Reset` | Clears bit 6 of the view's byte 1, writes 10, 12, 14 into the list at view `+0x62`, sets bits 1 and 2, and sets the panel screen (`+0x72`) to 0. `0x441b31`, `0x441ba2`, `0x441d2e`, and `0x442509` are byte-identical copies. | confirmed | game | Called by `View_RunButtonAction` and `0x44362b`. |
| `0x441b31` | — | Byte-identical copy of `Panel_Reset`. | confirmed | game | Called by `View_RunButtonAction`. |
| `0x441ba2` | — | Byte-identical copy of `Panel_Reset`. | confirmed | game | Called by `View_RunButtonAction` and `0x441062`. |
| `0x441d2e` | — | Byte-identical copy of `Panel_Reset`. | confirmed | game | Called by `View_RunButtonAction`. |
| `0x442509` | — | Byte-identical copy of `Panel_Reset`. | confirmed | game | Called by `View_RunButtonAction`. |
| `0x444780` | `Map_CountPlayerObjects` | Clears player `+0x12a` to `+0x130` and walks every node of the map through `0x444954`. | inferred | game | Called by `View_HandleClick`. |
| `0x444954` | `Map_CountNodeObject` | For a node owned by the player (top bits of the height byte), counts objects of class 2 to 4 into the player's counters. | inferred | game | Called by `0x444780`. |
| `0x44c8cf` | `Panel_WriteButtonList` | Writes a short button list through the EDI block's `+0x24` pointer (0, then 0x10 or 7 by bit 0 of byte 2 of the `+0x30` record) and stores 0x20 and 0x21 through the `+0x28` pointer. | confirmed | game | Only caller is `0x44c80f`; [M3](../milestones/m3-first-loop.md) guesses the build menu. |
| `0x45080d` | `Render_MarkTwoDirty` | Sets bit 0x10 in the bytes that the game state block's `+0x78` and `+0x7c` pointers name, saving and restoring the EDI block's `+0x20`. Meaning of the bit unknown. | confirmed | game | Called by `Road_Cancel`, `Castle_Place`, and `Territory_Update`. |
| `0x4513a8` | `Panel_UpdateView` | The part of `Panel_Update` for one view: runs the handler of the panel screen at view `+0x72`. | inferred | game | Only caller is `Panel_Update`. |
| `0x45ac83` | `Minimap_DrawPanel` | Draws the minimap panel by calling sixteen layer routines from `0x45afcf` to `0x45ccc6`. | inferred | game | Only caller is `0x45591a`; [M9](../milestones/m9-free-play-release.md). |
| `0x45ae45` | `Minimap_DrawTiles` | Part of the minimap panel; draws raw images through `0x44de56`. | inferred | game | Only caller is `0x45591a`. |
| `0x45afcf` | — | First layer routine of `Minimap_DrawPanel`; calls `0x406790`. | unknown | game | Only caller is `Minimap_DrawPanel`. |
| `0x45bb96` | — | A layer routine of `Minimap_DrawPanel`; calls `0x406790`. | unknown | game | Only caller is `Minimap_DrawPanel`. |
| `0x45ce7b` | `Panel_CopyViewToScreen` | The part of `Panel_CopyToScreen` for one view: copies the panel through `Gfx_CopyRect` and draws raw images 660 to 677 through `Gfx_DrawSprite` (sites `0x45cf10`, `0x45cf57`, `0x45d02c`). | inferred | game | Only caller is `Panel_CopyToScreen`. |
| `0x45f961` | `Panel_DrawStateImage` | Draws the raw image 25 plus the state word `+0x5c` at (8, 9) to the surface of the record at EDI `+0x20`, clears bit 1 of the record's byte 1, then runs `Panel_CopyToScreen` and `0x45d716`. Entry inferred. | confirmed | game | Called at `0x451841` and `0x451cd7`; Gfx_DrawSprite at `0x45f9ae`. |

| Address | Name | Role | Status | Layer |
| --- | --- | --- | --- | --- |
| `0x43f4b7` | `Settler_CarriedFigures` | Data: 27 words, one for each good, added to a carrier's figure word; the high byte is the row of the figure bases ([Goods](goods.md#drawing-a-carrier-with-a-good)). | confirmed | game |

Sites:

- `0x43f742`: jumps by job through the table of 8-byte jumps at `0x43f744` to the job's figure routine (in `0x43f537`).
- `0x43f904`: loads the carried-goods table at `0x43f4b7`; `0x43f92a`: the jump out at the end (in `Carrier_FigureWord`).

### Simulation

The simulation runs while `Game_Flags` bit 3 is set. It works through the item tables in slices of 32
entries per phase (see "Item tables").

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x4291c2` | `Sim_StepPhase` | Advances `Sim_Phase` and calls the routine for the new phase. Phases 0 to 31 use the table at `0x42923c`: the per-player update for player `phase & 3`, skipped while the clock step is 0 or `Table70_Count` is 50 or more. Phase 32 and up use the table at `0x4292ee`. The random draws happen in those phase-table routines ([Timing](timing.md#rules-that-count-passes)). | confirmed | game | Called by `Main_Loop`; tables at `0x4292ee` and `0x42923c`. |
| `0x474da3` | `Ai_UpdatePlayer` | Per-player update for the players whose block has bits 7 and 6 of byte 2 set (inferred: active computer players). | inferred | game | Reached from `Sim_StepPhase`; calls `Rng_Step`. |
| `0x46414d` | `Table70_Pass` | Processes the phase's slice of `Table70` (the flags whose slot divided by 32 is the phase, phases 0 to 31): for each flag in use, its goods (not read), then through `0x464c36` its roads. | confirmed | game | Called by `Main_Loop`. [Carriers](carriers.md#the-flag-pass). |
| `0x464c36` | `Flag_VisitRoads` | The part of `0x46414d` after a flag's goods: for each direction from north to east that holds a road, sets the flag status bit of a road that holds a carrier, and asks `0x42b455` for a carrier for a road whose byte shows none, unless the flag's status bit 7 says a request found none since the retry scan. The same Ghidra function holds the end of a good's direction step: at `0x46501a` a good given a road on which another good waits takes the waiting place only with a higher transport priority ([Goods](goods.md#the-turn-of-a-flags-goods)). | confirmed | game | Called by `0x46414d`. [Carriers](carriers.md#the-flag-pass). |
| `0x464bb2` | `Flag_SeedRoadSearch` | Seeds the search for a good's direction with the flag's carrier roads, by the number of goods heading that way ([Goods](goods.md)). | confirmed | game | Called by `Table70_Pass`. |
| `0x462ca4` | `Good_ReleaseRequest` | Gives back the request of a good that cannot reach its destination; a storage counts none ([Goods](goods.md)). | confirmed | game | Called by `Good_CountLost` and `Flag_VisitRoads`. |
| `0x42abed` | `Player_CheckWarnings` | The player block's per-turn warning check: posts messages 10 and 11 through `Msg_Post` from the counters at `+0x14` and `+0x18` of a `Table120` item ([Carriers](carriers.md)). | confirmed | game | Only caller is `0x42abac`. |
| `0x42cb2d` | `Table18_Pass` | Processes the phase's slice of `Table18` (slots `32 * phase` on, in groups of 32 every 1024): a finished building goes to its type's handler through the table at `0x42cd1d`, a building under construction to the countdown. | confirmed | game | Called by `Main_Loop`. [Settlers](settlers.md#the-pass). |
| `0x42f3d9` | `Table16_Pass` | Processes the phase's slice of `Table16`: the slots whose number is the phase modulo 16 plus a multiple of 16, each through `0x42fc81`. | confirmed | game | Called by `Main_Loop`. [Settlers](settlers.md#the-pass). |
| `0x42f4f1` | `View_BuildSettlerRecords` | For each view (the pointers at `+0x78` and `+0x7c` of the game state block), runs `0x42f512` with the view at `+0x20` of the EDI block. So a settler on a shown tile takes one more turn every pass, others every 16th, and gets a draw record. | confirmed | game | Called by `Main_Loop`. Run by the `draw:` verb ([Settlers](settlers.md#the-draw-path-emulated)). |
| `0x42f512` | `View_ScanSettlers` | One view: returns for a hidden view (bit 0 of its byte 0); clears the row lists (`0x4409d5`); scans the nodes of the view row by row (two rows a step, the columns from the view's west edge) for the settler word of each node (`+2` of the second layer); for each settler runs its turn (`0x42fc81`) and builds its record (`0x43f537`); ends the row lists with `-1` (`0x440a53`). | confirmed | game | Called by `0x42f4f1`. |
| `0x43f537` | `Settler_BuildRecord` | Builds the draw record of the settler at the item pointer. It ends at once unless the node's settler word is the settler's slot. It takes the view row: the view's `+0x4c` plus the node's row plus the signed byte of the frame in the table at `0x43f402`. It takes the position `(32 x - 16 y + view +0x9a, 20 y + view +0x90 - 4 * height - 2)`. It takes the triple of the sequence of its frame (entry 2, through `+0xb0`) at 3 times the timer shifted right 3. Then it jumps to the job's handler (`0x43f744`). The handler appends (flags, figure word, x, y) to the row's list at `+0x92` of the view if the row is below the view's `+0x50`. | confirmed | game | Called by `0x42f512`. |
| `0x43f402` | `Settler_FrameRows` | Data: 181 signed bytes, one for each frame of a settler (up to the table at `0x43f4b7`): what `0x43f537` adds to the row of the record; -1 for the frames 9 to 26, 63 to 80, 121 and 122, and 125 to 128, else 0. | confirmed | game | Read by `0x43f537`. `ProgramTables.FrameRows`. |
| `0x4286d8` | — | Runs `0x428728`, `Sound_RunAmbient`, and `0x428d10`, then `0x428a75` for each player block. | unknown | game | Called by `Main_Loop`. |
| `0x428728` | `Sim_ClearRetryBits` | Each pass clears bit 2 of the status of a window of `Table18` buildings from a cursor (`+0x54`, the window `+0x52`, stops after 10 set bits), and bit 7 of the flag status of a window of `Table70` (cursor `+0x56`). | confirmed | game | Called by `0x4286d8`. [Settlers](settlers.md#the-retry-scan). |
| `0x42be63` | `Building_RequestWorker` | A building asks the storages for a settler of the job in the registers: a search over the flags from the building's flag; at a storage flag it takes the waiting settler of the job (state 15, mode 255, target the building's flag) or makes one of a jobless settler and a tool. | confirmed | game | Called by the building handlers (`0x42e0b8` and its siblings through `0x42cd1d`). [Settlers](settlers.md#the-request). |
| `0x42b455` | `Road_RequestCarrier` | A land road asks the storages for a carrier: the search of `0x42be63` started at both ends of the road marks every flag with the end it was reached from; at a storage's flag it takes the waiting settler of job 0, or makes one of a waiting settler of job 21 (no tool), sends it (state 15, mode the direction of the road at the end the storage's flag was reached from, target that end's flag), and sets bit 7 of the road byte at both ends. | confirmed | game | Called by `0x464c36`. [Carriers](carriers.md#the-request). |
| `0x42b4bc` | `Road_RequestBoatman` | The same search for a water road (the flag item's `+4` bit of the direction clear): it asks for a settler of job 1 and, failing that, for a settler of job 21 and a unit of good 8 (the count at `+0x16` of the storage item). Not ported. | inferred | game | Called by `0x464c36`. [Carriers](carriers.md#the-request). |
| `0x43ad78` | `Settler_WalkRoad` | State 3: a carrier on its road. When the timer has run out it steps from a flag in the direction of its heading or from a road node in the road's other direction, through `0x43b788` and its siblings (the entries of the table at `0x43b508`); a step that would end at a flag is its last, and a carrier that carries a good, or finds a good waiting for its road at that flag, steps onto the flag. On a flag it delivers a good it carries to the flag's own destination (`0x43c45e`, state 0xe) or swaps goods with the flag (`0x43af07`). | confirmed | game | Entry 3 of `0x42fca1`. [Carriers](carriers.md#the-carriers-walk), [Goods at a flag](goods.md#the-carriers-exchange-at-a-flag). |
| `0x43c4ea` | `Settler_DeliverGood` | State 0xe (entered at `0x43c45e`): the carrier on a building's flag. When the timer has run out, a good carried gains `0x0f` in byte 8 or 9 of the building's item and, on a carry out of the byte, one in the building's storage word for the good (at most 50000); then the frame is turned round and, at the next run-out, the carrier takes state 3 again. | confirmed | game | Entry 0xe of `0x42fca1`. [Goods at a flag](goods.md#the-delivery-into-the-castle). |
| `0x43b788` | `Settler_StepRoadEast` | The step east of `Settler_WalkRoad` (the siblings `0x43b9eb`, `0x43bda1`, `0x43c008`, `0x43c156`, `0x43c29c` step south-east, south, west, north-west, north): the carrier next to the flag at the end of its road takes state 0x42; a free node is stepped to as in state 2; an occupied one is marked in the heading (the direction less 6) and tried again, or, on a step east, south-east, or south, swapped with a settler coming the other way. | confirmed | game | Entries of the table at `0x43b508`. [Carriers](carriers.md#the-carriers-walk). |
| `0x42ff81` | `Settler_CarrierWaits` | State 0x42: a carrier waits next to a flag at the end of its road. A good waiting at the near flag or at the far end of the road sends the carrier on (state 0x43, which `0x43001a` runs at once, then state 3); else the item does not change. In the state `0x43` part (`0x43001a`), where its node's settler word is 0, it clears byte 1 of record B of the node and takes the node. | confirmed | game | Entry 0x42 of `0x42fca1`. [Carriers](carriers.md#the-carriers-walk), [Goods at a flag](goods.md#the-carrier-told-to-fetch). |
| `0x42ca7a` | `Route_NextGeneration` | Counts the generation (`+0x26e`) that marks the flags a search has reached; when it wraps, clears the mark of every `Table70` item. | confirmed | game | Called by the searches and by `0x42965c`, `0x45d3ea`, `0x46414d`. |
| `0x42fc81` | `Settler_Dispatch` | Runs the turn of the settler at the item pointer: jumps through the table at `0x42fca1` (8 bytes an entry) by the state byte (item `+0xa`). | confirmed | game | Called by `0x42f3d9` and `0x42f512`. |
| `0x438eeb` | `Settler_WaitInStorage` | State 1: a settler in a storage enters its slot in the storage's list of waiting settlers by its job (`+0x42` plus twice the job); the knights' jobs take their own branches. | confirmed | game | Entry 1 of `0x42fca1`. |
| `0x43f17a` | `Settler_StandByCastle` | State 10: the first settler of the castle adds the elapsed units times 128 to the castle's build progress (`+0xc`); when it wraps the castle stands (type byte bit 7 off) and the settler takes state 12. | confirmed | game | Entry 10 of `0x42fca1`. |
| `0x43dd4a` | `Settler_LeaveBuilding` | State 15: a settler of a storage steps south-east onto the building's flag when that node and the door node are free, takes the time of the step times the door's speed, and waits it out in state 5. | confirmed | game | Entry 15 of `0x42fca1`. |
| `0x43dceb` | `Settler_WaitStep` | State 5: takes the elapsed units off the timer (`+2`) and goes to the state in `+0xf` when they were more than the time left. | confirmed | game | Entry 5 of `0x42fca1`. |
| `0x4398ea` | `Settler_Walk` | State 2: when the timer has run out, chooses the next direction (the road's other direction on a road node, the route search's first direction on a flag), steps through `0x43a64c` and its five siblings, and enters the building at the flag it walks to. | confirmed | game | Entry 2 of `0x42fca1`. [Settlers](settlers.md#the-walk). |
| `0x43a64c` | `Settler_StepEast` | The step east of `Settler_Walk` (the siblings `0x43a7fd`, `0x43a9b0`, `0x43ab63`, `0x43abf9`, `0x43ac8b` step south-east, south, west, north-west, north) with the rest of the walk's turn merged in. | confirmed | game | Entries of the table at `0x43a2d8` and `0x43a3bd`. |
| `0x43c96b` | `Settler_PassDoor` | State 4: waits out the step through a building's door, then runs the job's start routine through the table at `0x43ca54` (8 bytes an entry) when the building's status bit 5 is clear; else the settler takes state 0x19. | confirmed | game | Entry 4 of `0x42fca1`. [Woodcutting](woodcutting.md#the-start-at-the-hut). |
| `0x43cf15` | `Woodcutter_Start` | The start routine of job 5, entry 5 of `0x43ca54`: where the settler is not sent home (mode `0xfe`) it leaves the hut's node and takes state 0x12; sent home, it goes back into the storage's list (state 1). | confirmed | game | Entry of `0x43ca54` for job 5. |
| `0x438b8c` | `Woodcutter_SearchTree` | State 0x12: when the timer has run out, draws a node of the spiral table's entries 1 to 128 around the hut until an object code from 8 to 0x17 stands on it, adding 400 to the timer for each miss until that wraps; on a hit sets the vector to the tree and steps to the flag as state 7 does. | confirmed | game | Entry 0x12 of `0x42fca1`; draws through `0x44257a`. |
| `0x43e036` | `Settler_StepToFlag` | State 7: the step from a building's node onto its flag (south-east) when the nodes are free; the time of the step is scaled by the door's speed and the settler waits it out in state 5. | confirmed | game | Entry 7 of `0x42fca1`. |
| `0x436c38` | `Settler_FreeWalk` | State 0x10: the walk along a vector of east and south steps kept in bytes 11 and 12 of the settler item; each step takes the first direction of the row `0x4388ff` names for the vector's heading class, and the arrival (vector 0, byte 15 equal 8) goes to `0x437ddd`. | confirmed | game | Entry 0x10 of `0x42fca1`. |
| `0x4388ff` | `Settler_WalkRows` | Data: 8 bytes for each heading class of the free walk, the directions to try in order, each as 4 times its number plus 4. | confirmed | game | Read by `0x436c38`. |
| `0x437ddd` | `Settler_FreeWalkArrived` | The arrival of the free walk: jumps by the job to the job's end of the walk; for job 5 to `0x437f53` (at the tree: start cutting; back at the flag: put the log on the flag and enter the hut). | confirmed | game | Reached from `0x436c38`. |
| `0x438a9e` | `Woodcutter_Cut` | State 0x11: each time the timer runs out writes the next code of the felled tree (`0x5d` to `0x66`) into the object byte and sets the frame; after the fifth the settler walks back with the log (byte 13 = `0x80`, state 0x10). | confirmed | game | Entry 0x11 of `0x42fca1`. |
| `0x436b46` | `Flag_PutGood` | Puts a good (the byte in the first register) on the first empty one of the 8 places of the flag the settler stands on (item bytes `0xc` to `0x13`), clears the place's word at `0x14` plus twice the place, sets bit 7 of byte 4 of the flag item, and counts the good up in the player block. | confirmed | game | Called by `0x437ffd`. |
| `0x43cd06` | `Settler_EnterStorage` | The routine the job entries of `0x43ca54` (all but those of the jobs 4 and 21) go on at for the mode `0xfe`: clears the node's settler word and puts the settler into the storage of the building it stands in (state 1, mode 0, word 12 0, storage slot as bytes 14 and 15). | confirmed | game | Jump target of the job entries of `0x43ca54`. [Settlers](settlers.md#walking-home). |
| `0x45d09b` | `Storage_Pick` | The walk home's choice of a storage: a breadth-first search over the flags (directions 5 to 0), from the flag a settler stands on, for the nearest flag whose item has bit 7 in byte `0x42`; the result is the word at 6 of the building at that flag (its flag slot), or -1. | confirmed | game | Called by `Settler_Walk` (`0x4398ea`) at `0x439b75`. [Settlers](settlers.md#walking-home). |
| `0x43c7c2` | `Settler_EnterBuilding` | The step from a flag into the building north-west of it, and the turn of state 6 (`0x43c7ca`): takes the node when no settler stands on it, the step's time as timer, the part of it the door's speed leaves in word 12, and state 4; else frame `0x55` and state 6. | confirmed | game | Entry 6 of `0x42fca1`. |
| `0x42d945` | `Building_HandleType2` | The handler of the building type 2, the woodcutter's hut: asks for a settler of job 5 when bits 2, 6, and 7 of its status are clear. | confirmed | game | Entry 2 of `0x42cd1d`. |
| `0x42e0b8` | `Building_HandleType1` | The handler of the building type 1: asks for a settler of job 11, whose work (`0x433f71`, state 0x1f) looks for a node with no object and no road by the water. | confirmed | game | Entry 1 of `0x42cd1d`. |
| `0x44257a` | `Rng_StepSettler` | The draw the settlers' routines use: the same step as `Rng_Step`, the new first word left in item word `+0x14` of the EDI block. | confirmed | game | Called by `0x438b8c` and the other searches. |
| `0x428d10` | `Map_ObjectPass` | Every 20 clock units takes the next nodes of a cursor that moves 23 columns on (a row down where it wraps), as many as the map's size gives (`+0x21c`): a lake node with detail may move its detail (one draw); an object code from `0x53` goes through the table at `0x428eb8`, 8 bytes an entry: `0x53` vanishes when a draw's low two bits are 0, `0x5d` to `0x66` become `0x53`, the codes from `0x67` grow or decay. | confirmed | game | Called by `0x4286d8`; draws through `Rng_Step`. [Woodcutting](woodcutting.md#the-object-pass). |
| `0x428a75` | — | Counts down three timers of a player block; the timer at `+0x120` repeats its work once per period that has elapsed. | unknown | game | Called by `0x4286d8`. |
| `0x42a74a` | — | Copies `Clock_ElapsedTotal` and resets it, then calls `0x42a7ac` for each player block. | unknown | game | Called by `0x42965c` while the clock runs. |
| `0x42965c` | — | Phase 32: calls `0x42b18d`, also while the clock is stopped; only while the clock runs, `0x42a3f1`, `0x42a74a`, `0x42abac`, and further routines ([Timing](timing.md#rules-that-count-passes)). | confirmed | game | Entry 0 of the phase table at `0x4292ee`. |
| `0x4293f6` | — | Phases 33 to 48, with `0x42944d`, `0x4294a4`, and `0x4294fb` for the player blocks at `+0x64` to `+0x70`: for a block with bits 7 and 6 of byte 2 set (inferred: an active computer player), draws through `Rng_Step` and may call `0x46986d`; otherwise advances `Sim_Phase` within the same pass ([Timing](timing.md#rules-that-count-passes)). | confirmed | game | Entries 1 to 16 of the phase table at `0x4292ee`; writes `Sim_Phase` at `0x429445`, `0x42949c`, `0x4294f3`, `0x42954a`, `0x429578`. |
| `0x429580` | `Ai_PhaseUpdate` | Phase-table entry: runs `Ai_UpdatePlayer` when the player block at `+0x64` has bits 7 and 6 of byte 2 set. | confirmed | game | Entry of the phase table at `0x42923c`. |
| `0x42a3f1` | — | Counts the player blocks with bit 6 of byte 2 set, sums their `+0x112`, compares the count with `+0x48`, then scales the sum and calls `0x42a567`. | unknown | game | Called by `0x42965c`. |
| `0x42a567` | — | Scales a player's `+0x112` by `+0x4a` and adds `+0x58`; role open. | unknown | game | Called by `0x42a3f1`. |
| `0x42b1ce` | — | Writes player `+0x188` (sum of `+0x17c` and `+0x180`, capped) and `+0x184` (a ratio to `+0x4c` scaled by `+0x5a`, plus `0x400`, adjusted by `+0x15e`). | unknown | game | Called by `0x42b18d`. |
| `0x42e7e0` | `Building_HandleCastle` | The handler of the building type 0x18, the castle (knights and defence). Not ported. Entry inferred. | inferred | game | Entry 0x18 of `0x42cd1d` (jump at `0x42cddd`); follows a `ret`. |
| `0x43013b` | — | A soldier's work at a military building (code to `0x4312ed`). Entry inferred. | inferred | game | Entry 0x34 of `0x42fca1` (jump at `0x42fe41`). |
| `0x434946` | `Settler_ClaimNode` | State 0x1c: takes the node it stands on when no other settler stands there (frame `0x52`, no timer, mode 0) and goes on as state 0x19; else waits. Entry inferred. | confirmed | game | Entry 0x1c of `0x42fca1`. [Settlers](settlers.md#walking-home). |
| `0x4350f8` | — | State 0x1a, the boatman's (water roads). Not ported. Entry inferred. | unknown | game | Entry 0x1a of `0x42fca1`. |
| `0x4353ef` | `Settler_SearchFlag` | State 0x19: when the timer has run out, searches the spiral table (entries 1 to 258) for the nearest flag of the settler's player with a road and sets the vector to it; with none, a random offset. Entry inferred. | confirmed | game | Entry 0x19 of `0x42fca1`. [Settlers](settlers.md#walking-home). |
| `0x4385ab` | `Settler_ArriveHome` | The generic arrival at the end of a free walk: on a flag of its player with a road the settler starts the walk to a storage (state 2, mode `0xfe`); elsewhere searches again (state 0x19). Entry inferred. | confirmed | game | Jumps from `Settler_ArrivalByJob` and the jobs' branches; follows a `ret`. [Settlers](settlers.md#walking-home). |
| `0x43cc5f` | `Settler_EnterStorageJob21` | The mode `0xfe` entry of job 21 into a storage; also counts the settler in word `0x40` of the storage item. Not ported. Entry inferred. | inferred | game | Job 21 entry of `0x43ca54` (jump at `0x43cafc`); follows a `ret`. |
| `0x43ceea` | `Settler_EnterStorageJob4` | The mode `0xfe` entry of job 4 into a storage. Not ported. Entry inferred. | inferred | game | Job 4 entry of `0x43ca54` (jump at `0x43ca74`); follows a `ret`. |
| `0x4457e6` | — | Stores default fractions in the player block (around `+0x140` to `+0x150` and `-0x80`); last of four siblings `0x445716`, `0x445752`, `0x44579c`; perhaps the food and goods distribution. | unknown | game | Only caller is `0x42055e`, in `0x420545`; [M5](../milestones/m5-food.md). |
| `0x462d6d` | `Good_Kinds` | Data: 32 bytes, for each good the byte of the destination building's fill a lost good counts against (0 byte 8, 1 byte 9). | confirmed | game | Read at `0x462cc2` (`Good_ReleaseRequest`); ported as `GoodKinds`. |
| `0x464117` | `Good_DestinationClasses` | Data: two bytes for each good, a class and the offset of a request word; class `0xff` sends the good to storage at once. | confirmed | game | Read at `0x46440e` and `0x4644ab` in `Table70_Pass`. [Goods](goods.md). |
| `0x475244` | `Ai_EvaluateAction` | The evaluator that the actions of `Ai_RunActions` share. | inferred | game | Called by eight of the nine actions of `0x4754b0`. |
| `0x4754b0` | `Ai_RunActions` | Dispatches the nine computer-opponent actions `0x47556e`, `0x4756ed`, `0x475950`, `0x475a7e`, `0x475c95`, `0x475e14`, `0x475ef6`, `0x475fd5`, `0x4760b4`; calls `Ai_RecordGoal`. | inferred | game | Only caller is `Ai_UpdatePlayer`. |
| `0x47556e` | — | An action of `Ai_RunActions`. | unknown | game | Only caller is `Ai_RunActions`; calls `Ai_EvaluateAction`. |
| `0x4756ed` | — | An action of `Ai_RunActions`. | unknown | game | Only caller is `Ai_RunActions`; calls `Ai_EvaluateAction`. |
| `0x475950` | — | An action of `Ai_RunActions`; the only one that does not call `Ai_EvaluateAction`. | unknown | game | Only caller is `Ai_RunActions`. |
| `0x475a7e` | — | An action of `Ai_RunActions`. | unknown | game | Only caller is `Ai_RunActions`; calls `Ai_EvaluateAction`. |
| `0x475c95` | — | An action of `Ai_RunActions`. | unknown | game | Only caller is `Ai_RunActions`; calls `Ai_EvaluateAction`. |
| `0x475e14` | — | An action of `Ai_RunActions`. | unknown | game | Only caller is `Ai_RunActions`; calls `Ai_EvaluateAction`. |
| `0x475ef6` | — | An action of `Ai_RunActions`. | unknown | game | Only caller is `Ai_RunActions`; calls `Ai_EvaluateAction`. |
| `0x475fd5` | — | An action of `Ai_RunActions`. | unknown | game | Only caller is `Ai_RunActions`; calls `Ai_EvaluateAction`. |
| `0x4760b4` | — | An action of `Ai_RunActions`. | unknown | game | Only caller is `Ai_RunActions`; calls `Ai_EvaluateAction`. |
| `0x47619e` | `Ai_ChooseGoal` | Tests about 25 goal bits one by one through the goal conditions `0x476af1` to `0x47895d`, each hit recorded through `Ai_RecordGoal`; works on arrays at player `+0x1dc` to `+0x33c`. | inferred | game | Only caller is `Ai_UpdatePlayer`. |
| `0x476529` | `Ai_RecordGoal` | Records the chosen goal. | inferred | game | Called by `Ai_ChooseGoal` and `Ai_RunActions`. |
| `0x47661b` | — | An evaluator that eleven goal conditions of `Ai_ChooseGoal` share; clamps a value to 3999 at `0x47664e`. | unknown | game | Called by `0x4772f7`, `0x47872c`, and nine others. |
| `0x476862` | — | An evaluator that five goal conditions of `Ai_ChooseGoal` share. | unknown | game | Called by `0x476b6b`, `0x476caf`, `0x476eb4`, `0x476f68`, `0x477b61`. |
| `0x4769ed` | — | An evaluator that five goal conditions of `Ai_ChooseGoal` share. | unknown | game | Called by `0x477073`, `0x477114`, `0x4771b5`, `0x477256`, `0x478823`. |
| `0x476af1` | — | The first goal condition of `Ai_ChooseGoal`. | unknown | game | Only caller is `Ai_ChooseGoal`. |
| `0x47820f` | — | A goal condition of `Ai_ChooseGoal`; clamps a value to 3999 at `0x478294`. | unknown | game | Only caller is `Ai_ChooseGoal`. |
| `0x47849d` | — | A goal condition of `Ai_ChooseGoal`. | unknown | game | Only caller is `Ai_ChooseGoal`. |
| `0x47872c` | — | A goal condition of `Ai_ChooseGoal`. | unknown | game | Only caller is `Ai_ChooseGoal`; calls `0x47661b`. |
| `0x47895d` | — | The last and largest goal condition of `Ai_ChooseGoal`. | unknown | game | Only caller is `Ai_ChooseGoal`. |
| `0x478f2b` | `Ai_RunSearches` | Clears the scratch arrays and branches on the opponent kind at player `+0x101` to the map searches `0x479293` to `0x479ade`, handing them `Ai_ObjectWeights`. | inferred | game | Only caller is `Ai_UpdatePlayer`. |
| `0x479213` | `Ai_ObjectWeights` | Data: 128 bytes, inferred one per map object code, that `Ai_RunSearches` puts at EDI `+0x30` before a map search. | inferred | game | Loaded at `0x479053`, `0x4790ac`, `0x479105`, `0x47914f`, `0x479196`, `0x4791ec`. |
| `0x479293` | — | A map search of `Ai_RunSearches`; calls itself. | unknown | game | Called by `Ai_RunSearches`. |
| `0x479403` | — | A map search of `Ai_RunSearches`; calls itself. | unknown | game | Called by `Ai_RunSearches`. |
| `0x47958c` | — | A map search of `Ai_RunSearches`; calls itself. | unknown | game | Called by `Ai_RunSearches`. |
| `0x4796f1` | — | A map search of `Ai_RunSearches`; calls itself. | unknown | game | Called by `Ai_RunSearches`. |
| `0x47978c` | — | A map search of `Ai_RunSearches`; calls itself. | unknown | game | Called by `Ai_RunSearches` at `0x479066`. |
| `0x4798e0` | — | A map search of `Ai_RunSearches`. | unknown | game | Called by `Ai_RunSearches`. |
| `0x479a9a` | — | A short step (34 bytes) after a map search of `Ai_RunSearches`. | unknown | game | Called by `Ai_RunSearches` at `0x47906b`. |
| `0x479abc` | — | A short step (34 bytes) of `Ai_RunSearches`. | unknown | game | Called by `Ai_RunSearches`. |
| `0x479ade` | — | A map search of `Ai_RunSearches`. | unknown | game | Called by `Ai_RunSearches`. |

| Address | Name | Role | Status | Layer |
| --- | --- | --- | --- | --- |
| `0x42a313` | — | Data: byte table of steps read by `0x42965c` (first of three, with `0x42a35d` and `0x42a3a7`). | inferred | game |
| `0x42a35d` | — | Data: second byte table of steps read by `0x42965c`. | inferred | game |
| `0x42a3a7` | — | Data: third byte table of steps read by `0x42965c`. | inferred | game |
| `0x437dfb` | `Settler_ArrivalByJob` | Data: the 8-byte jumps by job to each job's end of the free walk. | confirmed | game |
| `0x43c92b` | `Building_DoorSpeeds` | Data: one byte for each building type, the door speed (xor `0x1f` scales a step time). | confirmed | game |
| `0x43f248` | `Settler_StepTimes` | Data: 16-bit words, the time of a step for each step shape. | confirmed | game |

Sites:

- `0x428d6e`: the lake detail of a node: one draw, may grow below 10, moves to a lake neighbour (in `Map_ObjectPass`).
- `0x42bbea`: the request clears the job bits of a jobless settler (`& 0x83`) to make it a carrier; no tool taken (in the Ghidra function `0x42b4bc`, `Road_RequestBoatman`).
- `0x42fc88`: the jump by the state byte into the table at `0x42fca1` (in `Settler_Dispatch`).
- `0x4354b8`: forms the owner bits of the settler's player; `0x43560c`: the random choice where no flag is near; `0x435821`: a knight takes state 0x35 in place of the free walk (in `Settler_SearchFlag`, `0x4353ef`).
- `0x436e96`: the step from the memory row; `0x437072`: the choice from the vector's heading class; `0x4375ee`: the try of the other five directions (in `Settler_FreeWalk`).
- `0x437f8f`: the woodcutter at the end of its walk compares the node's object code with 8 and `0x18` (a tree); `0x438011`: puts the log (good 7) on the flag through `Flag_PutGood` (in `Settler_FreeWalkArrived`).
- `0x438c11`: compares the drawn node's object code with 8 and `0x18` (in `Woodcutter_SearchTree`).
- `0x43910b`, `0x4391eb`, `0x4392cb`, `0x4393ab`: the knights of the jobs 22 to 25 draw for promotion (limits 4000, 2000, 1000, 500), reached through the jumps by job at `0x43904b` (in `Settler_WaitInStorage`).
- `0x439985`: the step against a chain of blocked settlers; `0x43a24e`: a settler bound for a storage that finds no road on takes state 0x19 (in `Settler_Walk`).
- `0x43a6c1`: two settlers that block each other pass; `0x43a791`: a blocked step marks the heading and waits `0x7f` (in `Settler_StepEast`).
- `0x43aea4`: a carrier with byte 15 negative is sent home (not ported); `0x43b410`: the end of the exchange of goods at a flag that starts at `0x43af07` (in `Settler_WalkRoad`).
- `0x43c9cf`: a settler entering a building whose link is 0 takes state 0x19 (in `Settler_PassDoor`).
- `0x46427c`, `0x464364` to `0x46444c`, `0x464539`, `0x464aa8`: in the goods part of a flag's turn, the thermometer of goods per direction (kept at `+0x218` to `+0x21b`); the destination of a good with none; the direction chosen from a register's leftover when some road holds a carrier and no storage was reached; the start of the direction search (in `Table70_Pass`).
- `0x4650b9`: the end of the loop over a flag's places, the last instruction of the goods turn (in `Flag_VisitRoads`).
- `0x47664e`: clamps a value to 3999 (`0xf9f`), which the code scales as a multiplier, not an archive entry (in `0x47661b`). The same clamp is at `0x478294` (in `0x47820f`).

### Random generator and map start

Note: [Map start](map-start.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x466ade` | `Rng_Step` | Advances the random generator by one step. | confirmed | game | Reads `Rng_State`. |
| `0x41ee3b` | `FreeGame_NewSeed` | Draws three values as the free-game seed, then falls through into `Game_Start`. Its only entry is the jump at `0x41e701` at program start. | confirmed | game | Calls `Rng_Step`. |
| `0x4692d4` | `FreeGame_RandomizeOpponents` | Seeds the generator from the masked free-game seed and draws the settings of the computer players. | confirmed | game | Called by `FreeGame_NewSeed` and `FreeGame_OpenScreen`. |
| `0x4692aa` | `FreeGame_OpenScreen` | Action 12: opens the free-game page and calls `FreeGame_RandomizeOpponents`; draws no new seed. Entry inferred. | inferred | game | Slot `0x4681a8`. |

Sites:

- `0x4425a1`, `0x47a374`: the generator step, inline. `0x47a374` runs in `Sound_Play` on the timer
  thread.
- `0x467831`: the seed is shown as four 12-bit groups.
- `0x467ee1`: edited groups are written back into the seed.
- `0x468b3d`: a free game masks the seed words into the map seed.
- `0x4688f4`: a mission takes the map seed from its record.
- `0x41eeb9`: the game start calls `MapGen_Generate`.
- `0x466b93`: the return of `Rng_Step`; the harness runs one step to it.

### Map generation

Notes: [Map start](map-start.md), [Map form](map-form.md).

`Game_Start` calls `Map_SetGeometry`, then `MapGen_InitParams`, then `MapGen_Generate`. Passes marked
with an asterisk draw from the random generator. `MapGen_StepProgress` runs between the passes and draws
nothing. The call order of `MapGen_Generate`:

1. Copy `Map_Seed` into `Rng_State`, then `Rng_Step` twice.
2. `MapGen_ResetProgress`, `MapGen_ClearMap`, `MapGen_SeedHeights`\*, `MapGen_SubdivideHeights`\*,
   `MapGen_LimitSlopes`, `MapGen_FillLakes`\*, `MapGen_LowerToWaterLevel`, `MapGen_ClassifyTerrain`,
   `MapGen_KeepMainLand`, `MapGen_ScaleHeights`.
3. `MapGen_PlaceTerrainAndObjects`\*, in this order: `MapGen_GradeWaterDepth`, `MapGen_SetShoreTerrain`,
   `MapGen_PlacePatches`\*, `MapGen_BlendPatchEdges`, `MapGen_MarkPeaks`, the 15 object scatter passes
   `0x424175` to `0x4246f4`\*, `MapGen_PlaceDeposits3`\*, `MapGen_PlaceDeposits2`\*,
   `MapGen_PlaceDeposits1`\*, `MapGen_PlaceDeposits4`\*, `MapGen_ResolveObjectOverlap`.
4. `MapGen_MarkWaterAdjacent`, `MapGen_CountResource1`, `Minimap_Build`; then set `Game_Flags` bit 5.

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x42195c` | `Map_SetGeometry` | Derives the map masks, row stride, tile count, and neighbor steps from `Map_Width` and `Map_Height`; a tile is two 4-byte records, the second 4 times the columns after the first. | confirmed | game | Called by `Game_Start`. |
| `0x421530` | `MapGen_InitParams` | Sets the water level (20) and the lake radius (14) and clears other generator fields. | inferred | game | Called by `Game_Start`. Only the first two stores (water level, lake radius) are ported and emulated. |
| `0x4216e9` | `MapGen_Generate` | Seeds the generator from `Map_Seed` and runs the generation passes. | confirmed | game | Called by `Game_Start` at `0x41eeb9`. |
| `0x4218ca` | `MapGen_ResetProgress` | Zeroes the progress counter. | confirmed | game | Called by `MapGen_Generate`. |
| `0x4218d8` | `MapGen_StepProgress` | Draws segments of the progress bar. | inferred | game | Called between the passes. |
| `0x421d3a` | `MapGen_ClearMap` | Zeroes the map buffer. | confirmed | game | Pass. |
| `0x421d71` | `MapGen_SeedHeights` | Sets one random height on every 16th column and row. | confirmed | game | Pass; calls `Rng_Step`. |
| `0x421e75` | `MapGen_SubdivideHeights` | Fills the heights by midpoint subdivision from step 16 down to 1. | confirmed | game | Pass; calls `Rng_Step`. |
| `0x4220ec` | `MapGen_DisplaceMidpoint` | Sets one midpoint height from the mean of two heights and a random offset. | confirmed | game | Called by `MapGen_SubdivideHeights`. |
| `0x42215a` | `MapGen_LimitSlopes` | Repeats until no neighbor differs by more than 32 in height. | confirmed | game | Pass. |
| `0x42223b`, `0x4222ad` | `MapGen_LimitSlopeStepA`, `MapGen_LimitSlopeStepB` | One neighbor correction; the two routines are identical copies. | confirmed | game | Called by `MapGen_LimitSlopes`. |
| `0x422a0e` | `MapGen_FillLakes` | Floods each level up to the water level, then turns the lake marks into water and shore. | confirmed | game | Pass; calls `Rng_Step`. |
| `0x422b72` | `MapGen_FloodLevel` | Runs `MapGen_GrowLake` on each tile at the current level. | confirmed | game | Called by `MapGen_FillLakes`. |
| `0x422bf3` | `MapGen_GrowLake` | Marks a lake around a low tile and grows it ring by ring up to the lake radius. | confirmed | game | Called by `MapGen_FloodLevel`. |
| `0x423041` | `MapGen_GrowLakeTile` | Adds one tile to a growing lake. | confirmed | game | Called by `MapGen_GrowLake`. |
| `0x42231f` | `MapGen_LowerToWaterLevel` | Lowers every height so that water is 0 and the shore is 1. | confirmed | game | Pass. |
| `0x422857` | `MapGen_ClassifyTerrain` | Sets the terrain of each triangle from the sum of its corner heights. | confirmed | game | Pass. |
| `0x42297f` | `MapGen_TerrainFromHeightSum` | Maps a height sum to a terrain code. | confirmed | game | Called by `MapGen_ClassifyTerrain`. |
| `0x4223de` | `MapGen_KeepMainLand` | Keeps the first land mass that covers a quarter of the map and floods the rest. | confirmed | game | Pass. |
| `0x42280a` | `MapGen_ClearByte3` | Zeroes byte 3 of all tiles. | confirmed | game | Called by `MapGen_KeepMainLand`. |
| `0x42237d` | `MapGen_ScaleHeights` | Scales the heights from 0 to 250 down to 0 to 32. | confirmed | game | Pass. |
| `0x42336d` | `MapGen_PlaceTerrainAndObjects` | Runs the terrain detail, object, and deposit passes in a fixed order. | confirmed | game | Pass. |
| `0x4234bd` | `MapGen_GradeWaterDepth` | Grades water terrain by distance to the shore. | confirmed | game | Called by `MapGen_PlaceTerrainAndObjects`. |
| `0x423536` | `MapGen_SetShoreTerrain` | Sets the shore terrain. | confirmed | game | Called by `MapGen_PlaceTerrainAndObjects`. |
| `0x42355f` | `MapGen_ReplaceTerrainNear` | Replaces one terrain code by another where a third is next to it. | confirmed | game | Called by the terrain passes. |
| `0x423789` | `MapGen_HasTerrainNearUpper` | Tests the neighborhood of an upper triangle for a terrain code. | confirmed | game | Called by `MapGen_ReplaceTerrainNear`. |
| `0x4238b2` | `MapGen_HasTerrainNearLower` | The same for a lower triangle. | inferred | game | Called by `MapGen_ReplaceTerrainNear`. |
| `0x4239db` | `MapGen_PlacePatches` | Places terrain patches at random positions. | confirmed | game | Calls `MapGen_RandomPos`. |
| `0x423aa3` | `MapGen_RandomPos` | Draws a random map position. | confirmed | game | Calls `Rng_Step`. |
| `0x423b30` | `MapGen_SpiralPos` | Returns the position at an index of `Map_SpiralTable` around a center. | confirmed | game | Called by the patch and deposit passes. |
| `0x423c10`, `0x423cc7` | `MapGen_CanPatchLower`, `MapGen_CanPatchUpper` | Test whether a triangle can take a patch. | confirmed | game | Called by `MapGen_PlacePatches`. |
| `0x423d74` | `MapGen_BlendPatchEdges` | Blends the edges of the patches. | confirmed | game | Calls `MapGen_ReplaceTerrainNear`. |
| `0x423e6a` | `MapGen_ResetTypes7To9` | Sets terrain codes 7 to 9 back to 5. | confirmed | game | Called by `MapGen_BlendPatchEdges`. |
| `0x423fec` | `MapGen_MarkPeaks` | Marks local height maxima with an object. | confirmed | game | Called by `MapGen_PlaceTerrainAndObjects`. |
| `0x424755` | `MapGen_ScatterObjects` | Places clusters of objects on fitting terrain with the parameters a scatter pass set. | confirmed | game | Calls `Rng_Step`. |
| `0x423f07` | `MapGen_RandomSpiralPos` | Draws a random position near a cluster center. | confirmed | game | Calls `Rng_Step`. |
| `0x424802` | `MapGen_FitsTerrain` | Tests whether all triangles around a tile lie in a terrain range. | confirmed | game | Called by the scatter and deposit passes. |
| `0x424a9e` | `MapGen_PlaceDeposits` | Places resource deposits in rings of decreasing amount. | confirmed | game | Calls `Rng_Step`. |
| `0x424be5` | `MapGen_FillDepositRing` | Writes one ring of a deposit into the resource layer. | confirmed | game | Called by `MapGen_PlaceDeposits`. |
| `0x424976` | `MapGen_PlaceDeposits1` | Places resource type 1. | confirmed | game | Calls `MapGen_PlaceDeposits`. |
| `0x4249bc` | `MapGen_PlaceDeposits2` | Places resource type 2. | confirmed | game | Calls `MapGen_PlaceDeposits`. |
| `0x424a03` | `MapGen_PlaceDeposits3` | Places resource type 3. | confirmed | game | Calls `MapGen_PlaceDeposits`. |
| `0x424a58` | `MapGen_PlaceDeposits4` | Places resource type 4. | confirmed | game | Calls `MapGen_PlaceDeposits`. |
| `0x424c57` | `MapGen_ResolveObjectOverlap` | Keeps large objects only where their neighbors are free. | inferred | game | Called by `MapGen_PlaceTerrainAndObjects`. |
| `0x4232ef` | `MapGen_MarkWaterAdjacent` | Marks tiles next to water. | confirmed | game | Pass. |
| `0x421841` | `MapGen_CountResource1` | Sums the amounts of resource type 1. | confirmed | game | Pass. |
| `0x421d28` | `Map_ScanWindowTable` | Data: 16-bit words, by map size value, the window of the retry scan (16, 30, 55 buildings for the sizes 1, 2, 3). | confirmed | game | `Map_SetGeometry` copies the entry into `+0x52`; read by `Sim_ClearRetryBits`; read from the executable by `ProgramTables`. |

The 15 object scatter passes `0x424175`, `0x4241e3`, `0x424247`, `0x4242ab`, `0x42430f`, `0x424373`,
`0x4243d7`, `0x42443b`, `0x42449f`, `0x424503`, `0x424567`, `0x4245cb`, `0x42462f`, `0x424693`, and
`0x4246f4` each set the parameters of `MapGen_ScatterObjects` and call it. They have no rows.

Sites:

- `0x424ad8`: `MapGen_PlaceDeposits` tests the resource layer one byte past the tile it writes.
- `0x421546`: the end of the first two stores (water level, lake radius); the emulator harness stops here (in `MapGen_InitParams`).
- `0x421749`, `0x421761`, `0x421774`, `0x421779`, `0x42178c`, `0x42179f`, `0x4217b2`, `0x4217c5`, `0x4217d8`, `0x4217eb`, `0x421803`, `0x421808`: the instructions after the seeding and after the calls of the passes `MapGen_ClearMap` to `MapGen_ScaleHeights`, `MapGen_MarkWaterAdjacent`, and `MapGen_CountResource1`, where the emulator harness and the Windows check read the state; `0x421808` ends the last pass. `0x421757`, `0x42176a`, `0x421782`, `0x421795`, `0x4217a8`, `0x4217bb`, `0x4217ce`, `0x4217e1`, `0x4217f4`: the calls of `MapGen_StepProgress` (in `MapGen_Generate`).
- `0x423372`, `0x423385`, `0x423398`, `0x4233ab`, `0x4233be`, `0x4233d1`, `0x4233d6`, `0x4233e9`, `0x4233ee`, `0x423401`, `0x423406`, `0x423419`, `0x42341e`, `0x423423`, `0x423428`, `0x42343b`, `0x423440`, `0x423445`, `0x42344a`, `0x42344f`, `0x423462`, `0x423475`, `0x423488`, `0x42349b`: the instructions after each pass call, `MapGen_GradeWaterDepth` through the 15 scatter passes to `MapGen_PlaceDeposits4`, where the emulator harness records a step. `0x42337b`, `0x42338e`, `0x4233a1`, `0x4233b4`, `0x4233c7`, `0x4233df`, `0x4233f7`, `0x42340f`, `0x423431`, `0x423458`, `0x42346b`, `0x42347e`, `0x423491`: the calls of `MapGen_StepProgress` (in `MapGen_PlaceTerrainAndObjects`).
- `0x4234a4`, `0x4234b7`: the last two calls of `MapGen_StepProgress`, each after a pass; `0x4234ae`: the instruction after the call of `0x424c57`, the last recorded step end of the emulator (in `MapGen_PlaceTerrainAndObjects`).

### Castle and build sites

How the program judges a node for a flag, a building, or the first castle, and what placing the castle writes.
The rules are in [Build sites](build-sites.md) and [Castle](castle.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x41f1f4` | `Castle_PlaceAtStart` | For one player: when the start position is not negative, copies it to the player block, centers the view, judges the node, and calls `Castle_Place`. Skips every player of a free game. | confirmed | game | Called by `0x41eeeb` once for each player; start positions at state `+0x1e6` to `+0x1ed`. |
| `0x41f6a5` | `Map_BuildNeighborOffsets` | Turns the spiral table into the masked node offsets at state `+0xc4`, 295 of them. | confirmed | game | Called by `Game_Start` and `Game_ResumeLoaded`. |
| `0x442630` | `Castle_PlaceFromClick` | The click action for the castle: judges the node under the cursor and places the castle where the node state is 7 and the site is 5, else asks for the error sound. | confirmed | game | Called by `0x440d23` for build action 5. |
| `0x442704` | `Castle_Place` | Takes the first slots of the storage, building, and flag tables, writes the castle, its flag, the heights, and the start stock, and calls `Castle_CreateSettlers`. | confirmed | game | Called by `Castle_PlaceAtStart`. |
| `0x442f0c` | `Castle_CreateSettlers` | Creates the castle's 20 first settlers one by one and takes their tools from the storage item. | inferred | game | Called by `Castle_Place`. |
| `0x44333d` | `Settler_TakeFromStorage` | Creates one settler of a player from the storage item that holds the fewest. | inferred | game | Called by `Castle_CreateSettlers` and `0x428a75`. |
| `0x442244` | `Flag_PlaceFromButton` | The flag button: judges the node under the cursor and, where the site is not 0 and the node state is 4, 6, or 7, goes on to `Flag_Place`, else to the error report (`0x44c768`). | confirmed | game | Called by `0x440d23`. |
| `0x4422c5` | `Flag_Place` | Takes the first free slot of the flag table, writes the flag bit and the flag object on the node, moves the node's deposit to its neighbors, and stores the slot as the node's link. Where the node state at `+0x100` is 4 (the node holds road bits) it then calls `Road_SplitAtFlag` on the node. | confirmed | game | Called by `Flag_PlaceFromButton` (`0x4422c3`) and twice by `Road_ClickStep` (`0x44426c`, `0x444491`). See [Roads](roads.md#a-flag-on-a-road). |
| `0x45d7ad` | `Table70_Allocate` | Takes the first free slot of the flag table, clears the item, and leaves the slot number in the registers; reports a full table. | confirmed | game | Called by `Flag_Place`, `Castle_Place`, `0x421530`, and `0x449a63`. |
| `0x45da8f` | `Table18_Allocate` | Takes the first free slot of the building table ([Item tables](item-tables.md)). | confirmed | game | Called by `0x421530`, `Castle_Place`, and `0x449a63`. |
| `0x45dd1c` | `Table120_Allocate` | Takes the first free slot of the storage table ([Item tables](item-tables.md)). | confirmed | game | Called by `Castle_Place`. |
| `0x45e121` | `Table16_Allocate` | Takes the first free slot of the settler table ([Item tables](item-tables.md)). | confirmed | game | Called by `0x421530`, `Settler_TakeFromStorage`, and `0x41f2d2`. |
| `0x449f58` | `Flag_MoveDeposit` | Moves the resource deposit of a node that is about to hold a flag to the nodes around it. | confirmed | game | Called by `Flag_Place` and `0x449a63`. |
| `0x449a63` | `Building_Place` | Places a building on the judged node (`+0xfc`, `+0xfe` of the player block): takes a building slot, and a flag slot unless the node state at `+0x100` is 5. It writes the building's object and marks on the node. Where the node state is not 5 it writes a flag on the south-east node as `Flag_Place` does: the object code 1, the flag bit `0x80`, the deposit moved through `Flag_MoveDeposit`, and the new slot as the link. Where the node state is 5 it takes the flag that stands there. | confirmed | game | Two callers, none a player's click: `0x41f2f6` (the start-of-mission placement `0x41f2d2`, which hands it a building of type 2 or 3 with no node-state check) and `0x42d274` (in `Building_Free`, only where the node state is 5, 6, or 7). Where the node state is 6 it ends with a call of `Road_SplitAtFlag` on the south-east node (`0x449f12`). The click that places a building is not recovered. See [Castle and build sites](build-sites.md#placing-a-building). |
| `0x44a0aa` | `Deposit_MergeInto` | Adds a deposit to the byte of one node: into an empty byte, or into one of the same type up to 31, carrying the rest on. | confirmed | game | Called by `Flag_MoveDeposit`. |
| `0x44db26` | `Render_DrawFlag` | Draws a flag (object code 1): the flag sprite, then the road stubs and goods that wait at it. | inferred | game | Called by `Render_DrawRowTiles` for object code 1. |
| `0x4617fd` | `Building_Remove` | Takes a building down: marks it (it burns, `0x42cb2d`), clears the marks of the building on its node and its flag, sends its worker home, releases what waits for it (`0x4312ed`), and takes its flag down where no road leaves it. The castle, the military buildings, and the storage's settlers have branches of their own. Not a placement. | confirmed | game | Called by `0x45ed5f` and `0x4615cf`. See [Removal](removal.md). |
| `0x4615cf` | `Click_Remove` | The remove click: runs the check `0x44b6e6` on the node, takes a flag down through `0x462153` where the check says class 2, a building through `0x4617fd` where it says class 3, and asks for the error sound otherwise. | confirmed | game | Called by the click handler `0x440d23`. See [Removal](removal.md). |
| `0x462d88` | `Click_RemoveRoad` | The click that takes a road down: the check must say class 4 (a node of a road that is no flag), then `0x462e1d` runs; else the error sound. | confirmed | game | Called by the click handler `0x440d23`. See [Removal](removal.md). |
| `0x462153` | `Flag_TakeDown` | Takes a flag down: a flag with two roads joins them into one between the flags at their far ends; the flag then goes as one with no road does (settlers walking to it go home, its goods are counted lost, its slot is freed). | confirmed | game | Called by `0x45ed5f`, `0x4615cf`, and `0x4617fd`. |
| `0x462c8d` | `Good_CountLost` | Counts a lost good back: the goods 14 and 15 against the total of resource 1, and a good with a kind against the byte of the fill of the building at its destination flag. | confirmed | game | Called by `0x462153`, `0x4617fd`, `0x46385b`, and `0x466719`. |
| `0x462bbc`, `0x462bf8`, `0x462ad5` | `Flag_ClearPlayerRequests`, `Player_ClearRequestWords`, `Building_ClearPlayerRequests` | Clear, in the player blocks, the request words that name the flag or building being taken down. Not ported (the engine keeps no player block). | confirmed | game | Called by `0x462153` and `0x4617fd`. |
| `0x4312ed` | `Flag_ReleaseGoods` | Releases what waits for the building at a flag: settlers on their way to it go home, a carrier's good for it loses its destination, goods on any flag headed for it lose destination and direction, and the storage items' request fields are cleared (not ported). | confirmed | game | Called by `0x4617fd`. |
| `0x42cf9d` | `Building_Free` | The end of a burning building: clears the marks of its node, its object and link, and frees its slot; a building with a territory also gives the territory back (not ported). | inferred | game | Called by `0x42cb2d`. Judges the node again with `BuildSite_Evaluate` and calls `Building_Place` at `0x42d274` where the node state is 5, 6, or 7 and the site fits a type taken from the table at `0x42d307`. |
| `0x45d974`, `0x45dc0d`, `0x45e273`, `0x45e012` | `Table70_Free`, `Table18_Free`, `Table16_Free`, `Table120_Free` | Free a slot of the flag, building, settler, and storage tables: clear the slot's bit in the table's bitmap and, at the end of the table, let the count fall past the free slots. | confirmed | game | Called by the teardown routines. |
| `0x43007b` | `Settler_StateDismissed` | State `0x44`: a carrier sent home from where it waited takes its node when no settler stands on it, clears byte 1 of record B of the node, and goes on as state `0x19`. | confirmed | game | State table entry. |
| `0x4300f1` | `Settler_StateRejoined` | State `0x45`: a carrier that waited at the end of a road that was joined takes state `0x43` with the highest road direction of its node as its heading, and runs it. | confirmed | game | State table entry. |
| `0x43bc52` | `Carrier_WaitAtEnd` | The end of a carrier's road: with a road that holds two carriers the carrier counts its turns and after four leaves the road; with a good waiting at the far end it does nothing; else it takes state `0x42`. Taking it, it writes the player (low two bits of the settler's byte 0) with bit 7 set (`0x43bcf0`) to byte 1 of record B of its node and clears the node's settler word. | confirmed | game | Called by the step routines of `0x43ad78` (`0x43b788` and siblings). |
| `0x44b6e6` | `BuildSite_Evaluate` | Judges the node in the player block (`+0xfc`, `+0xfe`) and leaves the node state, the site, and the height at `+0x100` to `+0x102`. | confirmed | game | Called by the click handlers, `Castle_PlaceAtStart`, and the build marker. |
| `0x44ba52` | `BuildSite_EvaluateFromSouthEast` | A copy of the judgment from the south-east node on, also entered from `0x44b63a`. | inferred | game | Called by `BuildSite_Evaluate` and `0x44b63a`. |
| `0x44bd72` | `BuildSite_EvaluateBuilding` | The judgment for a node that holds a building, with its own copy of the conditions. | inferred | game | Called by `BuildSite_Evaluate` and `BuildSite_EvaluateFromSouthEast`. |
| `0x44be72` | `BuildSite_SortTriangle` | Sorts one terrain code into plain land, mountain, or other kind. | confirmed | game | Called by `BuildSite_EvaluateBuilding`. |
| `0x45e375` | `Territory_Update` | Claims the territory around the new building and updates the border marks. | inferred | game | Called by `Castle_Place`. |
| `0x442e8a` | `Castle_StartStockRows` | Data: five rows of 26 start stock numbers, for the levels 0, 10, 20, 30, and 40. | confirmed | game | Read by `Castle_Place` through `+0x162` of the player block. |
| `0x41e640` | `Map_ObjectClassTable` | Data: 128 bytes, the class (size) of each object code; code 1 is class 3 (flag), codes 2 to 4 are classes 4 to 6 (buildings, castle), generated trees and stones classes 1 and 2. | confirmed | game | Stored into `+0xc8` by `0x41dbea`; read from the executable by `ProgramTables`. |
| `0x45e44b` | — | One of the five checks `Territory_Update` runs on the tiles around a position. | unknown | game | Only caller is `Territory_Update`. |
| `0x45e4ca` | — | One of the five checks `Territory_Update` runs on the tiles around a position. | unknown | game | Only caller is `Territory_Update`. |
| `0x45eb4c` | — | One of the five checks `Territory_Update` runs on the tiles around a position; calls `0x45ed5f`. | unknown | game | Only caller is `Territory_Update`. |
| `0x45f154` | — | One of the five checks `Territory_Update` runs on the tiles around a position; reads the map geometry fields `+0x0` to `+0xc`. | unknown | game | Only caller is `Territory_Update`. |
| `0x674a30` | `BuildSite_NeighborTiles` | Data: a list of tile pointers around a node; the build-site checks read the object byte of each; game state `+0x98e`. | inferred | game | Read by `BuildSite_EvaluateFromSouthEast`, `Road_UpdateCursor`, `BuildSite_Evaluate`, `0x44bd72`. |

Sites:

- `0x44e329` draws a finished building by its type through the jump table at `0x44e3aa`. The type `0xe0` of a
  castle reaches the stub at `0x44fd2f`, which stores the object number 178; its sprite is entry 1428.
- `0x45f402` sets two border bits of the new flag from the owners around it; not ported.
- `0x41f2d2`: places a group of 3, 6, or 12 settlers on the start node of a mission start; not recovered further.
- `0x44e62b` to `0x44fd2f`: the building stubs that the jump table at `0x44e3aa` in `0x44e329` reaches; each stores an object number and jumps or calls into `Gfx_DrawObjectSprite` or `Gfx_DrawSpriteBase320`. `0x44e62b` stores object 0xa7 (entry 1417), `0x44e63b` object 0xa8 (entry 1418); `0x44e636` and on to `0x44f928` are the jumps into `Gfx_DrawObjectSprite` for objects 152 to 171 and 174; `0x44e653` tests object byte 9 before `0x44e6c5` draws byte 9 plus 174 plus 320; `0x44e720`, `0x44e86d`, `0x44e9ba`, `0x44eb07` and `0x44e781`, `0x44e8ce`, `0x44ea1b`, `0x44eb68` draw 152 and 153 plus 320; `0x44ecfc`, `0x44f6aa`, `0x44f79a`, `0x44f82d` draw entries 502 to 517; `0x44ee56`, `0x44eeda`, `0x44ef5e`, `0x44efd8`, `0x44f05c`, `0x44f0e0`, `0x44f160`, `0x44f1e3` draw the first byte of a pair of the table `0x44f225` plus 320 (data after the jump at `0x44f220`, code again at `0x44f325`); `0x44f42e` and `0x44f528`, `0x44f5c8`, `0x44f908` are tail jumps for 474 to 481 and 448 to 455; `0x44f3a8` forms 188 plus 0 to 3 and jumps at `0x44f3ae`; `0x44ec2a` jumps with 192; `0x44f933` to `0x44fa5d` store an index of 1 to 23 for the table that `0x44fc87` reads before the call at `0x44fcac`; `0x44fa74` jumps for object 144; `0x44faf3`, `0x44fba7` draw 330 and 328. Source: [Graphics archives](graphics-archives.md#entries-no-routine-reads), [Graphics images](graphics-images.md).
- `0x461b77`: a settler inside the building is sent home: its state byte (`+0xa`) is set to `0x1c` and nothing else (in `Building_Remove`).

### Roads

How the player draws a road between two flags, what the commit writes, and how a road is drawn. The rules are in
[Roads](roads.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x440c24` | `View_HandleClick` | The click handler of a game view: routes a click on the map or on a panel; in road mode the click on a node runs the step at `0x443f62`. | inferred | game | Called by `0x440b01`. |
| `0x440d23` | `View_RunButtonAction` | Runs the action of the button a click hit: the road button (action `0x18`) starts road mode, the flag and castle buttons place theirs. With bit 3 of the view's flag byte at `+1` set (the second pointer button held) the road, flag, and building buttons (actions 1 to 4, 8, `0x11`, `0x12`, `0x17`, and `0x18`) only run `0x4417db`, which toggles bit 6 of byte 0 where bit 1 is set and road mode (bit 7) is off, and place nothing; the building buttons without the bit open the building panels and place nothing. | inferred | game | Called by `0x440c24`. |
| `0x443f62` | `Road_ClickStep` | The click on a node in road mode, inside `0x440c24`: works out the direction from the end of the road to the node, checks it against the mask of allowed directions, takes a step back, extends the road, or ends it at a flag. With bit 3 of the view's flag byte at `+1` set, a click on a node that the check `0x44b6e6` gives a site (and where bit 1 of the player block's byte at `+3` is clear: unrecovered) puts a flag there (`Flag_Place`) and ends the road at it; without the bit a click on a node with road bits is refused. | confirmed | game | Entered by jumps inside `0x440c24`; returns at `0x44477f`. See [Roads](roads.md#a-flag-on-a-road). |
| `0x44b6ce` | `Road_UpdateCursor` | In road mode, leaves at `+0xd0` of the view the mask of the directions in which the road's end may go on, and at `+0xaa` the cursor code for each of the six. It refuses a direction whose node holds an object of class 2 or 4 of the class table. A tree (class 1) does not refuse it. | confirmed | game | Called by `0x440b01`. |
| `0x444e68` | `Road_Commit` | Walks the drawn road from the flag it ends at back to the one it started at, checks the owner of every node and that the triangles beside the road are all land or all water, and joins the two flags' items. | confirmed | game | Called at `0x444534` by the step. |
| `0x4454e7` | `Road_LengthClass` | Turns a road's length into a class 0 to 7 (steps up at 4, 6, 7, 10, 13, 18, and 24) that the commit stores for each end. | confirmed | game | Called by `Road_Commit`. |
| `0x442002` | `Road_Cancel` | Leaves road mode and takes the road drawn so far off the map, from the road's end back, one bit pair for each step counted at `+0xce`. | confirmed | game | Called by `0x440d23`, `0x444534`. |
| `0x462e1d` | `Road_TakeDown` | Takes a road down from a node of it: the two road bits of the node, each followed by `0x463204` to a flag. | confirmed | game | Called by `0x45ed5f` and `0x462d88`. See [Removal](removal.md). |
| `0x463204` | `Road_TakeDownWalk` | Walks from a node along one direction to the flag at the road's end, clearing the road's bits; settlers on the road go home (`0x46385b`), waiting carriers are dismissed (`0x463ef9`), and the flag at the end loses the road. | confirmed | game | Called by `0x462e1d`. |
| `0x46385b` | `Settler_DismissFromRoad` | Sends a settler on a road home (state `0x19`): one on its way to a building or a road frees what it was to take, one that carries a good counts it lost. | confirmed | game | Called by `0x463204`. |
| `0x463a40` | `Road_GatherSettlers` | Walks a road from a flag to the flag at its end, counts its steps, and takes the settlers on it into a list (a waiting carrier takes state `0x45`). It leaves its result in a block of 8 bytes plus the list (`Road_GatherResultA`, `Road_GatherResultB`): the steps, the settlers listed, the slot of the end flag, the direction by which the road leaves that flag. | confirmed | game | Called by `0x462153` and, once for each of the two roads under a new flag, by `Road_SplitAtFlag`. |
| `0x4662e2` | `Road_SplitAtFlag` | Splits the road under a node that just took a flag into two roads: walks both ways to the end flags, keeps the carrier on its way to the end it targets, and links each half through `Road_LinkHalf`. Asks for no carrier. | confirmed | game | Called by `Flag_Place` (`0x44239e`) and `Building_Place` (`0x449f12`). See [Roads](roads.md#a-flag-on-a-road). |
| `0x466719` | `Road_LinkHalf` | Joins the new flag to one end flag of a split road: the road bit, the land bit, the length class, the pointers and the far direction at both ends, the carrier count and mark, with the settlers beyond the class's capacity sent away. | confirmed | game | Called twice by `Road_SplitAtFlag`; calls `Road_LengthClass` and `Good_CountLost`. |
| `0x674adc`, `0x674b04` | `Road_GatherResultA`, `Road_GatherResultB` | Data: the two result blocks of `Road_GatherSettlers` that `Road_SplitAtFlag` fills, one for the lower and one for the higher direction of the node's road. | confirmed | game | Read by `Road_SplitAtFlag` and `Road_LinkHalf`; in `Flag_TakeDown` the same two blocks hold the two roads it joins. |
| `0x463ef9` | `Carrier_DismissWaiting` | Sends the carriers that wait on a node (states `0x42`, `0x43`, `0x45`) home as state `0x44`. | confirmed | game | Called by `0x463204` and `0x463a40`. |
| `0x464008` | `Carrier_TakeWaiting` | A piece of the flag pass that finds a carrier waiting on a node and gives it state `0x45`. | inferred | game | Called by `0x463a40`. |
| `0x444b29` | `Road_WalkEnds` | Finds the two directions of a node's road and runs `0x444c61` on each: a walk of the road to its ends, which the cursor update calls for a road under the cursor when a start-up option is set. | inferred | game | Called by `Road_UpdateCursor` and `Road_TakeDown`. |
| `0x427c09` | `Render_DrawRoadsAndBorders` | For each pair of rows, the passes that draw the roads (and the territory borders where no road lies) of the view's window of nodes. | confirmed | game | Called at `0x427133`. |
| `0x428008` | `Render_DrawRoadEast` | Draws the strip of the road that leaves a node east: the mask 234 plus the height difference, the road texture from the slope and the terrain. | confirmed | game | Called by `0x427d3e`. |
| `0x428105` | `Render_DrawRoadSouthEast` | The same for the road leaving south-east: the mask 243 plus the height difference. | confirmed | game | Called by `0x427e3a`. |
| `0x4281cc` | `Render_DrawRoadSouth` | The same for the road leaving south: the mask 252 plus the height difference. | confirmed | game | Called by `0x427f1f`. |
| `0x428389` | — | Where no road lies and the owners of two neighbors differ, draws the border between them (east); with `0x42846d` and `0x428528` for the other two directions. | inferred | game | Called by `0x427d3e`, `0x427e3a`, `0x427f1f`. |
| `0x444d24` | — | A step of the road walk: moves to the next node and returns 0 at an object code 1, else finds the road bits (`& 0x3f`) to go on. | inferred | game | Called by `0x444c74`; calls `0x444c61`. |
| `0x462acd` | `Road_CarrierCapacities` | Data: 8 bytes, the number of carriers a road of each length class (`0x4454e7`) holds at most. | confirmed | game | Read at `0x46252a` (`Flag_TakeDown`), `0x465189` (`Flag_VisitRoads`), `0x466928`; ported as `RoadCapacities`. |

Sites:

- `0x427dc7`, `0x427eb3`, `0x427f98`: the calls of `Render_DrawRoadEast`, `Render_DrawRoadSouthEast`, and `Render_DrawRoadSouth` (in `0x427d3e`, `0x427e3a`, and `0x427f1f`).
- `0x44426c`, `0x444491`: the calls of `Flag_Place` in `Road_ClickStep` (a click on a road node, a click on an empty node, each with bit 3 of the view's flag byte set); `0x444261`: the call of `Road_WalkEnds` before the first, which refuses the placement where the node's road leads to no flag (the road being drawn).
- `0x444079`, `0x44439b`, `0x444544`, `0x4445b6`, `0x444608`, `0x444657`, `0x4446cb`, `0x4446f6`, `0x444716`, `0x444743`: the sound requests (`Sound_Request`); `0x4445a8`: the call of `0x42f7ea` (in `Road_ClickStep`).
- `0x46358c`: the walk reaches the flag at the road's end and turns the direction round (direction minus 3, modulo 6) for the flag's side; code after the computed jump at `0x463308` that Ghidra left outside the function (in `Road_TakeDownWalk`).

### Save and load

The save game format is in "Save format".

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x450609` | `Save_ConfirmSlot` | Panel action 80: saves the game to the selected slot, or in load mode jumps to `Load_LoadSlot`. Entry inferred. | confirmed | game | Slot `0x44690c` of the table at `0x44668c`. |
| `0x45f755` | `Save_WriteArchiveIndex` | Creates `ARCHIV.DS` in the `[SP]` folder and writes the archive index into it. | confirmed | game | String `[SP]/ARCHIV.DS`. |
| `0x45fa13` | `Save_WriteGame` | Builds record 1 in the save buffer and writes it. | confirmed | game | Save fields at `0x45fbf8` and `0x45fc29`. |
| `0x4600a8` | `Save_WriteMap` | Writes record 2, the map. | inferred | game | Called from `Save_ConfirmSlot` at `0x4507b4`. |
| `0x46010d` | `Save_WriteTable16` | Writes record 3, `Table16`. | confirmed | game | Called at `0x4507bb`. |
| `0x4601f4` | `Save_WriteTable70` | Writes record 4, `Table70`. | confirmed | game | Called at `0x4507c2`. |
| `0x4602db` | `Save_WriteTable18` | Writes record 5, `Table18`. | confirmed | game | Called at `0x4507c9`. |
| `0x4603c2` | `Save_WriteTable120` | Writes record 6, `Table120`. | confirmed | game | Called at `0x4507d0`. |
| `0x46049a` | `Save_WriteRecord` | Writes the save buffer to the open file; the EDI block gives the length. | confirmed | game | Calls `File_Write`. |
| `0x45f7ab` | `Load_LoadSlot` | Opens the save game file of the selected slot, reads the six records, and marks the game as loaded. Entry inferred. | confirmed | game | Jump at `0x450631`. |
| `0x4604ed` | `Load_ReadGame` | Reads record 1 and copies its fields back into the game state. | confirmed | game | Load fields at `0x4606bc` and `0x4606ee`. |
| `0x460bba` | `Load_ReadMap` | Reads record 2. | inferred | game | Called by `Load_LoadSlot`. |
| `0x460c28` | `Load_ReadTable16` | Clears the bitmap of `Table16`, reads record 3, and turns its offsets back into pointers. | confirmed | game | Called by `Load_LoadSlot`. |
| `0x460d5a` | `Load_ReadTable70` | The same for record 4. | confirmed | game | Called by `Load_LoadSlot`. |
| `0x460e8c` | `Load_ReadTable18` | The same for record 5. | confirmed | game | Called by `Load_LoadSlot`. |
| `0x460fbe` | `Load_ReadTable120` | The same for record 6. | confirmed | game | Called by `Load_LoadSlot`. |
| `0x4610e3` | `Load_ReadRecord` | Reads from the open file into the save buffer. | inferred | game | Calls `File_Read`. |
| `0x4614c3` | `Table_ToOffsets16` | Turns the link in each `Table16` item into an offset. | confirmed | game | Called by `Save_WriteTable16`. |
| `0x461548` | `Table_ToPointers16` | Turns the offsets in `Table16` items back into pointers. | confirmed | game | Called by `Save_WriteTable16`, `Load_ReadTable16`. |
| `0x461157` | `Table_ToOffsets70` | Turns the six links in each `Table70` item into offsets. | confirmed | game | Called by `Save_WriteTable70`. |
| `0x461231` | `Table_ToPointers70` | The reverse of `Table_ToOffsets70`. | confirmed | game | Called by `Save_WriteTable70`, `Load_ReadTable70`. |
| `0x46130b` | `Table_ToOffsets18` | Turns the link in each `Table18` item into an offset. | confirmed | game | Called by `Save_WriteTable18`. |
| `0x4613e7` | `Table_ToPointers18` | The reverse of `Table_ToOffsets18`. | confirmed | game | Called by `Save_WriteTable18`, `Load_ReadTable18`. |
| `0x461136` | `Mem_CopyBytes` | Copies a count of bytes between two pointers in the EDI block. | confirmed | game | Called by every save and load routine. |
| `0x407750` | `File_Create` | Opens a file for writing. | inferred | platform | Imports `CreateFileW`; called by the save code. |
| `0x4076d0` | `File_Open` | Opens a file for reading. | inferred | platform | Imports `CreateFileW`; called by `Load_LoadSlot`. |
| `0x407800` | `File_Write` | Writes to the open file. | confirmed | platform | Imports `WriteFile`. |
| `0x407870` | `File_Read` | Reads from the open file. | confirmed | platform | Imports `ReadFile`. |
| `0x4077e0` | `File_Close` | Closes the open file. | confirmed | platform | Imports `CloseHandle`. |

Sites:

- `0x45fbf8`: save writes the game clock. `0x45fc29`: save writes the generator state.
- `0x4606bc`: load reads the game clock. `0x4606ee`: load reads the generator state.
- `0x45074c`: `Save_ConfirmSlot` calls `Save_WriteArchiveIndex`. `0x450769`: it builds the save file
  name. `0x45078d`: it calls `File_Create`. `0x4507ad` to `0x4507d0`: it calls the six save routines in
  order.
- `0x450631`: in load mode, `Save_ConfirmSlot` jumps to `Load_LoadSlot`.

### Files and configuration

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x401090` | — | References the `[SP]` folder prefix. | unknown | platform | String `[SP]`. |

| Address | Name | Role | Status | Layer |
| --- | --- | --- | --- | --- |
| `0x67d614` | `Loca_FileTable` | Table of the language files in `loca/`: an archive and a text file for each of English, German, and French. | inferred | platform |

Sites:

- `0x4015ac`: references the string `game.ini`.
- `0x40fc40`: references the registry key `Control Panel\Mouse`.

### Text files

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x412570` | `Text_LoadLanguage` | Reads the language's text file through `Text_ReadFile`, looks up 224 fixed keys with `Text_Lookup`, and appends a byte of value 255 after each found text. | confirmed | platform | Called by `Platform_RunGame`; strings such as `MENUTEXT1`. |
| `0x410e30` | `Text_ReadFile` | Puts the application-folder prefix before the file name and calls `Text_ParseFile`; answers whether the file opened. | confirmed | platform | String `[AP]/`; called by `Text_LoadLanguage`. |
| `0x410f40` | `Text_ParseFile` | Reads a file line by line and stores each `KEY="VALUE"` line in the key map; skips the other lines. | confirmed | platform | Callers `Text_ReadFile` and `0x416d70`; imports `_wfopen_s`, `fgetc`, `strchr`. |
| `0x4111a0` | `Text_Lookup` | Finds a value text by key in the key map; answers none when the key is absent. | confirmed | platform | Called by `Text_LoadLanguage` and `Platform_RunGame` (key `LANGUAGE`). |

Note: [Text files](text-files.md).

### Game code band

Note: [Game code](game-code.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x47b19c` | `Gfx_BlitTextured` | Draws a mask to the surface, clipped, copying each covered pixel from a raw-image texture that repeats down only (it never wraps within a row), with no blending. | confirmed | game | Called at `0x405b67`, in `Gfx_FillTextured`. Lies inside the band and takes its arguments in registers, not in the EDI block, like `Gfx_BlitSpriteOverColor8`. See [Graphics images](graphics-images.md). |
| `0x47b4f9` | `Gfx_BlitSpriteOverColor8` | Draws a transparent sprite stream, clipped, writing a pixel only where the surface pixel has the palette index 8. Last routine of the game code band. | confirmed | game | Called by `Gfx_SpriteOverColor8`; band comparison; takes its arguments in registers, not in the EDI block, like `Gfx_BlitTextured`. See [Graphics images](graphics-images.md). |

## Structures and data

### Game state block

Offsets into the game state block. Fields of a topic's own scratch area (for example `MapGen_` fields
from `+0x21e` to `+0x237`) are in the dump, not here.

| Offset | Name | Size | Role | Status |
| --- | --- | --- | --- | --- |
| `+0x0` to `+0x20`, `+0x60` | `Map_NeighborSteps` | 4 each (`+0x60`: 2) | The position wrap mask and the steps to the six neighbors and one column left. | confirmed |
| `+0x24` | `Map_Tiles` | 4 | Pointer to the map. | confirmed |
| `+0x28` | `Map_RowStride` | 4 | Bytes per map row: width × 8. | confirmed |
| `+0x2c` | `Map_TileCount` | 4 | Width × height. | confirmed |
| `+0x30` to `+0x42` except `+0x36` | `Map_Shape` | 2 or 4 each | Width bit length, width and height masks, column and row masks, half width and half height. | confirmed |
| `+0x36` | `Map_ResourceLayerOffset` | 4 | Offset from a tile to its byte in the resource layer: width × 4. | confirmed |
| `+0x44`, `+0x46` | `MapGen_WaterLevel`, `MapGen_LakeRadius` | 2 each | 20 and 14. | confirmed |
| `+0x4c` | `Map_Resource1Total` | 4 | Sum of the amounts of resource type 1. | confirmed |
| `+0x64`, `+0x68`, `+0x6c`, `+0x70` | `Player_Blocks` | 4 each | Pointers to the four player blocks of 8628 bytes. The save copies each from 128 bytes before the pointer. | confirmed |
| `+0x74` | `Save_Buffer` | 4 | Pointer to the save buffer. | confirmed |
| `+0x78`, `+0x7c` | `View_Records` | 4 each | Pointers to the two view records, one per player screen; bit 0 of byte 0 marks a view inactive. | confirmed |
| `+0x80` | `Map_SpiralTable` | 4 | Pointer to the position offsets around a center, ordered in rings of 1, 6, 12, and on. | confirmed |
| `+0x8c` | `Minimap_Buffer` | 4 | One byte per tile. | inferred |
| `+0x98`, `+0x9c`, `+0xa0`, `+0xf0` | `Table70_Base`, `Table18_Base`, `Table16_Base`, `Table120_Base` | 4 each | Pointers to the item tables. | confirmed |
| `+0xa4`, `+0xa8`, `+0xac`, `+0xf4` | `Table70_Bitmap`, `Table18_Bitmap`, `Table16_Bitmap`, `Table120_Bitmap` | 4 each | Pointers to their slot bitmaps. | confirmed |
| `+0xd8` | `Archive_Index` | 4 | Pointer to the 160-byte archive index: 16 bytes per slot. | confirmed |
| `+0xe8` | `Sound_Table` | 4 | Pointer to the sound variant table. | inferred |
| `+0x12c` to `+0x137` | `Sound_Channels` | 12 | Four channel timers and the four playing codes. | inferred |
| `+0x17e`, `+0x182` | `Game_NoticeCountdowns` | 4 each | Set to 180000 and 360000 at each start; when one goes negative, a message is posted. | confirmed |
| `+0x186` | `Game_Countdown` | 2 | Set to 6000 at each start and counted down by the elapsed units. | confirmed |
| `+0x188` | `MapGen_Progress` | 2 | Progress bar counter. | confirmed |
| `+0x1c2`, `+0x1c4` | `Map_Width`, `Map_Height` | 2 each | Map columns and rows: 32, doubled by size class. | confirmed |
| `+0x1c8` | `Game_Flags` | 1 | Bit 3: a game runs. Bit 4: start screens. Bit 5: set when the map is ready, at game end, and after a load. Bit 2: archive in save mode (inferred). Bit 6: a game was loaded. | confirmed for bits 3 and 4 |
| `+0x1c9` | `Game_Request` | 1 | Request bits that `Main_Loop` reads and clears: 0 quit, 1 new game, 2 back to the start screens, 3 resume a loaded game. | confirmed |
| `+0x1d6` to `+0x1ed` | `Game_StartParams` | 24 | Per-player start parameters. | confirmed |
| `+0x1ee`, `+0x1f0`, `+0x1f2` | `Map_Seed` | 2 each | The map seed, with the masks applied. | confirmed |
| `+0x1f7` | `Timer_WaitFlag` | 1 | `Main_Loop` waits until it is 0; `Timer_Handler` clears it. | confirmed |
| `+0x1f8` | `Save_Slot` | 2 | The selected save slot; negative when none is selected. | confirmed |
| `+0x1fa` | `Clock_SavedStep` | 4 | The clock step that pause saved. | confirmed |
| `+0x1fe` | `Clock_Step` | 4 | The clock step: 2.0 or 0. | confirmed |
| `+0x202` | `Clock_Game` | 4 | The game clock: fixed point, 16 fraction bits. | confirmed |
| `+0x206` | `Clock_PrevUnits` | 2 | Whole units of the game clock at the previous pass; `Main_ComputeElapsed` sets it to those of the current pass, which later readers in the pass see. | confirmed |
| `+0x208` | `Timer_CallCount` | 2 | Counts the timer calls. | confirmed |
| `+0x20a` | `Frame_LastCallCount` | 2 | `Timer_CallCount` at the previous frame. | confirmed |
| `+0x212`, `+0x214`, `+0x216` | `Rng_State` | 2 each | The random generator's three state words. | confirmed |
| `+0x21c` | `MapGen_PatchCount` | 2 | Base count of patches, clusters, and deposits. | confirmed |
| `+0x240` | `Archive_Result` | 2 | Result code of the last save or load. | confirmed |
| `+0x258` to `+0x266` | `Table70_Count` (`+0x25e`), `Table18_Count` (`+0x260`), `Table16_Count` (`+0x262`), `Table120_Count` (`+0x266`), and capacities | 2 each | Slots in use and slot capacity of the item tables. | confirmed |
| `+0x26c`, `+0x286` | `Sim_Phase`, `Sim_PhaseCount` | 2 each | The phase counter and its wrap value: 33, or 49 when a player's start parameter is from 1 to 11 (`0x42055e`). `Sim_StepPhase` and `0x4293f6` advance the counter, by 1 or more in a pass ([Timing](timing.md#rules-that-count-passes)). | confirmed |
| `+0x284` | `Clock_Elapsed` | 2 | Elapsed whole units of this pass. | confirmed |
| `+0x288` | `Clock_ElapsedTotal` | 2 | Running total of the elapsed units. | confirmed |
| `+0x28a` | — | 2 | The copy of `Clock_ElapsedTotal`: `0x42a74a` copies the total into it (`0x42a757`) before it resets the total; `Game_ResetCounters` zeroes it (`0x425a08`). `0x42a7ac`, which `0x42a74a` then calls for each player block, reads it at `0x42a7d8` and `0x42a872`. | confirmed |
| `+0x30c` | `Memory_SizeClass` | 1 | Low 4 bits: the size class of the game buffer; it caps the free-game map size. | confirmed |
| `+0x320` to `+0x32c` | `Stats_RingIndex` | 2 each | Ring indices and counters of `Stats_RecordHistory`. | confirmed |
| `+0x352` | `Game_Mode` | 2 | 0 and 1 take a start record, 2 is the free game (default). The meanings of 0, 1, 3, and 4 are inferred. | confirmed for the values |
| `+0x356`, `+0x358` | `Mission_Number`, `Mission_Highest` | 2 each | The current and the highest reached mission. | inferred |
| `+0x362` | `FreeGame_SizeClass` | 2 | Free-game map size class, 0 to 3. | inferred |
| `+0x364`, `+0x366`, `+0x368` | `FreeGame_Seed` | 2 each | The free-game seed. | confirmed |
| `+0x36a` to `+0x37d` | `FreeGame_PlayerSettings` | 20 | The free-game player settings. | confirmed |
| `+0x37e` | `Game_ModeFlags` | 1 | Bit 2: second view active (inferred). Bit 5: the panel shows player icons. Bit 6: the second view works on player 1. | confirmed for bits 5 and 6 |

### Player blocks

`Player_Blocks` points to four blocks of 8628 bytes, one per player. Known fields: byte `+2` (bit 6
active and bit 7 computer, both inferred; bit 3 set by `Msg_Post`), the message slots at `+0x1df4`, and
the timed messages at `+0x1f34`. The bytes `-0x54` to `-0x3b` (good g at `-0x55 + g`) hold the transport priorities that
`Player_SetTransportPriorities` writes; the carrier's exchange (`0x43b054`, in `Settler_WalkRoad`) and the direction step of
`Table70_Pass` (`0x46501a`) read them by the good's low 5 bits ([Goods](goods.md#transport-priorities)). The words at `-0x3a`
and `-0x36`, which those reads reach for the goods 27 and 31, are counters that `Castle_CreateSettlers` adds to (inferred:
counts of settlers).

### View records

`View_Records` points to two records, one per player screen. Each holds the screen surface, the panel
state (requested and current page at `+0x1b8` and `+0x1ba`, window at `+0x70`, panel type at `+0x72`),
and a four-slot sound request list at `+0x16`.

### Item tables

The game state holds four tables of fixed-size items. `0x41dbea` lays them out in the game buffer.
Each table has a slot bitmap with one bit per slot, a count of slots in use, and a capacity. The
simulation processes them in slices of 32 entries per phase. What the items are is not confirmed, so
the names state only the item size.

| Table | Item size | Base | Bitmap | Count | Save record |
| --- | --- | --- | --- | --- | --- |
| `Table16` | 16 | `+0xa0` | `+0xac` | `+0x262` | 3 |
| `Table70` | 70 | `+0x98` | `+0xa4` | `+0x25e` | 4 |
| `Table18` | 18 | `+0x9c` | `+0xa8` | `+0x260` | 5 |
| `Table120` | 120 | `+0xf0` | `+0xf4` | `+0x266` | 6 |

Items link to other items by pointer: a `Table16` item to a `Table70` item, a `Table70` item to six
`Table70` items and one `Table70` or `Table18` item, and a `Table18` item to a `Table120` or `Table70`
item.

### Map tiles

The map has 8 bytes per tile. A row holds width × 4 bytes of tile records, then width × 4 bytes in which
every fourth byte is the tile's byte of the resource layer. A tile record has 4 bytes: flags, height,
terrain (one 4-bit code for each of the tile's two triangles), and object. A resource byte holds the
type in its top 3 bits and the amount in its low 5 bits.

### Save format

**Status:** confirmed by the save and load routines, except where marked.

A save game is the file `SAVEn.DS` in the `[SP]` folder, where `n` is the digit of `Save_Slot`. The save
code takes the name from the string `[SP]/SAVE-.DS` at `0x674886` and puts the digit in place of the `-`.
The file holds six records, written and read in a fixed order without a length prefix. Each record's size
follows from fields of record 1.

| Record | Writer | Content | Size in bytes |
| --- | --- | --- | --- |
| 1 | `Save_WriteGame` | Header and the four player blocks. | 34762 |
| 2 | `Save_WriteMap` | The map. | `Map_TileCount` × 8 |
| 3 | `Save_WriteTable16` | Bitmap, then `Table16` items. | 0 if n = 0, else 4 × ceil(n / 32) + 16 × n, n = `Table16_Count` |
| 4 | `Save_WriteTable70` | Bitmap, then `Table70` items. | The same with 70 and `Table70_Count` |
| 5 | `Save_WriteTable18` | Bitmap, then `Table18` items. | The same with 18 and `Table18_Count` |
| 6 | `Save_WriteTable120` | Bitmap, then `Table120` items. | The same with 120 and `Table120_Count` |

The count is the length of the slot range in use, not the number of live items. Links between items are
saved as offsets.

Record 1:

| Save offset | Size | Content |
| --- | --- | --- |
| `0x0` | 250 | Header: single fields copied from the game state block. |
| `0xfa` | 8628 | Player block 1. |
| `0x22ae` | 8628 | Player block 2. |
| `0x4462` | 8628 | Player block 3. |
| `0x6616` | 8628 | Player block 4. |

Header fields with a known role:

| Save offset | Size | State offset | Name |
| --- | --- | --- | --- |
| `0x0` to `0x1e`, `0xce` | 4 each (`0x10`: 2) | `+0x0` to `+0x20`, `+0x10`, `+0x60` | `Map_NeighborSteps` |
| `0x22`, `0x26` | 4 each | `+0x28`, `+0x2c` | `Map_RowStride`, `Map_TileCount` |
| `0x2a` to `0x3c` | 2 or 4 each | `+0x30` to `+0x42` | `Map_Shape`, `Map_ResourceLayerOffset` |
| `0x3e`, `0x40` | 2 each | `+0x1c2`, `+0x1c4` | `Map_Width`, `Map_Height` |
| `0x4c` | 4 | `+0x202` | `Clock_Game` |
| `0x54`, `0x56`, `0x58` | 2 each | `+0x212`, `+0x214`, `+0x216` | `Rng_State` |
| `0x5a`, `0x5c`, `0x5e`, `0xae` | 2 each | `+0x25e`, `+0x260`, `+0x262`, `+0x266` | The table counts |
| `0x78` | 2 | `+0x21c` | `MapGen_PatchCount` |

The other header fields copy single fields of the game state block. Their roles are unknown.

## By address

Built from the topic tables by `tools/GhidraDump/program_index.py`; do not edit by hand.
`ApplyIndexNames.java` reads this table.

<!-- by-address:start -->
| Address | Name | Status | Topic |
| --- | --- | --- | --- |
| `0x401090` | — | unknown | Files and configuration |
| `0x405590` | `Gfx_BlitSprite` | confirmed | Graphics images |
| `0x405730` | `Gfx_BlitRaw` | confirmed | Graphics images |
| `0x405880` | `Gfx_BlitSpriteOneColor` | confirmed | Graphics images |
| `0x405b40` | `Gfx_FillTextured` | confirmed | Graphics images |
| `0x405b80` | `Gfx_BlitShadow` | confirmed | Graphics images |
| `0x405d00` | `Gfx_SpriteOverColor8` | confirmed | Graphics images |
| `0x405d70` | `Archive_ReadFile` | confirmed | Graphics archives |
| `0x405e00` | `Archive_Load` | confirmed | Graphics archives |
| `0x405f60` | `Archive_Free` | confirmed | Graphics archives |
| `0x405fe0` | `Speed_SetFactor` | confirmed | Timing |
| `0x4060b0` | `Gfx_DrawSprite` | confirmed | Views, panels, and drawing |
| `0x406120` | `Gfx_DrawTexturedFill` | confirmed | Graphics images |
| `0x4061a0` | `Gfx_DrawShadow` | confirmed | Graphics images |
| `0x4062e0` | `Gfx_DrawSpriteTransparent` | confirmed | Graphics images |
| `0x406350` | `Gfx_DrawSpriteGrowing` | inferred | Graphics images |
| `0x406420` | `Gfx_DrawSpriteOverColor8` | confirmed | Graphics images |
| `0x406490` | `Gfx_DrawSpriteOneColor` | confirmed | Graphics images |
| `0x406500` | `Gfx_CopyRect` | confirmed | Views, panels, and drawing |
| `0x406590` | `Gfx_DrawSpriteNoOffset` | confirmed | Graphics images |
| `0x406610` | `Gfx_DrawSettler` | confirmed | Graphics images |
| `0x406880` | `Frame_Present` | inferred | Main loop |
| `0x406980` | `Key_ReadCode` | confirmed | Input |
| `0x4069f0` | `Input_ReadPointerA` | confirmed | Input |
| `0x406a30` | `Input_ReadPointerB` | confirmed | Input |
| `0x406b20` | `Input_ReadButtons` | confirmed | Input |
| `0x406c60` | `Frame_Handshake` | confirmed | Main loop |
| `0x406c90` | `Timer_ThreadMain` | confirmed | Timing |
| `0x406fa0` | `Music_SetVolumeFromEdi` | inferred | Music |
| `0x406fb0` | `Sound_LoadFile` | confirmed | Sound |
| `0x407130` | `Sound_Start` | inferred | Sound |
| `0x4071c0` | `Palette_Load` | confirmed | Graphics images |
| `0x407250` | `Palette_LoadGameFlagged` | confirmed | Graphics images |
| `0x407290` | `Archive_FillAliases` | confirmed | Graphics archives |
| `0x4073d0` | `Archive_FindEntry` | confirmed | Graphics archives |
| `0x407400` | `Palette_LoadGame` | confirmed | Graphics images |
| `0x407410` | `Palette_LoadTitle` | confirmed | Graphics images |
| `0x407480` | `Platform_RunGame` | confirmed | Program start and game flow |
| `0x4076d0` | `File_Open` | inferred | Save and load |
| `0x407750` | `File_Create` | inferred | Save and load |
| `0x4077e0` | `File_Close` | confirmed | Save and load |
| `0x407800` | `File_Write` | confirmed | Save and load |
| `0x407870` | `File_Read` | confirmed | Save and load |
| `0x4080e0` | `Palette_Present` | inferred | Main loop |
| `0x40b8c0` | `Sound_CreateBuffer` | confirmed | Sound |
| `0x410e30` | `Text_ReadFile` | confirmed | Text files |
| `0x410f40` | `Text_ParseFile` | confirmed | Text files |
| `0x4111a0` | `Text_Lookup` | confirmed | Text files |
| `0x412570` | `Text_LoadLanguage` | confirmed | Text files |
| `0x415130` | `Music_LoadFile` | confirmed | Music |
| `0x415390` | `Music_Stop` | inferred | Music |
| `0x415480` | `Music_SetVolume` | inferred | Music |
| `0x415940` | `Music_ReadVarLen` | confirmed | Music |
| `0x417bb0` | `Platform_GameThreadMain` | confirmed | Program start and game flow |
| `0x417c50` | `Platform_StartGameThread` | confirmed | Program start and game flow |
| `0x419a60` | `Music_ApplyOption` | confirmed | Music |
| `0x41da10` | `Game_Entry` | confirmed | Program start and game flow |
| `0x41dbea` | — | unknown | Program start and game flow |
| `0x41e640` | `Map_ObjectClassTable` | confirmed | Castle and build sites |
| `0x41e6c0` | `Game_InitOnce` | confirmed | Program start and game flow |
| `0x41e706` | `Game_Restart` | confirmed | Program start and game flow |
| `0x41e710` | `Game_StartClock` | confirmed | Timing |
| `0x41e8e0` | `RuleData_Load` | confirmed | Graphics archives |
| `0x41e958` | `StartScreen_PlayLogo` | inferred | Program start and game flow |
| `0x41ea82` | `StartScreen_LogoStep1` | confirmed | Program start and game flow |
| `0x41edea` | `StartScreen_LogoStepLast` | confirmed | Program start and game flow |
| `0x41edf0` | `StartScreen_DrawList` | confirmed | Program start and game flow |
| `0x41ee3b` | `FreeGame_NewSeed` | confirmed | Random generator and map start |
| `0x41ee84` | `Game_Start` | confirmed | Program start and game flow |
| `0x41eeeb` | — | unknown | Program start and game flow |
| `0x41f1f4` | `Castle_PlaceAtStart` | confirmed | Castle and build sites |
| `0x41f4d7` | `Game_ResumeLoaded` | inferred | Program start and game flow |
| `0x41f6a5` | `Map_BuildNeighborOffsets` | confirmed | Castle and build sites |
| `0x42055e` | — | unknown | Program start and game flow |
| `0x420cc5` | `View_DrawFrameList` | confirmed | Views, panels, and drawing |
| `0x420d9f` | `View_FramePieceLists` | confirmed | Views, panels, and drawing |
| `0x421021` | `View_DrawFrameBorders` | confirmed | Views, panels, and drawing |
| `0x4213ef` | `View_DrawPanelFrames` | confirmed | Views, panels, and drawing |
| `0x421530` | `MapGen_InitParams` | inferred | Map generation |
| `0x4216e9` | `MapGen_Generate` | confirmed | Map generation |
| `0x421841` | `MapGen_CountResource1` | confirmed | Map generation |
| `0x4218ca` | `MapGen_ResetProgress` | confirmed | Map generation |
| `0x4218d8` | `MapGen_StepProgress` | inferred | Map generation |
| `0x42195c` | `Map_SetGeometry` | confirmed | Map generation |
| `0x421d28` | `Map_ScanWindowTable` | confirmed | Map generation |
| `0x421d3a` | `MapGen_ClearMap` | confirmed | Map generation |
| `0x421d71` | `MapGen_SeedHeights` | confirmed | Map generation |
| `0x421e75` | `MapGen_SubdivideHeights` | confirmed | Map generation |
| `0x4220ec` | `MapGen_DisplaceMidpoint` | confirmed | Map generation |
| `0x42215a` | `MapGen_LimitSlopes` | confirmed | Map generation |
| `0x42223b` | `MapGen_LimitSlopeStepA` | confirmed | Map generation |
| `0x4222ad` | `MapGen_LimitSlopeStepB` | confirmed | Map generation |
| `0x42231f` | `MapGen_LowerToWaterLevel` | confirmed | Map generation |
| `0x42237d` | `MapGen_ScaleHeights` | confirmed | Map generation |
| `0x4223de` | `MapGen_KeepMainLand` | confirmed | Map generation |
| `0x42280a` | `MapGen_ClearByte3` | confirmed | Map generation |
| `0x422857` | `MapGen_ClassifyTerrain` | confirmed | Map generation |
| `0x42297f` | `MapGen_TerrainFromHeightSum` | confirmed | Map generation |
| `0x422a0e` | `MapGen_FillLakes` | confirmed | Map generation |
| `0x422b72` | `MapGen_FloodLevel` | confirmed | Map generation |
| `0x422bf3` | `MapGen_GrowLake` | confirmed | Map generation |
| `0x423041` | `MapGen_GrowLakeTile` | confirmed | Map generation |
| `0x4232ef` | `MapGen_MarkWaterAdjacent` | confirmed | Map generation |
| `0x42336d` | `MapGen_PlaceTerrainAndObjects` | confirmed | Map generation |
| `0x4234bd` | `MapGen_GradeWaterDepth` | confirmed | Map generation |
| `0x423536` | `MapGen_SetShoreTerrain` | confirmed | Map generation |
| `0x42355f` | `MapGen_ReplaceTerrainNear` | confirmed | Map generation |
| `0x423789` | `MapGen_HasTerrainNearUpper` | confirmed | Map generation |
| `0x4238b2` | `MapGen_HasTerrainNearLower` | inferred | Map generation |
| `0x4239db` | `MapGen_PlacePatches` | confirmed | Map generation |
| `0x423aa3` | `MapGen_RandomPos` | confirmed | Map generation |
| `0x423b30` | `MapGen_SpiralPos` | confirmed | Map generation |
| `0x423c10` | `MapGen_CanPatchLower` | confirmed | Map generation |
| `0x423cc7` | `MapGen_CanPatchUpper` | confirmed | Map generation |
| `0x423d74` | `MapGen_BlendPatchEdges` | confirmed | Map generation |
| `0x423e6a` | `MapGen_ResetTypes7To9` | confirmed | Map generation |
| `0x423f07` | `MapGen_RandomSpiralPos` | confirmed | Map generation |
| `0x423fec` | `MapGen_MarkPeaks` | confirmed | Map generation |
| `0x424755` | `MapGen_ScatterObjects` | confirmed | Map generation |
| `0x424802` | `MapGen_FitsTerrain` | confirmed | Map generation |
| `0x424976` | `MapGen_PlaceDeposits1` | confirmed | Map generation |
| `0x4249bc` | `MapGen_PlaceDeposits2` | confirmed | Map generation |
| `0x424a03` | `MapGen_PlaceDeposits3` | confirmed | Map generation |
| `0x424a58` | `MapGen_PlaceDeposits4` | confirmed | Map generation |
| `0x424a9e` | `MapGen_PlaceDeposits` | confirmed | Map generation |
| `0x424be5` | `MapGen_FillDepositRing` | confirmed | Map generation |
| `0x424c57` | `MapGen_ResolveObjectOverlap` | inferred | Map generation |
| `0x424d87` | `Minimap_Build` | inferred | Views, panels, and drawing |
| `0x424fca` | `Game_InitSettings` | confirmed | Program start and game flow |
| `0x42593a` | `Game_ResetCounters` | confirmed | Program start and game flow |
| `0x425a10` | `Main_Loop` | confirmed | Main loop |
| `0x425c9c` | `Timer_WaitNextCall` | confirmed | Timing |
| `0x425cb9` | `Stats_RecordHistory` | inferred | Main loop |
| `0x426350` | `Stats_SumRing` | confirmed | Main loop |
| `0x42637e` | — | confirmed | Main loop |
| `0x42639a` | — | unknown | Main loop |
| `0x426664` | `Stats_Scale` | inferred | Main loop |
| `0x4266d7` | `Stats_ComputeShares` | confirmed | Main loop |
| `0x426abe` | `Main_HandleKeys` | inferred | Main loop |
| `0x426b57` | `Input_BufferKey` | confirmed | Input |
| `0x426dfc` | `Main_ComputeElapsed` | confirmed | Main loop |
| `0x427062` | `Input_ApplyPointerMotion` | confirmed | Input |
| `0x427642` | `Terrain_DrawTiles` | confirmed | Graphics images |
| `0x4279d4` | `Render_TriangleVariantsUpper` | confirmed | Graphics images |
| `0x427b38` | `Render_TriangleVariantsLower` | confirmed | Graphics images |
| `0x427b89` | `Render_TerrainTextures` | confirmed | Graphics images |
| `0x427c09` | `Render_DrawRoadsAndBorders` | confirmed | Roads |
| `0x428008` | `Render_DrawRoadEast` | confirmed | Roads |
| `0x428105` | `Render_DrawRoadSouthEast` | confirmed | Roads |
| `0x4281cc` | `Render_DrawRoadSouth` | confirmed | Roads |
| `0x428389` | — | inferred | Roads |
| `0x4286d8` | — | unknown | Simulation |
| `0x428728` | `Sim_ClearRetryBits` | confirmed | Simulation |
| `0x428964` | `Sound_RunAmbient` | inferred | Sound |
| `0x428a75` | — | unknown | Simulation |
| `0x428d10` | `Map_ObjectPass` | confirmed | Simulation |
| `0x4291c2` | `Sim_StepPhase` | confirmed | Simulation |
| `0x4293f6` | — | confirmed | Simulation |
| `0x429580` | `Ai_PhaseUpdate` | confirmed | Simulation |
| `0x42965c` | — | confirmed | Simulation |
| `0x42a313` | — | inferred | Simulation |
| `0x42a35d` | — | inferred | Simulation |
| `0x42a3a7` | — | inferred | Simulation |
| `0x42a3f1` | — | unknown | Simulation |
| `0x42a567` | — | unknown | Simulation |
| `0x42a74a` | — | unknown | Simulation |
| `0x42abed` | `Player_CheckWarnings` | confirmed | Simulation |
| `0x42b1ce` | — | unknown | Simulation |
| `0x42b455` | `Road_RequestCarrier` | confirmed | Simulation |
| `0x42b4bc` | `Road_RequestBoatman` | inferred | Simulation |
| `0x42be63` | `Building_RequestWorker` | confirmed | Simulation |
| `0x42ca7a` | `Route_NextGeneration` | confirmed | Simulation |
| `0x42cb2d` | `Table18_Pass` | confirmed | Simulation |
| `0x42cf9d` | `Building_Free` | inferred | Castle and build sites |
| `0x42d945` | `Building_HandleType2` | confirmed | Simulation |
| `0x42e0b8` | `Building_HandleType1` | confirmed | Simulation |
| `0x42e7e0` | `Building_HandleCastle` | inferred | Simulation |
| `0x42f3d9` | `Table16_Pass` | confirmed | Simulation |
| `0x42f4f1` | `View_BuildSettlerRecords` | confirmed | Simulation |
| `0x42f512` | `View_ScanSettlers` | confirmed | Simulation |
| `0x42f7ea` | — | unknown | Graphics images |
| `0x42fc81` | `Settler_Dispatch` | confirmed | Simulation |
| `0x42ff81` | `Settler_CarrierWaits` | confirmed | Simulation |
| `0x43007b` | `Settler_StateDismissed` | confirmed | Castle and build sites |
| `0x4300f1` | `Settler_StateRejoined` | confirmed | Castle and build sites |
| `0x43013b` | — | inferred | Simulation |
| `0x430ee9` | `Player_SwapInSecondView` | confirmed | Input |
| `0x430fd3` | `Player_SwapOutSecondView` | confirmed | Input |
| `0x4312ed` | `Flag_ReleaseGoods` | confirmed | Castle and build sites |
| `0x431c31` | `Msg_Post` | confirmed | Main loop |
| `0x434946` | `Settler_ClaimNode` | confirmed | Simulation |
| `0x4350f8` | — | unknown | Simulation |
| `0x4353ef` | `Settler_SearchFlag` | confirmed | Simulation |
| `0x436b46` | `Flag_PutGood` | confirmed | Simulation |
| `0x436c38` | `Settler_FreeWalk` | confirmed | Simulation |
| `0x437ddd` | `Settler_FreeWalkArrived` | confirmed | Simulation |
| `0x437dfb` | `Settler_ArrivalByJob` | confirmed | Simulation |
| `0x4385ab` | `Settler_ArriveHome` | confirmed | Simulation |
| `0x4388ff` | `Settler_WalkRows` | confirmed | Simulation |
| `0x438a9e` | `Woodcutter_Cut` | confirmed | Simulation |
| `0x438b8c` | `Woodcutter_SearchTree` | confirmed | Simulation |
| `0x438eeb` | `Settler_WaitInStorage` | confirmed | Simulation |
| `0x4398ea` | `Settler_Walk` | confirmed | Simulation |
| `0x43a64c` | `Settler_StepEast` | confirmed | Simulation |
| `0x43ad78` | `Settler_WalkRoad` | confirmed | Simulation |
| `0x43b788` | `Settler_StepRoadEast` | confirmed | Simulation |
| `0x43bc52` | `Carrier_WaitAtEnd` | confirmed | Castle and build sites |
| `0x43c4ea` | `Settler_DeliverGood` | confirmed | Simulation |
| `0x43c7c2` | `Settler_EnterBuilding` | confirmed | Simulation |
| `0x43c92b` | `Building_DoorSpeeds` | confirmed | Simulation |
| `0x43c96b` | `Settler_PassDoor` | confirmed | Simulation |
| `0x43cc5f` | `Settler_EnterStorageJob21` | inferred | Simulation |
| `0x43cd06` | `Settler_EnterStorage` | confirmed | Simulation |
| `0x43ceea` | `Settler_EnterStorageJob4` | inferred | Simulation |
| `0x43cf15` | `Woodcutter_Start` | confirmed | Simulation |
| `0x43dceb` | `Settler_WaitStep` | confirmed | Simulation |
| `0x43dd4a` | `Settler_LeaveBuilding` | confirmed | Simulation |
| `0x43e036` | `Settler_StepToFlag` | confirmed | Simulation |
| `0x43f17a` | `Settler_StandByCastle` | confirmed | Simulation |
| `0x43f248` | `Settler_StepTimes` | confirmed | Simulation |
| `0x43f402` | `Settler_FrameRows` | confirmed | Simulation |
| `0x43f4b7` | `Settler_CarriedFigures` | confirmed | Views, panels, and drawing |
| `0x43f537` | `Settler_BuildRecord` | confirmed | Simulation |
| `0x43f744` | `Settler_FigureStubs` | confirmed | Graphics images |
| `0x43f8ae` | `Carrier_FigureWord` | confirmed | Graphics images |
| `0x43fb6b` | `Woodcutter_FigureWord` | confirmed | Graphics images |
| `0x43fed8` | `Settler_FigureJob11` | confirmed | Views, panels, and drawing |
| `0x440aae` | `Ui_HandleViews` | inferred | Views, panels, and drawing |
| `0x440c24` | `View_HandleClick` | inferred | Roads |
| `0x440d23` | `View_RunButtonAction` | inferred | Roads |
| `0x441062` | `Msg_HandleOpenClick` | inferred | Views, panels, and drawing |
| `0x44126d` | `Msg_StepNext` | inferred | Views, panels, and drawing |
| `0x4415c0` | `Msg_Open` | inferred | Views, panels, and drawing |
| `0x441987` | `Panel_Reset` | confirmed | Views, panels, and drawing |
| `0x441b31` | — | confirmed | Views, panels, and drawing |
| `0x441ba2` | — | confirmed | Views, panels, and drawing |
| `0x441d2e` | — | confirmed | Views, panels, and drawing |
| `0x442002` | `Road_Cancel` | confirmed | Roads |
| `0x442244` | `Flag_PlaceFromButton` | confirmed | Castle and build sites |
| `0x4422c5` | `Flag_Place` | confirmed | Castle and build sites |
| `0x442509` | — | confirmed | Views, panels, and drawing |
| `0x44257a` | `Rng_StepSettler` | confirmed | Simulation |
| `0x442630` | `Castle_PlaceFromClick` | confirmed | Castle and build sites |
| `0x442704` | `Castle_Place` | confirmed | Castle and build sites |
| `0x442e8a` | `Castle_StartStockRows` | confirmed | Castle and build sites |
| `0x442f0c` | `Castle_CreateSettlers` | inferred | Castle and build sites |
| `0x44333d` | `Settler_TakeFromStorage` | inferred | Castle and build sites |
| `0x443f62` | `Road_ClickStep` | confirmed | Roads |
| `0x444780` | `Map_CountPlayerObjects` | inferred | Views, panels, and drawing |
| `0x444954` | `Map_CountNodeObject` | inferred | Views, panels, and drawing |
| `0x444b29` | `Road_WalkEnds` | inferred | Roads |
| `0x444d24` | — | inferred | Roads |
| `0x444e68` | `Road_Commit` | confirmed | Roads |
| `0x4454e7` | `Road_LengthClass` | confirmed | Roads |
| `0x445570` | `Player_SetTransportPriorities` | confirmed | Program start and game flow |
| `0x445643` | `Player_SetGoodsOrder` | confirmed | Program start and game flow |
| `0x445716` | `Player_SetDefaults1` | confirmed | Program start and game flow |
| `0x445752` | `Player_SetDefaults2` | confirmed | Program start and game flow |
| `0x44579c` | `Player_SetDefaults3` | confirmed | Program start and game flow |
| `0x4457e6` | — | unknown | Simulation |
| `0x44591d` | `Panel_HandleClick` | inferred | Views, panels, and drawing |
| `0x446f54` | — | unknown | Main loop |
| `0x448418` | `Game_ConfirmEnd` | inferred | Program start and game flow |
| `0x448436` | `Game_EndToStartScreen` | confirmed | Program start and game flow |
| `0x449a63` | `Building_Place` | confirmed | Castle and build sites |
| `0x449f58` | `Flag_MoveDeposit` | confirmed | Castle and build sites |
| `0x44a0aa` | `Deposit_MergeInto` | confirmed | Castle and build sites |
| `0x44b6ce` | `Road_UpdateCursor` | confirmed | Roads |
| `0x44b6e6` | `BuildSite_Evaluate` | confirmed | Castle and build sites |
| `0x44ba52` | `BuildSite_EvaluateFromSouthEast` | inferred | Castle and build sites |
| `0x44bd72` | `BuildSite_EvaluateBuilding` | inferred | Castle and build sites |
| `0x44be72` | `BuildSite_SortTriangle` | confirmed | Castle and build sites |
| `0x44c8cf` | `Panel_WriteButtonList` | confirmed | Views, panels, and drawing |
| `0x44cb6e` | `Msg_RunTimers` | confirmed | Main loop |
| `0x44cb8f` | `Msg_RunPlayerTimers` | inferred | Main loop |
| `0x44d19c` | — | unknown | Main loop |
| `0x44d1c0` | `Msg_DrawIcon` | inferred | Main loop |
| `0x44d263` | — | unknown | Main loop |
| `0x44d270` | `Msg_DrawIconAlt` | inferred | Main loop |
| `0x44d2fc` | — | unknown | Views, panels, and drawing |
| `0x44d38d` | `Render_DrawMapView` | inferred | Views, panels, and drawing |
| `0x44d3ae` | `Render_DrawViewRows` | confirmed | Views, panels, and drawing |
| `0x44d5b4` | `Render_DrawRowTiles` | confirmed | Views, panels, and drawing |
| `0x44d77e` | `Render_AddWaitingCarrier` | confirmed | Views, panels, and drawing |
| `0x44d87d` | `Render_WaitingCarrierPhases` | confirmed | Graphics images |
| `0x44d89d` | `Render_WaitingCarrierFigures` | confirmed | Graphics images |
| `0x44da4d` | `Gfx_DrawObjectSprite` | confirmed | Graphics images |
| `0x44db26` | `Render_DrawFlag` | inferred | Castle and build sites |
| `0x44de9a` | `Gfx_DrawSpriteBase320` | confirmed | Graphics images |
| `0x44e114` | `Gfx_SpriteLists` | inferred | Graphics images |
| `0x44f225` | `Gfx_BuildingPairTable` | confirmed | Graphics images |
| `0x44fd16` | `Gfx_ObjectNumberTable` | confirmed | Graphics images |
| `0x44fd4a` | `Gfx_DrawSpriteBase320Low` | confirmed | Graphics images |
| `0x44fd5f` | `Sound_Request` | inferred | Sound |
| `0x44ff59` | `Gfx_DrawPhaseSprite` | confirmed | Graphics images |
| `0x44ffe7` | `Render_DrawRowSettlers` | confirmed | Graphics images |
| `0x44fff3` | `Gfx_DrawSettlerRecords` | confirmed | Graphics images |
| `0x4501a1` | `Settler_FigureBases` | confirmed | Graphics images |
| `0x4503c5` | `Settler_FigureOffsets` | confirmed | Graphics images |
| `0x450609` | `Save_ConfirmSlot` | confirmed | Save and load |
| `0x45080d` | `Render_MarkTwoDirty` | confirmed | Views, panels, and drawing |
| `0x450a35` | `Render_DrawViewOverlay` | inferred | Views, panels, and drawing |
| `0x4511d5` | `Text_DrawGlyphs` | inferred | Graphics images |
| `0x451355` | `Panel_Update` | inferred | Views, panels, and drawing |
| `0x4513a8` | `Panel_UpdateView` | inferred | Views, panels, and drawing |
| `0x45772e` | `Game_Pause` | confirmed | Timing |
| `0x457751` | `Game_Resume` | confirmed | Timing |
| `0x45ac83` | `Minimap_DrawPanel` | inferred | Views, panels, and drawing |
| `0x45ae45` | `Minimap_DrawTiles` | inferred | Views, panels, and drawing |
| `0x45afcf` | — | unknown | Views, panels, and drawing |
| `0x45bb96` | — | unknown | Views, panels, and drawing |
| `0x45ce5a` | `Panel_CopyToScreen` | inferred | Views, panels, and drawing |
| `0x45ce7b` | `Panel_CopyViewToScreen` | inferred | Views, panels, and drawing |
| `0x45d09b` | `Storage_Pick` | confirmed | Simulation |
| `0x45d718` | `Frame_WaitAndPresent` | confirmed | Main loop |
| `0x45d7ad` | `Table70_Allocate` | confirmed | Castle and build sites |
| `0x45d974` | `Table70_Free` | confirmed | Castle and build sites |
| `0x45da8f` | `Table18_Allocate` | confirmed | Castle and build sites |
| `0x45dc0d` | `Table18_Free` | confirmed | Castle and build sites |
| `0x45dd1c` | `Table120_Allocate` | confirmed | Castle and build sites |
| `0x45e012` | `Table120_Free` | confirmed | Castle and build sites |
| `0x45e121` | `Table16_Allocate` | confirmed | Castle and build sites |
| `0x45e273` | `Table16_Free` | confirmed | Castle and build sites |
| `0x45e375` | `Territory_Update` | inferred | Castle and build sites |
| `0x45e44b` | — | unknown | Castle and build sites |
| `0x45e4ca` | — | unknown | Castle and build sites |
| `0x45eb4c` | — | unknown | Castle and build sites |
| `0x45f154` | — | unknown | Castle and build sites |
| `0x45f755` | `Save_WriteArchiveIndex` | confirmed | Save and load |
| `0x45f7ab` | `Load_LoadSlot` | confirmed | Save and load |
| `0x45f961` | `Panel_DrawStateImage` | confirmed | Views, panels, and drawing |
| `0x45fa13` | `Save_WriteGame` | confirmed | Save and load |
| `0x4600a8` | `Save_WriteMap` | inferred | Save and load |
| `0x46010d` | `Save_WriteTable16` | confirmed | Save and load |
| `0x4601f4` | `Save_WriteTable70` | confirmed | Save and load |
| `0x4602db` | `Save_WriteTable18` | confirmed | Save and load |
| `0x4603c2` | `Save_WriteTable120` | confirmed | Save and load |
| `0x46049a` | `Save_WriteRecord` | confirmed | Save and load |
| `0x4604ed` | `Load_ReadGame` | confirmed | Save and load |
| `0x460bba` | `Load_ReadMap` | inferred | Save and load |
| `0x460c28` | `Load_ReadTable16` | confirmed | Save and load |
| `0x460d5a` | `Load_ReadTable70` | confirmed | Save and load |
| `0x460e8c` | `Load_ReadTable18` | confirmed | Save and load |
| `0x460fbe` | `Load_ReadTable120` | confirmed | Save and load |
| `0x4610e3` | `Load_ReadRecord` | inferred | Save and load |
| `0x461136` | `Mem_CopyBytes` | confirmed | Save and load |
| `0x461157` | `Table_ToOffsets70` | confirmed | Save and load |
| `0x461231` | `Table_ToPointers70` | confirmed | Save and load |
| `0x46130b` | `Table_ToOffsets18` | confirmed | Save and load |
| `0x4613e7` | `Table_ToPointers18` | confirmed | Save and load |
| `0x4614c3` | `Table_ToOffsets16` | confirmed | Save and load |
| `0x461548` | `Table_ToPointers16` | confirmed | Save and load |
| `0x4615cf` | `Click_Remove` | confirmed | Castle and build sites |
| `0x4617fd` | `Building_Remove` | confirmed | Castle and build sites |
| `0x462153` | `Flag_TakeDown` | confirmed | Castle and build sites |
| `0x462acd` | `Road_CarrierCapacities` | confirmed | Roads |
| `0x462ad5` | `Building_ClearPlayerRequests` | confirmed | Castle and build sites |
| `0x462bbc` | `Flag_ClearPlayerRequests` | confirmed | Castle and build sites |
| `0x462bf8` | `Player_ClearRequestWords` | confirmed | Castle and build sites |
| `0x462c8d` | `Good_CountLost` | confirmed | Castle and build sites |
| `0x462ca4` | `Good_ReleaseRequest` | confirmed | Simulation |
| `0x462d6d` | `Good_Kinds` | confirmed | Simulation |
| `0x462d88` | `Click_RemoveRoad` | confirmed | Castle and build sites |
| `0x462e1d` | `Road_TakeDown` | confirmed | Roads |
| `0x463204` | `Road_TakeDownWalk` | confirmed | Roads |
| `0x46385b` | `Settler_DismissFromRoad` | confirmed | Roads |
| `0x463a40` | `Road_GatherSettlers` | confirmed | Roads |
| `0x463ef9` | `Carrier_DismissWaiting` | confirmed | Roads |
| `0x464008` | `Carrier_TakeWaiting` | inferred | Roads |
| `0x464117` | `Good_DestinationClasses` | confirmed | Simulation |
| `0x46414d` | `Table70_Pass` | confirmed | Simulation |
| `0x464bb2` | `Flag_SeedRoadSearch` | confirmed | Simulation |
| `0x464c36` | `Flag_VisitRoads` | confirmed | Simulation |
| `0x4662e2` | `Road_SplitAtFlag` | confirmed | Roads |
| `0x466719` | `Road_LinkHalf` | confirmed | Roads |
| `0x466ade` | `Rng_Step` | confirmed | Random generator and map start |
| `0x466b94` | `StartScreen_SwitchPage` | confirmed | Program start and game flow |
| `0x466cc7` | `StartScreen_BuildPage` | inferred | Program start and game flow |
| `0x46736c` | — | unknown | Program start and game flow |
| `0x467547` | `StartScreen_ShowSeed` | confirmed | Program start and game flow |
| `0x4678c7` | `StartScreen_FormatSeedGroup` | confirmed | Program start and game flow |
| `0x4679a3` | — | unknown | Program start and game flow |
| `0x467c03` | `StartScreen_HandleClick` | inferred | Program start and game flow |
| `0x4684b6` | `StartScreen_ResumeLoaded` | confirmed | Program start and game flow |
| `0x468577` | `StartScreen_OpenLoad` | inferred | Program start and game flow |
| `0x4685bc` | `StartScreen_StartGame` | confirmed | Program start and game flow |
| `0x468717` | `Game_SetStartParams` | confirmed | Program start and game flow |
| `0x468ba9` | — | inferred | Program start and game flow |
| `0x468c1c` | — | inferred | Program start and game flow |
| `0x4692aa` | `FreeGame_OpenScreen` | inferred | Random generator and map start |
| `0x4692d4` | `FreeGame_RandomizeOpponents` | confirmed | Random generator and map start |
| `0x4694e0` | `Game_RequestQuit` | confirmed | Program start and game flow |
| `0x4694f8` | — | inferred | Program start and game flow |
| `0x474da3` | `Ai_UpdatePlayer` | inferred | Simulation |
| `0x475244` | `Ai_EvaluateAction` | inferred | Simulation |
| `0x4754b0` | `Ai_RunActions` | inferred | Simulation |
| `0x47556e` | — | unknown | Simulation |
| `0x4756ed` | — | unknown | Simulation |
| `0x475950` | — | unknown | Simulation |
| `0x475a7e` | — | unknown | Simulation |
| `0x475c95` | — | unknown | Simulation |
| `0x475e14` | — | unknown | Simulation |
| `0x475ef6` | — | unknown | Simulation |
| `0x475fd5` | — | unknown | Simulation |
| `0x4760b4` | — | unknown | Simulation |
| `0x47619e` | `Ai_ChooseGoal` | inferred | Simulation |
| `0x476529` | `Ai_RecordGoal` | inferred | Simulation |
| `0x47661b` | — | unknown | Simulation |
| `0x476862` | — | unknown | Simulation |
| `0x4769ed` | — | unknown | Simulation |
| `0x476af1` | — | unknown | Simulation |
| `0x47820f` | — | unknown | Simulation |
| `0x47849d` | — | unknown | Simulation |
| `0x47872c` | — | unknown | Simulation |
| `0x47895d` | — | unknown | Simulation |
| `0x478f2b` | `Ai_RunSearches` | inferred | Simulation |
| `0x479213` | `Ai_ObjectWeights` | inferred | Simulation |
| `0x479293` | — | unknown | Simulation |
| `0x479403` | — | unknown | Simulation |
| `0x47958c` | — | unknown | Simulation |
| `0x4796f1` | — | unknown | Simulation |
| `0x47978c` | — | unknown | Simulation |
| `0x4798e0` | — | unknown | Simulation |
| `0x479a9a` | — | unknown | Simulation |
| `0x479abc` | — | unknown | Simulation |
| `0x479ade` | — | unknown | Simulation |
| `0x479cb2` | `Timer_GameEntry` | confirmed | Timing |
| `0x479cc0` | `Timer_Handler` | confirmed | Timing |
| `0x479e54` | `Sound_UpdateChannels` | inferred | Sound |
| `0x47a32d` | `Sound_Play` | confirmed | Sound |
| `0x47a4c3` | `Input_UpdatePointer` | inferred | Input |
| `0x47b19c` | `Gfx_BlitTextured` | confirmed | Game code band |
| `0x47b4f9` | `Gfx_BlitSpriteOverColor8` | confirmed | Game code band |
| `0x674048` | `Speed_Factor` | confirmed | Timing |
| `0x674824` | `StartScreen_SpriteList` | confirmed | Graphics images |
| `0x674923` | `Render_WaitingCarrierOffsets` | confirmed | Graphics images |
| `0x6749a3` | `Render_WaitingCarriers` | confirmed | Graphics images |
| `0x674a30` | `BuildSite_NeighborTiles` | inferred | Castle and build sites |
| `0x674adc` | `Road_GatherResultA` | confirmed | Roads |
| `0x674b04` | `Road_GatherResultB` | confirmed | Roads |
| `0x67d608` | `Timer_RateHz` | confirmed | Timing |
| `0x67d614` | `Loca_FileTable` | inferred | Files and configuration |
| `0x6812ec` | `Archive_Base` | confirmed | Graphics archives |
| `0x6816d0` | `Palette_Current` | confirmed | Graphics images |
<!-- by-address:end -->

## Unexplored

The Ghidra project of the dump holds 7489 functions in `Siedler1.exe` and 471 of them in the game code
band, after `CreateMissingFunctions` and the index entries (`tools/GhidraDump/README.md`). A routine
reached only through a computed jump can still be missing, so 471 is a lower bound.

<!-- coverage:start -->
Coverage: the index names 310 routines of the 513 functions in the dump's game code band and lists 67 more without a name: 253 confirmed, 73 inferred, 51 unknown.
<!-- coverage:end -->

Open anchors:

- What the four item tables hold, and the routines that process them: `0x46414d`, `0x42cb2d`,
  `0x42f3d9`, `0x428728`, and the dispatch `0x42fc81`.
- `Ai_UpdatePlayer` and `0x46986d`, which the routines of phases 33 to 48 call.
- The terrain codes 0 to 15 and the resource types 1 to 4.
- The start-up routines without a role: `0x41e62e`, `0x420545`, `0x41e726`, `0x41f76b`, `0x420452`,
  `0x420fff`, `0x41f513`, `0x41f58b`, `0x41f6a5`, and the resource loaders called by
  `Game_InitSettings`.
- The panel handlers dispatched by `Panel_HandleClick` and `Panel_Update`.
- What the rule data of entry 2 holds, and the routines that read its 200 records (`0x43f537` is one).
