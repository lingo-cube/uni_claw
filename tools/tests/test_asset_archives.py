"""Archive/restore contracts through the public Run and original consumer seams."""
import json
import os
import time
from pathlib import Path
import runpy
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import test_assets as assets
import test_asset_archives as archives


class ArchiveContracts(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name).resolve(); self.repo = self.base / 'repo'
        (self.repo / 'changes/C-001').mkdir(parents=True)
        (self.repo / 'changes/C-001/state.md').write_text('implemented')
        self.config = self.base / 'layout.json'
        self.data = {'version': 1, 'parentRoot': str(self.base / 'assets'), 'repositoryKey': 'repo',
            'workPath': 'work', 'archivePath': 'archives',
            'categoryPaths': {'raw': 'captures/raw', 'logs': 'logs', 'results': 'output/results', 'scratch': 'scratch'}}
        self.config.write_text(json.dumps(self.data))
        bound = patch.object(assets, 'binding_home', return_value=self.base / 'bindings')
        bound.start(); self.addCleanup(bound.stop)
        self.layout = assets.load_layout(self.repo, str(self.config))

    def run_fixture(self, with_packet=False):
        run = assets.AssetRun(self.repo, self.layout, 'C-001', 'archive-proof', 'a', 'b')
        trx = run.path('raw', 'actual.trx')
        trx.write_text('<TestRun><UnitTestResult testName="archive.case" outcome="Failed"><Output><ErrorInfo><Message>controlled mismatch</Message></ErrorInfo></Output></UnitTestResult><UnitTestResult testName="skip.case" outcome="NotExecuted"/></TestRun>')
        adapter = runpy.run_path(str(TOOLS / 'evolution-signal.py'))
        signal = adapter['scan_dev']([trx], self.repo, context={})[0]
        signal_file = Path(adapter['persist']([signal], run.directory('results') / 'evolution', self.repo)[0])
        run.signal_relative = signal_file.relative_to(run.root).as_posix()
        if with_packet:
            report = self.repo / 'protected-report.json'; report.write_text('{"status":"controlled"}')
            packet = {'schema': 'uniclaw.diagnosis.packet.v0', 'authority': 'non-authoritative',
                'meta': {'diagnosedAt': '2026-10-25', 'producer': {'kind': 'agent-skill'}},
                'subject': {'inputs': [{'path': str(report), 'sha256': assets.file_digest(report), 'role': 'run-report'}]},
                'evidenceLevel': 'E1', 'evidenceSummary': ['controlled archive fixture, no model diagnosis'],
                'failureClass': 'B', 'lifecycleStage': 'archive-test', 'lastCorrectState': 'unknown',
                'firstDivergence': {'where': 'unknown', 'expected': 'controlled fixture', 'observed': 'controlled fixture', 'evidenceRefs': ['protected-report.json']},
                'owner': 'unknown', 'rootCauseSupport': [], 'remainingUncertainty': ['not live evidence'],
                'escalation': {'required': False, 'decisionRequest': []}}
            run.path('raw', 'diagnosis.packet.json').write_text(json.dumps(packet))
        run.finish({'status': 'PASS', 'operation': 'archive-proof', 'consumersCompleted': True, 'sourceStable': True})
        return run, trx

    def discard_fixture(self, run):
        # Only this test's freshly constructed source, not application history.
        shutil.rmtree(run.root)

    def test_archive_unpack_restore_and_original_signal_reference(self):
        run, trx = self.run_fixture(); original = assets.read_index(self.repo, 'C-001')['runs'][0]
        archived = archives.archive_run(self.repo, 'C-001', run.run_id)
        self.assertTrue(archived['verified'])
        self.assertTrue(any(c['reader'] == 'scenario-coverage.parse_trx' for c in archived['consumerChecks']))
        self.assertEqual(assets.read_index(self.repo, 'C-001')['runs'][0]['files'], original['files'])
        self.discard_fixture(run)
        self.assertTrue(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])
        self.assertFalse(run.root.exists())  # validate does not restore.
        path = assets.asset_path(self.repo, 'C-001', run.run_id, run.signal_relative)
        signal = json.loads(path.read_text())
        self.assertEqual(Path(signal['source']['path']), trx)
        self.assertEqual(assets.file_digest(trx), signal['source']['sha256'])
        self.assertTrue(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])

    def test_current_config_change_cannot_move_history_or_archive(self):
        run, _ = self.run_fixture()
        entry = archives.archive_run(self.repo, 'C-001', run.run_id)
        self.data['parentRoot'] = str(self.base / 'new-root'); self.config.write_text(json.dumps(self.data))
        self.discard_fixture(run)
        restored = archives.restore_run(self.repo, 'C-001', run.run_id)
        self.assertEqual(restored['root'], str(run.root))
        self.assertFalse((self.base / 'new-root').exists())
        self.assertTrue(archives.verify_archive(self.repo, 'C-001', entry['archiveId'])['valid'])

    def test_archive_refuses_open_or_drifted_or_uninventoried_work(self):
        run = assets.AssetRun(self.repo, self.layout, 'C-001', 'open', 'a', 'b')
        with self.assertRaises(ValueError): archives.archive_run(self.repo, 'C-001', run.run_id)
        run.finish({'status': 'PASS', 'consumersCompleted': True})
        extra = run.path('scratch', 'unknown.txt'); extra.write_text('not inventoried')
        with self.assertRaises(ValueError): archives.archive_run(self.repo, 'C-001', run.run_id)
        self.assertTrue(extra.exists())
        extra.unlink()
        (run.root / 'layout.json').write_text('changed')
        with self.assertRaises(ValueError): archives.archive_run(self.repo, 'C-001', run.run_id)

    def test_restore_never_overwrites_existing_corrupt_file_or_link(self):
        run, trx = self.run_fixture(); archives.archive_run(self.repo, 'C-001', run.run_id)
        trx.write_text('owner changed this')
        with self.assertRaises(ValueError): archives.restore_run(self.repo, 'C-001', run.run_id)
        self.assertEqual(trx.read_text(), 'owner changed this')
        self.assertFalse(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])
        self.discard_fixture(run)
        outside = self.base / 'outside'; outside.mkdir()
        run.root.symlink_to(outside, target_is_directory=True)
        with self.assertRaises(ValueError): archives.restore_run(self.repo, 'C-001', run.run_id)
        self.assertEqual(list(outside.iterdir()), [])

    def test_tampered_archive_or_wrong_run_is_rejected_without_restore(self):
        run, _ = self.run_fixture(); entry = archives.archive_run(self.repo, 'C-001', run.run_id)
        package = self.layout.archive_root('C-001') / entry['path']
        other, _ = self.run_fixture()
        self.discard_fixture(other)
        assets.update_index(self.repo, 'C-001', lambda index: next(a for a in index['archives'] if a['archiveId'] == entry['archiveId']).update(runId=other.run_id))
        with self.assertRaises(ValueError): archives.restore_run(self.repo, 'C-001', other.run_id)
        self.assertFalse(other.root.exists())
        assets.update_index(self.repo, 'C-001', lambda index: next(a for a in index['archives'] if a['archiveId'] == entry['archiveId']).update(runId=run.run_id))
        self.discard_fixture(run)
        package.write_bytes(package.read_bytes() + b'corrupted')
        with self.assertRaises(ValueError): archives.restore_run(self.repo, 'C-001', run.run_id)
        self.assertFalse(run.root.exists())
        self.assertFalse(archives.verify_archive(self.repo, 'C-001', entry['archiveId'])['valid'])

    def test_zip_slip_duplicate_and_link_members_rejected(self):
        run, _ = self.run_fixture(); entry = archives.archive_run(self.repo, 'C-001', run.run_id)
        package = self.layout.archive_root('C-001') / entry['path']
        clean = package.read_bytes()
        for malicious in ('../outside', 'layout.json', 'link'):
            package.write_bytes(clean)
            with zipfile.ZipFile(package, 'a') as archive:
                if malicious == 'link':
                    item = zipfile.ZipInfo('link'); item.create_system = 3; item.external_attr = 0o120777 << 16
                    archive.writestr(item, '../outside')
                else: archive.writestr(malicious, 'unexpected')
            # Simulate a hostile but rehashed package descriptor; decoder must
            # independently enforce scope, membership and member types.
            assets.update_index(self.repo, 'C-001', lambda index: next(a for a in index['archives'] if a['archiveId'] == entry['archiveId']).update(sha256=assets.file_digest(package), bytes=package.stat().st_size))
            self.assertFalse(archives.verify_archive(self.repo, 'C-001', entry['archiveId'])['valid'])
        self.assertFalse((self.base / 'outside').exists())

    def test_packet_external_inputs_and_formal_baseline_stay_independent(self):
        run, _ = self.run_fixture(with_packet=True)
        baseline = self.repo / 'formal.json'; baseline.write_text('{}')
        assets.update_index(self.repo, 'C-001', lambda index: index['baselineRefs'].append({'logicalRoot': 'repository', 'path': 'formal.json', 'sha256': assets.file_digest(baseline)}))
        entry = archives.archive_run(self.repo, 'C-001', run.run_id)
        self.assertTrue(baseline.exists())
        self.assertTrue(any(c['reader'].startswith('uniclaw.diagnosis.packet') for c in entry['consumerChecks']))
        self.discard_fixture(run); archives.restore_run(self.repo, 'C-001', run.run_id)
        self.assertEqual(baseline.read_text(), '{}')
        self.assertFalse((run.root / 'formal.json').exists())
    def test_restore_preserves_trx_mtime_and_original_freshness_rejection(self):
        run, trx = self.run_fixture()
        old = time.time_ns() - 3600 * 10**9; os.utime(trx, ns=(old, old))
        observed = trx.stat().st_mtime_ns
        source = self.repo / 'src/UniClaw.Kernel/current.cs'; source.parent.mkdir(parents=True); source.write_text('current source')
        reader = runpy.run_path(str(TOOLS / 'scenario-coverage.py'))['freshness_violation']
        with patch.dict(reader.__globals__, {'ROOT': self.repo, 'TEST_ROOTS': (), 'SCENARIOS': self.repo / 'scenarios'}):
            self.assertIsNotNone(reader(trx))
            archives.archive_run(self.repo, 'C-001', run.run_id)
            self.discard_fixture(run)
            restored = assets.asset_path(self.repo, 'C-001', run.run_id, trx.relative_to(run.root).as_posix())
            self.assertEqual(restored.stat().st_mtime_ns, observed)
            self.assertIsNotNone(reader(restored))

    def test_legacy_archive_without_timestamps_restores_conservatively(self):
        run, trx = self.run_fixture(); entry = archives.archive_run(self.repo, 'C-001', run.run_id)
        package = self.layout.archive_root('C-001') / entry['path']
        with zipfile.ZipFile(package) as archive:
            contents = {name: archive.read(name) for name in archive.namelist()}
        header = json.loads(contents[archives.HEADER])
        header['schemaVersion'] = 'test-asset-archive.v1'; header.pop('mtimeNs')
        contents[archives.HEADER] = json.dumps(header).encode()
        with zipfile.ZipFile(package, 'w') as archive:
            for name, data in contents.items(): archive.writestr(name, data)
        def legacy(index):
            record = next(a for a in index['archives'] if a['archiveId'] == entry['archiveId'])
            record.pop('formatVersion')
            record.update(bytes=package.stat().st_size, sha256=assets.file_digest(package),
                manifestBytes=len(contents[archives.HEADER]), manifestSha256=__import__('hashlib').sha256(contents[archives.HEADER]).hexdigest())
        assets.update_index(self.repo, 'C-001', legacy)
        self.discard_fixture(run)
        restored = archives.restore_run(self.repo, 'C-001', run.run_id)
        self.assertEqual(restored['mtimeRestoration'], 'conservative-epoch-v1')
        self.assertEqual(trx.stat().st_mtime_ns, 0)
        self.assertTrue(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])
        # v2 can repackage the conservative v1 timestamp; ZIP's DOS field is
        # clamped while the exact (old) nanoseconds stay in the manifest.
        repacked = archives.archive_run(self.repo, 'C-001', run.run_id)
        self.assertEqual(repacked['formatVersion'], 2)
        self.assertTrue(archives.verify_archive(self.repo, 'C-001', repacked['archiveId'])['valid'])

    def test_archive_name_collision_cannot_delete_prior_registered_package(self):
        from types import SimpleNamespace
        run, _ = self.run_fixture()
        with patch.object(archives.uuid, 'uuid4', return_value=SimpleNamespace(hex='collision')):
            entry = archives.archive_run(self.repo, 'C-001', run.run_id)
            package = self.layout.archive_root('C-001') / entry['path']; before = package.read_bytes()
            with self.assertRaises(FileExistsError): archives.archive_run(self.repo, 'C-001', run.run_id)
        self.assertEqual(package.read_bytes(), before)
        self.assertTrue(archives.verify_archive(self.repo, 'C-001', entry['archiveId'])['valid'])
        foreign = self.layout.archive_root('C-001') / 'archive-orphan.zip'; foreign.write_bytes(b'not created by archive_run')
        with patch.object(archives.uuid, 'uuid4', return_value=SimpleNamespace(hex='orphan')):
            with self.assertRaises(FileExistsError): archives.archive_run(self.repo, 'C-001', run.run_id)
        self.assertEqual(foreign.read_bytes(), b'not created by archive_run')

    def test_external_packet_source_loss_or_restore_write_failure_never_claims_success(self):
        run, _ = self.run_fixture(with_packet=True)
        entry = archives.archive_run(self.repo, 'C-001', run.run_id)
        report = self.repo / 'protected-report.json'; report.write_text('changed by source owner')
        verification = archives.verify_archive(self.repo, 'C-001', entry['archiveId'])
        self.assertFalse(verification['valid']); self.assertTrue(verification['bytesValid'])
        self.discard_fixture(run)
        restored = archives.restore_run(self.repo, 'C-001', run.run_id)
        self.assertTrue(restored['bytesValid']); self.assertEqual(restored['consumerStatus'], 'pending')
        self.assertTrue((run.directory('raw') / 'diagnosis.packet.json').is_file())
        available = assets.validate_run(self.repo, 'C-001', run.run_id)
        self.assertFalse(available['valid']); self.assertTrue(available['bytesValid'])
        with self.assertRaises(ValueError): assets.asset_path(self.repo, 'C-001', run.run_id, 'captures/raw/diagnosis.packet.json')
        self.assertEqual(report.read_text(), 'changed by source owner')
        report.write_text('{"status":"controlled"}')
        self.assertTrue(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])
        self.assertTrue(assets.asset_path(self.repo, 'C-001', run.run_id, 'captures/raw/diagnosis.packet.json').is_file())

    def test_interrupted_partial_restore_can_retry_without_overwriting_valid_files(self):
        run, _ = self.run_fixture(); archives.archive_run(self.repo, 'C-001', run.run_id)
        self.discard_fixture(run)
        original = archives._publish_file
        count = [0]
        def fail_after_one(source, destination, expected):
            count[0] += 1
            if count[0] == 2: raise OSError('controlled restore interruption')
            return original(source, destination, expected)
        with patch.object(archives, '_publish_file', side_effect=fail_after_one):
            with self.assertRaises(OSError): archives.restore_run(self.repo, 'C-001', run.run_id)
        existing = {p: p.read_bytes() for p in run.root.rglob('*') if p.is_file()}
        self.assertTrue(existing)
        self.assertTrue(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])
        archives.restore_run(self.repo, 'C-001', run.run_id)
        self.assertTrue(all(path.read_bytes() == data for path, data in existing.items()))
        self.assertTrue(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])



if __name__ == '__main__': unittest.main(verbosity=2)
