# 11. The engine records what changed and why

**Status:** Accepted

Play scripts need to follow a settler, building, or flag over time, see its links, and see why it
changed. The engine gives each of these things a thing ID (`settler#12.3`): its slot and a count of how
often that slot was filled. Every rule that changes such a thing records an event in one event store on
the game. The event's type is the rule that made the change, and it carries the thing's state after the
change and the cause. The cause is the root of the chain: the player's command, or the rule area that
acted on its own. A thing's state at an earlier tick is the state in its last event up to that tick. The
store keeps every event, and it is always on.

The counters and the store stay outside the state hash and outside every rule's decision (INV-0007), so
the gameplay stays the original's ([ADR 0003](0003-rule-fidelity-not-state-identity.md)).

## Considered options

The decision weighed these options:

- The play run hands out IDs by comparing slots from tick to tick. It cannot see a slot emptied and filled
  within one tick, so two things would share one ID.
- Snapshot checks only: save an ID and check its state later, with no history. The engine can hold the
  history, and one source of truth beats scripts that rebuild it.
- One untyped "changed" event from the setters, with causes added later. A setter knows what changed but
  not why. Each rule knows its cause, and the set of rules is closed.
- Events for a few rule areas first. A half-covered store makes scripts guess which changes they can
  trust.
- One event type per original settler state. Most states have no name with evidence yet (ADR 0010), and a
  rule that sets several fields still makes one change.
- A copy of every thing on every tick. It costs memory on every tick, and the events already hold each
  state.
- A cause that names only the immediate trigger. Scripts would rebuild the chain back to the player's
  command themselves.

## Consequences

The decision has these consequences:

- A new rule that changes a settler, building, or flag records its events, with their cause, when it is
  built.
- Goods have no thing ID. Following one good needs its own ID carried through the goods rules.
- The store grows with the game, at roughly 30 MB for an hour of play. A limit waits until a long run
  needs one.
