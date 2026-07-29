# Prototype game rules

## Player goal

Move every marble through the concentric rings and into the matching color exit.

## Controls

- Swipe a ring to rotate its marble chain around the track.
- Tap a transfer gate to move one aligned marble outward.
- A marble aligned with its matching exit leaves automatically.
- Use Undo to restore the state before the previous completed action.
- Use Retry to reload the current level.

The greybox also supports `U` for undo, `R` for retry, and the left and
right arrow keys for level switching while testing in the Unity editor.

## Space and rotation

The tracks are visually continuous. Hidden logical positions make the board deterministic.

A ring needs at least one marble-sized gap to rotate. A completely full ring is jammed. This makes empty space the resource the player must manage.

## Transfer

- Prototype gates are one-way and point outward.
- A transfer requires an aligned source marble.
- The destination position must be empty.
- Gates move one marble at a time.
- Invalid transfers do not change the board state.

## Exit

- Prototype exits connect to the outer ring.
- Each exit accepts one color.
- A correctly aligned marble exits automatically after the current action settles.
- Every marble removed from the outer ring creates new usable space.

## Win and lose

The game has no timer and no move counter.

The player wins when every marble has exited.

The player loses when marbles remain and the deterministic board model finds no sequence of legal ring alignment, gate transfer, or exit that can create progress. The lose state is presented as:

```text
RINGS JAMMED

UNDO    RETRY
```

Undo remains available on the lose screen when a previous state exists.

## Feedback requirements

- Open track must make available space visible.
- The last remaining gap on a ring receives an amber warning.
- A full ring receives a red rim and a mechanical jam response.
- A blocked gate flashes or shakes when tapped.
- Deadlock evaluation runs only after animations and automatic exits finish.
