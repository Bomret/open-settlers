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
  "Entry inferred" marks a routine whose entry is not a Ghidra function and follows from a preceding
  return or a jump.
- **Layer:** **game** inside the game code band, **platform** outside it.
- **Anchor:** the string, save field, constant, import, or caller that leads to the routine.

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
| `0x467c03` | `StartScreen_HandleClick` | Click handler of the start screens; dispatches 42 actions through the table at `0x468148`. | inferred | game | Called by `0x440b01`. |
| `0x466b94` | `StartScreen_SwitchPage` | Moves a requested start-screen page into the current page and runs its handler. | confirmed | game | `Main_Loop` while `Game_Flags` bit 4 is set. |
| `0x4685bc` | `StartScreen_StartGame` | Action 38: sets the start parameters and the map size, then requests a new game. Entry inferred. | confirmed | game | Slot `0x468278` of the table at `0x468148`. |
| `0x4684b6` | `StartScreen_ResumeLoaded` | Action 40: resets the countdowns and requests the resume of a loaded game. Entry inferred. | confirmed | game | Slot `0x468288`. |
| `0x468577` | `StartScreen_OpenLoad` | Action 39: opens the archive window in load mode. Entry inferred. | inferred | game | Slot `0x468280`. |
| `0x4694e0` | `Game_RequestQuit` | Action 11: requests the end of the main loop. Entry inferred. | confirmed | game | Slot `0x4681a0`. |
| `0x448418` | `Game_ConfirmEnd` | Panel action 169: asks for confirmation while the countdown runs; otherwise ends the game. Entry inferred. | inferred | game | Slot `0x446bd4` of the table at `0x44668c`. |
| `0x448436` | `Game_EndToStartScreen` | Panel action 171: advances the mission number in mission mode and requests the start screens. Entry inferred. | confirmed | game | Slot `0x446be4`. |

Sites:

- `0x425a9c`, `0x425b51`, `0x425b8f`: `Main_Loop` handles the requests new game, end of game, and resume
  of a loaded game. `0x425b12`: it returns on the quit request.
- `0x468702`, `0x4484cf`, `0x468508`, `0x4694e3`: the actions set the request bits 1, 2, 3, and 0.
- `0x46813a`: the start-screen action dispatch. `0x44667e`: the panel action dispatch (255 slots at
  `0x44668c`).

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
| `0x425cb9` | `Stats_RecordHistory` | Every 1500 clock units, records values for the four players in ring buffers. | inferred | game | Called by `Main_Loop`. |
| `0x44cb6e` | `Msg_RunTimers` | Counts down each player's timed messages by the elapsed units and posts the expired ones. | confirmed | game | Called by `Main_Loop` and the start routines. |
| `0x431c31` | `Msg_Post` | Writes a message into the first free of 64 slots of a player block. | confirmed | game | Called by `Main_ComputeElapsed`. |

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

### Input

Note: [Timing](timing.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x47a4c3` | `Input_UpdatePointer` | Updates one player's pointer; timing thresholds scale with `Speed_Factor`. Changed by the History Edition. | inferred | game | Called twice by `Timer_Handler`. |
| `0x427062` | `Input_ApplyPointerMotion` | Moves the accumulated pointer motion into the EDI block and runs the input handlers of each view. | confirmed | game | Called by `Main_Loop`. |
| `0x4069f0` | `Input_ReadPointerA` | Reads the first pointer device's position into the EDI block. | confirmed | platform | Called by `Timer_Handler`. |
| `0x406a30` | `Input_ReadPointerB` | The same for the second device. | confirmed | platform | Called by `Timer_Handler`. |
| `0x406b20` | `Input_ReadButtons` | Sets the button words and adds a device's motion to the EDI block. | confirmed | platform | Reached from `Timer_Handler` through `0x406bd0` and `0x406be0`. |
| `0x406980` | `Key_ReadCode` | Takes the next key from the key buffer and writes its code to the EDI block. | confirmed | platform | Called by `Main_Loop`, `Main_HandleKeys`. |
| `0x430ee9` | `Player_SwapInSecondView` | Swaps a second view's fields into player 1's block. | confirmed | game | Called by input and panel code when `+0x37e` bit 6 is set. |
| `0x430fd3` | `Player_SwapOutSecondView` | The reverse of `Player_SwapInSecondView`. | confirmed | game | Same callers. |

### Sound

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x479e54` | `Sound_UpdateChannels` | Counts down four sound channels and starts the requested sounds. | inferred | game | Called by `Timer_Handler`. |
| `0x47a32d` | `Sound_Play` | Picks a sound variant with an inline step of the random generator and starts it. | confirmed | game | Called by `Sound_UpdateChannels`; site `0x47a374`. |
| `0x44fd5f` | `Sound_Request` | Inserts a sound code into a view's four-slot request list. | inferred | game | 17 callers. |
| `0x428964` | `Sound_RunAmbient` | For each view, may request a random ambient sound. | inferred | game | Called by `0x4286d8`; calls `Rng_Step` through `0x428985`. |
| `0x407130` | `Sound_Start` | Scales the volume and starts playback. | inferred | platform | Called by `Sound_Play`. |
| `0x406fb0` | `Sound_LoadFile` | Opens a numbered sound file in `snd/` with the C runtime's file calls. | inferred | platform | String `snd/%02d.SND`. |

### Views, panels, and drawing

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x440aae` | `Ui_HandleViews` | For each view, runs the start-screen click handler or the in-game click and panel handlers. | inferred | game | Called by `Main_Loop`. |
| `0x44591d` | `Panel_HandleClick` | Hit-tests a panel's buttons and dispatches the action through the table at `0x44668c`. | inferred | game | Called by `0x440c24` and `StartScreen_HandleClick`. |
| `0x451355` | `Panel_Update` | For each view, runs the handler of the requested or current panel screen. | inferred | game | Called by `Main_Loop`. |
| `0x45ce5a` | `Panel_CopyToScreen` | For each view, copies the panel into the view's screen. | inferred | game | Called by `Main_Loop`. |
| `0x44d38d` | `Render_DrawMapView` | For each view, draws the map row by row. | inferred | game | Called by `Main_Loop` in a game. |
| `0x450a35` | `Render_DrawViewOverlay` | For each view, draws sprite lists over the map. | inferred | game | Called by `Main_Loop` in a game. |
| `0x44d2fc` | — | For each view, copies a rectangle from the surface at `+0x108` into the view's screen. | unknown | game | Called by `Main_Loop` in a game. |
| `0x424d87` | `Minimap_Build` | Writes one byte per tile from terrain and height to the buffer at `+0x8c`. | inferred | game | Called by `MapGen_Generate` and `Game_ResumeLoaded`. |
| `0x406500` | `Gfx_CopyRect` | Copies a rectangle between two surfaces. | confirmed | platform | Called by panel and view code. |
| `0x4060b0` | `Gfx_DrawSprite` | Draws a sprite with clipping. | inferred | platform | 13 callers. |

### Simulation

The simulation runs while `Game_Flags` bit 3 is set. It works through the item tables in slices of 32
entries per phase (see "Item tables").

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x4291c2` | `Sim_StepPhase` | Advances `Sim_Phase`; phases 0 to 31 run the per-player update for player `phase & 3`, later phases draw from the random generator and dispatch through a table. | confirmed | game | Called by `Main_Loop`; tables at `0x4292ee` and `0x42923c`. |
| `0x474da3` | `Ai_UpdatePlayer` | Per-player update for players whose block marks them active and computer-controlled. | inferred | game | Reached from `Sim_StepPhase`; calls `Rng_Step`. |
| `0x46414d` | — | Processes the phase's slice of `Table70`. | unknown | game | Called by `Main_Loop`. |
| `0x42cb2d` | — | Processes the phase's slice of `Table18`. | unknown | game | Called by `Main_Loop`. |
| `0x42f3d9` | — | Processes the phase's slice of `Table16`, dispatching each entry on its type byte through `0x42fc81`. | unknown | game | Called by `Main_Loop`. |
| `0x42f4f1` | — | For each view, gathers the objects on the shown tiles and dispatches them through `0x42fc81`. | unknown | game | Called by `Main_Loop`. |
| `0x4286d8` | — | Runs `0x428728`, `Sound_RunAmbient`, and `0x428d10`, then `0x428a75` for each player block. | unknown | game | Called by `Main_Loop`. |
| `0x428728` | — | Clears one flag bit per entry of `Table18` and `Table70`, a limited number per pass. | unknown | game | Called by `0x4286d8`. |
| `0x428d10` | — | Every 20 clock units, steps a cursor across the map and changes tiles with the random generator. | unknown | game | Called by `0x4286d8`; calls `Rng_Step`. |
| `0x428a75` | — | Counts down three timers of a player block. | unknown | game | Called by `0x4286d8`. |

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

### Map generation

Note: [Map start](map-start.md).

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
| `0x42195c` | `Map_SetGeometry` | Derives the map masks, row stride, tile count, and neighbor steps from `Map_Width` and `Map_Height`. | inferred | game | Called by `Game_Start`. |
| `0x421530` | `MapGen_InitParams` | Sets the water level and lake radius and clears other generator fields. | inferred | game | Called by `Game_Start`. |
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

The 15 object scatter passes `0x424175`, `0x4241e3`, `0x424247`, `0x4242ab`, `0x42430f`, `0x424373`,
`0x4243d7`, `0x42443b`, `0x42449f`, `0x424503`, `0x424567`, `0x4245cb`, `0x42462f`, `0x424693`, and
`0x4246f4` each set the parameters of `MapGen_ScatterObjects` and call it. They have no rows.

Sites:

- `0x424ad8`: `MapGen_PlaceDeposits` tests the resource layer one byte past the tile it writes.

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

### Game code band

Note: [Game code](game-code.md).

| Address | Name | Role | Status | Layer | Anchor |
| --- | --- | --- | --- | --- | --- |
| `0x47b4f9` | — | Last routine of the game code band. | unknown | game | Band comparison. |

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
| `+0x206` | `Clock_PrevUnits` | 2 | Whole units of the game clock at the previous pass. | confirmed |
| `+0x208` | `Timer_CallCount` | 2 | Counts the timer calls. | confirmed |
| `+0x20a` | `Frame_LastCallCount` | 2 | `Timer_CallCount` at the previous frame. | confirmed |
| `+0x212`, `+0x214`, `+0x216` | `Rng_State` | 2 each | The random generator's three state words. | confirmed |
| `+0x21c` | `MapGen_PatchCount` | 2 | Base count of patches, clusters, and deposits. | confirmed |
| `+0x240` | `Archive_Result` | 2 | Result code of the last save or load. | confirmed |
| `+0x258` to `+0x266` | `Table70_Count` (`+0x25e`), `Table18_Count` (`+0x260`), `Table16_Count` (`+0x262`), `Table120_Count` (`+0x266`), and capacities | 2 each | Slots in use and slot capacity of the item tables. | confirmed |
| `+0x26c`, `+0x286` | `Sim_Phase`, `Sim_PhaseCount` | 2 each | The phase counter and its wrap value. | confirmed |
| `+0x284` | `Clock_Elapsed` | 2 | Elapsed whole units of this pass. | confirmed |
| `+0x288` | `Clock_ElapsedTotal` | 2 | Running total of the elapsed units. | confirmed |
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
the timed messages at `+0x1f34`.

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

| Address | Name | Topic |
| --- | --- | --- |
| `0x401090` | — | Files and configuration |
| `0x405fe0` | `Speed_SetFactor` | Timing |
| `0x4060b0` | `Gfx_DrawSprite` | Views, panels, and drawing |
| `0x406500` | `Gfx_CopyRect` | Views, panels, and drawing |
| `0x406880` | `Frame_Present` | Main loop |
| `0x406980` | `Key_ReadCode` | Input |
| `0x4069f0` | `Input_ReadPointerA` | Input |
| `0x406a30` | `Input_ReadPointerB` | Input |
| `0x406b20` | `Input_ReadButtons` | Input |
| `0x406c60` | `Frame_Handshake` | Main loop |
| `0x406c90` | `Timer_ThreadMain` | Timing |
| `0x406fb0` | `Sound_LoadFile` | Sound |
| `0x407130` | `Sound_Start` | Sound |
| `0x407480` | `Platform_RunGame` | Program start and game flow |
| `0x4076d0` | `File_Open` | Save and load |
| `0x407750` | `File_Create` | Save and load |
| `0x4077e0` | `File_Close` | Save and load |
| `0x407800` | `File_Write` | Save and load |
| `0x407870` | `File_Read` | Save and load |
| `0x417bb0` | `Platform_GameThreadMain` | Program start and game flow |
| `0x417c50` | `Platform_StartGameThread` | Program start and game flow |
| `0x41da10` | `Game_Entry` | Program start and game flow |
| `0x41dbea` | — | Program start and game flow |
| `0x41e6c0` | `Game_InitOnce` | Program start and game flow |
| `0x41e706` | `Game_Restart` | Program start and game flow |
| `0x41e710` | `Game_StartClock` | Timing |
| `0x41ee3b` | `FreeGame_NewSeed` | Random generator and map start |
| `0x41ee84` | `Game_Start` | Program start and game flow |
| `0x41eeeb` | — | Program start and game flow |
| `0x41f4d7` | `Game_ResumeLoaded` | Program start and game flow |
| `0x421530` | `MapGen_InitParams` | Map generation |
| `0x4216e9` | `MapGen_Generate` | Map generation |
| `0x421841` | `MapGen_CountResource1` | Map generation |
| `0x4218ca` | `MapGen_ResetProgress` | Map generation |
| `0x4218d8` | `MapGen_StepProgress` | Map generation |
| `0x42195c` | `Map_SetGeometry` | Map generation |
| `0x421d3a` | `MapGen_ClearMap` | Map generation |
| `0x421d71` | `MapGen_SeedHeights` | Map generation |
| `0x421e75` | `MapGen_SubdivideHeights` | Map generation |
| `0x4220ec` | `MapGen_DisplaceMidpoint` | Map generation |
| `0x42215a` | `MapGen_LimitSlopes` | Map generation |
| `0x42223b` | `MapGen_LimitSlopeStepA` | Map generation |
| `0x4222ad` | `MapGen_LimitSlopeStepB` | Map generation |
| `0x42231f` | `MapGen_LowerToWaterLevel` | Map generation |
| `0x42237d` | `MapGen_ScaleHeights` | Map generation |
| `0x4223de` | `MapGen_KeepMainLand` | Map generation |
| `0x42280a` | `MapGen_ClearByte3` | Map generation |
| `0x422857` | `MapGen_ClassifyTerrain` | Map generation |
| `0x42297f` | `MapGen_TerrainFromHeightSum` | Map generation |
| `0x422a0e` | `MapGen_FillLakes` | Map generation |
| `0x422b72` | `MapGen_FloodLevel` | Map generation |
| `0x422bf3` | `MapGen_GrowLake` | Map generation |
| `0x423041` | `MapGen_GrowLakeTile` | Map generation |
| `0x4232ef` | `MapGen_MarkWaterAdjacent` | Map generation |
| `0x42336d` | `MapGen_PlaceTerrainAndObjects` | Map generation |
| `0x4234bd` | `MapGen_GradeWaterDepth` | Map generation |
| `0x423536` | `MapGen_SetShoreTerrain` | Map generation |
| `0x42355f` | `MapGen_ReplaceTerrainNear` | Map generation |
| `0x423789` | `MapGen_HasTerrainNearUpper` | Map generation |
| `0x4238b2` | `MapGen_HasTerrainNearLower` | Map generation |
| `0x4239db` | `MapGen_PlacePatches` | Map generation |
| `0x423aa3` | `MapGen_RandomPos` | Map generation |
| `0x423b30` | `MapGen_SpiralPos` | Map generation |
| `0x423c10` | `MapGen_CanPatchLower` | Map generation |
| `0x423cc7` | `MapGen_CanPatchUpper` | Map generation |
| `0x423d74` | `MapGen_BlendPatchEdges` | Map generation |
| `0x423e6a` | `MapGen_ResetTypes7To9` | Map generation |
| `0x423f07` | `MapGen_RandomSpiralPos` | Map generation |
| `0x423fec` | `MapGen_MarkPeaks` | Map generation |
| `0x424755` | `MapGen_ScatterObjects` | Map generation |
| `0x424802` | `MapGen_FitsTerrain` | Map generation |
| `0x424976` | `MapGen_PlaceDeposits1` | Map generation |
| `0x4249bc` | `MapGen_PlaceDeposits2` | Map generation |
| `0x424a03` | `MapGen_PlaceDeposits3` | Map generation |
| `0x424a58` | `MapGen_PlaceDeposits4` | Map generation |
| `0x424a9e` | `MapGen_PlaceDeposits` | Map generation |
| `0x424be5` | `MapGen_FillDepositRing` | Map generation |
| `0x424c57` | `MapGen_ResolveObjectOverlap` | Map generation |
| `0x424d87` | `Minimap_Build` | Views, panels, and drawing |
| `0x424fca` | `Game_InitSettings` | Program start and game flow |
| `0x42593a` | `Game_ResetCounters` | Program start and game flow |
| `0x425a10` | `Main_Loop` | Main loop |
| `0x425c9c` | `Timer_WaitNextCall` | Timing |
| `0x425cb9` | `Stats_RecordHistory` | Main loop |
| `0x426abe` | `Main_HandleKeys` | Main loop |
| `0x426dfc` | `Main_ComputeElapsed` | Main loop |
| `0x427062` | `Input_ApplyPointerMotion` | Input |
| `0x4286d8` | — | Simulation |
| `0x428728` | — | Simulation |
| `0x428964` | `Sound_RunAmbient` | Sound |
| `0x428a75` | — | Simulation |
| `0x428d10` | — | Simulation |
| `0x4291c2` | `Sim_StepPhase` | Simulation |
| `0x42cb2d` | — | Simulation |
| `0x42f3d9` | — | Simulation |
| `0x42f4f1` | — | Simulation |
| `0x430ee9` | `Player_SwapInSecondView` | Input |
| `0x430fd3` | `Player_SwapOutSecondView` | Input |
| `0x431c31` | `Msg_Post` | Main loop |
| `0x440aae` | `Ui_HandleViews` | Views, panels, and drawing |
| `0x44591d` | `Panel_HandleClick` | Views, panels, and drawing |
| `0x448418` | `Game_ConfirmEnd` | Program start and game flow |
| `0x448436` | `Game_EndToStartScreen` | Program start and game flow |
| `0x44cb6e` | `Msg_RunTimers` | Main loop |
| `0x44d2fc` | — | Views, panels, and drawing |
| `0x44d38d` | `Render_DrawMapView` | Views, panels, and drawing |
| `0x44fd5f` | `Sound_Request` | Sound |
| `0x450609` | `Save_ConfirmSlot` | Save and load |
| `0x450a35` | `Render_DrawViewOverlay` | Views, panels, and drawing |
| `0x451355` | `Panel_Update` | Views, panels, and drawing |
| `0x45772e` | `Game_Pause` | Timing |
| `0x457751` | `Game_Resume` | Timing |
| `0x45ce5a` | `Panel_CopyToScreen` | Views, panels, and drawing |
| `0x45d718` | `Frame_WaitAndPresent` | Main loop |
| `0x45f755` | `Save_WriteArchiveIndex` | Save and load |
| `0x45f7ab` | `Load_LoadSlot` | Save and load |
| `0x45fa13` | `Save_WriteGame` | Save and load |
| `0x4600a8` | `Save_WriteMap` | Save and load |
| `0x46010d` | `Save_WriteTable16` | Save and load |
| `0x4601f4` | `Save_WriteTable70` | Save and load |
| `0x4602db` | `Save_WriteTable18` | Save and load |
| `0x4603c2` | `Save_WriteTable120` | Save and load |
| `0x46049a` | `Save_WriteRecord` | Save and load |
| `0x4604ed` | `Load_ReadGame` | Save and load |
| `0x460bba` | `Load_ReadMap` | Save and load |
| `0x460c28` | `Load_ReadTable16` | Save and load |
| `0x460d5a` | `Load_ReadTable70` | Save and load |
| `0x460e8c` | `Load_ReadTable18` | Save and load |
| `0x460fbe` | `Load_ReadTable120` | Save and load |
| `0x4610e3` | `Load_ReadRecord` | Save and load |
| `0x461136` | `Mem_CopyBytes` | Save and load |
| `0x461157` | `Table_ToOffsets70` | Save and load |
| `0x461231` | `Table_ToPointers70` | Save and load |
| `0x46130b` | `Table_ToOffsets18` | Save and load |
| `0x4613e7` | `Table_ToPointers18` | Save and load |
| `0x4614c3` | `Table_ToOffsets16` | Save and load |
| `0x461548` | `Table_ToPointers16` | Save and load |
| `0x46414d` | — | Simulation |
| `0x466ade` | `Rng_Step` | Random generator and map start |
| `0x466b94` | `StartScreen_SwitchPage` | Program start and game flow |
| `0x467c03` | `StartScreen_HandleClick` | Program start and game flow |
| `0x4684b6` | `StartScreen_ResumeLoaded` | Program start and game flow |
| `0x468577` | `StartScreen_OpenLoad` | Program start and game flow |
| `0x4685bc` | `StartScreen_StartGame` | Program start and game flow |
| `0x468717` | `Game_SetStartParams` | Program start and game flow |
| `0x4692aa` | `FreeGame_OpenScreen` | Random generator and map start |
| `0x4692d4` | `FreeGame_RandomizeOpponents` | Random generator and map start |
| `0x4694e0` | `Game_RequestQuit` | Program start and game flow |
| `0x474da3` | `Ai_UpdatePlayer` | Simulation |
| `0x479cb2` | `Timer_GameEntry` | Timing |
| `0x479cc0` | `Timer_Handler` | Timing |
| `0x479e54` | `Sound_UpdateChannels` | Sound |
| `0x47a32d` | `Sound_Play` | Sound |
| `0x47a4c3` | `Input_UpdatePointer` | Input |
| `0x47b4f9` | — | Game code band |
| `0x674048` | `Speed_Factor` | Timing |
| `0x67d608` | `Timer_RateHz` | Timing |
| `0x67d614` | `Loca_FileTable` | Files and configuration |

## Unexplored

Ghidra finds 7232 functions in `Siedler1.exe` and 429 of them in the game code band. The game code
builds no stack frames, so Ghidra misses some of its routines, and 429 is a lower bound. This index names
122 routines in the band and lists 12 more without a name.

Anchors not yet followed:

- What the four item tables hold, and the routines that process them: `0x46414d`, `0x42cb2d`,
  `0x42f3d9`, `0x428728`, and the dispatch `0x42fc81`.
- `Ai_UpdatePlayer` and the phase table of `Sim_StepPhase` (target `0x46986d`).
- The terrain codes 0 to 15 and the resource types 1 to 4.
- The start-up routines without a role: `0x41e62e`, `0x420545`, `0x41e726`, `0x41f76b`, `0x420452`,
  `0x420fff`, `0x41f513`, `0x41f58b`, `0x41f6a5`, and the resource loaders called by
  `Game_InitSettings`.
- The panel handlers dispatched by `Panel_HandleClick` and `Panel_Update`.
- The asset readers of the game code. Few strings are referenced from the band, so file names are weak
  anchors there.
