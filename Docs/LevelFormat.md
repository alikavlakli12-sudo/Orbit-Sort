# JSON level format

## Location

The prototype catalog is stored at:

```text
Assets/OrbitSort/Resources/Levels/levels.json
```

The companion schema is stored at:

```text
Schemas/orbit-sort-levels.schema.json
```

## Design

The catalog follows the Marble Sort prototype pattern:

- One versioned top-level catalog
- Shared rules
- Stable IDs
- A list of level definitions
- Runtime loading through serializable data classes
- Validation before gameplay

The tracks have no visible sockets. `index` values represent hidden logical positions used by the deterministic model.

## Versioning

`schemaVersion` describes the meaning and structure of the JSON.

Increase it when a change would make an existing loader interpret data incorrectly. Adding a new optional field with a safe default does not necessarily require a new version.

## Rings

Every level contains two or three rings, listed from innermost to outermost.
Two-ring levels use `inner` and `outer`; three-ring levels use `inner`,
`middle`, and `outer`.

```json
{
  "id": "inner",
  "capacity": 8,
  "rotationOffset": 0,
  "marbles": [
    { "index": 1, "color": "blue" }
  ]
}
```

- `capacity` is the number of hidden positions.
- `rotationOffset` is the initial logical rotation.
- Every marble index must be unique and inside the ring capacity.
- Production levels start with at least one gap on every ring.

## Gates

Every adjacent ring pair has one one-way outward portal. A two-ring level has
one `inner`-to-`outer` portal. A three-ring level has an `inner`-to-`middle`
portal and a `middle`-to-`outer` portal.

```json
{
  "id": "gate_inner_middle",
  "fromRing": "inner",
  "toRing": "middle",
  "fromIndex": 0,
  "toIndex": 0,
  "direction": "outward"
}
```

The two indexes identify the aligned hidden positions on rings that may have different capacities.

## Exits

Prototype exits connect to the outermost ring.

```json
{
  "id": "exit_blue",
  "ring": "outer",
  "ringIndex": 0,
  "color": "blue"
}
```

Every marble color used by a level needs a matching exit.

## Validation

Run:

```bash
python3 Tools/validate_levels.py
```

The validator checks:

- Catalog and schema versions
- Prototype rule flags
- Stable and unique IDs
- Two or three rings in inner-to-outer order
- Capacity and index ranges
- Duplicate marble positions
- Supported colors
- Exactly one outward portal per adjacent ring pair
- Outermost-ring exits
- Exit coverage for every marble color
- An initial empty gap on every ring

Full search-based solvability validation will be added with the deterministic board model.
