"""Orchestrator consumers cross the same managed file paths as real runners."""
import contextlib
import io
import json
import os
import signal
import subprocess
import time
from pathlib import Path
import sys
import unittest
from unittest.mock import patch
import test_verify_entry as entry
verify = entry.verify
from test_assets import read_index, locate_run, validate_run


class VerifyAssets(unittest.TestCase):
    setUp = entry.EntryTests.setUp

    def invoke(self, scope='full', extra=None, failure=False, cancel=False, consumer_error=False, finish_error=False, snapshot_error=False):
        args = ['verify-change', 'VERIFY-ENTRY-001', '--scope', scope, '--no-snapshot', *(extra or [])]
        if snapshot_error: args.remove('--no-snapshot')
        self.coverage_inputs = []
        def runner(label, command):
            if command[:2] == ['dotnet', 'test']:
                directory = Path(command[command.index('--results-directory') + 1])
                name = next(x.split('=', 1)[1] for x in command if x.startswith('trx;LogFileName='))
                outcome = 'Failed' if failure and label == 'SIMULATION' else 'Passed'
                (directory / name).write_text(f'<TestRun><UnitTestResult testName="sample" outcome="{outcome}"/><UnitTestResult testName="skip" outcome="NotExecuted"/></TestRun>')
                if cancel and label == 'SIMULATION': raise KeyboardInterrupt()
                return (1 if outcome == 'Failed' else 0), 'test output'
            if label == 'COVERAGE':
                self.coverage_inputs = [Path(command[i + 1]) for i, word in enumerate(command) if word == '--trx']
                self.assertEqual(len(self.coverage_inputs), 2)
                self.assertTrue(all(p.is_file() for p in self.coverage_inputs))
            return 0, 'consumer output'
        with contextlib.ExitStack() as stack:
            stack.enter_context(patch.object(sys, 'argv', args))
            stack.enter_context(patch.object(verify, 'run', side_effect=runner))
            stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
            if consumer_error: stack.enter_context(patch.object(verify, 'emit_evolution_signals', side_effect=OSError('signal write failed')))
            if snapshot_error: stack.enter_context(patch.object(verify, 'write_resolved_config', side_effect=OSError('snapshot write failed')))
            if finish_error: stack.enter_context(patch('test_assets.AssetRun.finish', side_effect=OSError('receipt write failed')))
            return verify.main()

    def record(self):
        return read_index(self.repo, 'VERIFY-ENTRY-001')['runs'][-1]

    def test_real_generated_trx_coverage_signal_and_inventory_share_root(self):
        self.assertEqual(self.invoke(failure=True), 1)
        record = self.record(); root = locate_run(self.repo, 'VERIFY-ENTRY-001', record['runId'])
        self.assertTrue(all(p.is_relative_to(root) for p in self.coverage_inputs))
        self.assertTrue(validate_run(self.repo, 'VERIFY-ENTRY-001', record['runId'])['valid'])
        sim = next(t for t in record['summary']['trx'] if '/SIMULATION/' in t['path'])
        self.assertEqual(sim['outcomes'], {'Failed': 1, 'NotExecuted': 1})
        signals = list(root.glob('results/evolution/signals/*.json'))
        self.assertTrue(signals)
        refs = [json.loads(p.read_text())['source'].get('path', '') for p in signals]
        self.assertTrue(any(str(root) in p for p in refs))
        self.assertTrue(record['summary']['consumersCompleted'])
        self.assertFalse((self.repo / 'evidence').exists())
        self.assertFalse(list(self.repo.glob('tests/*/TestResults')))

    def test_cancel_retains_partial_counts_and_pending_receipt(self):
        self.assertEqual(self.invoke(cancel=True), 130)
        record = self.record(); root = locate_run(self.repo, 'VERIFY-ENTRY-001', record['runId'])
        self.assertEqual(record['summary']['status'], 'CANCELLED')
        self.assertTrue(any(x.get('outcomes') for x in record['summary']['trx']))
        receipt = json.loads((root / record['finalization']).read_text())
        self.assertTrue(receipt['pending']); self.assertFalse(receipt['completed'])
        self.assertEqual(receipt['deletedBytes'], 0)

    def test_retry_keeps_failed_run_and_binding(self):
        self.assertEqual(self.invoke(scope='quick', failure=True), 1)
        previous = self.record(); root = locate_run(self.repo, 'VERIFY-ENTRY-001', previous['runId'])
        old = (root / previous['files'][0]['path']).read_bytes()
        self.assertEqual(self.invoke(scope='quick', extra=['--retry-of', previous['runId']]), 0)
        self.assertEqual(self.record()['retryOf'], previous['runId'])
        self.assertEqual((root / previous['files'][0]['path']).read_bytes(), old)
        self.assertEqual(len(read_index(self.repo, 'VERIFY-ENTRY-001')['runs']), 2)

    def test_consumer_or_finalizer_failure_preserves_test_verdict(self):
        self.assertEqual(self.invoke(scope='quick', consumer_error=True), 0)
        self.assertFalse(self.record()['summary']['consumersCompleted'])
        self.assertEqual(self.invoke(scope='quick', finish_error=True), 0)
        self.assertIn('receipt write failed', self.record()['finalizationPending'])
        self.assertEqual(self.invoke(scope='quick', snapshot_error=True), 0)
        self.assertFalse(self.record()['summary']['consumersCompleted'])
        self.assertEqual(self.record()['summary']['consumerErrors'][0]['consumer'], 'resolved-config')

    def test_real_sigterm_stops_owned_writer_before_inventory(self):
        fakebin = self.fixture / 'fakebin'; fakebin.mkdir()
        marker = self.fixture / 'writer.pid'
        executable = fakebin / 'dotnet'
        executable.write_text("#!/usr/bin/env python3\nimport os,sys,time\nfrom pathlib import Path\n"
            "args=sys.argv[1:]\nd=Path(args[args.index('--results-directory')+1])\n"
            "name=next(a.split('=',1)[1] for a in args if a.startswith('trx;LogFileName='))\n"
            "(d/name).write_text('<TestRun/>')\n"
            "print('writer started',flush=True)\n"
            f"Path({str(marker)!r}).write_text(str(os.getpid()))\n"
            "time.sleep(60)\n")
        executable.chmod(0o755)
        script = ("import importlib.machinery,sys\nfrom pathlib import Path\n"
            f"v=importlib.machinery.SourceFileLoader('cancel_verify',{str(entry.ROOT / 'tools/verify-change')!r}).load_module()\n"
            f"v.ROOT={str(self.repo)!r}\n"
            f"import test_assets;test_assets.binding_home=lambda:Path({str(self.fixture / 'machine-bindings')!r})\n"
            "sys.argv=['verify-change','VERIFY-ENTRY-001','--scope','quick','--no-snapshot']\n"
            "sys.exit(v.main())\n")
        environment = dict(os.environ, PATH=str(fakebin) + os.pathsep + os.environ['PATH'])
        process = subprocess.Popen([sys.executable, '-c', script], env=environment, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        writer = None
        try:
            deadline = time.monotonic() + 5
            while not marker.exists() and process.poll() is None and time.monotonic() < deadline: time.sleep(.02)
            self.assertTrue(marker.exists())
            writer = int(marker.read_text())
            process.send_signal(signal.SIGTERM)
            output, _ = process.communicate(timeout=5)
            self.assertEqual(process.returncode, 130, output)
            with self.assertRaises(ProcessLookupError): os.kill(writer, 0)
            record = self.record()
            self.assertEqual(record['summary']['status'], 'CANCELLED')
            root = locate_run(self.repo, 'VERIFY-ENTRY-001', record['runId'])
            self.assertIn('writer started', (root / 'logs/ARCHITECTURE.txt').read_text())
            self.assertTrue(validate_run(self.repo, 'VERIFY-ENTRY-001', record['runId'])['valid'])
        finally:
            if process.poll() is None: process.kill(); process.wait()
            if writer:
                try: os.killpg(writer, signal.SIGKILL)
                except ProcessLookupError: pass

    def test_query_has_no_run_or_outputs_and_absolute_override_is_rejected(self):
        args = ['verify-change', 'VERIFY-ENTRY-001', '--print-asset-config']
        with patch.object(sys, 'argv', args), patch.object(verify, 'run') as run, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(verify.main(), 0); run.assert_not_called()
        self.assertFalse((self.fixture / 'assets').exists())
        self.assertEqual(self.invoke(scope='quick', extra=['--signal-dir', '/outside']), 2)
        self.assertEqual(read_index(self.repo, 'VERIFY-ENTRY-001')['runs'], [])


if __name__ == '__main__': unittest.main(verbosity=2)
