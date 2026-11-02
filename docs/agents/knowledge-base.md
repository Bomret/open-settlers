# Knowledge base: GitHub wiki

An external knowledge base for durable, human-facing docs (file-format specifications, design overviews),
published to the wiki of `Bomret/open-settlers` on GitHub. In-repo docs
(`docs/agents/documentation.md`) remain the source of truth; the wiki is a published view.

## Configuration

- **Backend:** Other wiki: the GitHub repository wiki of `Bomret/open-settlers`, a git repository at
  `https://github.com/Bomret/open-settlers.wiki.git`. GitHub has no wiki API, so every command clones that
  repository, changes one page file, and pushes.
- **Auth:** the `gh` CLI's login (`GITHUB_TOKEN`). Git reaches the wiki through `gh auth git-credential`.
  Every command first checks that the login has push access to `Bomret/open-settlers`, and exits 2 when
  `gh` is missing, cannot authenticate, or has no push access.
- **What to publish:** file-format specifications, design overviews, player guides, and the
  changelog — durable, reader-facing docs only, not every `AGENTS.md`.
- **Hierarchy:** links. A GitHub wiki nests no pages. Each child page links from a list item in its
  parent's body, and `/superdev:publish-kb` writes that link (ADR 0168).

## Audience, language & outline

The wiki is **human-consumption first**.

- **Audience(s):** players, modders, contributors/developers.
- **Language:** English. All wiki prose is written in this language, per the declarations in
  `docs/agents/documentation.md`.

## Outline

A living skeleton. `/superdev:plan` and `/superdev:document` fill and extend it.

- **`Home`.** The root page, with no parent. Hand-written. Its section `Pages` holds the link list to the
  audience pages, which `/superdev:publish-kb` maintains.
- **Audience pages** (`Players`, `Modders`, `Developers`). One page per audience. Each holds a section
  `Pages` with the link list to its own pages. Parent: `Home`, section `Pages`.
- **Player guides** (`Getting started`, `Known limitations`). One task or limitation per page. Parent:
  `Players`, section `Pages`.
- **File formats** (`Graphics archives`, `Graphics images`, `Music file`, `Sound files`, `Text files`,
  `Map start`, `Map form`, `Map objects`). One page per rule note of the same name in `docs/rules/`, which stays the source. Parent:
  `Modders`, section `Pages`.
- **Modding guides** (`Asset loading`). How a modder reaches and replaces game content. Parent: `Modders`,
  section `Pages`.
- **Developer pages** (`Architecture`, `Build and setup`, `Game rules`). One topic per page. `Game rules`
  summarizes `docs/rules/timing.md` and `docs/rules/game-code.md`; the program index stays in the
  repository. Parent: `Developers`, section `Pages`. The `Developers` page links to
  [`docs/roadmap.md`](https://github.com/Bomret/open-settlers/blob/main/docs/roadmap.md) by hand,
  outside its `Pages` section, instead of a wiki copy.
- **`Changelog`.** `(aggregate)`: one `## <feature>` section per shipped feature, never deeper and never
  `#`. Parent: `Developers`, section `Pages`.

**Create the root page first.** A child published before its root lands, but stays staged as not linked,
because its parent has no page to carry the link yet.

**`(aggregate)` marks a page many scopes contribute to.** Such a page is staged one section at a time
(`action: patch`) and merged into the live page by that section's heading. Every other page is staged
whole (ADR 0027).

**Links.** A parent's link line has the form `- [<title>](https://github.com/Bomret/open-settlers/wiki/{sub_url}): <one line>`.
`<title>` is the child's full title. `{sub_url}` is written literally. The doc-writer writes the whole line,
placeholder included, and `/superdev:publish-kb` replaces `{sub_url}` with the value the find call returns
for the child once the child has landed. `/superdev:publish-kb` then places the line after the last
top-level list item of the named section's own content, or below the heading as its first list item when
that content holds none yet.

**Page names.** A page is the file `<name>.md` at the wiki repository's root. `<name>` is the title with
each space replaced by `-`: `Build and setup` is `Build-and-setup.md`, and its URL ends in
`/wiki/Build-and-setup`. GitHub shows the file name with `-` turned back into spaces as the title, so a
title that holds a `-` shows with a space there.

## Staging

This is a **remote** backend, so nothing but `/superdev:publish-kb` calls it (ADR 0023). `/superdev:plan`
and `/superdev:document` write each page as a **staged page** under `.superdev/kb-pending/<scope>/` in the
main working tree, resolved by `scope.js staging <scope>` (ADR 0119). Each carries YAML frontmatter with
the page `title` and an `action` of `upsert`, `patch`, or `delete`, and the page body below. A page reaches
the wiki when a human runs `/superdev:publish-kb`, after the feature has reached the readers this wiki is
written for.

## Wiki git conventions

- **Clone:** `git clone --depth 1 https://github.com/Bomret/open-settlers.wiki.git` into a temporary
  directory, with `gh auth git-credential` as the only credential helper. The directory is removed on exit.
- **A wiki with no page has no git repository.** GitHub creates it when the first page is saved in the web
  UI, so a failed clone answers exit 2 with that hint.
- **Commits** carry the checkout's `user.name` and `user.email`, and the message `superdev: publish <title>`
  or `superdev: delete <title>`.

## Commands

The **backend command contract** (ADR 0069) `scripts/publish-kb.js` runs. Each command runs as
`sh -c <template> sh <path>`: `$1` names a JSON file holding `title`, `bodyFile` (`create` and `update`),
the `find` result's fields under `found` (`read`, `update`, and `delete`), and the `knowledge-base`
section of `.superdev/info.json` under `operator` (ADR 0207). Every command reads its values from that
file with `jq`. Every template opens with the same prelude: the push-access check, the clone, and the page
name derived from `title`. A failed prelude answers exit 2. `find` emits `{"sub_url": "<name>"}` when
`<name>.md` exists, and exits 3 when it does not. `read`, `update`, and `delete` take `.found.sub_url`.
`create` and `update` share one template: they write the body file to `<name>.md`, commit, and push.
`read`, `create`, `update`, and `delete` map any failure to exit 1.

Every command below runs under a **30-second bound** (ADR 0130), or whatever
`knowledge-base.timeoutMs` of `.superdev/info.json` names. `scripts/publish-kb.js` applies it at the spawn. A command killed at
the bound leaves its page staged and **stops the run**: the remaining pages are reported untried and stay
staged for the next one.

### find

```sh
W=https://github.com/Bomret/open-settlers.wiki.git
push=$(gh api repos/Bomret/open-settlers --jq .permissions.push 2>/dev/null) || { echo "superdev: gh cannot read Bomret/open-settlers; check GITHUB_TOKEN or run: gh auth login" >&2; exit 2; }
[ "$push" = true ] || { echo "superdev: the GitHub login has no push access to Bomret/open-settlers" >&2; exit 2; }
d=$(mktemp -d) || exit 2; trap 'rm -rf "$d"' EXIT
git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 "$W" "$d" 2>/dev/null || { echo "superdev: cannot clone $W; create the wiki's first page on GitHub if it has none" >&2; exit 2; }
title=$(jq -er '.title' "$1") || exit 2
name=$(printf '%s' "$title" | tr ' ' '-')
[ -f "$d/$name.md" ] || exit 3
jq -cn --arg s "$name" '{sub_url: $s}'
```

### read

```sh
W=https://github.com/Bomret/open-settlers.wiki.git
push=$(gh api repos/Bomret/open-settlers --jq .permissions.push 2>/dev/null) || { echo "superdev: gh cannot read Bomret/open-settlers; check GITHUB_TOKEN or run: gh auth login" >&2; exit 2; }
[ "$push" = true ] || { echo "superdev: the GitHub login has no push access to Bomret/open-settlers" >&2; exit 2; }
d=$(mktemp -d) || exit 2; trap 'rm -rf "$d"' EXIT
git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 "$W" "$d" 2>/dev/null || { echo "superdev: cannot clone $W; create the wiki's first page on GitHub if it has none" >&2; exit 2; }
title=$(jq -er '.title' "$1") || exit 2
name=$(printf '%s' "$title" | tr ' ' '-')
s=$(jq -er '.found.sub_url' "$1") || exit 1
cat "$d/$s.md" || exit 1
```

### create

```sh
W=https://github.com/Bomret/open-settlers.wiki.git
push=$(gh api repos/Bomret/open-settlers --jq .permissions.push 2>/dev/null) || { echo "superdev: gh cannot read Bomret/open-settlers; check GITHUB_TOKEN or run: gh auth login" >&2; exit 2; }
[ "$push" = true ] || { echo "superdev: the GitHub login has no push access to Bomret/open-settlers" >&2; exit 2; }
d=$(mktemp -d) || exit 2; trap 'rm -rf "$d"' EXIT
git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 "$W" "$d" 2>/dev/null || { echo "superdev: cannot clone $W; create the wiki's first page on GitHub if it has none" >&2; exit 2; }
title=$(jq -er '.title' "$1") || exit 2
name=$(printf '%s' "$title" | tr ' ' '-')
cp "$(jq -er '.bodyFile' "$1")" "$d/$name.md" || exit 1
git -C "$d" add -A && git -C "$d" -c user.name="$(git config user.name)" -c user.email="$(git config user.email)" commit -qm "superdev: publish $title" || exit 1
git -C "$d" -c credential.helper= -c credential.helper='!gh auth git-credential' push -q origin HEAD 2>/dev/null || exit 1
```

### update

```sh
W=https://github.com/Bomret/open-settlers.wiki.git
push=$(gh api repos/Bomret/open-settlers --jq .permissions.push 2>/dev/null) || { echo "superdev: gh cannot read Bomret/open-settlers; check GITHUB_TOKEN or run: gh auth login" >&2; exit 2; }
[ "$push" = true ] || { echo "superdev: the GitHub login has no push access to Bomret/open-settlers" >&2; exit 2; }
d=$(mktemp -d) || exit 2; trap 'rm -rf "$d"' EXIT
git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 "$W" "$d" 2>/dev/null || { echo "superdev: cannot clone $W; create the wiki's first page on GitHub if it has none" >&2; exit 2; }
title=$(jq -er '.title' "$1") || exit 2
name=$(printf '%s' "$title" | tr ' ' '-')
cp "$(jq -er '.bodyFile' "$1")" "$d/$name.md" || exit 1
git -C "$d" add -A && git -C "$d" -c user.name="$(git config user.name)" -c user.email="$(git config user.email)" commit -qm "superdev: publish $title" || exit 1
git -C "$d" -c credential.helper= -c credential.helper='!gh auth git-credential' push -q origin HEAD 2>/dev/null || exit 1
```

### delete

```sh
W=https://github.com/Bomret/open-settlers.wiki.git
push=$(gh api repos/Bomret/open-settlers --jq .permissions.push 2>/dev/null) || { echo "superdev: gh cannot read Bomret/open-settlers; check GITHUB_TOKEN or run: gh auth login" >&2; exit 2; }
[ "$push" = true ] || { echo "superdev: the GitHub login has no push access to Bomret/open-settlers" >&2; exit 2; }
d=$(mktemp -d) || exit 2; trap 'rm -rf "$d"' EXIT
git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 "$W" "$d" 2>/dev/null || { echo "superdev: cannot clone $W; create the wiki's first page on GitHub if it has none" >&2; exit 2; }
title=$(jq -er '.title' "$1") || exit 2
name=$(printf '%s' "$title" | tr ' ' '-')
s=$(jq -er '.found.sub_url' "$1") || exit 1
git -C "$d" rm -q "$s.md" || exit 1
git -C "$d" -c user.name="$(git config user.name)" -c user.email="$(git config user.email)" commit -qm "superdev: delete $title" || exit 1
git -C "$d" -c credential.helper= -c credential.helper='!gh auth git-credential' push -q origin HEAD 2>/dev/null || exit 1
```

## superdev usage

- `/superdev:plan` — seeds the in-repo feature specification/ADRs into the outline and stages their pages.
- `/superdev:document` — reconciles and extends the KB (via its doc-writer) and stages the pages it
  changed, after a scope lands and on demand.
- `/superdev:publish-kb` — sends the staged pages to the wiki. The only command that calls it.

## Degrade / switching

No `gh` on the path, a `gh` login without push access, a wiki with no page yet, or an unreachable host →
`/superdev:publish-kb` leaves every page staged, reports why, and deletes nothing; a later run retries
exactly the pages that did not land. The in-repo docs are the record meanwhile. To move to a different
knowledge base later, re-run `/superdev:setup`.
