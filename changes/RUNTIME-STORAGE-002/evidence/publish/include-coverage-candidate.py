#!/usr/bin/env python3
"""Include the source-bound Coverage candidate, preserving declared sibling isolation."""
import difflib,hashlib,json,os,subprocess
from pathlib import Path
h=Path(__file__).resolve().parent;r=h.parents[3];d=json.loads((h/'draft-run.json').read_text());e=dict(os.environ,GIT_INDEX_FILE=d['index'])
candidate=r/'changes/RUNTIME-STORAGE-002/evidence/coverage-delivery/candidate.json';records=json.loads(candidate.read_text())['files']
for f in records:assert hashlib.sha256((r/f['path']).read_bytes()).hexdigest()==f['sha256'],f['path']
paths=[f['path'] for f in records];runner='src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs'
subprocess.run(['git','add','--',*[p for p in paths if p!=runner]],cwd=r,env=e,check=True)
original=(r/runner).read_text();s=original
s=''.join(line for line in s.splitlines(True) if 'config.ObservationRecoverySettings.' not in line)
a=s.index('                    ProjectExpectedEffect = (step, context) =>');b=s.index('                    },',a)+len('                    },\n');s=s[:a]+s[b:]
assert 'OverlayObservationCodec.Subject' in s and 'RequiresOverlayEvidence' in s
assert 'ProjectExpectedEffect =' not in s and 'OverlayDismissed' not in s and 'ObservationRecoverySettings' not in s
blob=subprocess.check_output(['git','hash-object','-w','--stdin'],input=s.encode(),cwd=r).decode().strip();subprocess.run(['git','update-index','--add','--cacheinfo','100644,'+blob+','+runner],cwd=r,env=e,check=True)
subprocess.run(['git','checkout-index','--force','--prefix='+d['view']+'/',*paths],cwd=r,env=e,check=True)
(h/'coverage-excluded-siblings.patch').write_text(''.join(difflib.unified_diff(s.splitlines(True),original.splitlines(True),fromfile='a/'+runner,tofile='b/'+runner)))
(h/'coverage-integration.json').write_text(json.dumps({'candidate':str(candidate.relative_to(r)),'candidateSha256':hashlib.sha256(candidate.read_bytes()).hexdigest(),'files':records,'selectedRunnerSha256':hashlib.sha256(s.encode()).hexdigest(),'scope':'Coverage source-bound candidate; native closed SIM-019 input base retained; later Act authored ExpectedEffect and separate configurable observation recovery remain excluded','status':'owner review in progress; no commit yet','workingFilesChanged':False},indent=2)+'\n')
print('included',len(paths),'bound paths')
