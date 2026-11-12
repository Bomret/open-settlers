#!/bin/sh
# Runs a play script under Movie Maker, from the repository root, and reuses the stored result when its inputs
# are unchanged:
#   sh tools/play-run.sh <stem> [--game-view] [--layout <file>] [--seed <s>] [--size <n>] [--graphics <source>] --fps <n> [--fresh]
# The script is play/<stem>.play and the install is SIEDLER_DIR. Without --game-view the map view runs;
# --game-view runs the game view with --seed and --size; --layout runs the game view on the layout file and
# implies --game-view (a layout holds its own seed and size, so --layout with --seed or --size exits 2, and so
# does a missing layout file or an empty layout value). The input hash covers the mode, the layout's path and
# content, the content and path of every tracked or untracked, not ignored file under src/, scenes/ and content/, the
# project files (project.godot, OpenSettlers.csproj, global.json, Directory.Build.props), the play script, the seed, the size, the graphics source (history-edition when none is given), the
# fps, the Godot version and the SIEDLER_DIR path. --graphics takes history-edition or open-settlers, once. A result is stored in
# play-out/.cache/<hash>/ only for exit status 0 or 1. Each call past the argument checks appends one line to
# play-out/runs.log; the line names the mode (map or game-view), the layout, and the graphics source. Bad arguments exit 2 and leave no trace.
# Calls for one stem run one at a time through the lock play-out/.lock-<stem>, which holds the owner's pid: a
# call waits on a live owner (a process of another user counts as live), says so once on stderr, and takes over
# the lock of a dead one under the mutex play-out/.lock-<stem>.takeover, which holds its holder's pid. A mutex
# whose holder is dead, or that holds no pid and is older than 5 seconds, is stale and cleared in place by one
# reaper per holder (the directory $mutex.reap.<holder>, <holder> being the pid or "nopid", holds the reaper's pid
# and is stale by the same rule, then removed by the next call that judges a mutex of that holder; one whose mutex
# is gone stays until removed by hand and blocks nothing); a mutex is never moved off its path, and a call
# removes a mutex holding its own pid outside its critical section. A pid counts as dead only when `ps -p` exits 1
# with no output and lists the caller's own pid; any other result reads as live. A lock is never moved off its
# path, and one is removed only after it is judged dead twice, the second time under the mutex and with the same
# pid in it. Two gaps are left, and each needs a call that stalls or is killed. A holder that stalls more than 5
# seconds between creating the mutex and writing its pid reads as holding none, so a reaper can remove it and a
# third call can take the path: two holders, and one can remove a lock another took. A reaper killed inside its
# step leaves its reap directory, and two calls that both judge it stale can remove it one after the other's new
# reaper has made it: two reapers of one holder, and the one that finishes second can remove a mutex a live
# holder took. An empty --size or --seed exits 2. An unwritable play-out/ exits 2. A run builds OpenSettlers.csproj first; a
# failed build starts no Godot, stores nothing, and exits 3. Build and Godot run through `mise exec`, which
# supplies the pinned tools and $GODOT. PLAY_RUN_MISE replaces the mise binary (default mise), PLAY_RUN_GODOT
# the Godot binary (default $GODOT from mise.toml), PLAY_RUN_DOTNET the dotnet binary (default dotnet).
set -u

usage() {
  echo "play-run.sh: $1" >&2
  echo "usage: sh tools/play-run.sh <stem> [--game-view] [--layout <file>] [--seed <s>] [--size <n>] [--graphics <source>] --fps <n> [--fresh]" >&2
  exit 2
}

[ $# -ge 1 ] || usage "missing stem"
stem=$1
shift
case "$stem" in
  "" | */* | *..* | -* | .*) usage "bad stem: $stem" ;;
esac

seed=
size=
fps=
fresh=
mode=map
layout=
graphics=
while [ $# -gt 0 ]; do
  case "$1" in
    --seed | --size | --fps | --layout | --graphics)
      [ $# -ge 2 ] || usage "$1 needs a value"
      case "$1" in
        --seed) [ -n "$2" ] || usage "--seed needs a value"; seed=$2 ;;
        --size) [ -n "$2" ] || usage "--size needs a value"; size=$2 ;;
        --fps) fps=$2 ;;
        --layout) [ -n "$2" ] || usage "--layout needs a file"; layout=$2 ;;
        --graphics)
          [ -z "$graphics" ] || usage "repeated --graphics"
          case "$2" in
            history-edition | open-settlers) graphics=$2 ;;
            *) usage "--graphics takes history-edition or open-settlers" ;;
          esac
          ;;
      esac
      shift 2
      ;;
    --game-view)
      mode=game-view
      shift
      ;;
    --fresh)
      fresh=1
      shift
      ;;
    *) usage "unknown argument: $1" ;;
  esac
done

case "$fps" in
  "" | 0* | *[!0-9]*) usage "--fps needs a positive whole number" ;;
esac
case "$size" in
  0* | *[!0-9]*) usage "--size needs a positive whole number" ;;
esac
if [ -n "$layout" ]; then
  mode=game-view
  [ -z "$seed" ] && [ -z "$size" ] || usage "--layout holds its own seed and size: pass no --seed or --size"
  [ -f "$layout" ] || usage "no layout file $layout"
fi
script=play/$stem.play
[ -f "$script" ] || usage "no play script $script"
[ -n "${SIEDLER_DIR:-}" ] || usage "set SIEDLER_DIR to a History Edition install"
mise=${PLAY_RUN_MISE:-mise}
dotnet=${PLAY_RUN_DOTNET:-dotnet}

# Runs Godot under mise, so that $GODOT and the pinned tools are on hand.
godot() {
  "$mise" exec -- sh -c 'exec "${PLAY_RUN_GODOT:-$GODOT}" "$@"' sh "$@"
}

version=$(godot --version 2>/dev/null) || usage "cannot read the Godot version"

# One call per stem at a time, locked before the hash so a waiting call hashes the inputs it runs on. The lock
# holds the owner's pid; a lock whose pid is dead is taken over, and so is a lock with no pid after a grace of
# 5 seconds (its owner was killed between creating it and writing the pid). `ps -p` finds a live process of
# another user, which kill -0 reports as gone (EPERM), and needs no locale. A pid counts as dead only when
# `ps -p <pid> -o pid=` exits 1 with no output and the same `ps` lists this call's own pid; a missing ps, a ps
# without -p, and any other result read as live. The takeover mutex holds its holder's pid and is stale when that
# pid is dead, or, with no pid in it, after the same 5 seconds grace. A lock is never moved off its path: it is
# removed only by a call that holds the mutex (its own pid is in it), judged the lock stale there, and finds the
# pid it judged still in the lock. A stale mutex is never moved: one reaper per holder, serialized by the directory
# $mutex.reap.<holder> (which holds the reaper's pid), judges it again in place with the same holder in it and
# removes it. The EXIT trap removes only a lock that holds its pid.
lock=play-out/.lock-$stem
mutex=$lock.takeover
older() {
  m=$(stat -c %Y "$1" 2>/dev/null || stat -f %m "$1" 2>/dev/null) || return 1
  [ $(($(date +%s) - m)) -gt 5 ]
}
# Is the process of pid $1 dead?
gone() {
  listed=$(ps -p "$1" -o pid= 2>/dev/null)
  [ $? -eq 1 ] && [ -z "$listed" ] && [ -n "$(ps -p $$ -o pid= 2>/dev/null)" ]
}
# Judges the lock, and leaves the pid it judged in $judged.
stale() {
  judged=$(cat "$lock/pid" 2>/dev/null)
  if [ -n "$judged" ]; then gone "$judged"; else older "$lock"; fi
}
# Judges the mutex at path $1.
mutex_stale() {
  holder=$(cat "$1/pid" 2>/dev/null)
  if [ -n "$holder" ]; then gone "$holder"; else older "$1"; fi
}
# Does the mutex at its path hold this call's pid?
holds_mutex() { [ "$(cat "$mutex/pid" 2>/dev/null)" = "$$" ]; }
waited=
mkdir -p play-out 2>/dev/null
until mkdir "$lock" 2>/dev/null; do
  if [ ! -d play-out ] || [ ! -w play-out ]; then
    echo "play-run.sh: cannot write play-out/" >&2
    exit 2
  fi
  if stale; then
    # Only the holder of the takeover mutex removes a lock, after judging it stale again while holding it, and
    # only a lock that still holds the pid it judged. No other call can create the lock path while the stale
    # lock still stands there. The holder acts only while its pid is in the mutex, and removes the mutex then.
    if mkdir "$mutex" 2>/dev/null; then
      echo $$ >"$mutex/pid"
      if holds_mutex && stale && [ "$(cat "$lock/pid" 2>/dev/null)" = "$judged" ] && holds_mutex; then rm -rf "$lock"; fi
      if holds_mutex; then rm -rf "$mutex"; fi
      continue
    fi
    # A call never waits on a mutex that holds its own pid outside the section above (pid reuse: a dead earlier
    # call with this call's pid left it there).
    if holds_mutex; then
      rm -rf "$mutex"
      continue
    fi
    # A stale mutex is cleared in place, never moved: while it stands no call can create the mutex path. One
    # reaper per holder (the pid in the mutex, or "nopid") holds the directory $mutex.reap.<holder>, which holds
    # the reaper's pid, judges the mutex stale again with the same holder in it, and removes it. A reap directory
    # is stale by the mutex's rule and removed by the next call that judges a mutex of that holder, which retries.
    if [ -d "$mutex" ] && mutex_stale "$mutex"; then
      key=${holder:-nopid}
      reap=$mutex.reap.$key
      if mkdir "$reap" 2>/dev/null; then
        echo $$ >"$reap/pid"
        if mutex_stale "$mutex" && [ "${holder:-nopid}" = "$key" ]; then rm -rf "$mutex"; fi
        if [ "$(cat "$reap/pid" 2>/dev/null)" = "$$" ]; then rm -rf "$reap"; fi
        continue
      fi
      if mutex_stale "$reap"; then
        rm -rf "$reap"
        continue
      fi
    fi
  fi
  if [ -z "$waited" ]; then
    echo "play-run.sh: waiting on $lock or $mutex (remove them if no run is live)" >&2
    waited=1
  fi
  sleep 1
done
trap 'if [ "$(cat "$lock/pid" 2>/dev/null)" = "$$" ]; then rm -rf "$lock"; fi' EXIT
trap 'exit 130' INT TERM HUP
echo $$ >"$lock/pid"

export LC_ALL=C
hash=$(
  {
    list=$(mktemp)
    git ls-files --cached --others --exclude-standard -- src scenes content | sort -u | while IFS= read -r f; do
      [ -f "$f" ] && printf '%s\n' "$f"
    done >"$list"
    [ -s "$list" ] && git hash-object --stdin-paths <"$list" | paste -d ' ' "$list" -
    rm -f "$list"
    for f in project.godot OpenSettlers.csproj global.json Directory.Build.props; do
      if [ -f "$f" ]; then echo "$f $(git hash-object -- "$f")"; else echo "$f missing"; fi
    done
    echo "script $(git hash-object -- "$script")"
    echo "mode $mode"
    echo "layout $layout"
    [ -n "$layout" ] && echo "layout-content $(git hash-object -- "$layout")"
    echo "seed $seed"
    echo "size $size"
    echo "graphics ${graphics:-history-edition}"
    echo "fps $fps"
    echo "godot $version"
    echo "siedler $SIEDLER_DIR"
  } | git hash-object --stdin
)

out=play-out/$stem
cache=play-out/.cache
entry=$cache/$hash

rm -rf "$out"
mkdir -p "$out"
if [ -z "$fresh" ] && [ -f "$entry/status" ]; then
  how=reused
  cp -R "$entry/out/." "$out/"
  status=$(cat "$entry/status")
  echo "play-run.sh: reused $hash exit $status"
  cat "$entry/summary"
else
  how=run
  log=$(mktemp)
  # Godot does not build C# itself: on a missing or stale build it runs old code, or none and waits on an open
  # window, and closing that window exits 0.
  if ! "$mise" exec -- "$dotnet" build OpenSettlers.csproj >"$log" 2>&1; then
    how=build
    status=3
  else
    set -- --path . --write-movie "$out/frame.png" --fixed-fps "$fps" -- --game "$SIEDLER_DIR"
    if [ "$mode" = game-view ]; then set -- "$@" --game-view; else set -- "$@" --map; fi
    [ -n "$layout" ] && set -- "$@" --layout "$layout"
    [ -n "$seed" ] && set -- "$@" --seed "$seed"
    [ -n "$size" ] && set -- "$@" --size "$size"
    [ -n "$graphics" ] && set -- "$@" --graphics "$graphics"
    godot "$@" --play "$script" >"$log" 2>&1
    status=$?
  fi
  cat "$log"
  if [ "$status" -le 1 ]; then
    mkdir -p "$cache"
    tmp=$(mktemp -d "$cache/tmp.XXXXXX")
    mkdir "$tmp/out"
    for f in "$out"/*; do
      [ -e "$f" ] || continue
      case "${f##*/}" in
        frame[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9].png) ;;
        *) cp -R "$f" "$tmp/out/" ;;
      esac
    done
    grep -E 'WARN|ERROR' "$log" >"$tmp/summary"
    echo "$status" >"$tmp/status"
    # Move a replaced entry aside first so the rename never lands inside an existing entry.
    [ -d "$entry" ] && mv "$entry" "$tmp.old"
    mv "$tmp" "$entry" && rm -rf "$tmp.old"
  fi
  rm -f "$log"
fi

head=$(git rev-parse HEAD 2>/dev/null || echo none)
if [ -n "$(git status --porcelain)" ]; then dirty=dirty; else dirty=clean; fi
printf '%s\t%s\t%s\t%s\t%s\tmode=%s\tlayout=%s\tseed=%s\tsize=%s\tfps=%s\tgraphics=%s\texit=%s\t%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$hash" \
  "$head" "$dirty" "$stem" "$mode" "$layout" "$seed" "$size" "$fps" "${graphics:-history-edition}" "$status" "$how" >>play-out/runs.log
exit "$status"
