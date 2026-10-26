"""Contract tests for the non-mutating task-retirement reference projection."""
import json
from pathlib import Path
import sys
import tempfile
import unittest
import shutil
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from test_asset_task_references import scan_task_references
import test_assets as assets
import test_asset_archives as archives


class TaskReferenceContracts(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.base = Path(temporary.name).resolve(); self.repo = self.base / 'repo'; self.repo.mkdir()
        binding = patch.object(assets, 'binding_home', return_value=self.base / 'bindings')
        binding.start(); self.addCleanup(binding.stop)
        self.root = (self.repo / '.work/TARGET/runs/r1').resolve(); self.root.mkdir(parents=True)
        self.packet = self.root / 'results/packet.json'; self.packet.parent.mkdir(); self.packet.write_text('{}')
        self.archive = (self.repo / '.archives/TARGET/r1.zip').resolve(); self.archive.parent.mkdir(parents=True); self.archive.write_bytes(b'zip')
        self.record = {'runId': 'r1', 'files': [{'path': 'results/packet.json'}], 'layout': {}}
        self.targets = {'r1': {'record': self.record, 'roots': [self.root, self.archive], 'files': [self.packet]}}

    def write(self, name, value):
        path = self.repo / name; path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(value if isinstance(value, str) else json.dumps(value)); return path

    def test_external_logical_and_physical_aliases_are_retained_but_self_history_is_not(self):
        self.write('plans/retire.md', 'test-assets:TARGET/r1/results/packet.json')
        self.write('workitems/consumer.json', {'input': {'localPath': str(self.root.parent)}})
        self.write('changes/TARGET/state.md', str(self.root))
        result = scan_task_references(self.repo, 'TARGET', self.targets)
        self.assertEqual(result['errors'], [])
        self.assertEqual(len(result['external']['r1']), 2)
        self.assertTrue(any('plans/retire.md' in ref for ref in result['external']['r1']))
        self.assertTrue(any('workitems/consumer.json' in ref for ref in result['external']['r1']))

    def test_registered_same_task_provenance_is_an_internal_edge_including_from_archive(self):
        source = {'schemaVersion': 'test-assets.v1', 'changeId': 'TARGET', 'runs': [
            {'runId': 'r2', 'layout': {}, 'files': [{'path': 'raw/provenance.json', 'bytes': 2, 'sha256': 'wrong'}]}]}
        self.write('changes/TARGET/assets.json', source)
        # The current-run document is explicitly supplied as an inventoried candidate.
        consumer = self.root.parent / 'r2/raw/provenance.json'; consumer.parent.mkdir(parents=True)
        consumer.write_text(json.dumps({'inputFiles': [{'localPath': str(self.packet)}]}))
        self.targets['r2'] = {'record': {'runId': 'r2', 'files': [{'path': 'raw/provenance.json'}]},
                              'roots': [consumer.parents[2]], 'files': [consumer]}
        result = scan_task_references(self.repo, 'TARGET', self.targets)
        self.assertEqual(result['internal'], {'r2': ['r1']})

    def test_other_change_declared_baseline_and_original_source_are_protected(self):
        self.write('changes/OTHER/assets.json', {
            'schemaVersion': 'test-assets.v1', 'changeId': 'OTHER',
            'baselineRefs': [{'localPath': str(self.packet)}],
            'runs': [{'runId': 'other', 'protectedRefs': [{'localPath': str(self.root), 'role': 'original-source'}], 'files': []}]})
        result = scan_task_references(self.repo, 'TARGET', self.targets)
        self.assertEqual(len(result['baselines']['r1']), 2)
        self.assertEqual(result['external'], {})

    def test_repository_symlink_to_a_run_directory_is_an_external_physical_consumer(self):
        (self.repo / 'live-reader').symlink_to(self.root, target_is_directory=True)
        result = scan_task_references(self.repo, 'TARGET', self.targets)
        self.assertEqual(result['errors'], [])
        self.assertTrue(any('live-reader:physical-link' in ref for ref in result['external']['r1']))

    def test_bad_registered_consumer_is_an_error_not_an_empty_projection(self):
        self.write('changes/OTHER/assets.json', {
            'schemaVersion': 'test-assets.v1', 'changeId': 'OTHER',
            'runs': [{'runId': 'other', 'root': 'repository', 'files': [{'path': 'changes/OTHER/missing.json'}]}]})
        result = scan_task_references(self.repo, 'TARGET', self.targets)
        self.assertTrue(result['errors'])

    def test_retiring_run_may_explicitly_skip_its_missing_own_document_only(self):
        missing = self.root / 'results/missing.json'
        self.targets['r1']['files'].append(missing)
        self.assertTrue(scan_task_references(self.repo, 'TARGET', self.targets)['errors'])
        self.targets['r1']['skipOwnDocuments'] = True
        self.assertEqual(scan_task_references(self.repo, 'TARGET', self.targets)['errors'], [])

    def test_archived_own_asset_run_json_is_read_without_restoring_its_work_tree(self):
        state = self.repo / 'changes/TARGET/state.md'; state.parent.mkdir(parents=True, exist_ok=True); state.write_text('active')
        layout = assets.Layout(self.repo, {'version': 1, 'parentRoot': str(self.base / 'data'), 'repositoryKey': 'r',
            'workPath': 'work', 'archivePath': 'archives', 'categoryPaths': {'raw': 'raw', 'logs': 'logs', 'results': 'results', 'scratch': 'scratch'}}, 'test')
        target = assets.AssetRun(self.repo, layout, 'TARGET', 'verification', 'a', 'b')
        packet = target.path('results', 'packet.json'); packet.write_text('{}'); target.finish({'consumersCompleted': True})
        source = assets.AssetRun(self.repo, layout, 'TARGET', 'verification', 'c', 'd')
        source.path('raw', 'provenance.json').write_text(json.dumps({'inputFiles': [{'localPath': str(packet)}]})); source.finish({'consumersCompleted': True})
        archive = archives.archive_run(self.repo, 'TARGET', source.run_id); shutil.rmtree(source.root)
        records = {item['runId']: item for item in assets.read_index(self.repo, 'TARGET')['runs']}
        targets = {target.run_id: {'record': records[target.run_id], 'roots': [target.root], 'files': [packet]},
                   source.run_id: {'record': records[source.run_id], 'roots': [source.root, layout.archive_root('TARGET') / archive['path']], 'files': []}}
        result = scan_task_references(self.repo, 'TARGET', targets)
        self.assertEqual(result['errors'], [])
        self.assertEqual(result['internal'][source.run_id], [target.run_id])
        self.assertFalse(source.root.exists())

    def test_other_change_markdown_is_scanned_but_unrelated_tmp_or_home_text_is_not(self):
        self.write('changes/OTHER/state.md', 'test-assets:TARGET/r1/results/packet.json')
        self.write('plans/noise.md', 'paths /tmp and /Users/fran are unrelated')
        result = scan_task_references(self.repo, 'TARGET', self.targets)
        self.assertTrue(any('changes/OTHER/state.md:logical' == item for item in result['external']['r1']))
        self.assertEqual(len(result['external']['r1']), 1)

    def test_declared_retired_consumer_does_not_create_a_new_reference(self):
        self.write('changes/OTHER/assets.json', {'schemaVersion': 'test-assets.v1', 'changeId': 'OTHER',
            'retiredRuns': ['old'], 'runs': [{'runId': 'old', 'root': 'repository',
            'files': [{'path': 'changes/OTHER/consumer.json'}]}]})
        self.write('changes/OTHER/consumer.json', {'input': {'localPath': str(self.packet)}})
        self.assertEqual(scan_task_references(self.repo, 'TARGET', self.targets)['baselines'], {})

    def test_own_deletion_plan_is_not_a_consumer_but_other_change_source_is(self):
        self.write('delete-plan.json', {'schemaVersion': 'test-asset-task-deletion-plan.v1', 'changeId': 'TARGET',
            'candidates': [{'path': str(self.packet)}]})
        self.write('changes/OTHER/assets.json', {'schemaVersion': 'test-assets.v1', 'changeId': 'OTHER',
            'runs': [{'runId': 'live', 'protectedRefs': [{'localPath': str(self.packet), 'role': 'original-source'}], 'files': []}]})
        result = scan_task_references(self.repo, 'TARGET', self.targets)
        self.assertEqual(result['external'], {})
        self.assertTrue(result['baselines']['r1'])


if __name__ == '__main__': unittest.main()
