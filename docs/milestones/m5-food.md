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

- The building types of the fisher (1), the farm (0xc), the baker (0xd), the mill (0xe), the pig farm (0xf), and the butcher
  (0x10), their jobs, tools, sizes, and materials, and the goods 1 to 6 (fish, flour, bread, grain, pig, meat) with the classes
  of all goods: [Food buildings](../rules/food-buildings.md). known
- The original has no well: [Food buildings](../rules/food-buildings.md#the-well). known

## Open questions

- Which of the goods 9 and 11 to 26 are which goods? [Food buildings](../rules/food-buildings.md#the-goods) names the tool goods
  by their jobs and leaves goods 9, 11 to 15, 25, and 26 for M6.
- The work cycle of each food job: the fisher's walk to the water (`0x433f71`, `0x438232`), the farm's fields (`0x433b35`,
  `0x438391`), and the indoor jobs' states `0x23` to `0x26`.
