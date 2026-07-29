# Prototype architecture

## Guiding rule

Gameplay state is deterministic. Physics, particles, sound, and animation present state changes but do not decide them.

This allows reliable undo, replay, deadlock detection, JSON validation, automated testing, and eventual solvability analysis.

## Layers

### Data

Serializable catalog classes mirror the versioned JSON format. They contain no scene references and no gameplay behavior.

Planned location:

```text
Assets/OrbitSort/Runtime/Data/
```

### Core model

The pure board model owns:

- Ring capacities and logical offsets
- Marble colors and hidden logical indexes
- Gate connectivity and direction
- Exit positions and accepted colors
- Legal-action generation
- Win and deadlock evaluation
- State snapshots for undo

Planned location:

```text
Assets/OrbitSort/Runtime/Core/
```

### Gameplay orchestration

Commands validate and apply one deterministic change:

- `RotateRing`
- `TransferMarble`
- `ResolveExits`
- `Undo`
- `Restart`

The controller locks input while a command is being presented, then evaluates win or deadlock after all automatic exits settle.

Planned location:

```text
Assets/OrbitSort/Runtime/Gameplay/
```

### Presentation

The approved board geometry is authored in Blender and exported as
separate FBX models for the three rings, portal, receiver, center hub, and
marble. Unity imports and positions these meshes; it does not recreate
their geometry.

The view uses a perpendicular orthographic camera and renders smooth,
unsegmented ring channels. Hidden logical positions are converted into
angles, then marbles rotate with their ring pivot.

The view never mutates gameplay state directly.

### UI

The prototype HUD contains:

- Level identifier
- Pause
- Undo
- Restart
- Exit progress

It does not contain a timer or move counter.

## Unity assemblies

- `OrbitSort.Runtime` contains runtime data, model, gameplay, and UI code.
- `OrbitSort.Editor` contains authoring and validation tools.
- `OrbitSort.Tests.EditMode` tests the deterministic model and data.
- `OrbitSort.Tests.PlayMode` tests scene integration and presentation.

The folder structure may be split into narrower runtime assemblies after the prototype proves the boundaries are useful.

## Level pipeline

1. Load the catalog text from `Resources/Levels/levels.json`.
2. Deserialize into plain serializable data.
3. Validate schema version and semantic rules.
4. Build an immutable starting board state.
5. Reject invalid or initially deadlocked production levels.
6. Start presentation only after validation succeeds.

## Deadlock evaluation

Deadlock is not based on the absence of a currently highlighted button. The model must consider every alignment reachable by a ring that still has a gap.

Evaluation order:

1. If no marbles remain, return `Won`.
2. Generate all productive exits and transfers reachable through legal rotations.
3. If at least one productive action exists, return `Playing`.
4. Otherwise return `Deadlocked`.
