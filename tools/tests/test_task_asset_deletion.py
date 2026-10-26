"""Explicit task asset retirement through the public CLI and planning seam."""
import contextlib
import io
import json
from pathlib import Path
import runpy
import sys
import tempfile
import unittest
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import test_assets as assets
import test_asset_archives as archives


class TaskDeletionContracts(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(); self.addCleanup(temp.cleanup)
        self.base = Path(temp.name).resolve(); self.repo = self.base / 'repo'
        owner = self.repo / 'changes/C-001'; owner.mkdir(parents=True)
        (owner / 'state.md').write_text('lifecycle_state: closed\n')
        binding = patch.object(assets, 'binding_home', return_value=self.base / 'bindings')
        binding.start(); self.addCleanup(binding.stop)
        self.layout = assets.Layout(self.repo, {'version': 1, 'parentRoot': str(self.base / 'data'),
            'repositoryKey': 'r', 'workPath': 'staging/work', 'archivePath': 'retained/packages',
            'categoryPaths': dict(raw='payload/raw', logs='diagnostics/logs', results='output/results', scratch='temporary')}, 'test')

    def fixture(self, complete=True):
        run = assets.AssetRun(self.repo, self.layout, 'C-001', 'verification', 'a', 'b')
        run.path('raw', 'case.trx').write_text('<TestRun><UnitTestResult testName="retire" outcome="Failed"/></TestRun>')
        if complete: run.finish({'status': 'COPY', 'consumersCompleted': True, 'sourceStable': True})
        return run

    def cli(self, args):
        namespace = runpy.run_path(str(TOOLS / 'test-assets'), run_name='test_asset_cli')
        namespace['legacy_main'].__globals__['ROOT'] = self.repo
        out, error = io.StringIO(), io.StringIO()
        with patch.object(sys, 'argv', ['test-assets', *args]), contextlib.redirect_stdout(out), contextlib.redirect_stderr(error):
            try: code = namespace['main']()
            except SystemExit as exit: code = exit.code
        return code, out.getvalue(), error.getvalue()

    def test_cli_plan_inventories_without_deleting_or_rebinding(self):
        run = self.fixture()
        original = (self.repo / 'changes/C-001/assets.json').read_bytes()
        code, text, error = self.cli(['plan-delete-task', 'C-001'])
        self.assertEqual(code, 0, error)
        plan = json.loads(text)
        self.assertEqual(plan['changeId'], 'C-001')
        self.assertEqual(len(plan['planId']), 64)
        self.assertIn(str(run.root / 'payload/raw/case.trx'), {item['path'] for item in plan['candidates']})
        self.assertTrue((run.root / 'payload/raw/case.trx').exists())
        self.assertEqual((self.repo / 'changes/C-001/assets.json').read_bytes(), original)


    def test_external_logical_reference_retains_work_and_archive(self):
        from test_asset_task_cleanup import plan_task_delete
        run = self.fixture(); package = archives.archive_run(self.repo, 'C-001', run.run_id)
        other = self.repo / 'changes/OTHER'; other.mkdir()
        (other / 'state.md').write_text('input: test-assets:C-001/' + run.run_id + '/payload/raw/case.trx')
        plan = plan_task_delete(self.repo, 'C-001')
        self.assertNotIn(run.run_id, {item['runId'] for item in plan['candidates']})
        self.assertTrue(any(item.get('runId') == run.run_id and item.get('references') for item in plan['kept']))
        self.assertTrue((run.root / 'payload/raw/case.trx').exists())
        self.assertTrue((self.layout.archive_root('C-001') / package['path']).exists())


    def test_cli_deletes_only_planned_assets_and_retains_audit_and_original_facts(self):
        run = self.fixture(); package = archives.archive_run(self.repo, 'C-001', run.run_id)
        original = assets.read_index(self.repo, 'C-001')['runs'][0]
        package_path = self.layout.archive_root('C-001') / package['path']
        plan_file = self.repo / 'delete-plan.json'
        code, _, error = self.cli(['plan-delete-task', 'C-001', '--out', str(plan_file)])
        self.assertEqual(code, 0, error)
        plan = json.loads(plan_file.read_text())
        expected_bytes = sum(item['bytes'] for item in plan['candidates'])
        code, text, error = self.cli(['delete-task-assets', 'C-001', '--plan', str(plan_file)])
        self.assertEqual(code, 0, error + text)
        receipt = json.loads(text)
        self.assertTrue(receipt['completed'])
        self.assertEqual(receipt['deletedBytes'], expected_bytes)
        self.assertFalse(run.root.exists()); self.assertFalse(package_path.exists())
        self.assertTrue((self.repo / 'changes/C-001/state.md').exists())
        index = assets.read_index(self.repo, 'C-001')
        self.assertEqual(index['runs'][0], original)
        self.assertTrue(all(not Path(item['path']).is_absolute() for item in index['taskDeletions'][-1]['deleted']))
        self.assertTrue(all('logicalRoot' in item for item in index['taskDeletions'][-1]['deleted']))
        self.assertEqual(index['taskDeletions'][-1]['operationId'], receipt['operationId'])
        with self.assertRaisesRegex(ValueError, 'retir'):
            assets.asset_path(self.repo, 'C-001', run.run_id, 'payload/raw/case.trx')
        self.assertFalse(run.root.exists())
        with self.assertRaisesRegex(ValueError, 'retir'):
            assets.AssetRun(self.repo, self.layout, 'C-001', 'verification', 'new', 'new')


    def test_active_run_is_inventoried_and_protects_the_unfinished_task(self):
        from test_asset_task_cleanup import plan_task_delete
        finished = self.fixture(); active = self.fixture(complete=False)
        plan = plan_task_delete(self.repo, 'C-001')
        active_plan = next(item for item in plan['runs'] if item['runId'] == active.run_id)
        self.assertIn(str(active.root), active_plan['roots'])
        self.assertTrue(any(item['path'] == str(active.root / 'payload/raw/case.trx') for item in plan['kept']))
        self.assertEqual(plan['candidates'], [])
        self.assertTrue(plan['pending'])
        self.assertTrue((finished.root / 'payload/raw/case.trx').exists())


    def test_partial_failure_is_persisted_and_fresh_plan_retries_without_double_counting(self):
        from test_asset_task_cleanup import plan_task_delete, delete_task_assets
        run=self.fixture();package=archives.archive_run(self.repo,'C-001',run.run_id)
        archive=self.layout.archive_root('C-001')/package['path']
        plan=plan_task_delete(self.repo,'C-001')
        work_bytes=sum(item['bytes'] for item in plan['candidates'] if item['location']=='work')
        unlink=Path.unlink
        def fail_archive(path,*args,**kwargs):
            if path==archive:raise PermissionError('controlled archive removal failure')
            return unlink(path,*args,**kwargs)
        with patch.object(Path,'unlink',fail_archive):first=delete_task_assets(self.repo,'C-001',plan)
        self.assertFalse(first['completed']);self.assertTrue(first['pending'])
        self.assertEqual(first['deletedBytes'],work_bytes)
        self.assertTrue(archive.exists())
        self.assertFalse((run.root/'payload/raw/case.trx').exists())
        with self.assertRaisesRegex(ValueError,'retir'):assets.asset_path(self.repo,'C-001',run.run_id,'payload/raw/case.trx')
        second=delete_task_assets(self.repo,'C-001',plan_task_delete(self.repo,'C-001'))
        self.assertTrue(second['completed'],second)
        self.assertEqual(second['deletedBytes'],package['bytes'])
        self.assertEqual(len(second['alreadyAbsent']),4)
        self.assertFalse(run.root.exists());self.assertFalse(archive.exists())
        self.assertEqual(len(assets.read_index(self.repo,'C-001')['taskDeletions']),2)


    def test_active_external_consumer_provenance_protects_its_original_input(self):
        from test_asset_task_cleanup import plan_task_delete
        target=self.fixture();owner=self.repo/'changes/OTHER';owner.mkdir();(owner/'state.md').write_text('implemented')
        consumer=assets.AssetRun(self.repo,self.layout,'OTHER','verification','c','d')
        consumer.path('raw','provenance.json').write_text(json.dumps({'inputFiles':[{'localPath':str(target.root/'payload/raw/case.trx')}]}))
        plan=plan_task_delete(self.repo,'C-001')
        self.assertEqual(plan['candidates'],[])
        self.assertTrue(any(item.get('runId')==target.run_id for item in plan['kept']))


    def test_new_reference_or_changed_file_rejects_the_old_plan_without_deletion(self):
        from test_asset_task_cleanup import plan_task_delete, delete_task_assets
        run=self.fixture();plan=plan_task_delete(self.repo,'C-001')
        path=self.repo/'consumer.md';path.write_text('test-assets:C-001/'+run.run_id+'/payload/raw/case.trx')
        receipt=delete_task_assets(self.repo,'C-001',plan)
        self.assertFalse(receipt['completed']);self.assertEqual(receipt['deleted'],[])
        self.assertIn('stale',receipt['pending'][0]['reason'])
        self.assertNotIn('assetRetirement',assets.read_index(self.repo,'C-001'))
        path.unlink();plan=plan_task_delete(self.repo,'C-001')
        (run.root/'payload/raw/case.trx').write_text('changed')
        receipt=delete_task_assets(self.repo,'C-001',plan)
        self.assertFalse(receipt['completed']);self.assertEqual(receipt['deleted'],[])
        self.assertEqual((run.root/'payload/raw/case.trx').read_text(),'changed')

    def test_retained_consumer_keeps_its_same_task_input_and_independent_run_is_deleted(self):
        from test_asset_task_cleanup import plan_task_delete, delete_task_assets
        source=self.fixture();consumer=self.fixture(complete=False)
        consumer.path('raw','provenance.json').write_text(json.dumps({'inputFiles':[{'localPath':str(source.root/'payload/raw/case.trx')}]}))
        consumer.finish({'consumersCompleted':True,'sourceStable':True})
        independent=self.fixture()
        (self.repo/'consumer.md').write_text('test-assets:C-001/'+consumer.run_id+'/payload/raw/case.trx')
        plan=plan_task_delete(self.repo,'C-001')
        self.assertEqual({item['runId'] for item in plan['candidates']},{independent.run_id})
        self.assertIn(source.run_id,plan['internal'][consumer.run_id])
        receipt=delete_task_assets(self.repo,'C-001',plan)
        self.assertTrue(receipt['completed'],receipt)
        self.assertTrue((source.root/'payload/raw/case.trx').exists())
        self.assertTrue((consumer.root/'payload/raw/case.trx').exists())
        self.assertFalse(independent.root.exists())
        self.assertTrue(assets.asset_path(self.repo,'C-001',source.run_id,'payload/raw/case.trx').exists())

    def test_archive_operation_and_task_retirement_share_an_outer_gate(self):
        import multiprocessing
        from test_asset_task_cleanup import plan_task_delete, delete_task_assets
        run=self.fixture();plan=plan_task_delete(self.repo,'C-001')
        ctx=multiprocessing.get_context('fork');entered=ctx.Event();release=ctx.Event()
        original=archives.consumer_checks
        def blocked(*args):
            entered.set()
            if not release.wait(5):raise RuntimeError('controlled gate timeout')
            return original(*args)
        def archive():
            with patch.object(archives,'consumer_checks',blocked):archives.archive_run(self.repo,'C-001',run.run_id)
        child=ctx.Process(target=archive);child.start()
        try:
            self.assertTrue(entered.wait(4))
            receipt=delete_task_assets(self.repo,'C-001',plan)
            self.assertFalse(receipt['completed']);self.assertEqual(receipt['deleted'],[])
            self.assertIn('busy',receipt['pending'][0]['reason'])
            self.assertTrue((run.root/'payload/raw/case.trx').exists())
        finally:
            release.set();child.join(5)
            if child.is_alive():child.terminate();child.join()
        self.assertEqual(child.exitcode,0)


    def test_each_old_binding_and_nested_category_is_used_after_profile_changes(self):
        from test_asset_task_cleanup import plan_task_delete, delete_task_assets
        first=self.fixture()
        second_layout=assets.Layout(self.repo,{'version':1,'parentRoot':str(self.base/'second-data'),'repositoryKey':'r',
            'workPath':'work-two','archivePath':'archive-two','categoryPaths':dict(raw='r/input',logs='l',results='o/results',scratch='s')},'test')
        second=assets.AssetRun(self.repo,second_layout,'C-001','verification','c','d')
        second.path('raw','case.trx').write_text('<TestRun><UnitTestResult testName="second" outcome="Passed"/></TestRun>')
        second.finish({'consumersCompleted':True,'sourceStable':True})
        with patch.object(assets,'load_layout',side_effect=AssertionError('current profile must not locate old assets')):
            plan=plan_task_delete(self.repo,'C-001')
            self.assertIn(str(first.root/'payload/raw/case.trx'),{item['path'] for item in plan['candidates']})
            self.assertIn(str(second.root/'r/input/case.trx'),{item['path'] for item in plan['candidates']})
            receipt=delete_task_assets(self.repo,'C-001',plan)
        self.assertTrue(receipt['completed'],receipt)
        self.assertEqual({item['configDigest'] for item in receipt['deleted']},{self.layout.digest,second_layout.digest})
        self.assertFalse(first.root.exists());self.assertFalse(second.root.exists())

    def test_baseline_keeps_the_whole_run_and_unknown_task_evidence_is_never_deleted(self):
        from test_asset_task_cleanup import plan_task_delete, delete_task_assets
        run=self.fixture();original=(run.root/'payload/raw/case.trx').read_bytes()
        assets.update_index(self.repo,'C-001',lambda index:index['baselineRefs'].append({'localPath':str(run.root/'payload/raw/case.trx'),'role':'controlled-independent-baseline'}))
        evidence=self.repo/'changes/C-001/evidence/unmanaged.bin';evidence.parent.mkdir();evidence.write_bytes(b'not-owned-by-run')
        receipt=delete_task_assets(self.repo,'C-001',plan_task_delete(self.repo,'C-001'))
        self.assertTrue(receipt['completed'],receipt);self.assertEqual(receipt['deleted'],[])
        self.assertEqual((run.root/'payload/raw/case.trx').read_bytes(),original)
        self.assertEqual(evidence.read_bytes(),b'not-owned-by-run')
        self.assertTrue(any(item.get('path')=='changes/C-001/evidence/unmanaged.bin' for item in receipt['kept']))

    def test_unknown_changed_and_linked_run_material_is_preserved(self):
        import os
        from test_asset_task_cleanup import plan_task_delete
        for variant in ('unknown','changed','symlink','hardlink'):
            run=self.fixture();target=run.root/'payload/raw/case.trx'
            if variant=='unknown':run.path('scratch','foreign').write_text('unregistered')
            elif variant=='changed':target.write_text('changed')
            elif variant=='symlink':
                target.unlink();foreign=self.base/('foreign-'+run.run_id);foreign.write_text('foreign');target.symlink_to(foreign)
            else:os.link(target,self.base/('alias-'+run.run_id))
            plan=plan_task_delete(self.repo,'C-001')
            self.assertNotIn(run.run_id,{item['runId'] for item in plan['candidates']})
            self.assertTrue(any(item.get('runId')==run.run_id for item in plan['pending']))
            self.assertTrue(target.exists())


    def test_partly_retiring_external_consumer_is_still_a_protected_dependency(self):
        from test_asset_task_cleanup import plan_task_delete
        target=self.fixture();owner=self.repo/'changes/OTHER';owner.mkdir();(owner/'state.md').write_text('implemented')
        consumer=assets.AssetRun(self.repo,self.layout,'OTHER','verification','c','d')
        consumer.path('raw','provenance.json').write_text(json.dumps({'inputFiles':[{'localPath':str(target.root/'payload/raw/case.trx')}]}))
        consumer.finish({'consumersCompleted':True,'sourceStable':True})
        assets.update_index(self.repo,'OTHER',lambda index:index.update(retiredRuns={consumer.run_id:{'status':'retiring'}}))
        plan=plan_task_delete(self.repo,'C-001')
        self.assertEqual(plan['candidates'],[])
        self.assertTrue((target.root/'payload/raw/case.trx').exists())


    def test_external_run_logical_input_and_workitem_bare_identity_are_both_protected(self):
        from test_asset_task_cleanup import plan_task_delete
        target=self.fixture();owner=self.repo/'changes/OTHER';owner.mkdir();(owner/'state.md').write_text('implemented')
        consumer=assets.AssetRun(self.repo,self.layout,'OTHER','verification','c','d')
        consumer.path('raw','provenance.json').write_text(json.dumps({'source':'test-assets:C-001/'+target.run_id+'/payload/raw/case.trx'}))
        consumer.finish({'consumersCompleted':True,'sourceStable':True})
        self.assertEqual(plan_task_delete(self.repo,'C-001')['candidates'],[])
        # A second independent source has no URI/path, only an explicit Run identity.
        other=self.fixture();work=self.repo/'workitems';work.mkdir()
        (work/'consumer.json').write_text(json.dumps({'sourceRunId':other.run_id}))
        self.assertNotIn(other.run_id,{item['runId'] for item in plan_task_delete(self.repo,'C-001')['candidates']})


    def test_retained_current_plan_inside_the_task_protects_its_input(self):
        from test_asset_task_cleanup import plan_task_delete
        run=self.fixture();(self.repo/'changes/C-001/plan.md').write_text('Next input: test-assets:C-001/'+run.run_id+'/payload/raw/case.trx')
        plan=plan_task_delete(self.repo,'C-001')
        self.assertEqual(plan['candidates'],[])
        self.assertTrue(any('plan.md' in ref for ref in plan['external'][run.run_id]))

    def test_unregistered_file_appearing_during_unlink_remains_pending(self):
        from test_asset_task_cleanup import plan_task_delete,delete_task_assets
        run=self.fixture();plan=plan_task_delete(self.repo,'C-001');unlink=Path.unlink
        unknown=run.root/'temporary/unregistered'
        injected=[]
        def concurrent_file(path,*args,**kwargs):
            if str(path).startswith(str(run.root)) and not injected:
                unknown.write_text('unregistered-writer');injected.append(True)
            return unlink(path,*args,**kwargs)
        with patch.object(Path,'unlink',concurrent_file):receipt=delete_task_assets(self.repo,'C-001',plan)
        self.assertFalse(receipt['completed'])
        self.assertTrue(receipt['pending']);self.assertEqual(unknown.read_text(),'unregistered-writer')
        self.assertNotEqual(assets.read_index(self.repo,'C-001')['retiredRuns'][run.run_id]['status'],'retired')

    def test_pending_audit_projects_repository_paths_in_diagnostics(self):
        from test_asset_task_cleanup import plan_task_delete,delete_task_assets
        record={'runId':'missing-json','root':'repository','files':[{'path':'changes/C-001/evidence/missing.json','bytes':2,'sha256':'0'*64,'kind':'raw','retention':'task'}],
            'summary':{'consumersCompleted':True,'sourceStable':True}}
        assets.update_index(self.repo,'C-001',lambda index:index['runs'].append(record))
        receipt=delete_task_assets(self.repo,'C-001',plan_task_delete(self.repo,'C-001'))
        self.assertTrue(receipt['pending'])
        audit=assets.read_index(self.repo,'C-001')['taskDeletions']
        self.assertNotIn(str(self.repo),json.dumps(audit))
        self.assertNotIn(str(self.repo),json.dumps(receipt))
        self.assertIn('missing.json',json.dumps(audit))

    def test_repository_owned_legacy_asset_relative_document_reference_is_protected(self):
        from test_asset_task_cleanup import plan_task_delete
        owner=self.repo/'changes/C-001';file=owner/'evidence/owned.bin';file.parent.mkdir();file.write_bytes(b'legacy-owned')
        record={'runId':'legacy-run','root':'repository','files':[{'path':'changes/C-001/evidence/owned.bin','bytes':12,'sha256':assets.file_digest(file),'kind':'raw','retention':'task'}],
            'summary':{'consumersCompleted':True,'sourceStable':True}}
        assets.update_index(self.repo,'C-001',lambda index:index['runs'].append(record))
        other=self.repo/'changes/OTHER';other.mkdir();(other/'state.md').write_text('[input](../C-001/evidence/owned.bin)')
        plan=plan_task_delete(self.repo,'C-001')
        self.assertEqual(plan['candidates'],[])
        self.assertEqual(file.read_bytes(),b'legacy-owned')


if __name__ == '__main__': unittest.main()
