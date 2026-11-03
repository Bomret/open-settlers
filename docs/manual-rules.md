# Rules stated in the game manual

This file lists the game rules that the English manual of The Settlers states. The manual is
`THE SETTLERS - Manual-EN.pdf`, a file of the History Edition install (ADR 0004). Its text is the
original Blue Byte manual (copyright 1993) and covers the Amiga and PC versions. Each rule is in our own
words, with the manual's page number. Page numbers can be off by one.

These are claims of the manual. They are not program evidence. A rule here is a lead for an
investigation in `Siedler1.exe`. Before the engine uses it, confirm it at an address and record it in
`docs/rules/` (see `docs/rules/AGENTS.md`).

## World

- A world has a size from 1 to 8. (p. 19, 108)
- The world wraps in both directions. It has no edges. (p. 24)
- A 16-digit number (each digit 1 to 8) and the world size select the world. The same number and size
  give the same world. An option also derives the opponents from the same number. (p. 20, 94–95)
- Terrain classes on the map: meadow (green), desert (yellow), water (blue), mountain (brown, white by
  height). (p. 23)
- The maximum number of settlers per player depends on the world size: 500, 1000, 2000, 4000, 8000,
  16000, 32000, 64000 for sizes 1 to 8. The growth rate sets how often new settlers arrive, not this
  maximum. (p. 101)

## Game setup

- Each player has three settings: stock size, growth rate, and (for computer players) intelligence.
  (p. 19)
- A player starts with 20 settlers. Some already have a job. (p. 33)
- There are ten computer opponents, each with a fixed behavior: Lady Amalie, Kumpy Onefinger, Balduin,
  Frollin, Kallina, Rasparuk, Count Aldaba, King Rolph VII, Homen Doublehorn, Sollok the Joker.
  (p. 101–103)
- The training missions use world size 3. (p. 108)

## Castle and territory

- The player places the castle first. It is the only building that is not built by settlers. (p. 27)
- The castle needs a flat, grassy area. (p. 26)
- The geologist estimate of the area (gold, iron, coal, granite) is available only before the castle
  is placed. (p. 25, 27)
- The territory border shows as black and white poles on land and red posts in water. A player builds
  only inside its territory. (p. 27)
- The territory grows when the first knight enters a guard hut, watchtower, or castle. (p. 40)
- If an enemy military building finishes first near the border, the border moves. The player loses
  land, and a building on the lost land burns. (p. 62, 92)

## Building placement

- The cursor shows what can go at a spot: nothing, a flag only, a small building, a large building, or
  a mine. (p. 28)
- A building must be on own land, with no tree or rock on the spot. (p. 29)
- A small or large building needs meadow. A mine needs mountain, but not snow. (p. 29)
- A large building needs level ground. Steep slopes forbid it. (p. 29)
- A military building cannot be built in the mountains. (p. 53)
- A large building first gets a leveler, who flattens the ground. Then construction starts. (p. 30, 33)
- Every building has a flag in front of it. (p. 30)
- The large-building menu offers 20 buildings. (p. 29)
- Ordering a new building on an existing one burns the old one (needs a special click). (p. 36)

## Construction

- A construction site needs a road to a warehouse or the castle before work starts. (p. 30)
- Building materials are wood (planks) and, for some buildings, stone. (p. 34)
- A guard hut needs one delivery of wood and stone. A castle needs at least 10. (p. 40)
- A builder (yellow helmet) works the site. When done, the builder goes back to the nearest warehouse.
  Then the right worker for the building moves in. (p. 34, 59)
- The player can give many build orders at once. (p. 35)

## Roads and flags

- A road always runs from flag to flag. A flag has 6 directions, so at most 6 roads. (p. 30)
- Each road step shows the slope: green flat, yellow moderate, red steep. (p. 31)
- Settlers tire faster on slopes. A road is as fast as its slowest segment. (p. 32, 48)
- A flag cannot be placed on a road next to an existing flag, or on a tree. (p. 32)
- A road runs on land or on water, never both. (p. 33)
- A water way runs between two flags on the shore. Only boats use it, and only for goods. (p. 32–33)
- Two parallel roads between the same flags are allowed. Goods use the less crowded one. (p. 49)
- When a road is built, a carrier leaves the castle to work that road. (p. 31, 33)
- A carrier moves goods one road at a time, from flag to flag. (p. 33)
- Route choice: the route with fewer roads (fewer flags) wins over a shorter route with more flags.
  (p. 107)
- A changed road net slows transport at first. Goods on a new road wait for a new carrier. (p. 104, 107)

## Demolition

- Demolishing needs a special click. (p. 35)
- A demolished building burns. Its worker goes back to a warehouse. (p. 35)
- A demolished road sends its carriers back to a warehouse. Other settlers on it walk to the next flag.
  (p. 35–36)
- A flag can be removed only if it has no building, and it has no road or exactly two roads. With two
  roads, the two roads merge into one. (p. 36)

## Raw materials

- Trees: a woodcutter fells a tree and carries the trunk home. A sawmill makes planks from trunks.
  (p. 43–44)
- A forester plants trees. Trees grow and can then be felled. A forester cannot plant on roads.
  (p. 44, 105)
- Stone: a stonecutter breaks stone from granite rocks on the surface. Rocks do not grow back. Granite
  also exists underground. (p. 43–44)
- Underground resources (gold, iron, coal, granite) exist only in mountains. (p. 25)
- A geologist is sent to a connected flag. The geologist samples nearby ground and places a sign: small
  or large circle for little or much; yellow gold, red iron, black coal, light grey granite; blank for
  nothing. The geologist returns if there are no mountains. Signs fade over time. (p. 52–54)
- A geologist request fails without free workers or tools. Two requests need 2 seconds between them.
  (p. 54)
- Deposits run out. A low deposit yields less. A message comes when a mine is empty. (p. 54)
- A worker can work on enemy land, for example to fell trees, harvest, or mine. (p. 107)

## Food

- Fish: a fisher catches fish from water. Fish breed while some remain. Too many fishers empty a lake.
  A screen-sized lake feeds 3 or 4 fishers. (p. 46, 105)
- Bread: farm grows wheat, mill makes flour, bakery makes bread. (p. 46–47)
- Meat: farm wheat feeds pigs at the pig farm, butcher makes meat. (p. 47)
- A farm needs free land for fields. Roads are not free land. (p. 105)
- Miners eat fish, bread, or meat. A mine without food stops; the miner strikes. (p. 47–48, 79)
- Mine yield shows as the share of trips that brought ore. (p. 78)

## Production chains

- Iron foundry (smelter): iron ore + coal → iron. (p. 64, 79)
- Gold foundry (mint): gold ore + coal → gold. (p. 64, 79)
- Weapon smith: iron + coal → sword and shield. (p. 56, 79)
- Tool maker: iron + planks → one of 9 tools. (p. 57, 79)
- Boat builder: planks → boat. (p. 56)
- Goods stored per building: boat builder (planks), sawmill (trunks), farm (wheat), butcher (pigs),
  mill (wheat), baker (flour), smelter (iron ore, coal), mint (gold ore, coal), weapon smith (iron,
  coal), tool maker (iron, planks). (p. 79)
- Woodcutter, forester, fisher, stonecutter, and farm need no input. (p. 79–80)

## Tools per job

| Job | Tool |
|---|---|
| Leveler | shovel |
| Fisher | fishing rod |
| Butcher | cleaver |
| Farmer | scythe |
| Woodcutter | axe |
| Sawmill worker | saw |
| Miner | pick |
| Stonecutter | pick |
| Builder | hammer |
| Boat builder | hammer |
| Geologist | hammer |
| Tool maker | hammer and saw |
| Weapon smith | hammer and pliers |

Baker and carrier need no tool. (p. 57, 74)

- A settler with no job takes a job only if a warehouse has the tool. If two needed tools are in
  different warehouses, the goods move to one warehouse. (p. 77–78)
- At the start, some settlers already carry the key tools, so tool making cannot dead-lock. (p. 110)

## Warehouses

- A warehouse works like the castle: stores goods, holds idle settlers. (p. 56, 58)
- New settlers split between castle and warehouses. (p. 58)
- Surplus goods go to the nearest warehouse. A site orders from the nearest warehouse that has the
  goods. (p. 58–59)
- A worker done with a job goes to the nearest warehouse. A new worker comes from the nearest
  warehouse. (p. 59)
- Each warehouse has two three-way switches, one for goods and one for settlers: accept, do not accept,
  or empty out. (p. 76–77)
- A conquered or burned warehouse loses its goods. Only some settlers escape. (p. 77)

## Distribution and priorities

- Distribution menus split food across the 4 mine types, planks across construction / tool maker /
  boat builder, iron across weapon smith / tool maker, coal across smelter / mint / weapon smith, and
  wheat across pig farm / mill. (p. 81–82)
- A bar at zero means that building type gets nothing. A bar twice as long gets twice as much. When a
  good is scarce, only the top receiver gets it. More stock reaches the lower receivers. (p. 82–83)
- The tool menu sets the ratio of the 9 tools. Default: many hammers and picks, few scythes and pliers.
  (p. 89–90)
- The transport priority list orders all goods. Default: planks first, gold low. (p. 90)
- An evacuation priority list orders which goods leave a warehouse first. (p. 91)

## Emergency program

- At the start, a reserve of 2 stone and 7 planks exists. It is enough for a woodcutter, a sawmill, and
  a stonecutter. (p. 109)
- When castle building stock is too low, only woodcutter, sawmill, and stonecutter sites get material.
  A message tells when this starts and when it stops. (p. 92, 109–110)

## Military

- Military buildings and knight capacity: guard hut 3, watchtower 6, castle 12. (p. 40)
- Gold capacity: guard hut 2, watchtower 4, castle 8. (p. 79)
- A building flag shows threat: white (no enemy), stroke (third line), cross (second line), thick cross
  (front line). (p. 40–41, 83–84)
- There are 5 knight ranks. A new knight starts at the lowest rank. Knights train fast in the castle or a
  warehouse and slowly in a military building. (p. 60)
- The knight rate (default 30 %) is the share of settlers that may become knights, if swords and
  shields exist. A knight needs a sword and a shield. (p. 78, 85–86)
- The player can recruit idle settlers as knights directly, limited by weapons in stock. (p. 86)
- Per threat line, the player sets the target occupation and the minimum that stays during an attack.
  Default front-line text "full/good", back to "minimum" in the hinterland. The target cannot be
  below the minimum. (p. 84–85)
- A knight that leaves for an attack is replaced from a warehouse, if one waits there. (p. 85)
- An option picks whether weak or strong knights attack. Default: weak ones attack. (p. 86)
- A swap option sends weak knights back to warehouses to train and brings strong ones. (p. 86)

## Combat

- Only military buildings (guard hut, watchtower, castle) can be attacked. (p. 60)
- The attack window shows available knights by distance. Some knights always stay to defend. (p. 61)
- Attackers walk to the target. One defender comes out per fight. One of the two knights wins. (p. 61)
- When no defender is left, the attacker takes the building. Borders change. Enemy buildings on lost
  land burn. (p. 61)
- Knight strength = rank and motivation. Motivation grows with gold stored in military buildings.
  Gold in transport does not count. Defense strength does not depend on gold. (p. 87–88)
- Early in the game an attack needs about 4:1 strength to win. (p. 87–88)
- A conquered castle (the headquarters) burns with its goods and settlers. The winner's morale rises,
  the loser's falls. (p. 88)

## End of game

- The game ends when one player is left. (p. 110)
- A player can give up with the white flag. (p. 88, 110)
- When several players remain and none can make weapons (no knights, no ore or coal), the game is a
  draw. (p. 110–111)

## Messages

The game sends a message for: first knight in a military building, new warehouse, geologist find, empty
mine, attack on the player, conquest, building lost, land lost to an enemy building (with or without
buildings), emergency program start and stop. It keeps more than 50 messages. (p. 91–92)
