# Map object codes

What each of the 127 object codes (the low 7 bits of a node's object byte, [Map objects](map-objects.md)) is, and the
name Open Settlers gives it. The names feed the name table (INV-0006): a code named "unnamed" here has no proof
strong enough for a name and shows as its raw number.

## Sources

**Status:** confirmed for the class and the sprite entry of each code; the sprite descriptions are read from the
rendered sprites.

- **Class:** the byte of the code in the class table at `0x41e640` of `Siedler1.exe` (`RuleTables.ClassOf`).
  Class 1 and 2 are the trees and stones of the generation, 2 and up are large objects
  ([Map form](map-form.md), `MapGen_ResolveObjectOverlap`), 3 is a flag, 4 to 6 are buildings.
- **Sprite:** the entry `ObjectSprites.Entry` picks (`1250 + code - 8`, the animated codes 8 to 31 by frame),
  rendered from the install with `ObjectGraphics.Load`. The images stay outside the repository (INV-0001).
- **Scatter pass:** the generation pass that writes the code ([Map form](map-form.md), passes 16 to 25), with the
  terrain codes it accepts. "none" means no generation pass writes it.

## Naming rule

A code gets a name when its sprite shows one clear thing and a program fact agrees: an engine constant, a rule in
`docs/rules/`, its class, or the pass that places it. A code whose meaning rests on the sprite alone stays unnamed.
The table gives what the sprite shows in either case, so a later rule can name it.

## The codes

| Code | Class | Sprite | Scatter pass | Name | Evidence |
| --- | --- | --- | --- | --- | --- |
| 1 (`0x01`) | 3 | flag (entry 1378, drawn by `0x44db26`) | none | `flag` | `ObjectSprites.FlagCode`; [Flags](flags.md) |
| 2 (`0x02`) | 4 | building (drawn by `0x44e329` from a record table) | none | `building` | class 4 to 6; [Build sites](build-sites.md) names the codes 2 to 4 buildings |
| 3 (`0x03`) | 5 | building (drawn by `0x44e329` from a record table) | none | `building` | class 4 to 6; [Build sites](build-sites.md) names the codes 2 to 4 buildings |
| 4 (`0x04`) | 6 | building (drawn by `0x44e329` from a record table) | none | `building` | class 4 to 6; [Build sites](build-sites.md) names the codes 2 to 4 buildings |
| 5 (`0x05`) | 0 | none (the draw routine draws nothing) | none | unnamed | no class, no sprite, no pass |
| 6 (`0x06`) | 0 | none (the draw routine draws nothing) | none | unnamed | no class, no sprite, no pass |
| 7 (`0x07`) | 0 | none (the draw routine draws nothing) | none | unnamed | no class, no sprite, no pass |
| 8 (`0x08`) | 1 | broad-leaved tree (frames 1250 to 1257) | `0x424175`, `0x4241e3`, `0x4242ab` (terrain 5 to 6) | `tree` | `Game.Woodcutting` `EndTree`/`EndFirstTrees`; [Woodcutting](woodcutting.md) (codes 8 to 0x17) |
| 9 (`0x09`) | 1 | broad-leaved tree (frames 1250 to 1257) | `0x424175`, `0x4241e3`, `0x4242ab` (terrain 5 to 6) | `tree` | `Game.Woodcutting` `EndTree`/`EndFirstTrees`; [Woodcutting](woodcutting.md) (codes 8 to 0x17) |
| 10 (`0x0a`) | 1 | broad-leaved tree (frames 1250 to 1257) | `0x424175`, `0x4241e3`, `0x4242ab` (terrain 5 to 6) | `tree` | `Game.Woodcutting` `EndTree`/`EndFirstTrees`; [Woodcutting](woodcutting.md) (codes 8 to 0x17) |
| 11 (`0x0b`) | 1 | broad-leaved tree (frames 1250 to 1257) | `0x424175`, `0x4241e3`, `0x4242ab` (terrain 5 to 6) | `tree` | `Game.Woodcutting` `EndTree`/`EndFirstTrees`; [Woodcutting](woodcutting.md) (codes 8 to 0x17) |
| 12 (`0x0c`) | 1 | broad-leaved tree (frames 1250 to 1257) | `0x424175`, `0x4241e3`, `0x4242ab` (terrain 5 to 6) | `tree` | `Game.Woodcutting` `EndTree`/`EndFirstTrees`; [Woodcutting](woodcutting.md) (codes 8 to 0x17) |
| 13 (`0x0d`) | 1 | broad-leaved tree (frames 1250 to 1257) | `0x424175`, `0x4241e3`, `0x4242ab` (terrain 5 to 6) | `tree` | `Game.Woodcutting` `EndTree`/`EndFirstTrees`; [Woodcutting](woodcutting.md) (codes 8 to 0x17) |
| 14 (`0x0e`) | 1 | broad-leaved tree (frames 1250 to 1257) | `0x424175`, `0x4241e3`, `0x4242ab` (terrain 5 to 6) | `tree` | `Game.Woodcutting` `EndTree`/`EndFirstTrees`; [Woodcutting](woodcutting.md) (codes 8 to 0x17) |
| 15 (`0x0f`) | 1 | broad-leaved tree (frames 1250 to 1257) | `0x424175`, `0x4241e3`, `0x4242ab` (terrain 5 to 6) | `tree` | `Game.Woodcutting` `EndTree`/`EndFirstTrees`; [Woodcutting](woodcutting.md) (codes 8 to 0x17) |
| 16 (`0x10`) | 1 | conifer (frames 1258 to 1265) | `0x424175`, `0x4242ab` (terrain 5 to 6); `0x424247` (terrain 4 to 6) | `tree` | as codes 8 to 15 |
| 17 (`0x11`) | 1 | conifer (frames 1258 to 1265) | `0x424175`, `0x4242ab` (terrain 5 to 6); `0x424247` (terrain 4 to 6) | `tree` | as codes 8 to 15 |
| 18 (`0x12`) | 1 | conifer (frames 1258 to 1265) | `0x424175`, `0x4242ab` (terrain 5 to 6); `0x424247` (terrain 4 to 6) | `tree` | as codes 8 to 15 |
| 19 (`0x13`) | 1 | conifer (frames 1258 to 1265) | `0x424175`, `0x4242ab` (terrain 5 to 6); `0x424247` (terrain 4 to 6) | `tree` | as codes 8 to 15 |
| 20 (`0x14`) | 1 | conifer (frames 1258 to 1265) | `0x424175`, `0x4242ab` (terrain 5 to 6); `0x424247` (terrain 4 to 6) | `tree` | as codes 8 to 15 |
| 21 (`0x15`) | 1 | conifer (frames 1258 to 1265) | `0x424175`, `0x4242ab` (terrain 5 to 6); `0x424247` (terrain 4 to 6) | `tree` | as codes 8 to 15 |
| 22 (`0x16`) | 1 | conifer (frames 1258 to 1265) | `0x424175`, `0x4242ab` (terrain 5 to 6); `0x424247` (terrain 4 to 6) | `tree` | as codes 8 to 15 |
| 23 (`0x17`) | 1 | conifer (frames 1258 to 1265) | `0x424175`, `0x4242ab` (terrain 5 to 6); `0x424247` (terrain 4 to 6) | `tree` | as codes 8 to 15 |
| 24 (`0x18`) | 1 | palm pair (frames 1266 to 1269) | `0x4246f4` (terrain 10) | `palm` | sprite; class 1 as the trees; placed on terrain 10 only; outside the woodcutter's range (`EndTree` 0x18) |
| 25 (`0x19`) | 1 | palm pair (frames 1266 to 1269) | `0x4246f4` (terrain 10) | `palm` | sprite; class 1 as the trees; placed on terrain 10 only; outside the woodcutter's range (`EndTree` 0x18) |
| 26 (`0x1a`) | 1 | palm pair (frames 1266 to 1269) | `0x4246f4` (terrain 10) | `palm` | sprite; class 1 as the trees; placed on terrain 10 only; outside the woodcutter's range (`EndTree` 0x18) |
| 27 (`0x1b`) | 1 | palm pair (frames 1266 to 1269) | `0x4246f4` (terrain 10) | `palm` | sprite; class 1 as the trees; placed on terrain 10 only; outside the woodcutter's range (`EndTree` 0x18) |
| 28 (`0x1c`) | 2 | drooping green plant pair (frames 1270 to 1273) | `0x42449f` (terrain 2 to 3) | unnamed | sprite is not clearly one thing; placed in terrain 2 to 3 (water depth codes) |
| 29 (`0x1d`) | 2 | drooping green plant pair (frames 1270 to 1273) | `0x42449f` (terrain 2 to 3) | unnamed | sprite is not clearly one thing; placed in terrain 2 to 3 (water depth codes) |
| 30 (`0x1e`) | 2 | drooping green plant pair (frames 1270 to 1273) | `0x42449f` (terrain 2 to 3) | unnamed | sprite is not clearly one thing; placed in terrain 2 to 3 (water depth codes) |
| 31 (`0x1f`) | 2 | drooping green plant pair (frames 1270 to 1273) | `0x42449f` (terrain 2 to 3) | unnamed | sprite is not clearly one thing; placed in terrain 2 to 3 (water depth codes) |
| 32 (`0x20`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 33 (`0x21`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 34 (`0x22`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 35 (`0x23`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 36 (`0x24`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 37 (`0x25`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 38 (`0x26`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 39 (`0x27`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 40 (`0x28`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 41 (`0x29`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 42 (`0x2a`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 43 (`0x2b`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 44 (`0x2c`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 45 (`0x2d`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 46 (`0x2e`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 47 (`0x2f`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 48 (`0x30`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 49 (`0x31`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 50 (`0x32`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 51 (`0x33`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 52 (`0x34`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 53 (`0x35`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 54 (`0x36`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 55 (`0x37`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 56 (`0x38`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 57 (`0x39`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 58 (`0x3a`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 59 (`0x3b`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 60 (`0x3c`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 61 (`0x3d`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 62 (`0x3e`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 63 (`0x3f`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 64 (`0x40`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 65 (`0x41`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 66 (`0x42`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 67 (`0x43`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 68 (`0x44`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 69 (`0x45`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 70 (`0x46`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 71 (`0x47`) | 0 | none (empty entry) | none | unnamed | no class, no sprite, no pass |
| 72 (`0x48`) | 2 | grey stone heap, largest at 72, smaller toward 79 | `0x42430f`, `0x424373` (terrain 5 to 6) | `stone` | class 2; sprite; one pass places all 8 sizes |
| 73 (`0x49`) | 2 | grey stone heap, largest at 72, smaller toward 79 | `0x42430f`, `0x424373` (terrain 5 to 6) | `stone` | class 2; sprite; one pass places all 8 sizes |
| 74 (`0x4a`) | 2 | grey stone heap, largest at 72, smaller toward 79 | `0x42430f`, `0x424373` (terrain 5 to 6) | `stone` | class 2; sprite; one pass places all 8 sizes |
| 75 (`0x4b`) | 2 | grey stone heap, largest at 72, smaller toward 79 | `0x42430f`, `0x424373` (terrain 5 to 6) | `stone` | class 2; sprite; one pass places all 8 sizes |
| 76 (`0x4c`) | 2 | grey stone heap, largest at 72, smaller toward 79 | `0x42430f`, `0x424373` (terrain 5 to 6) | `stone` | class 2; sprite; one pass places all 8 sizes |
| 77 (`0x4d`) | 2 | grey stone heap, largest at 72, smaller toward 79 | `0x42430f`, `0x424373` (terrain 5 to 6) | `stone` | class 2; sprite; one pass places all 8 sizes |
| 78 (`0x4e`) | 2 | grey stone heap, largest at 72, smaller toward 79 | `0x42430f`, `0x424373` (terrain 5 to 6) | `stone` | class 2; sprite; one pass places all 8 sizes |
| 79 (`0x4f`) | 2 | grey stone heap, largest at 72, smaller toward 79 | `0x42430f`, `0x424373` (terrain 5 to 6) | `stone` | class 2; sprite; one pass places all 8 sizes |
| 80 (`0x50`) | 2 | brown rock (80 tall, 81 low) | `0x42443b` (terrain 5 to 6) | `rock` | class 2; sprite |
| 81 (`0x51`) | 2 | brown rock (80 tall, 81 low) | `0x42443b` (terrain 5 to 6) | `rock` | class 2; sprite |
| 82 (`0x52`) | 1 | wooden cross | none; `MapGen_MarkPeaks` writes it on peaks | `cross` | sprite; `MapGen_MarkPeaks` puts it on peaks ([Map form](map-form.md)) |
| 83 (`0x53`) | 0 | tree stump | `0x424503` (terrain 5 to 6) | `stump` | `FirstHandledObject` 0x53 in `Game.Woodcutting`; [Woodcutting](woodcutting.md) calls it the stump |
| 84 (`0x54`) | 0 | small stone with grass (84 grey, 85 brown) | `0x424567` (terrain 5 to 6) | unnamed | class 0; sprite only |
| 85 (`0x55`) | 0 | small stone with grass (84 grey, 85 brown) | `0x424567` (terrain 5 to 6) | unnamed | class 0; sprite only |
| 86 (`0x56`) | 0 | pale bones (86) and a small pale piece (87) | `0x4245cb` (terrain 10) | unnamed | class 0; sprite only |
| 87 (`0x57`) | 0 | pale bones (86) and a small pale piece (87) | `0x4245cb` (terrain 10) | unnamed | class 0; sprite only |
| 88 (`0x58`) | 2 | grey rocks | `0x424693` (terrain 0 to 2) | unnamed | class 2; placed in water (terrain 0 to 2); sprite only |
| 89 (`0x59`) | 2 | grey rocks | `0x424693` (terrain 0 to 2) | unnamed | class 2; placed in water (terrain 0 to 2); sprite only |
| 90 (`0x5a`) | 1 | cactus | `0x42462f` (terrain 8 to 10) | `cactus` | class 1; sprite; placed on terrain 8 to 10 |
| 91 (`0x5b`) | 1 | cactus | `0x42462f` (terrain 8 to 10) | `cactus` | class 1; sprite; placed on terrain 8 to 10 |
| 92 (`0x5c`) | 1 | dead bare tree | `0x4243d7` (terrain 5 to 6) | `dead-tree` | class 1; sprite; the object pass leaves 0x54 to 0x5c alone ([Woodcutting](woodcutting.md)) |
| 93 (`0x5d`) | 1 | conifer falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x5d to 0x61 for codes 16 to 23 ([Woodcutting](woodcutting.md)) |
| 94 (`0x5e`) | 1 | conifer falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x5d to 0x61 for codes 16 to 23 ([Woodcutting](woodcutting.md)) |
| 95 (`0x5f`) | 1 | conifer falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x5d to 0x61 for codes 16 to 23 ([Woodcutting](woodcutting.md)) |
| 96 (`0x60`) | 1 | conifer falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x5d to 0x61 for codes 16 to 23 ([Woodcutting](woodcutting.md)) |
| 97 (`0x61`) | 0 | conifer falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x5d to 0x61 for codes 16 to 23 ([Woodcutting](woodcutting.md)) |
| 98 (`0x62`) | 1 | broad-leaved tree falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x62 to 0x66 for codes 8 to 15 ([Woodcutting](woodcutting.md)) |
| 99 (`0x63`) | 1 | broad-leaved tree falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x62 to 0x66 for codes 8 to 15 ([Woodcutting](woodcutting.md)) |
| 100 (`0x64`) | 1 | broad-leaved tree falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x62 to 0x66 for codes 8 to 15 ([Woodcutting](woodcutting.md)) |
| 101 (`0x65`) | 1 | broad-leaved tree falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x62 to 0x66 for codes 8 to 15 ([Woodcutting](woodcutting.md)) |
| 102 (`0x66`) | 0 | broad-leaved tree falling, stages 1 to 5 | none | `felled-tree` | the cut writes 0x62 to 0x66 for codes 8 to 15 ([Woodcutting](woodcutting.md)) |
| 103 (`0x67`) | 1 | small sapling (103 smaller) | none | unnamed | class 1; [Woodcutting](woodcutting.md) says codes from 0x67 grow, saplings to trees, without naming each code; not ported |
| 104 (`0x68`) | 1 | small sapling (103 smaller) | none | unnamed | class 1; [Woodcutting](woodcutting.md) says codes from 0x67 grow, saplings to trees, without naming each code; not ported |
| 105 (`0x69`) | 2 | field, green (105 to 107) to ripe yellow (108 to 110) | none | unnamed | class 2; sprite only; no handler ported |
| 106 (`0x6a`) | 2 | field, green (105 to 107) to ripe yellow (108 to 110) | none | unnamed | class 2; sprite only; no handler ported |
| 107 (`0x6b`) | 2 | field, green (105 to 107) to ripe yellow (108 to 110) | none | unnamed | class 2; sprite only; no handler ported |
| 108 (`0x6c`) | 2 | field, green (105 to 107) to ripe yellow (108 to 110) | none | unnamed | class 2; sprite only; no handler ported |
| 109 (`0x6d`) | 2 | field, green (105 to 107) to ripe yellow (108 to 110) | none | unnamed | class 2; sprite only; no handler ported |
| 110 (`0x6e`) | 2 | field, green (105 to 107) to ripe yellow (108 to 110) | none | unnamed | class 2; sprite only; no handler ported |
| 111 (`0x6f`) | 0 | harvested field | none | unnamed | class 0; sprite only |
| 112 (`0x70`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 113 (`0x71`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 114 (`0x72`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 115 (`0x73`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 116 (`0x74`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 117 (`0x75`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 118 (`0x76`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 119 (`0x77`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 120 (`0x78`) | 0 | sign on a post: yellow (112, 113), red (114, 115), black (116, 117), white (118, 119), blank (120); the odd code of each pair smaller | none | unnamed | class 0; sprite only; meaning not recovered |
| 121 (`0x79`) | 2 | ripe field | none | unnamed | class 2; sprite only |
| 122 (`0x7a`) | 2 | ripe field | none | unnamed | class 2; sprite only |
| 123 (`0x7b`) | 2 | ripe field | none | unnamed | class 2; sprite only |
| 124 (`0x7c`) | 2 | ripe field | none | unnamed | class 2; sprite only |
| 125 (`0x7d`) | 2 | ripe field | none | unnamed | class 2; sprite only |
| 126 (`0x7e`) | 2 | ripe field | none | unnamed | class 2; sprite only |
| 127 (`0x7f`) | 255 | none (empty entry) | none | unnamed | class 255; no pass |

## Open leads

- The codes 28 to 31 (in water), 84 to 89, the saplings 103 and 104, the fields 105 to 111 and 121 to 126, and the
  signs 112 to 120 need the handler that creates or reads them before they get a name.
