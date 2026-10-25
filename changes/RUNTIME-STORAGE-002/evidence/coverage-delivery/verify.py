#!/usr/bin/env python3
"""Read-only validation of fixed Coverage evidence; no builds, replay or cleanup."""
import hashlib
import json
from pathlib import Path
import sys
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
sys.path.insert(0,str(ROOT/'tools'))
import test_assets as assets

def read(name): return json.loads((HERE/name).read_text())
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    checks={}
    for row in read('candidate.json')['files']:
        checks['candidate:'+row['path']]=digest(ROOT/row['path'])==row['sha256']
    for row in read('entry.json')['files']:
        checks['before:'+row['path']]=digest(HERE/'before'/row['path'])==row['sha256']
    index=assets.read_index(ROOT,'RUNTIME-STORAGE-002')
    records={r['runId']:r for r in index['runs']}
    for path in sorted(HERE.glob('*-result.json')):
        result=json.loads(path.read_text())
        if 'runId' not in result: continue
        checks['run:'+result['runId']]=assets.validate_run(ROOT,'RUNTIME-STORAGE-002',result['runId'])['valid']
    for phase in ('coverage-host-final-v2','coverage-dsh-final-v1'):
        result=read(phase+'-result.json'); summary=records[result['runId']]['summary']
        checks[phase]=result['exitCode']==0 and result['counts'].get('Passed',0)>0 and set(result['counts'])=={'Passed'}
        checks[phase+':fixed-consumed']=summary['consumersCompleted'] and summary['sourceStable']
    checks['behavior-red']=read('coverage-red-result.json')['counts']=={'Failed':1}
    checks['review-red']=read('coverage-review-red-v1-result.json')['counts']=={'Failed':2}
    checks['old-python']= 'Ran 9 tests' in (HERE/'run-report-python.txt').read_text() and (HERE/'run-report-python.txt').read_text().rstrip().endswith('OK')
    checks['independent-reviews']=all((HERE/f'review-{axis}-v2.md').is_file() for axis in ('standards','spec'))
    checks['shared-runner-owned-patch']= (HERE/'runner-only.patch').is_file()
    result={'passed':all(checks.values()),'checks':checks,'scope':'Coverage original output delivery only; no live model/device, parent closure, task rerun or historical GC'}
    print(json.dumps(result,ensure_ascii=False,indent=2))
    return 0 if result['passed'] else 1
if __name__=='__main__': sys.exit(main())
