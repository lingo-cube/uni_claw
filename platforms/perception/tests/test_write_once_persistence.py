"""IMM-01..08 representative falsifiers for canonical history persistence."""
from __future__ import annotations

import concurrent.futures
import tempfile
import unittest
from pathlib import Path

from persistence import WriteOnceIntegrityError, write_once_json


class WriteOnceJsonTests(unittest.TestCase):
    def test_IMM_01_identical_replay_is_idempotent_and_canonical(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "record.json"
            self.assertEqual(write_once_json(path, {"b": 2, "a": 1}), path)
            initial = path.read_bytes()
            write_once_json(path, {"a": 1, "b": 2})
            self.assertEqual(path.read_bytes(), initial)
            self.assertEqual(initial, b'{"a":1,"b":2}')

    def test_IMM_02_different_content_is_refused_without_replacement(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "record.json"
            write_once_json(path, {"value": "frozen"})
            initial = path.read_bytes()
            with self.assertRaises(WriteOnceIntegrityError):
                write_once_json(path, {"value": "replacement"})
            self.assertEqual(path.read_bytes(), initial)

    def test_IMM_03_concurrent_same_record_is_never_partially_exposed(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "record.json"
            with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
                futures = [pool.submit(write_once_json, path, {"stable": True})
                           for _ in range(24)]
                self.assertEqual([f.result() for f in futures], [path] * 24)
            self.assertEqual(path.read_bytes(), b'{"stable":true}')

    def test_IMM_04_concurrent_collision_preserves_one_complete_record(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "record.json"
            with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
                results = list(pool.map(
                    lambda payload: _attempt(path, payload), [{"winner": 1}, {"winner": 2}]))
            self.assertEqual(results.count("written"), 1)
            self.assertEqual(results.count("refused"), 1)
            self.assertIn(path.read_bytes(), (b'{"winner":1}', b'{"winner":2}'))


def _attempt(path: Path, payload: dict[str, int]) -> str:
    try:
        write_once_json(path, payload)
        return "written"
    except WriteOnceIntegrityError:
        return "refused"


if __name__ == "__main__":
    unittest.main()
