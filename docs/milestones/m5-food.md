# M5 Food: scope

Goal and done condition: [roadmap, M5](../roadmap.md#m5-food). Marks: [Leads](AGENTS.md#leads).

## Goods order

The order of the goods fixes the transport priorities that the castle's hand-out and the flags' goods use, and so the food goods' share
of the traffic in M5. `0x42055e` (known) calls these routines when it fills the player blocks of a new game:

- `0x445570`: writes the default transport priorities of the 26 goods before the player block
  (player `-0x54`): `Player_SetTransportPriorities` in the
  [program index](../rules/program-index.md#program-start-and-game-flow), ported
  ([Goods](../rules/goods.md#transport-priorities)). known

## Known

- The split of grain between the mill and the pig farm: [Grain split](../rules/grain-split.md). known
  - The two start ranks at player `+0x158` and `+0x15a` (`0x44579c`).
  - The request search that ranks both requests, and the castle's hand-out.
  - The second order of the goods at player `+0x60` (`0x445643`), which belongs to a storage branch that is not ported.
- The building types of the fisher (1), the farm (0xc), the butcher (0xd), the pig farm (0xe), the mill (0xf), and the baker
  (0x10), their jobs, tools, sizes, and materials, and the goods 1 to 26 (1 to 6: fish, pig, meat, grain, flour, bread; 7 to 10:
  log, plank, boat, stone; 11 to 15: iron ore, iron, coal, gold ore, gold; 16 to 24: the nine tools; 25 and 26: sword, shield) with
  the classes of all goods: [Food buildings](../rules/food-buildings.md). The names of the two grain-fed chains, of the goods 9 and 11
  to 26, are inferred: [which evidence settles each](../rules/food-buildings.md#the-evidence-for-the-chains). known
- The mill: [Milling](../rules/milling.md). known
  - The work cycle (state `0x23`, `0x43386b`).
  - The request for grain and its rank (`0x43d33d`, `0x42e2a0`, the player word at `+0x15a`).
  - The miller's figure (`0x4400d3`), the castle's hand-out of grain, and the fill byte of a food good.
- The baker: [Baking](../rules/baking.md). known
  - The work cycle (state `0x24`, `0x433694`).
  - The request for flour and its rank (`0x43d3d9`, `0x42e37e`), a constant start rank of `0xff`.
  - The baker's figure (`0x440112`).
- The pig farm: [Pig farming](../rules/pig-farming.md). known
  - The work cycle (state `0x25`, `0x4333c1`).
  - The request for grain and its rank (`0x43d179`, `0x42e0fa`, the player word at `+0x158`). The handler reads the player block through a
    scratch pointer.
  - The growth of the pigs the farm holds (the table at `0x433684`), the round that may give a pig, and the pig farmer's figure (`0x43ff56`).
- The original has no well: [Food buildings](../rules/food-buildings.md#the-well). known
- The farmer's work cycle (the search at `0x433b35`, the sowing and harvest at `0x433a3a`, the arrival at `0x438391`), the fields' codes
  (`0x69` to `0x6f`, `0x79` to `0x7e`), their growth and decay in the object pass (the table entries 22 to 28 and 38 to 43, and the decay
  counter at `+0x28c`), and the farm's removal: [Farming](../rules/farming.md). known
- The fisher's work cycle: the search for a node at a shore (`0x433f71`), the arrival (`0x438232`), the cast (`0x433e05`), the catch from the
  lake details, and the fish on the flag: [Fishing](../rules/fishing.md). known
- The butcher's work cycle (state `0x26`, `0x4332a8`), his request for pigs and its rank (`0x43d275`, `0x42e1b3`), the butcher's figure (`0x43ff90`), the castle's hand-out of pigs, and the shop's removal: [Butchering](../rules/butchering.md). known

## Open questions

- Which handlers between the castle's (`0x42e7e0`) and a pig farm's in the same group of 32 slots load the player block before the pig
  farm's handler reads its rank, and which slot orders occur in a game? The engine always reads the player word, a stand-in:
  [Pig farming](../rules/pig-farming.md#the-rank-of-the-request-for-grain).
- Is good 25 the sword and good 26 the shield, or the reverse? [Food buildings](../rules/food-buildings.md#the-goods) names them in that
  order from the sprite of good 25; no routine read puts or asks either, and a layout cannot lay good 26 on a flag.
- What are the player words at `+0x152`, `+0x154`, and `+0x156` (`0x44579c`) for? No read through a fixed displacement is found
  ([Grain split](../rules/grain-split.md#no-separate-split-of-grain)).
- What are the player words `0x445716` (`Player_SetDefaults1`) stores at `+0x140` to `+0x146`, and the nine words `0x4457e6` stores
  from player `-0x80` to `-0x70`, for? Both are called by `0x42055e` only. The split of grain does not read them, and no other reader is
  found; the roles are open ([Program index](../rules/program-index.md#simulation)).
- What are the player words `0x445752` (`Player_SetDefaults2`) stores at `+0x14a` to `+0x150` for? Only the word at `+0x148` has a
  known reader (the planks' rank, [Materials](../rules/construction-materials.md)); the four words after it have no read through a fixed
  displacement that a search finds.
- The castle's start holds 5 jobless settlers and the engine makes no more ([Castle](../rules/castle.md)): a carrier for each road and
  a worker for each food building without a tool need 11 for the whole chain. How the original brings new settlers is open
  ([Construction](../rules/construction.md): the player counter at `+0x116`, not ported); `play/food-chain.play` builds the three
  buildings that 5 can staff.
