# Controls

This page describes how to use the map view (`--map`) and the game view (`--game-view`). The game view has every
control of the map view, plus a game on the shown map and the build buttons. `README.md` gives the launch lines and
the exit codes.

## Map view

### Move the view

- Drag with the left mouse button.
- Hold an arrow key. The view moves at the same screen speed at every zoom.
- Swipe with two fingers on a touchpad.

The view wraps at the map's edges, so it never ends.

### Zoom

- Turn the mouse wheel, or pinch on a touchpad. The zoom keeps the map point under the pointer in place.
- Press `+` or `-`. The zoom keeps the middle of the view in place.

The zoom goes from 1/4 to 4 times. A view never shows more than two maps across, so a large window can stop the
zoom before 1/4.

### Seed and size

- The seed field takes 16 digits from 1 to 8, as the original's screen shows the seed. Press Enter to apply it.
- The size picker offers the three map sizes. A new size applies at once and keeps the last applied seed.

A valid seed or a new size makes a new map. An invalid seed shows its reason and keeps the last map. Text typed in
the seed field and not applied with Enter stays unapplied. The arrow keys do not move the view while the seed field
has the focus.

## Game view

The seed bar shows the tick count and the castle's log, plank, stone, and food counts, then each good that the content set's start stock does not list and the castle holds, by its stable name. A new seed or size starts a new game on the new map.

A click is a press and release of the left mouse button, or a tap on a touchpad, that does not move the view. A
press that moves the view by more than a few pixels is a drag and places nothing. A click goes to the node whose
point is nearest to it. A click on a flag's picture goes to that flag's node. Where two flag pictures overlap, the click goes to the southern flag. If both flags are in one row, it goes to the eastern one.

The game view's buttons stand in one column right of the map, top to bottom in the order named here. The Flag, Road, Woodcutter, Forester, Stonecutter, Sawmill, Fisher, Farm, Pig farm, Butcher, Mill, Baker, and Remove buttons are on/off buttons. At most one is on: pressing one turns the others
off. The Build help button is an on/off button apart from them (below). The Speed button stands last (below).

### No button on

- A click places the castle on the node, if the node is a castle site. The castle's flag stands south-east of it.
- Once the castle stands, a click with no button on does nothing.

### Flag

A click puts a flag on the node. The node must lie in the castle's territory and allow a flag: for example, a flag
does not go next to another flag, or on a tree or a stone. A flag on a node of a road splits the road into two roads
that meet at the new flag. The carrier of the old road keeps one half, and the other half waits for a carrier
to come from the castle.

### Road

A road goes from a flag to a flag, one node per click.

1. Click a flag. The castle's flag counts. A white line shows the road while you draw it.
   The road markers show what the next step does. A mark stands on the end of the road. A sign stands on each of the
   six nodes next to it. A blocked sign marks a node the step may not go to. A way-back sign marks the node the road
   came from. Every other node shows a slope sign for the height difference of that step. The sign's color and tilt
   show the difference. With the basic content set, the color is green up to 1 either way, yellow for 2, and red for
   3 and 4. A road node where a flag could stand shows a slope sign, although the click on it is refused.
   A click on a sign is a click on its node.
2. Click a node next to the end of the road. The road grows by that node. A node outside the territory is refused.
   A node with a stone, a rock, or a building is refused. A node with a road and no flag is refused.
   A tree does not refuse the step. A road may cross a tree node.
3. Click the node before the end to take the last step back.
4. Click a flag next to the end. The road is built, and the drawing ends. The flag may be the one the road started
   from: the road is then a loop, and it gets a carrier.

A road cannot end on a node of another road that holds no flag. Put a flag on that road first, which splits it, and end
the new road at the flag.

Escape, a right click, a click on the Road button, another button, or a new map drops a road that is not finished.
Nothing of it is left on the map. Escape also turns the Road button off.

### Woodcutter, Forester, Stonecutter, Sawmill, Fisher, Farm, Pig farm, Butcher, Mill, and Baker

These buttons are the stand-in for the original's build panel. A click puts a building site of the button's type on the
node and its flag on the node south-east of it. Where a flag with no building stands there already, the site takes that flag.
Where a road runs through the node south-east of the site, the flag splits that road like the Flag button does. The node must
be a site for the type's size. The woodcutter, the forester, the stonecutter, the fisher, and the mill are small buildings.
They need a site for a small or a large building. The sawmill, the farm, the pig farm, the butcher, and the baker are large
buildings. They need a site for a large building. Such a site has, for example, no tree or stone on it,
and nothing on or around the node south-east of it but such a flag. Connect the site's flag to the castle with a road. A
building site has no worker. The site shows as the original draws a site that has not started.

### Remove

A click takes down what stands on the node: a flag, a building, or the road through the node. The castle and its
flag cannot be taken down. A building or a building site that is taken down burns: it stays drawn with flames over it until
the burn frees its node.

### Build help

Use Build help to see where a flag, a mine, or a building fits before you click. The button turns the build-site overlay on
and off. It starts off. It turns off again when a new seed or size starts a new game.

Pressing Build help leaves the other buttons as they are. Pressing another button leaves Build help as it is. Build help
changes nothing in the game.

While it is on, every node of your territory shows what may be put there. A click on that node with your player would be
accepted for the same thing.

- A flag marker: a flag.
- A mine marker: a mine.
- A small house: a small building.
- A large house: a large or a small building.

Before the castle stands, no territory exists yet. The markers then show the sites where the castle may be placed.

A node with no marker allows none of these. Land outside your territory has no marker. A node that holds a flag or a
building has none, and neither does a node with no site.

Where a flag stands south-east of a node, the node can show a flag marker. A click puts no flag there. The original does the
same. The markers follow every change of the game in the frame that shows it. The map view has no Build help button.

### Speed

The Speed button sets how fast the game runs. It shows the speed factor and starts at 1x, the original's normal speed
of 12.5 ticks a second. Each press doubles the speed: 1x, 2x, 4x, 8x, and back to 1x. These are the factors of the
History Edition's game-speed switch; the order of the presses is a stand-in. A frame runs at most 4 ticks times the
factor, so after a long frame the game falls behind the clock and does not catch up. The speed stays when a new seed or size starts a new game. The map view has no
Speed button.

### Refused clicks

A click that is refused changes nothing, and the view shows nothing for it. The game keeps running.
