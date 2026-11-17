# M6 Content catalog: scope

Goal and done condition: [roadmap, M6](../roadmap.md#m6-content-catalog). Marks: [Leads](AGENTS.md#leads).

M6 moves facts that the rules derive from an original code today into the entries of the content catalog
([ADR 0020](../adr/0020-content-is-a-catalog-of-named-entries-and-the-original-s-codes-stay-at-its-edge.md),
[INV-0014](../invariants.md#inv-0014)). Each lead below is a code-value fact to read from its table into an
entry, or a limit to move to the edge.

## Facts of a good

- `Good_DestinationClasses` (`0x464117`): the request class and the request word of each good. known
- `Good_Kinds` (`0x462d6d`): the fill counter, low or high, a good counts against. known
- `Settler_DeliverGood` (`0x43c4ea`): the counter a carrier's handover fills. The routine tests the good's
  code, not the kind table. known
- `Good_CountLost` (`0x462c8d`): whether a lost good counts against the total of resource 1. The routine
  tests the codes 14 and 15. known
- `Player_SetTransportPriorities` (`0x445570`): the default transport rank of each good. known

## Facts of a building type

- `Building_SizeTable` (`0x42d307`): the size of each type. known
- `Building_NeedsTable` (`0x449f18`): the planks and the stones each type needs. known
- `Building_DoorSpeeds` (`0x43c92b`): the door speed of each type, standing and constructing. known
- `0x42cd1d`: the handler table of the finished building types, which names the job and the tool each type
  asks for. known (`Table18_Pass`)
- `Builder_MaterialOrder` (`0x43f0ba`): the order of the materials of each type. known
- `Builder_BlowSteps` (`0x43f0fa`): the steps a blow adds to the progress of each type. known
- `Builder_Start` (`0x43ce20`): the mask `0x4a0400`, tested at `0x43ceb4`, names the types whose site takes
  a second figure. known

## Limits to move to the edge

- The goods are numbered 0 to 31 in the program, and the rules mask a good code with `0x1f`. The mask
  belongs to the importer and the exporters ([INV-0008](../invariants.md#inv-0008)). known
- The original has 26 goods. The count belongs to the History Edition importer, not to the domain's rule
  tables. known
