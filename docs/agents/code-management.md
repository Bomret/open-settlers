# Code host: GitHub

Code lives on GitHub. Use the `gh` CLI for push + pull requests. `gh` infers the repo from
`git remote -v` when run inside a clone.

## Configuration

- **Owner / repo**: `Bomret/open-settlers`.
- **Remote**: `origin` (`https://github.com/Bomret/open-settlers.git`).
- **Default PR posture**: `ready`.
- **Base branch**: `main`.

## Conventions

- **Push the scope branch**: `git push -u origin <scope-branch>`.
- **Open the PR**:
  `gh pr create --base <base> --head <scope-branch> --title "$title" --body-file <file>`, `title` set from
  a quoted heredoc (`title=$(cat <<'SUPERDEV_TEXT_EOF' … SUPERDEV_TEXT_EOF)`) so a backtick or `$(` in it stays
  literal. Use a body file (heredoc) built per `/superdev:submit-pr`'s body rules.
- **Draft**: the same with `--draft`, for a scope that opens as a draft.
- **Never interpolate a store-derived value into a shell string.** The title is composed per
  `references/commit-messages.md` from the scope anchor's title, which a multi-writer slice store makes untrusted.
  Pass it only through the quoted heredoc above, and always use `--body-file` for the body — never build a command
  line by concatenating a title/body that could carry `"`/`` ` ``/`$( )`.
- **Report**: print the PR URL (`gh pr view --json url --jq .url`).

## Commit messages

The repository's commit convention, owned by the operator. Every commit, merge commit, and pull-request title
superdev writes here follows it (`references/commit-messages.md`).

- **Rule**: Conventional Commits. A header `type(scope): subject`, the type the kind of change, the scope the feature's
  slug where there is a feature (optional otherwise), the subject in the imperative; a body where the change needs one.
- **Commit**: `test(busy-roads): cover the second carrier on a busy road`
- **Merge commit**: `feat(busy-roads): merge slice/busy-roads/02`, its body naming the merged branch or commit.
- **Pull-request title**: `feat(busy-roads): give busy roads more carriers`

## Degrade when `gh` is absent

Do not fail. Push the branch, then print the compare URL
(`https://github.com/Bomret/open-settlers/compare/<base>...<scope-branch>?expand=1`) and the exact
`gh pr create ...` command the user can run once `gh` is available.
The merge call below degrades the same way: print the exact `gh pr merge`/REST call and merge nothing.
The check-status read below degrades the same way: print the exact `gh api` calls and judge nothing.
The pull-request close below degrades the same way: print the exact `gh pr close` call and close nothing.

## Merge

- **Read the pull request**: `gh pr view <number> --json state,mergeable,headRefOid,mergeCommit,title` (or
  `GET /repos/Bomret/open-settlers/pulls/<pull_number>` for `state`, `merged`, `merge_commit_sha`, `head.sha`,
  `mergeable`, and `title`). GitHub computes `mergeable` asynchronously: while it does, REST answers `null` and
  `gh pr view --json mergeable` answers `UNKNOWN`. Both read as not yet known: re-read up to 3 times with a short
  pause and, if it is still not known, report it as **unknown** — never as mergeable.
- **Read the draft state**: `gh pr view <number> --json isDraft --jq .isDraft` prints `true` for a draft.
- **Mark ready**: `gh pr ready <number>` takes a draft pull request out of draft. GitHub refuses to merge a
  draft, so `/superdev:ship` calls it before the merge under the merge consent.
- **Merge it, pinned**: `gh pr merge --squash --match-head-commit <tested-tip-sha> --subject "$title" <number>` (or
  `PUT /repos/Bomret/open-settlers/pulls/<pull_number>/merge` with
  `{ "merge_method": "squash", "commit_title": "<pr-title>", "sha": "<tested-tip-sha>" }`), `title` the pull
  request's title read right before the merge and set from a quoted heredoc as the PR-create line does, so the
  squash commit on base carries the title `/superdev:submit-pr` composed.
  `--match-head-commit`/`sha` pins the merge to that sha: the host answers 409 when the head moved
  since. Never pass `--admin` (or any other check/review bypass) — this seed never sends it.
- **A refusal names the reason**: 405 means the pull request cannot be merged (an unmet required
  review or status check, or it is already merged/closed); 409 means the head moved since it was read,
  or a conflict exists. Relay the host's own message to the operator instead of retrying.
- **Find an open pull request by its head branch** (a fallback, when no number is on hand): `gh pr list
  --head <branch> --state open` (or `GET /repos/Bomret/open-settlers/pulls?head=Bomret:<branch>&state=open`).
  Leaving `state=open` off would surface closed and merged pull requests too, including an old one left
  over from a reused branch name — never widen the filter to catch one of those.
- **`<tested-tip-sha>` above is never read from the host.** It is the local scope-branch tip
  `scope.js suite reusable` just accepted, the carry covered, ship's own local run passed, or whose named checks
  `scope.js ci` judged green, resolved with `git rev-parse <branch>` in the scope checkout.
  `/superdev:ship` reads the pull request's own `head.sha` only to decide whether to push and to refuse
  when it still differs from that tip after pushing, never as the merge's own pin.

## Check status

`/superdev:ship`, `/superdev:review`, and `/superdev:setup` read the latest check status of one commit through the
read script below, and `scope.js ci` judges it against the named checks the `## CI` section of
`docs/agents/verification.md` lists (ADR 0255). The script prints one `name<TAB>state<TAB>link` line per latest check
run and per latest commit-status context, across every page. It writes every line to a temporary file first and prints
the file only once both reads succeeded, so a failed `gh api` call exits non-zero and prints no line. `<sha>` stays in
the script: whoever runs it fills it with the commit.

```sh
set -eu
c=$(mktemp)
trap 'rm -f "$c"' EXIT
gh api --paginate 'repos/Bomret/open-settlers/commits/<sha>/check-runs?per_page=100' --jq '.check_runs[] | [.name, (if .status == "completed" then .conclusion else .status end), (.html_url // .details_url // "")] | @tsv' > "$c"
gh api --paginate 'repos/Bomret/open-settlers/commits/<sha>/status?per_page=100' --jq '.statuses[] | [.context, .state, (.target_url // "")] | @tsv' >> "$c"
cat "$c"
```

- **Check runs.** The endpoint's default `filter=latest` keeps the latest run per name, and `--paginate` follows
  every page.
- **Commit statuses.** The combined status holds the latest status per context.
- **A single read.** Write the script to a file with `<sha>` replaced by the commit, run it with `sh` into a second
  file, then judge: `node "${CLAUDE_PLUGIN_ROOT}/scripts/scope.js" ci --sha <sha> < "$c"`. Judge only when the script
  exited 0. A pipe drops the read's exit status, so a failed read would reach the verdict as empty input. No `gh`, no
  login, a 401, 403, or 404, or a network error ends the read with its error: report it, and pass nothing to
  `scope.js ci`.
- **A waiting read.** Write the script, `<sha>` left in place, to a file, and hand it to `node
  "${CLAUDE_PLUGIN_ROOT}/scripts/scope.js" ci --sha <sha> --wait --read <file>`, which fills `<sha>` itself.
- The only wait is `scope.js ci --wait`, bounded. Nothing here starts, re-runs, or cancels a CI run.

## Merge gate

`.github/rulesets/main.json` is the ruleset of `main`. It requires the results `check (ubuntu-latest)`,
`check (macos-latest)`, and `check (windows-latest)`, each as posted by GitHub Actions (`integration_id` 15368). It does
not require a branch to be up to date with `main`. Repository admins can bypass it only through a pull request.
`CiRulesetTests` fails where the CI job no longer posts the results the ruleset requires.

The file takes effect only when it is applied. Apply it once, after all three results are green on `main`:

```sh
gh api -X POST repos/Bomret/open-settlers/rulesets --input .github/rulesets/main.json
```

After a change to the file, update the applied ruleset by its id:

```sh
gh api -X PUT repos/Bomret/open-settlers/rulesets/<id> --input .github/rulesets/main.json
```

`gh api repos/Bomret/open-settlers/rulesets` lists the applied rulesets with their ids. Once the ruleset is applied, a
red or pending result refuses the merge of a non-admin, and the merge call answers with the host's message.

## Edit the pull request body

A run that resolves a deferred finding after `/superdev:submit-pr` opened the pull request rebuilds that
pull request's `## Deferred to a plan` section (`references/scope-findings.md`'s `## Deferred findings`,
ADR 0121). Only an open pull request is edited.

- **Read it**: `gh pr view <number> --json state,body` (or `GET /repos/Bomret/open-settlers/pulls/<pull_number>`
  for `state`, `merged`, and `body`), into a file. Only `state` `OPEN` (`open` and `merged` false on the
  REST read) is edited; a merged or closed pull request gets no edit.
- **Edit it, by file**: read into one file, build the new body into a second, and edit only when every
  step succeeded: `gh pr view <number> --json state,body > "$r" && jq -e '.state == "OPEN"' "$r"
  >/dev/null && jq -je .body "$r" > "$b" && <producer> < "$b" > "$f" && [ -s "$f" ] && gh pr edit
  <number> --body-file "$f"`. Name each file with `mktemp`. `<producer>` builds the new body from `"$b"`.
  A failed read, a merged or closed pull request, a failed producer, or an empty result stops the chain
  before the edit, and the body stays as it was. Never pass the body on stdin from a pipe: a pipe drops
  the producer's exit status, and an empty body would replace the pull request's own.

## Close the pull request

`/superdev:abandon` closes the scope branch's open pull request unmerged and posts nothing else.

- **Close it**: read it with `gh pr view <number> --json state` (or `GET /repos/Bomret/open-settlers/pulls/<pull_number>`
  for `state` and `merged`). A `state` of `CLOSED` or `MERGED` (`closed` or `merged` true on the REST read)
  reads as that state and needs nothing more. An `OPEN` one closes with `gh pr close <number>` (over plain
  REST, `PATCH /repos/Bomret/open-settlers/pulls/<pull_number>` with `{"state":"closed"}`). A close that fails
  stays a failed host call for its caller.

## Compute the base

A scope's BASE for the final-reviewer's BASE..HEAD diff (HEAD = scope branch tip) is its range base,
computed by `scope.js base resolve` per `${CLAUDE_PLUGIN_ROOT}/references/scope-mode.md`'s `## The scope's range base`.
An ad-hoc range with no run root uses `git merge-base <base> <scope-branch>`.
