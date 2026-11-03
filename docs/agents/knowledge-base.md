# Knowledge base: GitHub wiki

An external knowledge base for durable, human-facing docs (file-format specifications, design overviews),
published to the wiki of `Bomret/open-settlers` on GitHub. In-repo docs
(`docs/agents/documentation.md`) remain the source of truth; the wiki is a published view.

## Configuration

- **Backend:** GitHub repo Wiki, reached as the git repository
  `https://github.com/Bomret/open-settlers.wiki.git`. GitHub offers no wiki API, so every command clones
  that repository, changes one file, and pushes.
- **Auth:** the `gh` CLI's stored login for `github.com`. Every clone and every push passes
  `gh auth git-credential` as its credential helper and runs with `GIT_TERMINAL_PROMPT=0`, so no command
  waits on a password prompt. No token environment variable is read. No `gh`, or a `gh` with no login for
  `github.com`, stops the step: each template below answers exit 2, "could not answer", and
  `/superdev:publish-kb` leaves the page staged.
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

- **A page is one file.** A page titled `<title>` is the file `<title with each space as ->.md` at the
  root of the wiki repository. GitHub renders the file name, with each `-` shown as a space, as the page
  title. That file name without `.md` is the page's `sub_url`, and its URL is
  `https://github.com/Bomret/open-settlers/wiki/<sub_url>`.
- **A title holds no `/`.** A `/` cannot be part of a file name at the wiki root. Each template reads the
  title with `jq -er` on its own, checks that status, and refuses a missing, non-string, empty, or `/`-holding title
  with exit 2 and its cause on stderr, before it clones anything.
- **A page never touches the file system.** Each template clones without a checkout and reads and writes
  the page through git objects, so a title holding `:` or another character a Windows file name cannot
  hold still works. The page file name matches exactly, case included, on every file system. A title that differs only in case from an existing page (`home`
  beside `Home.md`) therefore reads absent, and `create` adds a second page.
- **Only `.md` pages count.** A page GitHub renders from another extension, such as `.markdown`,
  `.asciidoc`, or `.rst`, reads as absent, and `create` adds a `.md` page beside it.
- **The page is absent** only when a clone succeeds and holds no file for the title. That is the only
  outcome `/superdev:publish-kb` §4 treats as positive absence (ADR 0027). A failed clone could not answer.
- **The wiki repository exists only once the wiki holds a page.** GitHub creates it when the first page is
  saved in the web editor. Until then every clone fails and every command answers exit 2.
- **A write that changes nothing lands nothing.** `create` and `update` answer exit 0 without a commit when
  the new tree equals the cloned one.
- If `gh` is missing or not logged in, or the wiki cannot be cloned, `/superdev:publish-kb` **leaves every
  page staged**, reports why, and deletes nothing. A later run retries exactly the pages that did not land.

## Commands

The **backend command contract** (ADR 0069) `scripts/publish-kb.js` runs. Each command runs as
`sh -c <template> sh <path>`: `$1` names a JSON file holding `title`, `bodyFile` (`create` and `update`),
the `find` result's fields under `found` (`read`, `update`, and `delete`), and the `knowledge-base`
section of `.superdev/info.json` under `operator` (ADR 0207). Every command reads its values from that
file with `jq`. `find` emits the page's `sub_url`, and `/superdev:publish-kb` fills a `parent-link`
target's `{sub_url}` from it (`sub_url` is the contract's optional named `find` key, ADR 0168). `find`
answers exit 3 only when the clone succeeded and holds no file for the title; any other failure answers
exit 2, "could not answer," so a page never gets created from a clone that failed. Every failure exit
writes its cause to stderr.

Every template opens with the same lines: check the `gh` login for `github.com`, read the title, shallow-clone
the wiki without a checkout into a temporary directory that is removed on exit, and derive the page file from the title. So
each one stands alone under the bound below. The templates use POSIX `sh` and options every platform's
tools accept.

Every command below runs under a **30-second bound** (ADR 0130), or whatever
`knowledge-base.timeoutMs` of `.superdev/info.json` names. `scripts/publish-kb.js` applies it at the spawn.
A command killed at the bound leaves its page staged and **stops the run**: the remaining pages are
reported untried and stay staged for the next one.

### find

```sh
gh auth status --hostname github.com >/dev/null 2>&1 || { echo "superdev: gh is missing or not logged in to github.com; run: gh auth login --hostname github.com" >&2; exit 2; }
dir=$(mktemp -d) || { echo "superdev: cannot create a temporary directory" >&2; exit 2; }; trap 'rm -rf "$dir"' EXIT
title=$(jq -er '.title | select(type == "string" and length > 0)' "$1") || { echo "superdev: the values file holds no title" >&2; exit 2; }
case "$title" in */*) echo "superdev: the title holds /, which a wiki file name cannot: $title" >&2; exit 2 ;; esac
file="$(printf '%s' "$title" | tr ' ' '-').md"
GIT_TERMINAL_PROMPT=0 git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 --no-checkout https://github.com/Bomret/open-settlers.wiki.git "$dir/w" 2>"$dir/err" || { echo "superdev: cannot clone https://github.com/Bomret/open-settlers.wiki.git: $(cat "$dir/err"); check the wiki is enabled and holds a page" >&2; exit 2; }
git -C "$dir/w" cat-file -e "HEAD:$file" 2>/dev/null || exit 3
jq -cn --arg s "${file%.md}" '{sub_url: $s}'
```

### read

```sh
gh auth status --hostname github.com >/dev/null 2>&1 || { echo "superdev: gh is missing or not logged in to github.com; run: gh auth login --hostname github.com" >&2; exit 2; }
dir=$(mktemp -d) || { echo "superdev: cannot create a temporary directory" >&2; exit 2; }; trap 'rm -rf "$dir"' EXIT
title=$(jq -er '.title | select(type == "string" and length > 0)' "$1") || { echo "superdev: the values file holds no title" >&2; exit 2; }
case "$title" in */*) echo "superdev: the title holds /, which a wiki file name cannot: $title" >&2; exit 2 ;; esac
file="$(printf '%s' "$title" | tr ' ' '-').md"
GIT_TERMINAL_PROMPT=0 git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 --no-checkout https://github.com/Bomret/open-settlers.wiki.git "$dir/w" 2>"$dir/err" || { echo "superdev: cannot clone https://github.com/Bomret/open-settlers.wiki.git: $(cat "$dir/err"); check the wiki is enabled and holds a page" >&2; exit 2; }
git -C "$dir/w" cat-file blob "HEAD:$file" 2>"$dir/err" || { echo "superdev: cannot read the wiki file $file: $(cat "$dir/err")" >&2; exit 1; }
```

### create

```sh
gh auth status --hostname github.com >/dev/null 2>&1 || { echo "superdev: gh is missing or not logged in to github.com; run: gh auth login --hostname github.com" >&2; exit 2; }
dir=$(mktemp -d) || { echo "superdev: cannot create a temporary directory" >&2; exit 2; }; trap 'rm -rf "$dir"' EXIT
title=$(jq -er '.title | select(type == "string" and length > 0)' "$1") || { echo "superdev: the values file holds no title" >&2; exit 2; }
case "$title" in */*) echo "superdev: the title holds /, which a wiki file name cannot: $title" >&2; exit 2 ;; esac
file="$(printf '%s' "$title" | tr ' ' '-').md"
GIT_TERMINAL_PROMPT=0 git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 --no-checkout https://github.com/Bomret/open-settlers.wiki.git "$dir/w" 2>"$dir/err" || { echo "superdev: cannot clone https://github.com/Bomret/open-settlers.wiki.git: $(cat "$dir/err"); check the wiki is enabled and holds a page" >&2; exit 2; }
body=$(jq -er '.bodyFile' "$1") || { echo "superdev: the values file holds no bodyFile" >&2; exit 1; }
blob=$(git -C "$dir/w" hash-object -w -- "$body" 2>"$dir/err") || { echo "superdev: cannot store $body: $(cat "$dir/err")" >&2; exit 1; }
git -C "$dir/w" ls-tree -z HEAD >"$dir/tree" || { echo "superdev: cannot list the wiki tree" >&2; exit 1; }
tree=$({ tr '\000' '\n' <"$dir/tree" | f="$file" awk '{ n = $0; sub(/^[^\t]*\t/, "", n); if (n != ENVIRON["f"]) print }'; printf '100644 blob %s\t%s\n' "$blob" "$file"; } | tr '\n' '\000' | git -C "$dir/w" mktree -z) || { echo "superdev: cannot build the wiki tree for $file" >&2; exit 1; }
[ "$tree" = "$(git -C "$dir/w" rev-parse 'HEAD^{tree}')" ] && exit 0
login=$(gh api --hostname github.com user --jq .login 2>"$dir/err") && [ -n "$login" ] || { echo "superdev: cannot look up the gh user on github.com: $(cat "$dir/err")" >&2; exit 1; }
commit=$(git -C "$dir/w" -c user.name="$login" -c user.email="$login@users.noreply.github.com" commit-tree "$tree" -p HEAD -m "superdev: publish" 2>"$dir/err") && git -C "$dir/w" update-ref HEAD "$commit" 2>"$dir/err" || { echo "superdev: git commit failed: $(cat "$dir/err")" >&2; exit 1; }
GIT_TERMINAL_PROMPT=0 git -C "$dir/w" -c credential.helper= -c credential.helper='!gh auth git-credential' push -q origin HEAD 2>"$dir/err" || { echo "superdev: git push failed: $(cat "$dir/err")" >&2; exit 1; }
```

### update

```sh
gh auth status --hostname github.com >/dev/null 2>&1 || { echo "superdev: gh is missing or not logged in to github.com; run: gh auth login --hostname github.com" >&2; exit 2; }
dir=$(mktemp -d) || { echo "superdev: cannot create a temporary directory" >&2; exit 2; }; trap 'rm -rf "$dir"' EXIT
title=$(jq -er '.title | select(type == "string" and length > 0)' "$1") || { echo "superdev: the values file holds no title" >&2; exit 2; }
case "$title" in */*) echo "superdev: the title holds /, which a wiki file name cannot: $title" >&2; exit 2 ;; esac
file="$(printf '%s' "$title" | tr ' ' '-').md"
GIT_TERMINAL_PROMPT=0 git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 --no-checkout https://github.com/Bomret/open-settlers.wiki.git "$dir/w" 2>"$dir/err" || { echo "superdev: cannot clone https://github.com/Bomret/open-settlers.wiki.git: $(cat "$dir/err"); check the wiki is enabled and holds a page" >&2; exit 2; }
git -C "$dir/w" cat-file -e "HEAD:$file" 2>/dev/null || { echo "superdev: the wiki holds no file $file to update" >&2; exit 1; }
body=$(jq -er '.bodyFile' "$1") || { echo "superdev: the values file holds no bodyFile" >&2; exit 1; }
blob=$(git -C "$dir/w" hash-object -w -- "$body" 2>"$dir/err") || { echo "superdev: cannot store $body: $(cat "$dir/err")" >&2; exit 1; }
git -C "$dir/w" ls-tree -z HEAD >"$dir/tree" || { echo "superdev: cannot list the wiki tree" >&2; exit 1; }
tree=$({ tr '\000' '\n' <"$dir/tree" | f="$file" awk '{ n = $0; sub(/^[^\t]*\t/, "", n); if (n != ENVIRON["f"]) print }'; printf '100644 blob %s\t%s\n' "$blob" "$file"; } | tr '\n' '\000' | git -C "$dir/w" mktree -z) || { echo "superdev: cannot build the wiki tree for $file" >&2; exit 1; }
[ "$tree" = "$(git -C "$dir/w" rev-parse 'HEAD^{tree}')" ] && exit 0
login=$(gh api --hostname github.com user --jq .login 2>"$dir/err") && [ -n "$login" ] || { echo "superdev: cannot look up the gh user on github.com: $(cat "$dir/err")" >&2; exit 1; }
commit=$(git -C "$dir/w" -c user.name="$login" -c user.email="$login@users.noreply.github.com" commit-tree "$tree" -p HEAD -m "superdev: publish" 2>"$dir/err") && git -C "$dir/w" update-ref HEAD "$commit" 2>"$dir/err" || { echo "superdev: git commit failed: $(cat "$dir/err")" >&2; exit 1; }
GIT_TERMINAL_PROMPT=0 git -C "$dir/w" -c credential.helper= -c credential.helper='!gh auth git-credential' push -q origin HEAD 2>"$dir/err" || { echo "superdev: git push failed: $(cat "$dir/err")" >&2; exit 1; }
```

### delete

```sh
gh auth status --hostname github.com >/dev/null 2>&1 || { echo "superdev: gh is missing or not logged in to github.com; run: gh auth login --hostname github.com" >&2; exit 2; }
dir=$(mktemp -d) || { echo "superdev: cannot create a temporary directory" >&2; exit 2; }; trap 'rm -rf "$dir"' EXIT
title=$(jq -er '.title | select(type == "string" and length > 0)' "$1") || { echo "superdev: the values file holds no title" >&2; exit 2; }
case "$title" in */*) echo "superdev: the title holds /, which a wiki file name cannot: $title" >&2; exit 2 ;; esac
file="$(printf '%s' "$title" | tr ' ' '-').md"
GIT_TERMINAL_PROMPT=0 git -c credential.helper= -c credential.helper='!gh auth git-credential' clone -q --depth 1 --no-checkout https://github.com/Bomret/open-settlers.wiki.git "$dir/w" 2>"$dir/err" || { echo "superdev: cannot clone https://github.com/Bomret/open-settlers.wiki.git: $(cat "$dir/err"); check the wiki is enabled and holds a page" >&2; exit 2; }
git -C "$dir/w" cat-file -e "HEAD:$file" 2>/dev/null || { echo "superdev: the wiki holds no file $file to delete" >&2; exit 1; }
git -C "$dir/w" ls-tree -z HEAD >"$dir/tree" || { echo "superdev: cannot list the wiki tree" >&2; exit 1; }
tree=$(tr '\000' '\n' <"$dir/tree" | f="$file" awk '{ n = $0; sub(/^[^\t]*\t/, "", n); if (n != ENVIRON["f"]) print }' | tr '\n' '\000' | git -C "$dir/w" mktree -z) || { echo "superdev: cannot remove $file from the wiki tree" >&2; exit 1; }
login=$(gh api --hostname github.com user --jq .login 2>"$dir/err") && [ -n "$login" ] || { echo "superdev: cannot look up the gh user on github.com: $(cat "$dir/err")" >&2; exit 1; }
commit=$(git -C "$dir/w" -c user.name="$login" -c user.email="$login@users.noreply.github.com" commit-tree "$tree" -p HEAD -m "superdev: publish" 2>"$dir/err") && git -C "$dir/w" update-ref HEAD "$commit" 2>"$dir/err" || { echo "superdev: git commit failed: $(cat "$dir/err")" >&2; exit 1; }
GIT_TERMINAL_PROMPT=0 git -C "$dir/w" -c credential.helper= -c credential.helper='!gh auth git-credential' push -q origin HEAD 2>"$dir/err" || { echo "superdev: git push failed: $(cat "$dir/err")" >&2; exit 1; }
```

Human-consumption first: shape pages around the audience, write in the language above, follow the outline.

## superdev usage

- `/superdev:plan` — seeds the in-repo feature specification/ADRs into the outline and stages their pages.
- `/superdev:document` — reconciles and extends the KB (via its doc-writer) and stages the pages it
  changed, after a scope lands and on demand.
- `/superdev:publish-kb` — sends the staged pages to the wiki. The only command that calls it.

## Degrade / switching

No `gh`, no `gh` login for `github.com`, or a wiki that cannot be cloned →
`/superdev:publish-kb` leaves every page staged, reports why, and deletes nothing; a later run retries
exactly the pages that did not land. The in-repo docs are the record meanwhile. To move to a different
knowledge base later, re-run `/superdev:setup`.
