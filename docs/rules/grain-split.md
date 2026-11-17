# The split of grain

This note covers how grain is shared when the mill and the pig farm both ask for it. It covers the two player words that the rank of each
request starts from. It covers how the request search and the castle's hand-out turn those ranks into a split, for the grain of a farm and
the grain of the castle's stock alike. It also covers the roles of the other words around them. The ranks are in
[Milling](milling.md#the-rank-of-the-request-for-grain) and [Pig farming](pig-farming.md#the-rank-of-the-request-for-grain). The search and the
hand-out are in [Materials for a building site](construction-materials.md#the-castles-hand-out) and [Goods at a flag](goods.md). Addresses and
routine names are in the [Program index](program-index.md#simulation).

**Emulated.** The harness verb `construct:` (`tools/GhidraDump/emulate.py`) runs two finished buildings at once with the phase routine
`0x42965c`, as the verbs of the other food notes do for one. The buildings are a pig farm (type 14) at the flag 15,9 and a mill (type 15)
at the flag 18,6. Two fixtures in `tests/OpenSettlers.Tests/` hold the result:

- `grain-split.txt` is the castle's stock of five grains handed out while both buildings ask. The engine matches the request words of
  both flags, the fill bytes of both buildings, the castle's grain count, and the generator.
- `grain-split-farm.txt` is a grain put on a third flag, where a farm's flag would be, with no destination. The engine matches the same bytes.

The emulated pass numbers are in those files.

## No separate split of grain

**Status:** confirmed.

No fraction and no counter shares grain between the two buildings. `Player_SetDefaults3` (`0x44579c`) stores two words at the player's
`+0x158` (`0xffdc`) and `+0x15a` (`0x7fee`). They are the start ranks of the two requests, and the program reads nothing else from them:

- The pig farm's handler (`0x42e0fa`) reads `+0x158` at `0x42e184`. The mill's handler (`0x42e2a0`) reads `+0x15a` at `0x42e34f`. A search of
  the whole program for an access to the displacements `0x152`, `0x154`, `0x156`, `0x158`, and `0x15a` finds no other read of these words in
  the game's code. The other hits are the stores of `0x44579c` itself and a byte store at `0x428a24` into another structure. The rest are
  pushes of the number or accesses to other structures at `0x47e7d2` and above, none in a routine that handles the player block. The hits
  above `0x47e000` were not read one by one. That no routine in the unanalysed code reads the words is therefore **inferred** from the
  search alone.
- The words at `+0x152`, `+0x154`, and `+0x156`, stored by the same routine, have no read through a fixed displacement either. Their role is open.

The rank of a request is its start rank shifted right by the goods the building counts, which are both nibbles of its fill byte. The mill
starts at `0x7f` and the pig farm at `0xff`, and each grain halves what the request asks with. Both requests are class 4 of the first request
word, as [Food buildings](food-buildings.md#the-goods) shows, so the request search sees both.

## How the ranks split the grain

**Status:** confirmed for the rule, and **emulated**. The pointer the pig farm reads its word through is a **stand-in**
([Pig farming](pig-farming.md#the-rank-of-the-request-for-grain)). The split rests on it only where a pig farm's handler runs after the
castle's with no handler between them that loads the player block. The engine reads the player word in every case. The rest of the split is
no stand-in.

The castle's hand-out gives one grain in each cycle to the flag whose request ranks highest in the level search. The cycle holds the grain
step of each of the three orders. The grain of a flag that has no destination goes to the same search ([The search](goods.md)). That search
takes a request only where its rank is above the best so far, and it grows the best by a quarter and one after each level of flags. Grain
from either source therefore goes by these rules:

1. The highest rank wins, and a farther request must outrank a nearer one grown by a quarter and one for each level between them.
2. Equal ranks go to the flag the search meets first: within a level, the road directions are tried from the highest number down.
3. One asker takes all grain until its rank is 0 (the mill at seven grains held, the pig farm at eight). With neither asking the grain stays in the castle.

**Castle stock** (`grain-split.txt`). The castle holds five grains at the pass 700. The pig farm's flag and the mill's flag each lie one
level from the castle's flag, and the pig farm's road leaves by a higher direction. The first four hand-outs go to:

1. The pig farm, by `0xff` against `0x7f`.
2. The pig farm, because both ranks are `0x7f` and the search meets the pig farm first.
3. The mill, by `0x7f` against `0x3f`.
4. The pig farm, because both ranks are `0x3f` and the search meets the pig farm first.

The pig farm ends up with one grain more than the mill. The fifth cycle's hand-out depends on the castle's outstanding slots, which the
engine does not keep ([Materials](construction-materials.md#stand-ins-and-what-is-not-ported)). The original hands nothing out in the cycle
of the pass 856 and gives the fifth grain to the mill at the pass 889, both in the fixture's lines to the pass 899. The engine hands the
fifth out at 856, so the test checks the passes before 856.

**Grain from a farm** (`grain-split-farm.txt`). A grain on the flag of a farm lies one level from the mill's flag and three from the pig
farm's. It goes to the pig farm, whose `0xff` outranks the mill's `0x7f` grown by a quarter and one for the level between them. That
growth gives `0x9f`, then `0xc7` at the pig farm's level. The put and the hand-out take the same search, so the two sources share one
rule in the engine (`FlagRequestSearch`).

## What the engine keeps

The split has no state of its own. The counters it moves are the fill bytes of the two buildings, their request ranks, and the castle's
stock, which the digest holds. The same seed and commands give the same digest. `GrainSplitTests` runs the castle's split and the farm's
split twice and compares the digests. It shows that a mill's fill byte, one more grain in the castle, or the farm's grain changes them. It
also pins the cases above, including the case of both buildings full while neither asks.

## The second order of the goods

**Status:** the role is **inferred**; the engine does not port it.

`Player_SetGoodsOrder` (`0x445643`) writes a second order of the 26 goods at the player's `+0x60` to `+0x79`. The split does not read it.
The phase routine `0x42965c` reads it in the branch for a storage whose item has bit 1 of its second byte set. The engine does not port that
branch ([Materials](construction-materials.md#stand-ins-and-what-is-not-ported)). The storage then picks, among the goods its stock holds,
the one with the highest value in the order. It puts that good in an outstanding slot to hand out, with the first free destination. The
castle's own item has that bit clear in every game of the fixtures, so the castle uses the three orders of hand-out steps and never this
order. The bit means a storage that sends all its goods away, which is inferred from the branch's shape.

## The program's other default words

`0x4457e6` and the sibling `0x445716` are not read by the split either; their roles stay open ([Program index](program-index.md#simulation), [M5 open questions](../milestones/m5-food.md#open-questions)).
