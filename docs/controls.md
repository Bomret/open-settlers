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

The bar shows the tick count and the castle's log count. A new seed or size starts a new game on the new map.

A click is a press and release of the left mouse button, or a tap on a touchpad, that does not move the view. A
press that moves the view by more than a few pixels is a drag and places nothing. A click goes to the node whose
point is nearest to it. A click on a flag's picture goes to that flag's node. Where two flag pictures overlap, the click goes to the southern flag. If both flags are in one row, it goes to the eastern one.

The Flag, Road, Woodcutter, and Remove buttons are on/off buttons. At most one is on: pressing one turns the others
off.

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

### Woodcutter

A click puts a woodcutter's hut on the node and its flag on the node south-east of it. Where a flag with no building
stands there already, the hut takes that flag. Where a road runs through the node south-east of the hut, the flag splits that road like the
Flag button does. The node must be a site for a small or a large building: for example,
not a tree or a stone, and with nothing on or around the node south-east of it but such a flag. Connect the hut's flag
to the castle with a road. The hut stands finished at once, and its worker walks to it from the castle.

### Remove

A click takes down what stands on the node: a flag, a building, or the road through the node. The castle and its
flag cannot be taken down.

### Refused clicks

A click that is refused changes nothing, and the view shows nothing for it. The game keeps running.
