#!/usr/bin/env python3
"""Actual CLI deletion of a newly created, owned fixture task, with protected peers."""
import hashlib,json,os,shutil,subprocess,sys,atexit
from pathlib import Path
from unittest.mock import patch
R=Path(__file__).resolve().parents[4];H=Path(__file__).resolve().parent
sys.path.insert(0,str(R/'tools'))
import test_assets as assets
import test_asset_archives as archives
label=sys.argv[1]
if (H/(label+'-result.json')).exists():raise FileExistsError(label)
files=['test-assets','test_assets.py','test_asset_operations.py','test_asset_archives.py','test_asset_cleanup.py','test_asset_task_cleanup.py','test_asset_task_references.py','scenario-coverage.py','scenario_certify.py','evolution-signal.py']
source=[{'path':'tools/'+name,'sha256':assets.file_digest(R/'tools'/name)} for name in files]
run=assets.AssetRun(R,assets.load_layout(R),'TEST-ASSET-AUTOMATION-001','verification',assets.digest(source),assets.digest(['controlled CLI retirement',label]))
finalized=False
assets.atomic_json(run.path('results','source-binding.json'),{'sourceFiles':source,'qualification':'real subprocess CLI on a newly created isolated task'})
def record_failure():
 if finalized:return
 summary={'status':'harness-failed','consumersCompleted':True,'sourceStable':all(assets.file_digest(R/i['path'])==i['sha256'] for i in source),'controlledFixture':True,'realHistoricalTasksDeleted':False,'qualification':'failed assertions/diagnostics retained; not accepted as product verification'}
 run.finish(summary);assets.atomic_json(H/(label+'-result.json'),{'runId':run.run_id,'runRoot':str(run.root),**summary})
atexit.register(record_failure)
root=run.directory('raw')/'controlled';repo=root/'repo';(repo/'tools').mkdir(parents=True)
for name in files:shutil.copyfile(R/'tools'/name,repo/'tools'/name)
(repo/'schemas').mkdir()
for name in ['diagnosis-packet.schema.json','evolution-signal.schema.json']:shutil.copyfile(R/'schemas'/name,repo/'schemas'/name)
owner=repo/'changes/DELETE-PROOF';owner.mkdir(parents=True);(owner/'state.md').write_text('lifecycle_state: closed\n')
other=repo/'changes/OTHER';other.mkdir();(repo/'fixtures').mkdir()
baseline=repo/'fixtures/independent.json';baseline.write_text('{"expected":"independent"}\n')
unknown=owner/'evidence/unmanaged.json';unknown.parent.mkdir();unknown.write_text('{"keep":"unknown-owner"}\n')
bindings=root/'bindings'
layout=assets.Layout(repo,{'version':1,'parentRoot':str(root/'asset-parent'),'repositoryKey':'proof',
 'workPath':'work/nested','archivePath':'archive/nested','categoryPaths':dict(raw='payload/raw',logs='diagnostics/logs',results='out/results',scratch='temporary')},'controlled-proof')
with patch.object(assets,'binding_home',return_value=bindings):
 def fixture(value):
  item=assets.AssetRun(repo,layout,'DELETE-PROOF','verification',value,value)
  item.path('raw','case.trx').write_text('<TestRun><UnitTestResult testName="controlled" outcome="Failed"/></TestRun>')
  item.finish({'consumersCompleted':True,'sourceStable':True})
  package=archives.archive_run(repo,'DELETE-PROOF',item.run_id)
  return item,layout.archive_root('DELETE-PROOF')/package['path']
 disposable,disposable_zip=fixture('disposable');protected,protected_zip=fixture('retained')
 assets.update_index(repo,'DELETE-PROOF',lambda index:index['baselineRefs'].append({'logicalRoot':'repository','path':'fixtures/independent.json','sha256':assets.file_digest(baseline)}))
 original=assets.read_index(repo,'DELETE-PROOF')['runs']
(other/'state.md').write_text('Required input: test-assets:DELETE-PROOF/'+protected.run_id+'/payload/raw/case.trx\n')
kept=[baseline,unknown,protected_zip,*[p for p in protected.root.rglob('*') if p.is_file()]]
kept_hashes={str(p.relative_to(root)):assets.file_digest(p) for p in kept}
expected_bytes=disposable_zip.stat().st_size+sum(p.stat().st_size for p in disposable.root.rglob('*') if p.is_file())
bootstrap="import sys,runpy;from pathlib import Path;sys.path.insert(0,"+repr(str(repo/'tools'))+");import test_assets;test_assets.binding_home=lambda:Path("+repr(str(bindings))+");sys.argv=['test-assets']+sys.argv[1:];runpy.run_path("+repr(str(repo/'tools/test-assets'))+",run_name='__main__')"
env=os.environ.copy();env['UNICLAW_TEST_ASSET_CONFIG']=str(root/'absent-current-profile.json')
def cli(name,args):
 result=subprocess.run([sys.executable,'-c',bootstrap,*args],cwd=repo,env=env,capture_output=True,text=True)
 run.path('logs',name+'.stdout.json').write_text(result.stdout);run.path('logs',name+'.stderr.txt').write_text(result.stderr)
 return result
plan_file=repo/'delete-plan.json';planned=cli('plan',['plan-delete-task','DELETE-PROOF','--out',str(plan_file)])
assert planned.returncode==0,planned.stderr
assert disposable.root.exists() and disposable_zip.exists()
plan=json.loads(plan_file.read_text());assert {i['runId'] for i in plan['candidates']}=={disposable.run_id}
deleted=cli('delete',['delete-task-assets','DELETE-PROOF','--plan',str(plan_file)])
assert deleted.returncode==0,deleted.stdout+deleted.stderr
receipt=json.loads(deleted.stdout);assert receipt['completed'] and receipt['deletedBytes']==expected_bytes
assert not disposable.root.exists() and not disposable_zip.exists()
assert all(assets.file_digest(root/name)==sha for name,sha in kept_hashes.items())
with patch.object(assets,'binding_home',return_value=bindings):assert assets.read_index(repo,'DELETE-PROOF')['runs']==original
retired=cli('retired-read',['path','DELETE-PROOF',disposable.run_id,'payload/raw/case.trx']);assert retired.returncode==2 and not disposable.root.exists()
retained=cli('retained-read',['path','DELETE-PROOF',protected.run_id,'payload/raw/case.trx']);assert retained.returncode==0
# Source copies/bytecode are disposable after the real consumers finish. Keep the tiny audit and protected fixture evidence.
copied_files=sum(1 for p in (repo/'tools').rglob('*') if p.is_file());shutil.rmtree(repo/'tools');shutil.rmtree(repo/'schemas')
assert baseline.read_text()=='{"expected":"independent"}\n' and unknown.read_text()=='{"keep":"unknown-owner"}\n'
source_stable=all(assets.file_digest(R/item['path'])==item['sha256'] for item in source)
summary={'status':'passed','consumersCompleted':True,'sourceStable':source_stable,'controlledFixture':True,
 'deletedFiles':len(receipt['deleted']),'deletedBytes':receipt['deletedBytes'],'protectedFilesVerified':len(kept),
 'originalRunFactsUnchanged':True,'oldBindingsAndNestedPathsUsed':True,'retiredReadRejected':True,'protectedReadSucceeded':True,
 'temporarySourceCopiesRemoved':copied_files,'realHistoricalTasksDeleted':False}
assets.atomic_json(run.path('results','source-binding.json'),{'sourceFiles':source,'qualification':'real subprocess CLI on a newly created isolated task; no real baseline promotion or historical deletion'})
run.finish(summary)
finalized=True
result={'runId':run.run_id,'runRoot':str(run.root),**summary}
assets.atomic_json(H/(label+'-result.json'),result)
print(json.dumps(result))
if not source_stable:sys.exit(1)
