"""Test assets for Uni-Claw testing.

Fixture loading helpers and standard test data.
"""

import json
from pathlib import Path
from typing import Any, Dict

FIXTURES_DIR = Path(__file__).parent / "fixtures"


def load_fixture(name: str) -> dict:
    """Load a JSON fixture file from tests/assets/fixtures/."""
    with open(FIXTURES_DIR / name) as f:
        return json.load(f)


def load_virtual_pages(name: str = "virtual_pages_simple.json") -> Dict[str, Any]:
    """Load virtual_pages fixture for SimulationRunner.

    Standard fixtures:
    - virtual_pages_simple.json: single "home" page with no elements
    - pages_all.json: 7 settings pages (Wi-Fi, Bluetooth, Display, etc.)
    - pages_find.json: 5 pages for target search tests
    """
    return load_fixture(name)


def load_plan(name: str) -> Dict[str, Any]:
    """Load a traversal plan fixture."""
    return load_fixture(name)
