#!/usr/bin/env python3
"""Stage only declared records in the private index; keep raw/history patches local."""
import hashlib,json,os,subprocess
from pathlib import Path
HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
draft=json.loads((HERE/'draft-run.json').read_text());env=dict(os.environ,GIT_INDEX_FILE=draft['index'])
roots=['RUNTIME-STORAGE-001','RUNTIME-STORAGE-002','TEST-ASSET-LIFECYCLE-001','TEST-ASSET-LAYOUT-001','TEST-ASSET-AUTOMATION-001']
selected=[];local=[]
for name in roots:
 root=REPO/'changes'/name
 for f in sorted(root.rglob('*')):
  if not f.is_file() or f.is_symlink() or f==HERE/'evidence-publication-plan.json':continue
  rel=f.relative_to(root)
  frozen_codec=str(f.relative_to(REPO))=='changes/RUNTIME-STORAGE-002/evidence/world-baselines/before/src/UniClaw.Host/Runtime/WorldRevisionDeltaCodec.cs'
  raw_snapshot=rel.parts[0]=='evidence' and len(rel.parts)>1 and rel.parts[1] in ('entry-source','source-snapshot','before','sealed')
  ok=frozen_codec or (not raw_snapshot and len(rel.parts)<=3 and f.suffix in ['.md','.json','.py'] and str(f.relative_to(REPO))!='changes/TEST-ASSET-AUTOMATION-001/evidence/slice3/review-spec-v1.md' and f.stat().st_size<=1048576)
  item={'path':str(f.relative_to(REPO)),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()}
  (selected if ok else local).append(item)
# Contracts of exact, already-closed providers, not the rest of their implementation.
providers=['ORIGIN-CORRELATION-001','SIM-019','EVO-002','EVO-003']+['RUNTIME-RECOVERY-'+n for n in ['002','003','005','007','009','010','013','015','016']]
for name in providers:
 for f in sorted((REPO/'changes'/name).glob('*.md')):
  selected.append({'path':str(f.relative_to(REPO)),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
for name in ['runtime-storage-001.md','runtime-storage-002.md']:
 f=REPO/'plans'/name
 if f.exists():selected.append({'path':str(f.relative_to(REPO)),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
plan={'status':'selected compact records; raw retained locally unchanged','selected':selected,'retainedLocal':local,
      'policy':'No raw/source snapshots, intermediate or excluded source patches, binary/log/TRX/archive trees are added by this script. Historical local proofs remain local-input dependent.'}
(HERE/'evidence-publication-plan.json').write_text(json.dumps(plan,indent=2)+'\n')
paths=sorted(set(x['path'] for x in selected)|{str((HERE/'evidence-publication-plan.json').relative_to(REPO))})
if os.environ.get('PUB_RECORDS_PLAN_ONLY')=='1':
 print(json.dumps({'selectedRecords':len(paths),'planOnly':True}));raise SystemExit(0)
normal_before=subprocess.check_output(['git','diff','--cached','--binary'],cwd=REPO)
subprocess.run(['git','add','--',*paths],cwd=REPO,env=env,check=True)
assert subprocess.check_output(['git','diff','--cached','--binary'],cwd=REPO)==normal_before
# Export the candidate's own Change states and records for the canonical index generator.
existing=subprocess.check_output(['git','ls-files','changes/','plans/'],cwd=REPO,env=env,text=True).splitlines()
exports=[p for p in existing if p.endswith('/state.md') or p in paths]
subprocess.run(['git','checkout-index','--force','--prefix='+draft['view']+'/',*exports],cwd=REPO,env=env,check=True)
subprocess.run(['python3','tools/gen-open-changes.py'],cwd=draft['view'],check=True)
p='changes/INDEX.md';data=(Path(draft['view'])/p).read_bytes();blob=subprocess.check_output(['git','hash-object','-w','--stdin'],input=data,cwd=REPO).decode().strip()
subprocess.run(['git','update-index','--add','--cacheinfo','100644,'+blob+','+p],cwd=REPO,env=env,check=True)
print(json.dumps({'selectedRecords':len(paths),'selectedBytes':sum(x['bytes'] for x in selected),'retainedLocalFiles':len(local),'realIndexUnchanged':True}))
