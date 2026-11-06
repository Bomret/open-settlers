# 10. Open Settlers names what the original left as codes

**Status:** Accepted

The original holds map objects, settler jobs and states, and building types as bare numbers, a form its
memory and speed limits forced. Open Settlers gives each of them a name of its own in one name table:
`tree`, `stone`, `carrier`, `walks-along-road`, `woodcutter`. The gameplay stays the original's
([ADR 0003](0003-rule-fidelity-not-state-identity.md)); the names are Open Settlers' own.

A name enters the table only with evidence: a rule the engine already ports, the original's class table,
a `docs/rules/` page, or the code's sprite. A code without evidence keeps no name, and everything that
shows it shows the raw code. The raw value always shows beside the name.

## Considered options

The decision weighed these options:

- Show raw codes only. Always true, but play scripts and readouts become unreadable where the meaning is
  known.
- Name only what the engine already holds as a code constant. Stones and most settler states would stay
  numbers, though their meaning is known.

## Consequences

The decision has these consequences:

- A new name needs its evidence cited at its table entry.
- Play-script checks and lists use the table's names, so a renamed entry changes the scripts that use it.
