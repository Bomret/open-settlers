# 2. Milestones grow by the playable loop

**Status:** Accepted

The roadmap uses milestones with the free-play release as the first goal. The foundation milestones are
analysis access, the asset layer, and the map. After them, the roadmap grows the game one production
chain at a time. Growth starts from the smallest playable loop: a castle, a road with flags, carriers,
and a woodcutter whose log reaches storage. Each milestone ends in a mode a person can run and check by
hand.

The transport network is the backbone of the game, so it carries real load from the first playable
milestone. As a result, faults in the interplay of systems surface early, for example in carrier
congestion or in goods priorities.

## Considered options

The decision weighed these options:

- Grow by system: clear boundaries per system, but the game becomes playable late and faults in the
  interplay of systems surface late. This option completes each system before the next, in this order:
  building rules, transport, all production chains, military, and the computer opponent.
- Headless simulation first, rendering last: the game shows nothing for a long time, and the asset
  readers stay unverified until the end.
- Two phases, a faithful phase and then an extension phase: missions, music, and the split-screen mode
  are original content that fits neither phase.
