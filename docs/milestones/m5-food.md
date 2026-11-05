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

## Open questions

- Where are the fisher, the farm, the mill, the baker, the pig farm, the butcher, and the well? No lead
  names them. The object dispatch `0x42fc81` and the `Table18` slice `0x42cb2d` are the starting points.
- Which goods numbers 1 to 26 are which goods?
