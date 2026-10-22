"""SIM-015: one literal contract matrix consumed by Schema, Python and C#."""
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('component_scope_coverage', ROOT / 'tools/scenario-coverage.py')
coverage = importlib.util.module_from_spec(spec)
spec.loader.exec_module(coverage)
CASES = json.loads((ROOT / 'tests/UniClaw.Simulation.Tests/Fixtures/ComponentReplay/annotation-cases.json').read_text())['cases']

class ComponentScenarioScopeTests(unittest.TestCase):
    def test_schema_matches_literal_matrix(self):
        self.assertIsNotNone(coverage.jsonschema, 'jsonschema is required for contract validation')
        schema = json.loads((ROOT / 'scenarios/schema.json').read_text())
        for row in CASES:
            with self.subTest(case=row['id']):
                self.assertEqual(row['valid'], not coverage.validate_entry(schema, row['data'], row['id']))

    def test_python_policy_matches_literal_matrix(self):
        for row in CASES:
            with self.subTest(case=row['id']):
                self.assertEqual(row['valid'], not coverage.component_scope_violations(row['data']))

    def test_existing_scenarios_keep_scope_and_decision(self):
        schema = json.loads((ROOT / 'scenarios/schema.json').read_text())
        for file in (ROOT / 'scenarios').glob('SCN-*.json'):
            data = json.loads(file.read_text())
            with self.subTest(case=file.name):
                self.assertEqual([], coverage.validate_entry(schema, data, file.name))
                self.assertEqual([], coverage.component_scope_violations(data))

if __name__ == '__main__':
    unittest.main()
