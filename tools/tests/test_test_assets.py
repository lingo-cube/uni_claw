"""Public layout/run contracts; temp roots never touch repository assets."""
import importlib.machinery
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import test_assets as assets


class AssetContracts(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name).resolve()
        self.repo = self.root / 'repo'
        (self.repo / 'changes/C-001').mkdir(parents=True)
        (self.repo / 'changes/C-001/state.md').write_text('planned')
        self.env = patch.dict(os.environ, {'XDG_CONFIG_HOME': str(self.root / 'profile')})
        self.env.start(); self.addCleanup(self.env.stop)
        binding = patch.object(assets, 'binding_home', return_value=self.root / 'machine-bindings')
        binding.start(); self.addCleanup(binding.stop)
        self.config = self.root / 'layout.json'
        self.data = {'version': 1, 'parentRoot': str(self.root / 'assets'),
                     'repositoryKey': 'repo-stable', 'workPath': 'work', 'archivePath': 'archives',
                     'categoryPaths': {'raw': 'raw/captures', 'logs': 'logs', 'results': 'results', 'scratch': 'scratch'}}
        self.write_config()

    def write_config(self):
        self.config.write_text(json.dumps(self.data))

    def layout(self):
        return assets.load_layout(self.repo, str(self.config))

    def start(self, retry=None, source='a'):
        return assets.AssetRun(self.repo, self.layout(), 'C-001', 'verify quick', source, 'b', retry)

    def test_formula_nested_category_and_independent_runs(self):
        a, b = self.start(), self.start()
        self.assertNotEqual(a.root, b.root)
        self.assertEqual(a.directory('raw'), self.root / 'assets/work/repo-stable/C-001/runs' / a.run_id / 'raw/captures')
        self.assertEqual(a.layout.archive_root('C-001'), self.root / 'assets/archives/repo-stable/C-001')
        other = dict(self.data, repositoryKey='other-repo')
        self.config.write_text(json.dumps(other))
        self.assertNotEqual(self.layout().work_root, a.layout.work_root)

    def test_invalid_paths_and_collisions_fail_before_creation(self):
        for field, value in [('workPath', '../outside'), ('archivePath', 'work/nested'), ('repositoryKey', '..')]:
            with self.subTest(field=field):
                original = dict(self.data); self.data[field] = value; self.write_config()
                with self.assertRaises(ValueError): self.layout()
                self.data = original
        for value in ['/absolute', '../up', 'logs/nested', 'raw/../captures', 'layout.json/raw', 'LOGS/nested']:
            with self.subTest(value=value):
                self.data['categoryPaths']['raw'] = value; self.write_config()
                with self.assertRaises(ValueError): self.layout()
        self.assertFalse((self.root / 'assets').exists())

    def test_symlink_escape_rejected_on_write_and_read(self):
        run = self.start()
        outside = self.root / 'outside'; outside.mkdir()
        (run.directory('logs') / 'escape').symlink_to(outside, target_is_directory=True)
        with self.assertRaises(ValueError): run.path('logs', 'escape/file.txt')
        with self.assertRaises(ValueError): run.finish({'status': 'PASS'})

    def test_selection_is_whole_file_and_invalid_cli_never_falls_back(self):
        with patch.dict(os.environ, {'UNICLAW_TEST_ASSET_CONFIG': str(self.config)}):
            self.assertEqual(assets.load_layout(self.repo).digest, self.layout().digest)
            bad = self.root / 'bad.json'; bad.write_text('{}')
            with self.assertRaises(ValueError): assets.load_layout(self.repo, str(bad))

    def test_manifest_digests_consumption_and_old_layout_binding(self):
        run = self.start()
        target = run.path('results', 'test.trx'); target.write_text('<TestRun/>')
        run.finish({'status': 'PASS', 'groups': [], 'consumersCompleted': True})
        index = assets.read_index(self.repo, 'C-001')
        record = index['runs'][0]
        self.assertEqual(next(f for f in record['files'] if f['path'].endswith('test.trx'))['sha256'], assets.file_digest(target))
        self.assertNotIn(str(self.root), json.dumps(index))
        self.data['parentRoot'] = str(self.root / 'new-root'); self.write_config()
        self.assertEqual(assets.locate_run(self.repo, 'C-001', run.run_id), run.root)
        self.assertTrue(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])
        self.assertEqual(assets.asset_path(self.repo, 'C-001', run.run_id, 'results/test.trx'), target)
        target.write_text('changed')
        self.assertFalse(assets.validate_run(self.repo, 'C-001', run.run_id)['valid'])
        with self.assertRaises(ValueError): assets.asset_path(self.repo, 'C-001', run.run_id, 'results/test.trx')

    def test_retry_batch_and_cancel_preserve_previous_run(self):
        a = self.start(); a.finish({'status': 'FAIL', 'consumersCompleted': True})
        b = self.start(a.run_id); b.finish({'status': 'CANCELLED', 'consumersCompleted': False})
        index = assets.read_index(self.repo, 'C-001')
        self.assertEqual(len(index['runs']), 2)
        self.assertEqual(index['runs'][1]['retryOf'], a.run_id)
        self.assertEqual(index['runs'][0]['batchId'], index['runs'][1]['batchId'])
        self.assertEqual(index['runs'][1]['summary']['status'], 'CANCELLED')
        with self.assertRaises(ValueError): self.start(a.run_id, source='changed')
        c = self.start(source='changed'); c.finish({'status': 'PASS'})
        self.assertNotEqual(assets.read_index(self.repo, 'C-001')['runs'][2]['batchId'], index['runs'][0]['batchId'])

    def test_baseline_reference_is_independent_of_owned_run_files(self):
        baseline = self.repo / 'baseline.json'; baseline.write_text('{}')
        reference = {'logicalRoot': 'repository', 'path': 'baseline.json', 'sha256': assets.file_digest(baseline)}
        run = assets.AssetRun(self.repo, self.layout(), 'C-001', 'verify', 'a', 'b', baseline_refs=[reference])
        run.finish({'status': 'PASS', 'consumersCompleted': True})
        index = assets.read_index(self.repo, 'C-001')
        self.assertEqual(index['baselineRefs'], [reference])
        self.assertFalse(any(f['path'] == 'baseline.json' for f in index['runs'][0]['files']))
        self.assertTrue(baseline.exists())

    def test_old_run_binding_is_independent_of_profile_selection(self):
        run = self.start(); run.finish({'status': 'PASS', 'consumersCompleted': True})
        with patch.dict(os.environ, {'XDG_CONFIG_HOME': str(self.root / 'different-profile')}):
            self.assertEqual(assets.locate_run(self.repo, 'C-001', run.run_id), run.root)

    def test_first_binding_publication_is_atomic_across_changes(self):
        from concurrent.futures import ThreadPoolExecutor
        from threading import Event
        entered, release = Event(), Event()
        original_dump = assets.json.dump
        first = [True]
        def delayed_dump(value, stream, *args, **kwargs):
            if isinstance(value, dict) and value.get('parentRoot') and first[0]:
                first[0] = False
                entered.set()
                if not release.wait(5): raise TimeoutError('publication test stalled')
            return original_dump(value, stream, *args, **kwargs)
        owner = self.repo / 'changes/C-002'; owner.mkdir(); (owner / 'state.md').write_text('planned')
        layout = self.layout()
        with ThreadPoolExecutor(max_workers=2) as pool, patch.object(assets.json, 'dump', side_effect=delayed_dump):
            one = pool.submit(assets.AssetRun, self.repo, layout, 'C-001', 'verify', 'a', 'b')
            self.assertTrue(entered.wait(2))
            two = pool.submit(assets.AssetRun, self.repo, layout, 'C-002', 'verify', 'a', 'b')
            try:
                second = two.result(timeout=3)
            finally:
                release.set()
            first_run = one.result(timeout=3)
        self.assertNotEqual(first_run.root, second.root)
        self.assertEqual(assets.locate_run(self.repo, 'C-002', second.run_id), second.root)

    def test_non_metadata_summary_names_are_not_silently_omitted(self):
        run = self.start()
        target = run.path('raw', 'summary.json'); target.write_text('{}')
        run.finish({'status': 'PASS', 'consumersCompleted': True})
        self.assertEqual(assets.asset_path(self.repo, 'C-001', run.run_id, 'raw/captures/summary.json'), target)

    def test_parent_and_category_changes_affect_only_expected_paths(self):
        a = self.layout()
        self.data['parentRoot'] = str(self.root / 'elsewhere'); self.write_config()
        b = self.layout()
        self.assertEqual(a.work_root.relative_to(a.parent), b.work_root.relative_to(b.parent))
        self.data['categoryPaths']['raw'] = 'captures'; self.write_config()
        c = self.layout()
        self.assertEqual(b.categories['logs'], c.categories['logs'])
        self.assertNotEqual(b.categories['raw'], c.categories['raw'])


if __name__ == '__main__':
    unittest.main(verbosity=2)
