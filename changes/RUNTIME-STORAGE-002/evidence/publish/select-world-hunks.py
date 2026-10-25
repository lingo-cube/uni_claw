#!/usr/bin/env python3
import difflib, hashlib, json, os, subprocess
from pathlib import Path
here=Path(__file__).resolve().parent
repo=here.parents[3]
draft=json.loads((here/'draft-run.json').read_text())
env=dict(os.environ,GIT_INDEX_FILE=draft['index'])
p='src/UniClaw.Kernel/World/WorldModel.cs'
original=(repo/p).read_text()
s=original
branch='        if (record.Claim.Subject == OverlayObservationCodec.Subject)\n            return ReconcileOverlay(record, parent);\n'
assert branch in s
s=s.replace(branch,'')
s=s.replace('        revision = RequireCurrentOverlay(revision);\n','')
s=s.replace('if (current.CurrentPageObservation is { ContainerId: null }\n            || current.ObservationNeed?.Kind == OverlayObservationCodec.NeedKind)', 'if (current.CurrentPageObservation is { ContainerId: null })')
assert 'OverlayObservationCodec' not in s and 'RequireCurrentOverlay' not in s
blob=subprocess.check_output(['git','hash-object','-w','--stdin'],input=s.encode(),cwd=repo).decode().strip()
subprocess.run(['git','update-index','--add','--cacheinfo','100644,'+blob+','+p],cwd=repo,env=env,check=True)
subprocess.run(['git','checkout-index','--force','--prefix='+draft['view']+'/',p],cwd=repo,env=env,check=True)
(here/'excluded-world-hunks.patch').write_text(''.join(difflib.unified_diff(s.splitlines(True),original.splitlines(True),fromfile='a/'+p,tofile='b/'+p)))
(here/'world-hunk-selection.json').write_text(json.dumps({'tree':subprocess.check_output(['git','write-tree'],cwd=repo,env=env,text=True).strip(),'path':p,'sha256':hashlib.sha256(s.encode()).hexdigest(),'workingFileUnchanged':(repo/p).read_text()==original},indent=2)+'\n')
print(draft['view'])
