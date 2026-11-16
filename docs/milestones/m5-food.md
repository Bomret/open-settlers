# M5 Food: scope

Goal and done condition: [roadmap, M5](../roadmap.md#m5-food). Marks: [Leads](AGENTS.md#leads).

## Goods order

`0x42055e` (known) calls these routines when it fills the player blocks of a new game:

- `0x445570`: writes the default transport priorities of the 26 goods before the player block
  (player `-0x54`): `Player_SetTransportPriorities` in the
  [program index](../rules/program-index.md#program-start-and-game-flow), ported
  ([Goods](../rules/goods.md#transport-priorities)). known
- `0x445643`: fills a second order of the 26 goods at player `+0x60` to `+0x79`. likely
- `0x445716`, `0x445752`, `0x44579c`, `0x4457e6`: store default fractions at player `+0x140` to
  `+0x150` and around player `-0x80`, perhaps the distribution of food and other goods. likely

## Known

- The building types of the fisher (1), the farm (0xc), the butcher (0xd), the pig farm (0xe), the mill (0xf), and the baker
  (0x10), their jobs, tools, sizes, and materials, and the goods 1 to 26 (1 to 6: fish, pig, meat, grain, flour, bread; 7 to 10:
  log, plank, boat, stone; 11 to 15: iron ore, iron, coal, gold ore, gold; 16 to 24: the nine tools; 25 and 26: sword, shield) with
  the classes of all goods: [Food buildings](../rules/food-buildings.md). The names of the two grain-fed chains, of the goods 9 and 11
  to 26, are inferred: [which evidence settles each](../rules/food-buildings.md#which-chain-is-which). known
- The original has no well: [Food buildings](../rules/food-buildings.md#the-well). known
- The fisher's work cycle: the search for a node at a shore (`0x433f71`), the arrival (`0x438232`), the cast (`0x433e05`), the catch from the
  lake details, and the fish on the flag: [Fishing](../rules/fishing.md). known

## Open questions

- Is good 25 the sword and good 26 the shield, or the reverse? [Food buildings](../rules/food-buildings.md#the-goods) names them in that
  order from the sprite of good 25; no routine read puts or asks either, and a layout cannot lay good 26 on a flag.
- The work cycle of the farm's fields (`0x433b35`, `0x438391`) and of the indoor jobs' states `0x23` to `0x26`.
