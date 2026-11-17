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
- The mill's work cycle (state `0x23`, `0x43386b`), its request for grain and its rank (`0x43d33d`, `0x42e2a0`, the player word at `+0x15a`), the miller's figure (`0x4400d3`), the castle's hand-out of grain, and the fill byte of a food good: [Milling](../rules/milling.md). known
- The original has no well: [Food buildings](../rules/food-buildings.md#the-well). known
- The farmer's work cycle (the search at `0x433b35`, the sowing and harvest at `0x433a3a`, the arrival at `0x438391`), the fields' codes
  (`0x69` to `0x6f`, `0x79` to `0x7e`), their growth and decay in the object pass (the table entries 22 to 28 and 38 to 43, and the decay
  counter at `+0x28c`), and the farm's removal: [Farming](../rules/farming.md). known
- The butcher's work cycle (state `0x26`, `0x4332a8`), his request for pigs and its rank (`0x43d275`, `0x42e1b3`), the butcher's figure (`0x43ff90`), the castle's hand-out of pigs, and the shop's removal: [Butchering](../rules/butchering.md). known

## Open questions

- Is good 25 the sword and good 26 the shield, or the reverse? [Food buildings](../rules/food-buildings.md#the-goods) names them in that
  order from the sprite of good 25; no routine read puts or asks either, and a layout cannot lay good 26 on a flag.
- The work cycle of each food job: the fisher's walk to the water (`0x433f71`, `0x438232`) and the indoor jobs' states `0x24` and
  `0x25`. The mill's `0x23`, the farm's fields, and the butcher's `0x26` are known ([Milling](../rules/milling.md), [Farming](../rules/farming.md), [Butchering](../rules/butchering.md)).
