# Open Settlers glossary

## Game data

**Install**
A copy of Die Siedler that the player owns, on disk: the folder that holds `SIED.EXE` and the game's
data files. The engine reads all game data from it and ships none of its own. The reference install is
the German CD release, installed with the game's own `INSTALL.EXE`.
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
A behavior of the original game that the engine reproduces, recovered from `SIED.EXE` and restated in our
own words with its address as evidence. A rule the program does not confirm is marked **inferred** or
**stand-in**.
_Avoid_: mechanic, logic

**Simulation**
The game state and the rules that advance it, independent of rendering, input devices, and frame rate.
_Avoid_: game logic, model, world

**Tick**
One fixed step of the simulation, at the rate the original program uses.
_Avoid_: frame, update, turn

## Roadmap

**Free-play release**
The first goal of the roadmap: a free game against the computer opponent on a generated map, playable to
a win or a loss, with saving, loading, and sound effects.
_Avoid_: MVP, version 1.0
