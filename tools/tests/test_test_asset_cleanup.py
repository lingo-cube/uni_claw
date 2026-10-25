"""Opt-in work-copy release, preserving archive facts and original readers."""
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_assets as assets
import test_asset_archives as archives
import test_asset_cleanup as cleanup


class CleanupContracts(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.base = Path(temp.name).resolve(); self.repo = self.base / 'repo'
        owner = self.repo / 'changes/C-001'; owner.mkdir(parents=True)
        (owner / 'state.md').write_text('implemented')
        binding = patch.object(assets, 'binding_home', return_value=self.base / 'bindings')
        binding.start(); self.addCleanup(binding.stop)
        self.layout = assets.Layout(self.repo, {'version': 1, 'parentRoot': str(self.base / 'data'),
            'repositoryKey': 'r', 'workPath': 'work', 'archivePath': 'archives',
            'categoryPaths': dict(raw='raw', logs='logs', results='results', scratch='scratch')}, 'test')

    def fixture(self, opt_in=True, complete=True):
        run = assets.AssetRun(self.repo, self.layout, 'C-001', 'verification', 'a', 'b',
            work_release_reason='Disposable copy; reads use path-ref' if opt_in else None)
        run.path('raw', 'case.trx').write_text('<TestRun><UnitTestResult testName="copy" outcome="Failed"/></TestRun>')
        run.finish({'status': 'COPY', 'consumersCompleted': complete, 'sourceStable': True})
        package = archives.archive_run(self.repo, 'C-001', run.run_id)
        return run, package

    def release(self, run):
        return cleanup.release_work(self.repo, 'C-001', run.run_id)

    def test_release_validate_and_original_path_restore_preserve_facts(self):
        run, package = self.fixture()
        original = assets.read_index(self.repo, 'C-001')['runs'][0]
        timestamps = {item['path']: (run.root / item['path']).stat().st_mtime_ns for item in original['files']}
        result = self.release(run)
        self.assertTrue(result['completed']); self.assertEqual(len(result['deleted']), 4)
        self.assertFalse(run.root.exists())
        self.assertTrue(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])
        self.assertFalse(run.root.exists())
        self.assertTrue(archives.verify_archive(self.repo, 'C-001', package['archiveId'])['valid'])
        path = assets.asset_path(self.repo, 'C-001', run.run_id, 'raw/case.trx')
        self.assertTrue(path.exists())
        self.assertEqual(assets.read_index(self.repo, 'C-001')['runs'][0], original)
        self.assertEqual({name: (run.root / name).stat().st_mtime_ns for name in timestamps}, timestamps)

    def test_old_runs_pending_consumers_and_missing_archive_are_kept(self):
        for opt_in, complete in [(False, True), (True, False)]:
            run, _ = self.fixture(opt_in, complete)
            result = self.release(run)
            self.assertFalse(result['completed']); self.assertFalse(result['deleted'])
            self.assertTrue((run.root / 'raw/case.trx').exists())
        run, _ = self.fixture()
        assets.update_index(self.repo, 'C-001', lambda index: index.update(archives=[]))
        self.assertFalse(self.release(run)['completed'])

    def test_plan_workitem_and_other_index_raw_references_block(self):
        run, _ = self.fixture()
        for name, text in [('plans/next.md', str(run.root / 'raw/case.trx')),
                           ('workitems/next.json', json.dumps({'runId': run.run_id})),
                           ('changes/OTHER/assets.json', json.dumps({'source': run.run_id}))]:
            target = self.repo / name; target.parent.mkdir(parents=True, exist_ok=True); target.write_text(text)
            result = self.release(run)
            self.assertFalse(result['completed']); self.assertFalse(result['deleted'])
            target.unlink()

    def test_only_valid_logical_file_references_allow_release(self):
        run, _ = self.fixture()
        doc = self.repo / 'plan.md'
        doc.write_text(f'[input](test-assets:C-001/{run.run_id}/raw/case.trx)')
        self.assertTrue(self.release(run)['completed'])
        assets.asset_path(self.repo, 'C-001', run.run_id, 'raw/case.trx')
        doc.write_text(f'test-assets:C-001/{run.run_id}/raw/case.trx/unknown')
        self.assertFalse(self.release(run)['completed'])
        for suffix in ('#bad', '?bad', '@bad'):
            doc.write_text(f'test-assets:C-001/{run.run_id}/raw/case.trx{suffix}')
            self.assertFalse(self.release(run)['completed'])

    def test_changed_unknown_symlink_and_hardlinked_files_preserved(self):
        for variant in ('changed', 'unknown', 'symlink', 'hardlink', 'fifo'):
            run, _ = self.fixture(); target = run.root / 'raw/case.trx'
            if variant == 'changed': target.write_text('changed')
            elif variant == 'unknown': (run.root / 'scratch/unknown').write_text('foreign')
            elif variant == 'symlink': target.unlink(); target.symlink_to(self.repo / 'changes/C-001/state.md')
            elif variant == 'hardlink': os.link(target, self.base / ('external-' + run.run_id))
            else: os.mkfifo(run.root / 'scratch/unknown-pipe')
            self.assertFalse(self.release(run)['completed'])
            self.assertTrue(target.exists()); self.assertTrue((run.root / 'layout.json').exists())

    def test_corrupt_archive_blocks(self):
        run, package = self.fixture()
        (self.layout.archive_root('C-001') / package['path']).write_bytes(b'bad backup')
        self.assertFalse(self.release(run)['completed']); self.assertTrue(run.root.exists())

    def test_partial_unlink_failure_records_actual_deletion_and_retry(self):
        run, _ = self.fixture(); unlink = Path.unlink; calls = []
        def fail_second(path, *args, **kwargs):
            if path.is_relative_to(run.root):
                calls.append(path)
                if len(calls) == 2: raise PermissionError('controlled unlink failure')
            return unlink(path, *args, **kwargs)
        with patch.object(Path, 'unlink', fail_second): result = self.release(run)
        self.assertFalse(result['completed']); self.assertEqual(len(result['deleted']), 1)
        self.assertTrue(result['pending'])
        retry = self.release(run)
        self.assertTrue(retry['completed']); self.assertEqual(len(retry['alreadyAbsent']), 1)
        again = self.release(run)
        self.assertTrue(again['completed']); self.assertEqual(again['deletedBytes'], 0)
        self.assertEqual(assets.read_index(self.repo, 'C-001')['workReleases'][0], result)

    def test_archive_policy_cannot_be_changed_after_packaging(self):
        run, _ = self.fixture()
        def drift(index): index['runs'][0]['workReleasePolicy']['reason'] = 'rewritten'
        assets.update_index(self.repo, 'C-001', drift)
        self.assertFalse(self.release(run)['completed']); self.assertTrue(run.root.exists())

    def test_baseline_directory_without_run_id_is_protected(self):
        run, _ = self.fixture()
        target = self.repo / 'changes/OTHER/assets.json'; target.parent.mkdir()
        target.write_text(json.dumps({'baselineRefs': [{'path': str(run.root.parent), 'logicalRoot': 'external-0'}]}))
        self.assertFalse(self.release(run)['completed']); self.assertTrue(run.root.exists())

    def test_new_reference_during_release_stops_remaining_unlinks(self):
        run, _ = self.fixture(); unlink = Path.unlink
        def add_reference(path, *args, **kwargs):
            result = unlink(path, *args, **kwargs)
            if path.is_relative_to(run.root): (self.repo / 'plan.md').write_text(str(run.root))
            return result
        with patch.object(Path, 'unlink', add_reference): result = self.release(run)
        self.assertFalse(result['completed']); self.assertEqual(len(result['deleted']), 1)
        self.assertTrue((run.root / 'raw/case.trx').exists())

    def test_missing_reference_scanner_retains_work_and_records_pending(self):
        run, _ = self.fixture()
        with patch.object(cleanup.subprocess, 'run', side_effect=FileNotFoundError('rg unavailable')):
            result = self.release(run)
        self.assertFalse(result['completed']); self.assertFalse(result['deleted']); self.assertTrue(run.root.exists())

    def test_repository_symlink_is_an_effective_consumer_dependency(self):
        run, _ = self.fixture()
        (self.repo / 'consumer-link').symlink_to(run.root / 'raw/case.trx')
        result = self.release(run)
        self.assertFalse(result['completed']); self.assertFalse(result['deleted'])
        self.assertTrue((self.repo / 'consumer-link').exists())

    def test_release_policy_is_only_available_to_new_verification_runs(self):
        for purpose in ('diagnosis', 'replay'):
            with self.assertRaises(ValueError):
                assets.AssetRun(self.repo, self.layout, 'C-001', purpose, 'a', 'b', work_release_reason='no consumers')
        self.assertFalse(assets.read_index(self.repo, 'C-001')['runs'])

    def test_other_run_source_via_external_symlink_is_protected(self):
        run, _ = self.fixture()
        alias = self.base / 'external-alias'; alias.symlink_to(run.root, target_is_directory=True)
        consumer = assets.AssetRun(self.repo, self.layout, 'C-001', 'consumer', 'c', 'd')
        consumer.path('raw', 'provenance.json').write_text(json.dumps({'inputFiles': [
            {'localPath': str(alias / 'raw/case.trx'), 'path': 'case.trx', 'logicalRoot': 'external-0'}]}))
        consumer.finish({'consumersCompleted': False})
        result = self.release(run)
        self.assertFalse(result['completed']); self.assertFalse(result['deleted'])
        self.assertTrue((run.root / 'raw/case.trx').exists())

    def test_repository_local_manifests_do_not_require_layout_migration(self):
        run, _ = self.fixture()
        owner = self.repo / 'changes/OLD'; owner.mkdir()
        doc = owner / 'input.json'; doc.write_text('{}')
        manifest = owner / 'assets.json'
        for declared, name in [('repository', 'changes/OLD/input.json'),
                               ({'kind': 'repository', 'path': 'changes/OLD'}, 'input.json')]:
            manifest.write_text(json.dumps({'runs': [{'runId': 'legacy', 'root': declared, 'files': [{'path': name}]}]}))
            result = self.release(run)
            self.assertTrue(result['completed'])
            assets.asset_path(self.repo, 'C-001', run.run_id, 'raw/case.trx')
        alias = self.base / 'old-source-alias'; alias.symlink_to(run.root, target_is_directory=True)
        doc.write_text(json.dumps({'source': {'path': str(alias / 'raw/case.trx')}}))
        self.assertFalse(self.release(run)['completed'])

    def test_another_run_external_provenance_is_a_source_dependency(self):
        run, _ = self.fixture()
        consumer = assets.AssetRun(self.repo, self.layout, 'C-001', 'consumer', 'c', 'd')
        consumer.path('raw', 'provenance.json').write_text(json.dumps({'inputFiles': [
            {'localPath': str(run.root / 'raw/case.trx'), 'path': 'case.trx', 'logicalRoot': 'external-0'}]}))
        consumer.finish({'consumersCompleted': False})
        result = self.release(run)
        self.assertFalse(result['completed']); self.assertFalse(result['deleted'])
        archives.archive_run(self.repo, 'C-001', consumer.run_id)
        shutil.rmtree(consumer.root)
        result = self.release(run)
        self.assertFalse(result['completed']); self.assertFalse(result['deleted'])
        self.assertFalse(consumer.root.exists())  # Read archived dependencies without restoring.


if __name__ == '__main__': unittest.main()
