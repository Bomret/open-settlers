# Open Settlers glossary

## Game data

**Install**
A copy of Die Siedler that the player owns, on disk: the folder that holds the game's program and its
data files. The engine reads all game data from it and ships none of its own. The reference install is
The Settlers History Edition, whose program is `Siedler1.exe`.
_Avoid_: game folder, game directory, assets folder

**Asset**
One loadable piece of game data in an install, such as a palette, sprite, sound, or map.
_Avoid_: resource, game file

**Archive**
A graphics file of an install, such as `loca/SPAD.PA`, that holds numbered entries. Each entry present
in it is one asset.
_Avoid_: pack, container, PA file

**Asset kind**
What an asset is, such as a palette, a sprite, a sound, or rule data. The routine of the program that
reads an asset decides its kind; nothing in the asset itself states it.
_Avoid_: asset type, format

**Rule data**
An asset whose meaning only a rule explains, such as a table the simulation reads. The milestone that
recovers the rule decodes its meaning.
_Avoid_: game data, tables

**Asset check**
The engine's headless mode that reads every asset of an install and reports each one it cannot read.
_Avoid_: validator, scan

**Asset browser**
The engine's interactive mode for picking any asset of an install and inspecting its preview.
_Avoid_: sprite viewer, asset viewer

**Preview**
The asset browser's rendering of one asset, showing that its meaning was understood: a palette as its
colors, a sprite as the game draws it, a sound as playback.
_Avoid_: thumbnail, view

**Stand-in**
A value or check that the engine supplies itself because the install does not define it or the program does not
confirm it.
_Avoid_: default, placeholder

**Seed info**
The engine's headless mode that generates the map of one seed and size and prints its layer digests and
its counts of terrain, objects, and deposits. It needs the install, whose program tables say which objects are large.
_Avoid_: map summary, map dump

**Play script**
A text file of steps, one per line, that drive the map view or the game view through its own input
handlers (keys, the mouse, a click on a tile by `click-tile`, a click on a button by `button`), take
screenshots, and check its state (in the game view also the tick count and the castle's log count).
_Avoid_: test script, macro

**Play run**
One run of the map view or the game view under Godot's Movie Maker that executes a play script on a fixed frame rate and
quits after its last step.
_Avoid_: replay, recording

## Game rules

**Rule**
A behavior of the original game that the engine reproduces, recovered from the game code of `Siedler1.exe`
(`docs/rules/AGENTS.md`).
_Avoid_: mechanic, logic

**Game code**
The part of `Siedler1.exe` that the History Edition carries over from the original DOS program: the
game's rules, its state, and its drawing.
_Avoid_: core, original code

**Platform layer**
The part of `Siedler1.exe` that the History Edition adds for Windows: graphics and sound output, timing,
and input. It is new code, so a rule found only there is marked as such.
_Avoid_: wrapper, port layer

**Simulation**
The game state and the rules that advance it, independent of rendering, input devices, and frame rate.
_Avoid_: game logic, model, world

**Tick**
One fixed step of the simulation: one pass of the original's main loop at its shortest length, that is,
4 timer calls, 8 units of its game clock, 80 milliseconds at normal speed (`docs/rules/timing.md`,
ADR 0005).
_Avoid_: frame, update, turn

**Command**
A player's action that the simulation takes at a given tick, such as placing a flag or building a road.
The same seed and the same commands at the same ticks give the same game (INV-0002).
_Avoid_: input, event, order

**Flag**
A point on the map where roads meet and where goods wait to be carried on. A building stands at a flag.
_Avoid_: node, waypoint

**Road**
A path between two flags, built one of six directions at a time. One carrier serves each road.
_Avoid_: edge, path, street

**Carrier**
A settler who walks one road and carries goods between its two flags.
_Avoid_: porter, transporter, worker

**Transport priority**
A player's rank for each kind of good. Where several goods at one flag wait for the same road, the good
with the highest rank goes next. A new game starts every player with the same default order.
_Avoid_: goods priority, transport order

## Roadmap

**Free-play release**
The first goal of the roadmap: a free game against the computer opponent on a generated map. A person
plays the game to a win or a loss. The release includes saving, loading, and sound effects.
_Avoid_: MVP, version 1.0
