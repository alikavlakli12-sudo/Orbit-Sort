from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY_ROOT / "Tools"))

import validate_levels  # noqa: E402


class LevelCatalogValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        with validate_levels.DEFAULT_CATALOG.open(
            "r", encoding="utf-8"
        ) as stream:
            cls.valid_catalog = json.load(stream)

    def test_seed_catalog_is_valid(self) -> None:
        errors, _ = validate_levels.validate_catalog(
            copy.deepcopy(self.valid_catalog)
        )

        self.assertEqual([], errors)

    def test_catalog_uses_requested_ring_progression(self) -> None:
        ring_counts = [
            len(level["rings"])
            for level in self.valid_catalog["levels"]
        ]

        self.assertEqual([2, 2, 3, 3, 3], ring_counts)

    def test_first_level_uses_two_colors_and_all_safe_slots(self) -> None:
        level = self.valid_catalog["levels"][0]
        inner, outer = level["rings"]
        gate = level["gates"][0]
        colors = {
            marble["color"]
            for ring in level["rings"]
            for marble in ring["marbles"]
        }
        inner_occupied = {
            marble["index"] for marble in inner["marbles"]
        }
        outer_occupied = {
            marble["index"] for marble in outer["marbles"]
        }
        exit_indexes = {
            exit_data["ringIndex"] for exit_data in level["exits"]
        }

        self.assertEqual({"blue", "red"}, colors)
        self.assertEqual({"blue", "red"}, {
            exit_data["color"] for exit_data in level["exits"]
        })
        self.assertEqual(7, len(inner["marbles"]))
        self.assertEqual(9, len(outer["marbles"]))
        self.assertEqual(16, len(inner["marbles"]) + len(outer["marbles"]))
        self.assertNotIn(gate["fromIndex"], inner_occupied)
        self.assertNotIn(gate["toIndex"], outer_occupied)
        self.assertTrue(exit_indexes.isdisjoint(outer_occupied))
        self.assertNotIn(gate["toIndex"], exit_indexes)

    def test_duplicate_marble_index_is_rejected(self) -> None:
        catalog = copy.deepcopy(self.valid_catalog)
        marbles = catalog["levels"][0]["rings"][0]["marbles"]
        marbles[1]["index"] = marbles[0]["index"]

        errors, _ = validate_levels.validate_catalog(catalog)

        self.assertTrue(
            any("duplicate index" in error for error in errors),
            errors,
        )

    def test_move_counter_cannot_be_enabled(self) -> None:
        catalog = copy.deepcopy(self.valid_catalog)
        catalog["rules"]["moveCounterEnabled"] = True

        errors, _ = validate_levels.validate_catalog(catalog)

        self.assertTrue(
            any("moveCounterEnabled" in error for error in errors),
            errors,
        )

    def test_every_marble_color_needs_an_exit(self) -> None:
        catalog = copy.deepcopy(self.valid_catalog)
        catalog["levels"][0]["exits"] = [
            exit_data
            for exit_data in catalog["levels"][0]["exits"]
            if exit_data["color"] != "red"
        ]

        errors, _ = validate_levels.validate_catalog(catalog)

        self.assertTrue(
            any("missing exits for colors red" in error for error in errors),
            errors,
        )

    def test_every_level_requires_two_or_three_rings(self) -> None:
        catalog = copy.deepcopy(self.valid_catalog)
        level = catalog["levels"][0]
        level["rings"].pop()
        level["gates"].clear()

        errors, _ = validate_levels.validate_catalog(catalog)

        self.assertTrue(
            any("expected 2 or 3 rings" in error for error in errors),
            errors,
        )

    def test_every_level_requires_one_portal_per_ring_gap(self) -> None:
        catalog = copy.deepcopy(self.valid_catalog)
        catalog["levels"][0]["gates"].pop()

        errors, _ = validate_levels.validate_catalog(catalog)

        self.assertTrue(
            any("expected exactly 1 gate" in error for error in errors),
            errors,
        )

    def test_ring_ids_must_match_physical_order(self) -> None:
        catalog = copy.deepcopy(self.valid_catalog)
        catalog["levels"][2]["rings"][1]["id"] = "second"

        errors, _ = validate_levels.validate_catalog(catalog)

        self.assertTrue(
            any("expected 'middle'" in error for error in errors),
            errors,
        )

    def test_portal_indexes_must_share_the_same_angle(self) -> None:
        catalog = copy.deepcopy(self.valid_catalog)
        catalog["levels"][0]["gates"][0]["toIndex"] = 1

        errors, _ = validate_levels.validate_catalog(catalog)

        self.assertTrue(
            any("must share the same board angle" in error for error in errors),
            errors,
        )


if __name__ == "__main__":
    unittest.main()
