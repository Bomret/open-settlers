# 20. Content is a catalog of named entries, and the original's codes stay at its edge

**Status:** Accepted

Amends [ADR 0010](0010-open-settlers-names-what-the-original-left-as-codes.md),
[ADR 0013](0013-the-simulation-is-typed-and-the-original-s-bytes-stay-at-its-edge.md),
[ADR 0016](0016-jobs-are-types-and-each-layer-finds-its-job-modules.md), and
[ADR 0018](0018-a-content-set-carries-its-own-rules-and-one-flag-picks-it.md).

Modders are to add goods, buildings, and jobs after the free-play release. Content that is identified by the
original's numbers, with rules that branch on those numbers, has to be unpicked before any of that. So a good, a
building type, and a job are entries of an engine-owned content catalog. Each entry's identity is its stable name
(`log`, `castle`, `woodcutter`). Its original code is an optional attribute, and an entry the original lacks has
none. The catalog holds one typed entry record per kind, each with its name, its original code, and the facts the
rules read about it. It orders its entries by stable name, so a game plays the same on every run.

A content set's source builds the catalog through the rule tables port. The History Edition importer builds the
original's entries and fills their facts from the tables of `Siedler1.exe`. The basic set's reader builds entries
without original codes from its own data files. Only the History Edition importer and the exporters read an
original code. Where an exporter must write a code for an entry that has none, it writes the FNV-1a hash of the
entry's stable name, so the digests of the original's games do not change. A repository test holds the domain and
the application to this: they read no original code.

## Considered options

The decision weighed these options:

- One shared entry shape for every kind, with its facts in a bag: one type and one catalog interface, but the facts
  lose their types, against ADR 0013.
- A good that holds a reference to its catalog entry: it carries its facts, but the named goods and building types
  the rules use could not exist before an importer has built the catalog.
- A good that holds its position in the catalog: compact, but the position is a code again, and an added entry
  shifts it.
- Reserve the original's free good codes 27 to 31 for added goods: nothing in the encoders changes, but it allows
  five added goods and keeps the code as the identity.
- Number an added entry 32 plus its position among the added entries in the digest: readable values, but a game's
  digest shifts when another added entry sorts before it.
- Give the basic set's entries the original's codes in their data files: layouts and readouts stay numeric under
  both sets, but hand-written data then holds the original's codes, close to the encodings ADR 0018 keeps out of
  those files.
- A coding guideline that no rule branches on a code value: nothing would enforce it.

## Consequences

The decision has these consequences:

- The name table keeps only map objects and settler states. Goods, building types, and jobs take their names from
  their entries, and a name still needs its evidence (ADR 0010).
- A job's stable name sits on its domain type. Its original number is its original code. The job registries keep
  their order by type name (ADR 0016).
- The limits of the original (26 goods, 32 good classes, the 5-bit good field) hold only in the History Edition
  importer and the exporters. An added good gets its stock place, its transport rank, and its request from its
  entry.
- Layouts give goods by stable name, so one layout opens under both content sets.
- The content set keys its good images by stable name.
