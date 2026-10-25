"""Public development-asset operation seams, without invoking a real replay."""
import contextlib
import io
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import test_asset_operations as operations
import test_assets as assets


class OperationsContracts(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.repo = self.root / 'repo'; (self.repo / 'changes/C-001').mkdir(parents=True)
        (self.repo / 'changes/C-001/state.md').write_text('planned')
        self.config = self.root / 'layout.json'
        self.config.write_text(json.dumps({'version': 1, 'parentRoot': str(self.root / 'assets'),
            'repositoryKey': 'repo', 'workPath': 'work', 'archivePath': 'archive',
            'categoryPaths': {'raw': 'raw', 'logs': 'logs', 'results': 'results', 'scratch': 'scratch'}}))
        self.binding_root = self.root / 'bindings'
        self.bindings = patch.object(assets, 'binding_home', return_value=self.binding_root)
        self.bindings.start(); self.addCleanup(self.bindings.stop)
        self.source = self.repo / 'tool.py'; self.source.write_text('source')
        self.input = self.repo / 'input.json'; self.input.write_text('{}')

    def invoke(self, *args):
        stream = io.StringIO()
        with patch.object(sys, 'argv', ['test-assets', *args]), contextlib.redirect_stdout(stream), patch.object(operations, 'ROOT', self.repo):
            code = operations.main()
        return code, stream.getvalue()

    def test_begin_scope_finish_and_path_ref_use_one_bound_open_run(self):
        code, output = self.invoke('begin', 'C-001', '--operation', 'diagnosis', '--asset-config', str(self.config),
                                   '--source', 'tool.py', '--input', 'input.json', '--protected-root', str(self.repo))
        self.assertEqual(code, 0, output); begun = json.loads(output)
        self.assertEqual(set(begun), {'changeId', 'runId', 'root', 'raw', 'logs', 'results', 'scratch', 'configDigest'})
        self.assertTrue((Path(begun['raw']) / 'provenance.json').is_file())
        code, output = self.invoke('scope', 'C-001', begun['runId'])
        self.assertEqual(code, 0, output); self.assertEqual(json.loads(output)['root'], begun['root'])
        Path(begun['results'], 'packet.json').write_text('{}')
        code, output = self.invoke('finish', 'C-001', begun['runId'], '--summary', json.dumps({'status': 'PASS', 'consumersCompleted': True}))
        self.assertEqual(code, 0, output); finished = json.loads(output)
        self.assertTrue(finished['completed'])
        code, output = self.invoke('path-ref', f"test-assets:C-001/{begun['runId']}/results/packet.json")
        self.assertEqual(code, 0, output); self.assertEqual(Path(output.strip()), Path(begun['results'], 'packet.json'))
        code, _ = self.invoke('scope', 'C-001', begun['runId'])
        self.assertEqual(code, 2)

    def test_missing_input_is_bound_not_skipped_and_completed_run_is_not_rehashed(self):
        code, output = self.invoke('begin', 'C-001', '--operation', 'diagnosis', '--asset-config', str(self.config),
                                   '--source', 'missing.py', '--protected-root', str(self.repo))
        self.assertEqual(code, 0, output); begun = json.loads(output)
        provenance = json.loads((Path(begun['raw']) / 'provenance.json').read_text())
        self.assertEqual(provenance['sourceFiles'][0]['exists'], False)
        self.assertEqual(self.invoke('finish', 'C-001', begun['runId'], '--summary', '{"consumersCompleted":true}')[0], 0)
        record = assets.read_index(self.repo, 'C-001')['runs'][0]
        self.assertTrue(record['summary']['sourceStable'])
        self.assertTrue(record['summary']['sourceStability']['measured'])
        self.assertEqual(self.invoke('finish', 'C-001', begun['runId'], '--summary', '{"consumersCompleted":true}')[0], 2)

    def test_nested_raw_provenance_measures_source_stability(self):
        data = json.loads(self.config.read_text()); data['categoryPaths']['raw'] = 'raw/captures'; self.config.write_text(json.dumps(data))
        code, output = self.invoke('begin', 'C-001', '--operation', 'diagnosis', '--asset-config', str(self.config), '--source', 'tool.py')
        self.assertEqual(code, 0, output); begun = json.loads(output)
        self.assertEqual(self.invoke('finish', 'C-001', begun['runId'], '--summary', '{"consumersCompleted":true}')[0], 0)
        summary = assets.read_index(self.repo, 'C-001')['runs'][0]['summary']
        self.assertTrue(summary['sourceStable']); self.assertTrue(summary['sourceStability']['stable'])

    def test_scope_rechecks_immutable_binding_and_category_link_safety(self):
        code, output = self.invoke('begin', 'C-001', '--operation', 'diagnosis', '--asset-config', str(self.config))
        self.assertEqual(code, 0, output); begun = json.loads(output)
        outside = self.root / 'outside'; outside.mkdir()
        logs = Path(begun['logs']); logs.rmdir(); logs.symlink_to(outside, target_is_directory=True)
        self.assertEqual(self.invoke('scope', 'C-001', begun['runId'])[0], 2)
        logs.unlink(); logs.mkdir()
        record = assets.read_index(self.repo, 'C-001')['runs'][0]
        binding = self.binding_root / 'repo' / (record['configDigest'] + '.json')
        altered = json.loads(binding.read_text()); altered['parentRoot'] = str(self.root / 'other-assets'); binding.write_text(json.dumps(altered))
        self.assertEqual(self.invoke('scope', 'C-001', begun['runId'])[0], 2)

    def test_replay_uses_owned_process_and_preserves_carrier_exit(self):
        case = self.repo / 'case.json'; case.write_text('{}')
        calls = []
        class Process:
            pid = 999999
            def wait(self, timeout=None): return 1
            def poll(self): return 1
        def popen(command, **kwargs):
            calls.append((command, kwargs)); out = Path(command[-1]); out.mkdir(parents=True, exist_ok=True)
            (out / 'replay.json').write_text(json.dumps({'status': 'mismatch', 'carrierExitCode': 1}))
            (out / 'case.trx').write_text('<TestRun><UnitTestResult testName="sealed-case" outcome="Failed"><Output><ErrorInfo><Message>independent-popup-label-mismatch: expected</Message></ErrorInfo></Output></UnitTestResult></TestRun>'); return Process()
        def group_signal(_pid, value):
            if value == 0: raise ProcessLookupError()
        with patch.object(operations.subprocess, 'Popen', side_effect=popen), patch.object(operations.os, 'killpg', side_effect=group_signal) as stop_group:
            code, output = self.invoke('replay', 'C-001', '--case', str(case), '--asset-config', str(self.config))
        self.assertEqual(code, 1, output); result = json.loads(output)
        self.assertEqual(result['operation'], 'replay'); self.assertEqual(calls[0][0][1:3], [str(self.repo / 'tools/evolution-case.py'), 'replay'])
        self.assertEqual(result['trxOutcomes'], {'Failed': 1}); self.assertEqual(result['failures'][0]['name'], 'sealed-case')
        self.assertIn('independent-popup-label-mismatch:', result['failures'][0]['message'])
        stop_group.assert_any_call(999999, operations.signal.SIGTERM)

    def test_os_sigterm_kills_owned_ignoring_writer_then_finalizes(self):
        case = self.repo / 'case.json'; case.write_text('{}')
        writer = []
        real_popen = subprocess.Popen
        def popen(command, **kwargs):
            out = Path(command[-1]); out.mkdir(parents=True, exist_ok=True)
            (out / 'replay.json').write_text(json.dumps({'status': 'unusable', 'carrierExitCode': 2}))
            (out / 'case.trx').write_text('<TestRun/>')
            child = real_popen([sys.executable, '-c', 'import signal,time;signal.signal(signal.SIGTERM, lambda *_: None);time.sleep(60)'], start_new_session=True)
            writer.append(child); threading.Timer(.03, lambda: os.kill(os.getpid(), signal.SIGTERM)).start(); return child
        with patch.object(operations.subprocess, 'Popen', side_effect=popen), patch.object(operations, 'STOP_WAIT_SECONDS', .05):
            code, output = self.invoke('replay', 'C-001', '--case', str(case), '--asset-config', str(self.config))
        self.assertEqual(code, 130, output)
        self.assertTrue(writer[0].poll() is not None)
        record = assets.read_index(self.repo, 'C-001')['runs'][0]
        receipt = json.loads((assets.locate_run(self.repo, 'C-001', record['runId']) / record['finalization']).read_text())
        self.assertFalse(receipt['completed'])

    def test_sigterm_after_registered_begin_skips_carrier_and_finalizes(self):
        case = self.repo / 'case.json'; case.write_text('{}')
        actual_begin = operations.begin
        def begin_then_cancel(*args, **kwargs):
            result = actual_begin(*args, **kwargs); os.kill(os.getpid(), signal.SIGTERM); return result
        with patch.object(operations, 'begin', side_effect=begin_then_cancel), patch.object(operations.subprocess, 'Popen') as popen:
            code, output = self.invoke('replay', 'C-001', '--case', str(case), '--asset-config', str(self.config))
        self.assertEqual(code, 130, output); popen.assert_not_called()
        record = assets.read_index(self.repo, 'C-001')['runs'][0]
        receipt = json.loads((assets.locate_run(self.repo, 'C-001', record['runId']) / record['finalization']).read_text())
        self.assertFalse(receipt['completed']); self.assertTrue(receipt['pending'])

    def test_sealed_input_uses_current_bytes_and_rejects_retry_after_mutation(self):
        sealed = self.repo / 'sealed'; (sealed / 'inputs').mkdir(parents=True)
        payload = sealed / 'inputs/value.json'; payload.write_text('{"value":1}')
        declared = '0' * 64
        case = sealed / 'case.json'; case.write_text(json.dumps({'inputs': [{'path': 'inputs/value.json', 'sha256': declared}]}))
        with patch.object(operations, 'ROOT', self.repo):
            _, sources, inputs = operations._declared_case_materials(case)
            self.assertEqual(inputs[0]['sha256'], assets.file_digest(payload)); self.assertEqual(inputs[0]['declaredSha256'], declared)
            first = operations.begin('C-001', 'replay', str(self.config), None, [], [], None, [], sources, inputs)
            index = assets.read_index(self.repo, 'C-001')
            self.assertEqual(index['baselineRefs'], [])
            self.assertEqual({item['role'] for item in index['runs'][0]['protectedRefs']}, {'original-source'})
            operations.finish('C-001', first['runId'], {'consumersCompleted': True})
            payload.write_text('{"value":2}')
            _, changed_sources, changed_inputs = operations._declared_case_materials(case)
            with self.assertRaises(ValueError):
                operations.begin('C-001', 'replay', str(self.config), first['runId'], [], [], None, [], changed_sources, changed_inputs)
        outside = self.root / 'outside.json'; outside.write_text('{}')
        case.write_text(json.dumps({'inputs': [{'path': '../outside.json', 'sha256': declared}]}))
        with patch.object(operations, 'ROOT', self.repo):
            with self.assertRaises(ValueError): operations._declared_case_materials(case)

    def test_replay_restores_handlers_and_keeps_verdict_on_finalization_error(self):
        previous_term = signal.getsignal(signal.SIGTERM)
        previous_int = signal.getsignal(signal.SIGINT)
        case = self.repo / 'case.json'
        case.write_text(json.dumps({'inputs': [{'path': '../outside', 'sha256': '0' * 64}]}))
        try:
            self.assertEqual(self.invoke('replay', 'C-001', '--case', str(case), '--asset-config', str(self.config))[0], 2)
            self.assertIs(signal.getsignal(signal.SIGTERM), previous_term)
            self.assertIs(signal.getsignal(signal.SIGINT), previous_int)
        finally:
            signal.signal(signal.SIGTERM, previous_term); signal.signal(signal.SIGINT, previous_int)
        case.write_text('{}')
        class Process:
            pid = 999996
            def wait(self, timeout=None): return 1
            def poll(self): return 1
        def popen(command, **kwargs):
            out = Path(command[-1]); out.mkdir(parents=True)
            (out / 'replay.json').write_text(json.dumps({'status': 'mismatch', 'carrierExitCode': 1}))
            (out / 'case.trx').write_text('<TestRun/>')
            return Process()
        with patch.object(operations.subprocess, 'Popen', side_effect=popen), patch.object(operations.os, 'killpg', side_effect=ProcessLookupError()), patch.object(operations, 'finish', side_effect=OSError('controlled receipt failure')):
            code, output = self.invoke('replay', 'C-001', '--case', str(case), '--asset-config', str(self.config))
        self.assertEqual(code, 1, output)
        result = json.loads(output)
        self.assertIn('controlled receipt failure', result['finalizationPending'])
        self.assertEqual(result['changeId'], 'C-001')
        self.assertIs(signal.getsignal(signal.SIGTERM), previous_term)
        self.assertIn('finalizationPending', assets.read_index(self.repo, 'C-001')['runs'][0])

    def test_replay_cli_exit_wins_over_inner_carrier_exit(self):
        case = self.repo / 'case.json'; case.write_text('{}')
        class Process:
            pid = 999998
            def wait(self, timeout=None): return 2
            def poll(self): return 2
        def popen(command, **kwargs):
            out = Path(command[-1]); out.mkdir(parents=True, exist_ok=True)
            (out / 'replay.json').write_text(json.dumps({'status': 'unusable', 'carrierExitCode': 0}))
            (out / 'case.trx').write_text('<TestRun/>'); return Process()
        with patch.object(operations.subprocess, 'Popen', side_effect=popen):
            code, output = self.invoke('replay', 'C-001', '--case', str(case), '--asset-config', str(self.config))
        self.assertEqual(code, 2, output); self.assertEqual(json.loads(output)['carrierExitCode'], 0)

    def test_malformed_replay_output_is_finalized_as_pending_without_changing_cli_exit(self):
        case = self.repo / 'case.json'; case.write_text('{}')
        class Process:
            pid = 999997
            def wait(self, timeout=None): return 2
            def poll(self): return 2
        def popen(command, **kwargs):
            out = Path(command[-1]); out.mkdir(parents=True, exist_ok=True); (out / 'replay.json').write_text('{')
            return Process()
        with patch.object(operations.subprocess, 'Popen', side_effect=popen):
            code, output = self.invoke('replay', 'C-001', '--case', str(case), '--asset-config', str(self.config))
        self.assertEqual(code, 2, output)
        record = assets.read_index(self.repo, 'C-001')['runs'][0]
        self.assertFalse(record['summary']['consumersCompleted'])
        receipt = json.loads((assets.locate_run(self.repo, 'C-001', record['runId']) / record['finalization']).read_text())
        self.assertFalse(receipt['completed']); self.assertTrue(receipt['pending'])


if __name__ == '__main__': unittest.main(verbosity=2)
