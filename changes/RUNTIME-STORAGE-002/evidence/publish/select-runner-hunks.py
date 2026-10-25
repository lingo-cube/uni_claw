#!/usr/bin/env python3
import difflib,hashlib,json,os,subprocess
from pathlib import Path
h=Path(__file__).resolve().parent;r=h.parents[3];d=json.loads((h/'draft-run.json').read_text());e=dict(os.environ,GIT_INDEX_FILE=d['index'])
p='src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs'
s=subprocess.check_output(['git','show',':'+p],cwd=r,env=e,text=True);original=s
s=s.replace('            OverlayObservationCodec.Subject,\n','')
s=''.join(line for line in s.splitlines(True) if 'config.ObservationRecoverySettings.' not in line)
a=s.index('                    ProjectExpectedEffect = (step, context) =>');b=s.index('                    },',a)+len('                    },\n');s=s[:a]+s[b:]
s=s.replace('if (popupNow is not null && input is RunDriverInput.Observation legacy\n                && !legacy.Proposals.Any(p => p.Claim.Subject == OverlayObservationCodec.Subject))','if (popupNow is not null)')
a=s.index('            if (current?.CurrentPageObservation?.Capture.RequiresOverlayEvidence == true');b=s.index('            if (!_observedOnce',a);s=s[:a]+s[b:]
assert not any(x in s for x in ['OverlayObservationCodec','RequiresOverlayEvidence','OverlayDismissed','ObservationRecoverySettings'])
b=subprocess.check_output(['git','hash-object','-w','--stdin'],input=s.encode(),cwd=r).decode().strip();subprocess.run(['git','update-index','--add','--cacheinfo','100644,'+b+','+p],cwd=r,env=e,check=True)
provider='src/UniClaw.Host/Runtime/ConsultationArtifactLog.cs';subprocess.run(['git','add','--',provider],cwd=r,env=e,check=True)
subprocess.run(['git','checkout-index','--force','--prefix='+d['view']+'/',p,provider],cwd=r,env=e,check=True)
(h/'excluded-runner-hunks.patch').write_text(''.join(difflib.unified_diff(s.splitlines(True),original.splitlines(True),fromfile='a/'+p,tofile='b/'+p)))
(h/'runner-hunk-selection.json').write_text(json.dumps({'status':'pre-Coverage draft; reapply after owner freeze','path':p,'sha256':hashlib.sha256(s.encode()).hexdigest(),'workingFileUnchanged':True,'provider':provider},indent=2)+'\n')
