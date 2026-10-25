#!/usr/bin/env python3
"""Include the exact closed SIM-019 native observation base, never Act authoring hunks."""
import hashlib,json,os,subprocess,tarfile
from pathlib import Path
h=Path(__file__).resolve().parent;r=h.parents[3];d=json.loads((h/'draft-run.json').read_text());e=dict(os.environ,GIT_INDEX_FILE=d['index'])
paths=['src/UniClaw.Kernel/World/OverlayObservation.cs','src/UniClaw.Kernel/World/PageObservation.cs','src/UniClaw.Kernel/World/WorldModel.Overlay.cs']
def put(p,data):
 b=subprocess.check_output(['git','hash-object','-w','--stdin'],input=data,cwd=r).decode().strip();subprocess.run(['git','update-index','--add','--cacheinfo','100644,'+b+','+p],cwd=r,env=e,check=True)
records=[]
with tarfile.open(r/'changes/RUNTIME-STORAGE-002/evidence/slice1/pure-build-inputs.tar.gz') as t:
 for p in paths:
  b=t.extractfile(p).read();assert b==(r/p).read_bytes();put(p,b);records.append({'path':p,'sha256':hashlib.sha256(b).hexdigest(),'equalCurrent':True})
# These three native observation routing calls are part of the same closed source, not the later Act authoring.
p='src/UniClaw.Kernel/World/WorldModel.cs';put(p,(r/p).read_bytes());paths.append(p)
p='src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs';s=subprocess.check_output(['git','show',':'+p],cwd=r,env=e,text=True)
s=s.replace('            ProductAssociationStrategy.PopupSubject, // AGT-009', '            OverlayObservationCodec.Subject,\n            ProductAssociationStrategy.PopupSubject, // AGT-009')
s=s.replace('if (popupNow is not null)\n                _ledger.RecordPopupState(popupNow);', 'if (popupNow is not null && input is RunDriverInput.Observation legacy\n                && !legacy.Proposals.Any(p => p.Claim.Subject == OverlayObservationCodec.Subject))\n                _ledger.RecordPopupState(popupNow);')
needle='            var current = _kernel.CurrentBelief;\n            if (!_observedOnce'
replacement='            var current = _kernel.CurrentBelief;\n            if (current?.CurrentPageObservation?.Capture.RequiresOverlayEvidence == true\n                && current.WorldState.TryGetValue(ProductAssociationStrategy.PopupSubject, out var overlay)\n                && overlay.Value != _ledger.LatestPopupState)\n                _ledger.RecordPopupState(overlay.Value); // Owner projection, never raw proposal authority\n            if (!_observedOnce'
assert needle in s;s=s.replace(needle,replacement)
assert 'ProjectExpectedEffect =' not in s and 'OverlayDismissed' not in s
put(p,s.encode());paths.append(p)
subprocess.run(['git','checkout-index','--force','--prefix='+d['view']+'/',*paths],cwd=r,env=e,check=True)
(h/'closed-overlay-base.json').write_text(json.dumps({'owner':'SIM-019 (closed)','authority':'exact slice1 pure-build-inputs.tar.gz provider baseline, independently matched current bytes','providers':records,'pairedNativeCallers':paths[3:],'supersedes':'blanket native-Overlay exclusions in initial exploratory selection; Act/authored ExpectedEffect/OverlayDismissed remain excluded','workingFilesChanged':False},indent=2)+'\n')
