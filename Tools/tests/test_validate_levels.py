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
            if exit_data["color"] != "yellow"
        ]

        errors, _ = validate_levels.validate_catalog(catalog)

        self.assertTrue(
            any("missing exits for colors yellow" in error for error in errors),
            errors,
        )


if __name__ == "__main__":
    unittest.main()
