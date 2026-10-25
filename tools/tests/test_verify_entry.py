#!/usr/bin/env python3
"""Independent regression tests for verification entry points (stdlib only)."""
import contextlib
import importlib.machinery
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import shutil
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]


def module(name, path):
    loader = importlib.machinery.SourceFileLoader(name, str(path))
    spec = importlib.util.spec_from_loader(name, loader)
    result = importlib.util.module_from_spec(spec)
    loader.exec_module(result)
    return result


verify = module('verify_entry', ROOT / 'tools/verify-change')
coverage = module('scenario_coverage', ROOT / 'tools/scenario-coverage.py')
NAMES = [
    'UniClaw.Simulation.Tests.TheoryTests.Case(text: "hello world", count: 1)',
    'UniClaw.Simulation.Tests.TheoryTests.Case(text: "say \\"hello\\"", count: 2)',
    'UniClaw.Simulation.Tests.TheoryTests.Case(text: "hello world", count: 3)',
]
LISTING = 'Test run for Simulation.dll\nThe following Tests are available:\n' + ''.join('    ' + n + '\n' for n in NAMES)


class EntryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.fixture = Path(self.temp.name).resolve()
        self.repo = self.fixture / 'repo'
        (self.repo / 'tools').mkdir(parents=True)
        shutil.copyfile(ROOT / 'tools/evolution-signal.py', self.repo / 'tools/evolution-signal.py')
        (self.repo / 'scenarios').mkdir()
        for sid in verify.manifest_scenario_ids():
            shutil.copyfile(ROOT / f'scenarios/{sid}.json', self.repo / f'scenarios/{sid}.json')
        for change in ('VERIFY-ENTRY-001', 'EVO-002'):
            owner = self.repo / 'changes' / change
            owner.mkdir(parents=True); (owner / 'state.md').write_text('planned')
        self.config = self.fixture / 'layout.json'
        self.config.write_text(json.dumps({'version': 1, 'parentRoot': str(self.fixture / 'assets'),
            'repositoryKey': 'test-repo', 'workPath': 'work', 'archivePath': 'archives',
            'categoryPaths': {kind: kind for kind in ('raw', 'logs', 'results', 'scratch')}}))
        import os
        for item in (patch.dict(os.environ, {'UNICLAW_TEST_ASSET_CONFIG': str(self.config),
                                            'XDG_CONFIG_HOME': str(self.fixture / 'profile')}),
                     patch.object(verify, 'ROOT', str(self.repo)),
                     patch('test_assets.binding_home', return_value=self.fixture / 'machine-bindings')):
            item.start(); self.addCleanup(item.stop)

    def signals(self):
        return [json.loads(p.read_text()) for p in (self.fixture / 'assets').glob('**/signals/*.json')]

    def calls(self, scope, fail_host_dsh=False):
        calls = []
        def run(label, args):
            calls.append((label, args))
            return (1 if fail_host_dsh and 'Host.Dsh' in label else 0), ''
        with tempfile.TemporaryDirectory() as signals, patch.object(sys, 'argv', ['verify-change', 'VERIFY-ENTRY-001', '--scope', scope, '--no-snapshot']), patch.object(verify, 'run', side_effect=run), contextlib.redirect_stdout(io.StringIO()):
            rc = verify.main()
        return rc, calls

    def test_full_executes_solution_projects_once_and_reuses_simulation_trx(self):
        rc, calls = self.calls('full')
        expected = [p.attrib['Path'] for p in ET.parse(ROOT / 'UniClaw.Kernel.slnx').iter('Project') if p.attrib['Path'].startswith('tests/')]
        executed = [args[2] for label, args in calls if label == 'SIMULATION' or label.startswith('FULL_TEST_PROJECTS[')]
        self.assertCountEqual(executed, expected)
        self.assertEqual(len(executed), len(set(executed)))
        self.assertEqual(rc, 0)
        cov_args = next(args for label, args in calls if label == 'COVERAGE')
        self.assertIn('--trx', cov_args)
        self.assertNotIn('--run', cov_args)

    def test_host_dsh_failure_propagates(self):
        self.assertEqual(self.calls('full', fail_host_dsh=True)[0], 1)

    def test_failed_exit_without_fresh_trx_is_unknown_and_not_misattributed(self):
        with tempfile.TemporaryDirectory() as tmp:
            args = ['verify-change', 'EVO-002', '--scope', 'full', '--no-snapshot']
            def failed_group(label, _):
                return (1, 'build failed') if 'Host.Dsh' in label else (0, '')
            with patch.object(sys, 'argv', args), patch.object(verify, 'run', side_effect=failed_group), contextlib.redirect_stdout(io.StringIO()):
                rc = verify.main()
            self.assertEqual(rc, 1)
            signals = self.signals()
            self.assertTrue(signals)
            self.assertTrue(any(s['verdict'] == 'unknown' for s in signals))
            self.assertTrue(all(s['preliminaryClass'] is None for s in signals))

    def test_green_verification_still_emits_independent_asset_drift_signal(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'tools').mkdir()
            shutil.copyfile(ROOT / 'tools/evolution-signal.py', root / 'tools/evolution-signal.py')
            (root / 'scenarios').mkdir()
            for sid in verify.manifest_scenario_ids():
                shutil.copyfile(ROOT / f'scenarios/{sid}.json', root / f'scenarios/{sid}.json')
            manifest = root / 'tests/Test/Fixtures/Replay/composition.json'
            manifest.parent.mkdir(parents=True)
            manifest.write_text(json.dumps({'schemaVersion': 'component-replay-composition.v1',
                'pins': [{'path': 'changed.cs', 'sha256': '0' * 64}]}))
            (root / 'changed.cs').write_text('new bytes')
            (root / 'changes/EVO-002').mkdir(parents=True)
            (root / 'changes/EVO-002/state.md').write_text('planned')
            args = ['verify-change', 'EVO-002', '--scope', 'quick', '--no-snapshot']
            with patch.object(sys, 'argv', args), patch.object(verify, 'ROOT', str(root)), patch.object(verify, 'run', return_value=(0, '')), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(verify.main(), 0)  # original quick-scope verdict is preserved
            signals = self.signals()
            self.assertTrue(any(s['verdict'] == 'hash-mismatch' for s in signals))

    def test_quick_stays_filtered_without_full_or_coverage(self):
        rc, calls = self.calls('quick')
        self.assertEqual(rc, 0)
        self.assertFalse(any(label == 'COVERAGE' or label.startswith('FULL_TEST_PROJECTS') for label, _ in calls))
        sim = [args for label, args in calls if label == 'SIMULATION']
        self.assertEqual(len(sim), 1)
        self.assertIn('--filter', sim[0])

    def test_parameterized_names_join_trx_without_merging(self):
        proc = subprocess.CompletedProcess([], 0, LISTING, '')
        with patch.object(coverage, 'TEST_ROOTS', (coverage.SIM_TESTS,)), patch.object(coverage.subprocess, 'run', return_value=proc):
            names = coverage.scenario_test_map(['SCN-FIXTURE-001'], no_build=True)['SCN-FIXTURE-001']
        self.assertEqual(names, NAMES)
        root = ET.Element('TestRun')
        for name, outcome in zip(NAMES, ['Passed', 'Failed', 'Passed']):
            ET.SubElement(root, 'UnitTestResult', testName=name, outcome=outcome)
        with tempfile.TemporaryDirectory() as tmp:
            trx = Path(tmp) / 'fixture.trx'
            ET.ElementTree(root).write(trx)
            outcomes = coverage.parse_trx(trx)
        self.assertEqual([outcomes[n] for n in names], ['Passed', 'Failed', 'Passed'])
        self.assertEqual(coverage.derive_status([outcomes[n] for n in names]), ('failing', None))

    def test_conflicting_same_name_results_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            trx = Path(tmp) / 'duplicate.trx'
            trx.write_text('<TestRun><UnitTestResult testName="same" outcome="Failed"/><UnitTestResult testName="same" outcome="Passed"/></TestRun>')
            with self.assertRaises(ValueError):
                coverage.parse_trx(trx)

    def test_load_rejects_empty_unknown_and_missing_case(self):
        with tempfile.TemporaryDirectory() as tmp:
            scenarios = Path(tmp)
            data = json.loads((ROOT / 'scenarios/SCN-WIFI-001.json').read_text())
            sid = data['id']
            (scenarios / (sid + '.json')).write_text(json.dumps(data))
            with patch.object(coverage, 'SCENARIOS', scenarios), patch.object(coverage, 'runtime_source_hash', return_value='fixture'), patch.object(coverage, 'verify_entry', return_value=[]), patch.object(coverage, 'load_schema_violations', return_value=(None, [])):
                for outcomes, names in [({}, []), ({NAMES[0]: 'Unexpected'}, [NAMES[0]]), ({NAMES[0]: 'Passed'}, NAMES[:2])]:
                    with self.subTest(outcomes=outcomes, names=names):
                        _, violations = coverage.load(outcomes, {sid: names})
                        self.assertTrue(violations)

    def test_empty_and_unknown_never_pass(self):
        self.assertIsNone(coverage.derive_status([])[0])
        self.assertIsNone(coverage.derive_status(['Passed', 'Unexpected'])[0])
        with patch.object(coverage.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0, 'no tests', '')):
            self.assertEqual(coverage.scenario_test_map(['SCN-FIXTURE-001'], no_build=True), {'SCN-FIXTURE-001': []})


if __name__ == '__main__':
    unittest.main(verbosity=2)
