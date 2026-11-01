# M9 Free-play release: scope

Goal and done condition: [roadmap, M9](../roadmap.md#m9-free-play-release). Marks:
[Leads](AGENTS.md#leads).

## Start menu

- `StartScreen_*`, `FreeGame_*`, `Game_SetStartParams`: known.
- `0x4679e2`: opens a start-screen page; `Load_LoadSlot` also calls it. likely
- `0x466cc7`: builds the start and load screens. likely
- `0x467547` with `0x46736c`, `0x4678c7`, `0x4679a3`: lays out the lists and text of the start screen.
  likely
- `0x468ba9`, `0x468c1c`: reset a start-screen page. likely
- `0x4694f8`: an action of the start screen. likely

## Win and loss

- `0x42639a`: sums player `+0x116` over the four players and, above 49, checks `+0x5e` and `+0x380`.
  The best candidate for the end of a game. guess
- `0x4615cf`: see [M7](m7-military-and-territory.md); a lost castle can end a game. guess

## Statistics

- `0x4266d7`, `0x426664`, `0x426350`, `0x42637e`: shares and helpers of `Stats_RecordHistory`
  (known). likely

## Panels and messages

- `0x4513a8`: dispatches `Panel_Update` (known) on the panel screen at view `+0x72`. likely
- `0x441062`, `0x441987`, `0x441b31`, `0x441ba2`, `0x441d2e`, `0x442509`: panel page clicks and
  buttons. likely
- `0x44cb8f`, `0x44d1c0`, `0x44d270`, `0x44d19c`, `0x44d263`: message timers and the message icon.
  likely
- `0x44126d`, `0x4415c0`: step through and open messages. likely
- `0x45ac83` with `0x45ae45` and nine layer routines from `0x45afcf` to `0x45bb96`: the minimap panel.
  likely
- `0x45ce7b`: copies the panel through `Panel_CopyToScreen` (known). likely
- `0x420cc5`, `0x421021`, `0x4213ef`: choose and draw the screen frame for one or two views. likely
- `0x41e958` with nine steps from `0x41ea82` to `0x41edea`, and `0x41edf0`: register the panel
  elements at start-up. guess
- `0x444b29`, `0x444d24`: tile query for an info panel. guess

## Sound and input

- `0x428985`: picks a random ambient sound. likely
- `0x426b57`: keyboard ring buffer; one mode accepts only the digits 1 to 8. likely
- `0x446f54`: toggles the game state and restarts the map; reached from a key. guess

## Save

- `Save_*` and `Load_*`: known. The engine writes its own save format (roadmap non-goals), so they show
  what a save must hold.
- `0x41f513`: on resuming a loaded game, marks the objects that the views select. guess
