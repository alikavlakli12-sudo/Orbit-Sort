#!/usr/bin/env python3
"""Validate the Orbit Sort prototype level catalog without Unity dependencies."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CATALOG = (
    REPOSITORY_ROOT
    / "Assets"
    / "OrbitSort"
    / "Resources"
    / "Levels"
    / "levels.json"
)
DEFAULT_SCHEMA = REPOSITORY_ROOT / "Schemas" / "orbit-sort-levels.schema.json"

SUPPORTED_COLORS = {"blue", "red", "yellow"}
EXPECTED_RING_IDS = {
    2: ("inner", "outer"),
    3: ("inner", "middle", "outer"),
}
LEVEL_ID_PATTERN = re.compile(r"^level_[0-9]{3}$")
OBJECT_ID_PATTERN = re.compile(r"^[a-z][a-z0-9_]*$")

EXPECTED_RULES = {
    "timerEnabled": False,
    "moveCounterEnabled": False,
    "ringRequiresGapToRotate": True,
    "gatesAreOneWay": True,
    "deadlockWhenNoProgress": True,
}


def load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as stream:
            return json.load(stream)
    except FileNotFoundError as error:
        raise ValueError(f"File does not exist: {path}") from error
    except json.JSONDecodeError as error:
        raise ValueError(
            f"{path}:{error.lineno}:{error.colno}: invalid JSON: {error.msg}"
        ) from error


def validate_catalog(catalog: Any) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []

    if not isinstance(catalog, dict):
        return ["catalog: expected a JSON object"], warnings

    if catalog.get("schemaVersion") != 1:
        errors.append("catalog.schemaVersion: expected integer value 1")

    catalog_id = catalog.get("catalogId")
    if not is_valid_object_id(catalog_id):
        errors.append(
            "catalog.catalogId: expected lowercase snake-case identifier"
        )

    rules = catalog.get("rules")
    if not isinstance(rules, dict):
        errors.append("catalog.rules: expected an object")
    else:
        for rule_name, expected_value in EXPECTED_RULES.items():
            if rules.get(rule_name) is not expected_value:
                errors.append(
                    f"catalog.rules.{rule_name}: expected {expected_value!r}"
                )

    levels = catalog.get("levels")
    if not isinstance(levels, list) or not levels:
        errors.append("catalog.levels: expected at least one level")
        return errors, warnings

    if not 5 <= len(levels) <= 10:
        warnings.append(
            f"catalog.levels: prototype target is 5-10 levels; found {len(levels)}"
        )

    seen_level_ids: set[str] = set()
    for level_index, level in enumerate(levels):
        context = f"catalog.levels[{level_index}]"
        if not isinstance(level, dict):
            errors.append(f"{context}: expected an object")
            continue

        level_id = level.get("id")
        if not isinstance(level_id, str) or not LEVEL_ID_PATTERN.fullmatch(level_id):
            errors.append(f"{context}.id: expected format level_001")
        elif level_id in seen_level_ids:
            errors.append(f"{context}.id: duplicate level ID {level_id!r}")
        else:
            seen_level_ids.add(level_id)

        validate_level(level, context, errors)

    return errors, warnings


def validate_level(level: dict[str, Any], context: str, errors: list[str]) -> None:
    display_name = level.get("displayName")
    if not isinstance(display_name, str) or not display_name.strip():
        errors.append(f"{context}.displayName: expected non-empty string")

    rings = level.get("rings")
    if not isinstance(rings, list) or len(rings) not in EXPECTED_RING_IDS:
        errors.append(f"{context}.rings: expected 2 or 3 rings")
        return

    expected_ring_ids = EXPECTED_RING_IDS[len(rings)]

    ring_ids: list[str] = []
    ring_capacities: dict[str, int] = {}
    marble_colors: set[str] = set()

    for ring_index, ring in enumerate(rings):
        ring_context = f"{context}.rings[{ring_index}]"
        if not isinstance(ring, dict):
            errors.append(f"{ring_context}: expected an object")
            continue

        ring_id = ring.get("id")
        if not is_valid_object_id(ring_id):
            errors.append(
                f"{ring_context}.id: expected lowercase snake-case identifier"
            )
            continue
        if ring_id in ring_capacities:
            errors.append(f"{ring_context}.id: duplicate ring ID {ring_id!r}")
            continue
        if ring_id != expected_ring_ids[ring_index]:
            errors.append(
                f"{ring_context}.id: expected "
                f"{expected_ring_ids[ring_index]!r}"
            )

        capacity = ring.get("capacity")
        if not isinstance(capacity, int) or isinstance(capacity, bool):
            errors.append(f"{ring_context}.capacity: expected integer")
            continue
        if not 4 <= capacity <= 32:
            errors.append(f"{ring_context}.capacity: expected range 4-32")

        ring_ids.append(ring_id)
        ring_capacities[ring_id] = capacity

        rotation_offset = ring.get("rotationOffset")
        if (
            not isinstance(rotation_offset, int)
            or isinstance(rotation_offset, bool)
            or not 0 <= rotation_offset < capacity
        ):
            errors.append(
                f"{ring_context}.rotationOffset: expected index inside capacity"
            )

        marbles = ring.get("marbles")
        if not isinstance(marbles, list):
            errors.append(f"{ring_context}.marbles: expected an array")
            continue

        if len(marbles) >= capacity:
            errors.append(
                f"{ring_context}.marbles: production levels must start with a gap"
            )

        occupied_indexes: set[int] = set()
        for marble_index, marble in enumerate(marbles):
            marble_context = f"{ring_context}.marbles[{marble_index}]"
            if not isinstance(marble, dict):
                errors.append(f"{marble_context}: expected an object")
                continue

            logical_index = marble.get("index")
            if (
                not isinstance(logical_index, int)
                or isinstance(logical_index, bool)
                or not 0 <= logical_index < capacity
            ):
                errors.append(
                    f"{marble_context}.index: expected index inside capacity"
                )
            elif logical_index in occupied_indexes:
                errors.append(
                    f"{marble_context}.index: duplicate index {logical_index}"
                )
            else:
                occupied_indexes.add(logical_index)

            color = marble.get("color")
            if color not in SUPPORTED_COLORS:
                errors.append(
                    f"{marble_context}.color: unsupported color {color!r}"
                )
            else:
                marble_colors.add(color)

    validate_gates(level.get("gates"), context, ring_ids, ring_capacities, errors)
    exit_colors = validate_exits(
        level.get("exits"), context, ring_ids, ring_capacities, errors
    )

    missing_exit_colors = marble_colors - exit_colors
    if missing_exit_colors:
        errors.append(
            f"{context}.exits: missing exits for colors "
            f"{', '.join(sorted(missing_exit_colors))}"
        )

    tutorial_steps = level.get("tutorialSteps", [])
    if not isinstance(tutorial_steps, list) or any(
        step not in {"rotate", "transfer", "exit", "deadlock"}
        for step in tutorial_steps
    ):
        errors.append(f"{context}.tutorialSteps: contains unsupported step")


def validate_gates(
    gates: Any,
    context: str,
    ring_ids: list[str],
    ring_capacities: dict[str, int],
    errors: list[str],
) -> None:
    expected_gate_count = max(0, len(ring_ids) - 1)
    if not isinstance(gates, list) or len(gates) != expected_gate_count:
        errors.append(
            f"{context}.gates: expected exactly {expected_gate_count} gate(s)"
        )
        return

    seen_ids: set[str] = set()
    connected_pairs: set[tuple[str, str]] = set()
    for gate_index, gate in enumerate(gates):
        gate_context = f"{context}.gates[{gate_index}]"
        if not isinstance(gate, dict):
            errors.append(f"{gate_context}: expected an object")
            continue

        gate_id = gate.get("id")
        if not is_valid_object_id(gate_id):
            errors.append(
                f"{gate_context}.id: expected lowercase snake-case identifier"
            )
        elif gate_id in seen_ids:
            errors.append(f"{gate_context}.id: duplicate gate ID {gate_id!r}")
        else:
            seen_ids.add(gate_id)

        from_ring = gate.get("fromRing")
        to_ring = gate.get("toRing")
        if from_ring not in ring_capacities:
            errors.append(f"{gate_context}.fromRing: unknown ring {from_ring!r}")
        if to_ring not in ring_capacities:
            errors.append(f"{gate_context}.toRing: unknown ring {to_ring!r}")

        if from_ring in ring_capacities and to_ring in ring_capacities:
            from_order = ring_ids.index(from_ring)
            to_order = ring_ids.index(to_ring)
            if to_order != from_order + 1:
                errors.append(
                    f"{gate_context}: gate must connect adjacent rings outward"
                )
            elif (from_ring, to_ring) in connected_pairs:
                errors.append(
                    f"{gate_context}: duplicate adjacent ring portal"
                )
            else:
                connected_pairs.add((from_ring, to_ring))

            validate_index(
                gate.get("fromIndex"),
                ring_capacities[from_ring],
                f"{gate_context}.fromIndex",
                errors,
            )
            validate_index(
                gate.get("toIndex"),
                ring_capacities[to_ring],
                f"{gate_context}.toIndex",
                errors,
            )

            from_index = gate.get("fromIndex")
            to_index = gate.get("toIndex")
            from_capacity = ring_capacities[from_ring]
            to_capacity = ring_capacities[to_ring]
            indexes_are_valid = (
                isinstance(from_index, int)
                and not isinstance(from_index, bool)
                and 0 <= from_index < from_capacity
                and isinstance(to_index, int)
                and not isinstance(to_index, bool)
                and 0 <= to_index < to_capacity
            )
            if (
                indexes_are_valid
                and from_index * to_capacity != to_index * from_capacity
            ):
                errors.append(
                    f"{gate_context}: portal indexes must share "
                    "the same board angle"
                )

        if gate.get("direction") != "outward":
            errors.append(f"{gate_context}.direction: expected 'outward'")

    expected_pairs = {
        (ring_ids[index], ring_ids[index + 1])
        for index in range(len(ring_ids) - 1)
    }
    for from_ring, to_ring in expected_pairs - connected_pairs:
        errors.append(
            f"{context}.gates: missing portal from "
            f"{from_ring!r} to {to_ring!r}"
        )


def validate_exits(
    exits: Any,
    context: str,
    ring_ids: list[str],
    ring_capacities: dict[str, int],
    errors: list[str],
) -> set[str]:
    exit_colors: set[str] = set()
    if not isinstance(exits, list) or not exits:
        errors.append(f"{context}.exits: expected at least one exit")
        return exit_colors

    seen_ids: set[str] = set()
    occupied_indexes: set[int] = set()
    outer_ring = ring_ids[-1] if ring_ids else None

    for exit_index, exit_data in enumerate(exits):
        exit_context = f"{context}.exits[{exit_index}]"
        if not isinstance(exit_data, dict):
            errors.append(f"{exit_context}: expected an object")
            continue

        exit_id = exit_data.get("id")
        if not is_valid_object_id(exit_id):
            errors.append(
                f"{exit_context}.id: expected lowercase snake-case identifier"
            )
        elif exit_id in seen_ids:
            errors.append(f"{exit_context}.id: duplicate exit ID {exit_id!r}")
        else:
            seen_ids.add(exit_id)

        ring_id = exit_data.get("ring")
        if ring_id != outer_ring:
            errors.append(
                f"{exit_context}.ring: prototype exits must use outermost ring "
                f"{outer_ring!r}"
            )

        if ring_id in ring_capacities:
            logical_index = exit_data.get("ringIndex")
            validate_index(
                logical_index,
                ring_capacities[ring_id],
                f"{exit_context}.ringIndex",
                errors,
            )
            if isinstance(logical_index, int) and not isinstance(
                logical_index, bool
            ):
                if logical_index in occupied_indexes:
                    errors.append(
                        f"{exit_context}.ringIndex: duplicate exit index "
                        f"{logical_index}"
                    )
                else:
                    occupied_indexes.add(logical_index)

        color = exit_data.get("color")
        if color not in SUPPORTED_COLORS:
            errors.append(f"{exit_context}.color: unsupported color {color!r}")
        elif color in exit_colors:
            errors.append(f"{exit_context}.color: duplicate color {color!r}")
        else:
            exit_colors.add(color)

    return exit_colors


def validate_index(
    value: Any, capacity: int, context: str, errors: list[str]
) -> None:
    if (
        not isinstance(value, int)
        or isinstance(value, bool)
        or not 0 <= value < capacity
    ):
        errors.append(f"{context}: expected index inside capacity {capacity}")


def is_valid_object_id(value: Any) -> bool:
    return isinstance(value, str) and bool(OBJECT_ID_PATTERN.fullmatch(value))


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "catalog",
        nargs="?",
        type=Path,
        default=DEFAULT_CATALOG,
        help=f"Catalog path (default: {DEFAULT_CATALOG})",
    )
    parser.add_argument(
        "--schema",
        type=Path,
        default=DEFAULT_SCHEMA,
        help=f"Schema path to parse (default: {DEFAULT_SCHEMA})",
    )
    return parser.parse_args()


def main() -> int:
    arguments = parse_arguments()

    try:
        catalog = load_json(arguments.catalog)
        schema = load_json(arguments.schema)
    except ValueError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1

    if not isinstance(schema, dict) or schema.get("$schema") != (
        "https://json-schema.org/draft/2020-12/schema"
    ):
        print("ERROR: schema: expected JSON Schema draft 2020-12", file=sys.stderr)
        return 1

    errors, warnings = validate_catalog(catalog)

    for warning in warnings:
        print(f"WARNING: {warning}")

    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print(
            f"Level validation failed with {len(errors)} error(s).",
            file=sys.stderr,
        )
        return 1

    print(
        f"Level validation passed: {len(catalog['levels'])} level(s), "
        f"schemaVersion={catalog['schemaVersion']}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
