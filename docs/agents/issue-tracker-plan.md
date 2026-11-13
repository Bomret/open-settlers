# Slice store plan writes: GitHub Issues

The calls `/superdev:plan` makes on GitHub Issues and no other command makes: creating a plan's issues,
writing the anchor lines, and extending a feature. It uses the calls of `docs/agents/issue-tracker.md`
(**Edit an issue's body**, **Read a slice**, **Create a missing label**) and its *Configuration* values:
owner/repo `Bomret` / `open-settlers`, the token in env var `GITHUB_TOKEN`, REST sent through `gh api`.

## Plan-only fields

- **Parked questions**, **Non-goals**, and **Epic visuals** (all optional) ↔ `## Parked questions`,
  `## Non-goals`, and `## Slice graph`/`## Touch map` sections in the epic issue body — same body-text
  pattern as `acceptance`, epic-only and advisory (`/implement` never reads them; no equivalent on a
  single-slice feature's lone issue). GitHub renders mermaid in issue bodies, so the slice graph draws
  itself in the epic issue; `plan-authoring.md` owns their shape and when each is written.

## Creating issues

- **Labels exist first.** Before the first issue that carries a label, run **Create a missing label** for
  the feature label, the release label (`released`), the status labels (`ready`, `in-progress`,
  `in-review`, `blocked`), the `behavior`/`mechanical`/`docs` set, and `epic`. GitHub's docs don't specify
  how an unknown label name is handled on create, so creating them explicitly keeps the outcome from
  depending on it. Labels are addressed **by name** here, so nothing forces the lookup that would otherwise
  catch a missing one.
- **Two identifiers per issue.** A GitHub issue carries **both**, and they are not interchangeable:
  **`number`** — the repo-scoped `#<n>` a human sees, the slice's `id`, and what `#<epic>` references;
  **`id`** — the global database id, which **only** `sub_issue_id` and `issue_id` take. Capture both from
  the create response. Both relation calls mix them, which is where this goes wrong:

  | Call | Path takes | Body takes |
  |---|---|---|
  | `POST .../issues/<epic-number>/sub_issues` | the epic's **`number`** | `sub_issue_id` = the slice's **`id`** |
  | `POST .../issues/<blocked-number>/dependencies/blocked_by` | the blocked slice's **`number`** | `issue_id` = the blocker's **`id`** |

  Passing a `number` where an `id` is expected either 404s or links the wrong issue, and the mistake reads
  like a typo.
- **Create an issue:** `POST /repos/Bomret/open-settlers/issues` with
  `{ "title": "...", "body": "<specification + acceptance checklist>", "labels": ["<label>", …],
  "assignees": ["<assignee>"] }` — omit `assignees` entirely when the assignee resolves to `none`.
  Capture the returned `number` (the slice's `id`) **and** `id` (for the relation calls). Pass `labels`
  and `assignees` **at creation** — no separate label or assignee POST for the create path. With `gh`,
  `gh issue create` prints the issue URL rather than JSON: parse the `number` off it and fetch the `id`
  with **Read a slice**.
- **Link a slice to its epic (sub-issue):** `POST /repos/Bomret/open-settlers/issues/<epic-number>/sub_issues`
  with `{ "sub_issue_id": <the slice's global id> }`. Skip entirely for a single-slice feature, which has
  no epic. GitHub renders the epic's progress from these links (ADR 0011).
  **Best-effort:** this call needs Issues write, so the same missing permission that drops labels will
  `403`/`404` it. Log it and carry on — the link is display structure, and the **feature label**, not the
  link, is how slices are found.
- **Record a dependency (`depends-on`):**
  `POST /repos/Bomret/open-settlers/issues/<blocked-number>/dependencies/blocked_by` with
  `{ "issue_id": <the blocker's global id> }` → `201`.
  **Not best-effort**, unlike every other call here. `depends-on` is the execution DAG, and `/implement`
  cannot detect a lost edge later: it reads blockers from this API, so a slice missing one simply looks
  unblocked and gets run alongside the slice it must follow. The failure has to be loud at write time.
  On **any non-`201`** — permission, missing issue, validation, rate limit, or a transient `5xx` alike:
  name **every** failed edge as a slice-id pair, not just the first, and end `/plan`
  reporting failure. Nothing is deleted or rolled back — the issues that were created stay, and the plan is
  repaired by writing the missing edges, not by starting over. A token lacking Issues write fails every
  edge at once; report that as the single cause rather than one message per edge.
- **Parallel-safe creation order:** create the epic first (slices reference `#<epic>`), then fire **all
  slice creates in parallel**, then the sub-issue links **and** the dependency writes in parallel once
  every slice's `number` and `id` are known. Only the epic→slices→relations ordering is required; within
  each step the calls are independent.

## Anchor lines

Each is a body edit of the **anchor issue's own** body — the epic on a multi-slice feature, the lone slice
issue on a single-slice one — through **Edit an issue's body**, never a native relation, so the
slice-level and feature-level relations never collide on one issue.

- **Record an anchor dependency:** append a `**Blocked by feature:** #<blocker-anchor>` line. Not the
  `dependencies/blocked_by` call above, kept distinct so the two relations never collide on one issue
  (ADR 0040).
- **Record the deploy line:** append `**Deploy:** <target>[, <target>]` (in the operator's answer order)
  or `**Deploy:** none`, revising the line in place on an append that re-answers the deploy question
  (ADR 0101).
- **Record the Repairs line:** append `**Repairs:** #<original-anchor>` when `/superdev:plan` creates a
  repair, and never edit it afterwards (ADR 0124). Nothing is written to the original anchor.
- **Record the Absorbs line:** append `**Absorbs:** #<n>[, #<n>]` (every absorbed record's reference)
  when `/superdev:plan` absorbs records, revising the line in place on an append that absorbs more
  (ADR 0144).
- **Record the Intent line:** append `**Intent:** #<n>[, #<n>]` (every seeding intent record's reference)
  when `/superdev:plan` was seeded from an intent record, revising the line in place on an append seeded
  from another one (ADR 0200).

## Assignee

Operator configuration, not a slice field (`plan-contract.md` → *Assignee*; ADR 0010, ADR 0073). The
identifier is a GitHub **login**, read from `.superdev/info.json` under `issue-tracker.assignee`, and
applied to the **epic and every slice** in the create call, never a follow-up pass. The failure shape below
is the general *Assignee* recovery rule (`plan-contract.md`) made concrete for GitHub.

```json
{
  "issue-tracker": { "assignee": "<login>" }
}
```

- GitHub's `assignees` is an **array**, so the single configured login is sent as a one-element
  `["<assignee>"]`. The singular `assignee` field is documented as **closing down** — don't use it.
- **No file, no `issue-tracker` key, or no `assignee`** → resolve the token's own identity with
  `GET /user` and use its `login`.
- **`"assignee": "none"`** → omit `assignees` entirely. No warning, no error.
- **A token without push access silently drops the assignment** — and `labels` and `milestone` with it.
  The create **succeeds**, returns `201`, and the returned `assignees` is empty. Nothing is retried or
  rolled back.
- **A login GitHub cannot assign rejects the whole create**: a token with push access gets `422` with an
  `errors` entry naming `field: assignees`, `code: invalid`, and no issue exists. Retry that one create with
  `assignees` omitted, per the retry-once-without-the-assignee path in `plan-contract.md`, and report it
  once per run.
- **Check the returned `labels`, not just `assignees`.** Labels are sent on every create; an assignee is
  not sent at all under `"assignee": "none"`, so assignee-based detection is blind in exactly that
  configuration. An empty `labels` array on the response means the labels didn't stick — the documented
  cause is a token without push access — and since the feature label is the only listing path, those
  issues are unfindable. Report it **once** per run, independently of what the assignee is set to.
- **A leftover `Assignee:` bullet** in this repo's `docs/agents/issue-tracker.md` is configuration nothing
  reads. Report it once per run, naming the file and the move to `.superdev/info.json`.

## Definition of Done tags

`/superdev:plan` §6 writes each criterion's tag in `plan-contract.md`'s own notation (a slice `id`,
`scope`, or `docs`) when it creates the epic, before any slice issue exists. Once every slice issue is
created, the controller rewrites each slice-id tag into `#<number>`, in the same parallel step **Record a
dependency** above uses to write the native `blocked_by` links, each rewrite a body edit through **Edit an
issue's body** — both need the same resolved identifiers and both wait on the same event; a `scope` tag is
left as `scope`, and a `docs` tag as `docs`. That edit's producer is
`node ${CLAUDE_PLUGIN_ROOT}/scripts/scope.js dod-tags --map-file <path>`, the map file holding one
`<id> #<number>` pair per slice (ADR 0209). A tag naming several slices becomes `#<number>, #<number>`,
never bracketed. The controller types no store tag itself. `dod-tags` refuses, printing nothing, on any
unjudged line whose tag `dod-line` cannot read or whose slice id the map lacks, so the gate stops the edit
and the anchor body stays as it was.

## Append mode

`/superdev:plan` can extend a feature that already has issues (`plan-authoring.md` → *Append mode*,
ADR 0004).

- **Create only new records; never edit, close, or delete a merged slice.** Append fires new
  `POST /repos/Bomret/open-settlers/issues` calls for the new slices alone. It never `PATCH`es or closes an
  issue a prior run already merged, and never rewrites its labels, sub-issue link, or `blocked_by`
  dependency — except the anchor lines, which migrate to the new epic issue when append promotes a
  single-slice feature (below) — a merged issue's `number` is a satisfied `depends-on` root for a newly
  created one, nothing more.
- **Create the epic first when none exists.** A feature whose lone issue carries the feature label with
  no sub-issue parent has no epic yet (the single-slice shape, `issue-tracker.md`'s `## Scope anchor`).
  Append creates the epic issue first — its body holding the lone slice's `acceptance` checklist as the
  global DoD — then links every new slice as its sub-issue. The lone existing issue itself is never edited:
  no epic body copies onto it, no sub-issue link is written for it, and it stays nobody's child. **The
  `**Blocked by feature:**`, `**Deploy:**`, `**Repairs:**`, `**Absorbs:**`, and `**Intent:**` lines are the
  exceptions:** each **migrates to the new epic issue's own body** and is removed from the lone issue —
  the epic is the anchor now. Nothing else about the lone issue changes.
- **Amend the epic's own body — the one sanctioned edit.** An edit of the epic's body through **Edit an
  issue's body**, the amended body as its result, folds the interview's new criteria into the epic's
  Definition of Done in place, and, the same call, deletes a resolved parked question's line from the
  epic's `## Parked questions` section. Both are edits to the epic issue's own body, never to a merged
  slice's issue — the never-edit rule above still holds for every slice.

The `number` is server-assigned at creation, already unique and stable, so append names no continuation
rule for `id`.

## When a skill says "write slices to the store"

**≥2 slices** — create the epic first (global DoD), then one issue per slice, then link each slice as a
sub-issue of the epic and write the `blocked_by` dependencies. When the interview named a prerequisite
feature, also write the anchor dependency — a `**Blocked by feature:** #<n>` line — on the epic issue's
own body. **Exactly one slice** — skip the epic; create only the slice issue, folding the global DoD into
its `acceptance` checklist, and skip both relation calls entirely (a lone slice has no blocker and no
sub-issue parent). When the interview named a prerequisite feature, still write the anchor dependency on
that same lone slice issue: the anchor dependency is never skipped, because the lone slice is the anchor.
