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

**Stand-in**
A value that the engine supplies itself because the install does not define it or the program does not
confirm it.
_Avoid_: default, placeholder

## Game rules

**Rule**
A behavior of the original game that the engine reproduces. We recover each rule from the game code of
`Siedler1.exe` and restate it in our own words, with its address as evidence. We mark a rule that the program does not
confirm as **inferred** or **stand-in**.
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
One fixed step of the simulation: one timer call of the original, that is, 2 units of its game clock,
20 milliseconds at normal speed (`docs/rules/timing.md`).
_Avoid_: frame, update, turn

## Roadmap

**Free-play release**
The first goal of the roadmap: a free game against the computer opponent on a generated map. A person
plays the game to a win or a loss. The release includes saving, loading, and sound effects.
_Avoid_: MVP, version 1.0
