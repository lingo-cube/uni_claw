#!/usr/bin/env python3
"""One immutable, scoped local verification record in the configured asset layout."""
import hashlib,json,re,subprocess,sys,os
from pathlib import Path
R=Path(__file__).resolve().parents[4];H=Path(__file__).resolve().parent
sys.path.insert(0,str(R/'tools'))
from test_assets import AssetRun,load_layout,digest,atomic_json,file_digest
label=sys.argv[1]
if (H/(label+'-result.json')).exists():raise FileExistsError(label)
paths=['tools/test_assets.py','tools/test_asset_archives.py','tools/test_asset_cleanup.py','tools/test_asset_operations.py','tools/test-assets','tools/test_asset_task_cleanup.py','tools/test_asset_task_references.py','tools/tests/test_task_asset_deletion.py','tools/tests/test_task_asset_references.py']
def sources():return [{'path':p,'sha256':file_digest(R/p)} for p in paths]
before=sources();modules=sys.argv[2:]
run=AssetRun(R,load_layout(R),'TEST-ASSET-AUTOMATION-001','verification',digest(before),digest(modules))
command=[sys.executable,'-m','unittest',*modules,'-v']
atomic_json(run.path('results','source-binding.json'),{'sourceFiles':before,'command':command,'seam':'public asset CLI, planner/deleter and original consumers; isolated owned filesystem fixtures'})
environment=os.environ.copy();environment['PYTHONPATH']=str(R/'tools/tests')+os.pathsep+environment.get('PYTHONPATH','')
with run.path('logs','tests.txt').open('w') as log:code=subprocess.call(command,cwd=R,stdout=log,stderr=subprocess.STDOUT,env=environment)
log=run.path('logs','tests.txt').read_text();match=re.search(r'Ran (\d+) tests?',log)
counts={'total':int(match[1]) if match else 0,'passed':code==0}
summary={'exitCode':code,'counts':counts,'sourceStable':sources()==before,'consumersCompleted':True,'status':'passed' if code==0 else 'failed'}
run.finish(summary)
result={'runId':run.run_id,'runRoot':str(run.root),'command':command,**summary}
atomic_json(H/(label+'-result.json'),result)
print(json.dumps(result))
if code or not summary['sourceStable']:sys.exit(1)
