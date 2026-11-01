# M6 Mining and metal: scope

Goal and done condition: [roadmap, M6](../roadmap.md#m6-mining-and-metal). Marks: [Leads](AGENTS.md#leads).

## Resources

- The resource layer of the map: known (Map tiles). The resource types 1 to 4 are open in the program
  index.
- `MapGen_CountResource1` and `Map_Resource1Total`: known.
- `0x444780`, `0x444954`: scan the map and count four classes of objects or resources into player
  `+0x12a` to `+0x130`. guess

## Food and distribution

- `0x445716`, `0x445752`, `0x44579c`, `0x4457e6`: default distribution fractions; see
  [M5](m5-food.md). Some may set the food each mine receives. guess

## Open questions

- Where are the geologist, the mines, the smelters, and the toolmaker? No lead names them.
- What does the rule data at `+0xb0` hold? `0x41e8e0` fills it with 199 entries, and `0x43f537` reads
  it. It may hold per-building rules.
