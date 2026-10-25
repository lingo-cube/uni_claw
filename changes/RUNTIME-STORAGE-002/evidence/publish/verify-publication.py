#!/usr/bin/env python3
"""Bind accepted regressions and selected source bytes; never runs product effects."""
import hashlib,json,os,subprocess,sys
from pathlib import Path
H=Path(__file__).resolve().parent;R=H.parents[3]
sys.path.insert(0,str(R/'tools'))
from test_assets import fingerprints,file_digest
D=json.loads((H/'draft-run.json').read_text());V=Path(D['view']);E=dict(os.environ,GIT_INDEX_FILE=D['index'])
checks={};results={}
expected={'host-coverage-final-v3':411,'dsh-coverage-final-v4':42,'kernel-closure-v3':166,'python-final-v6':67}
for label,count in expected.items():
 d=json.loads((H/(label+'.json')).read_text());c=d['counts'];checks[label]=d['exitCode']==0 and int(c['total'])==count and d['sourceFilesUnchanged'] and d['consumersCompleted'] and (c.get('failed','0')=='0' if isinstance(c.get('passed'),str) else c['passed'] is True)
 results[label]={'runId':d['runId'],'counts':c,'ref':label+'.json'}
for label in ['final-world-tool-build','final-replay-tool-build']:
 d=json.loads((H/(label+'.json')).read_text());checks[label]=d['exitCode']==0 and d['counts']['built'] and d['sourceFilesUnchanged'];results[label]={'runId':d['runId'],'ref':label+'.json'}
provenance=fingerprints(V,[],'final-source-binding','joint storage publication',False)[2]
paths={x['path'] for x in provenance['sourceFiles']+provenance['inputFiles']}
paths.update(json.loads((H/'host-test-input-closure.json').read_text())['inputPaths'])
paths.add('changes/RUNTIME-STORAGE-002/evidence/world-baselines/before/src/UniClaw.Host/Runtime/WorldRevisionDeltaCodec.cs')
paths.add('tool-registry.yaml')
asset=json.loads((R/'changes/TEST-ASSET-AUTOMATION-001/evidence/integration-manifest.json').read_text())
paths.update(x['path'] for x in asset['sources'] if (V/x['path']).is_file())
source=[]
for p in sorted(paths):
 b=subprocess.check_output(['git','show',':'+p],cwd=R,env=E);sha=hashlib.sha256(b).hexdigest();checks['blob:'+p]=sha==file_digest(V/p)
 source.append({'path':p,'sha256':sha})
# Kernel was not changed by Coverage or asset/Host integration. Compare the actual verified sources.
k=json.loads((H/'kernel-closure-v3.json').read_text());old=json.loads((Path(k['runRoot'])/'results/provenance.json').read_text())['sourceFiles']
def kernel_consumed(p):return (p.startswith('src/') and not p.startswith('src/UniClaw.Host')) or p.startswith('tests/UniClaw.Kernel.Tests/') or '/' not in p
before={x['path']:x['sha256'] for x in old if kernel_consumed(x['path'])}
after={x['path']:x['sha256'] for x in provenance['sourceFiles'] if kernel_consumed(x['path'])}
checks['kernel-verified-sources-identical']=before==after
# Exact current owner candidate plus a documented, deterministic Runner sibling exclusion.
candidate=json.loads((R/'changes/RUNTIME-STORAGE-002/evidence/coverage-delivery/candidate.json').read_text())['files']
for x in candidate:checks['coverage-owner:'+x['path']]=file_digest(R/x['path'])==x['sha256']
runner='src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs';integration=json.loads((H/'coverage-integration.json').read_text());checks['selected-runner-binding']=file_digest(V/runner)==integration['selectedRunnerSha256']
checks['diff-check']=subprocess.run(['git','diff','--cached','--check'],cwd=R,env=E,capture_output=True).returncode==0
checks['real-index-empty']=not subprocess.check_output(['git','diff','--cached','--name-only'],cwd=R,text=True).strip()
checks['no-unmerged-paths']=not subprocess.check_output(['git','ls-files','-u'],cwd=R,text=True).strip()
failed=[k for k,v in checks.items() if not v]
(H/'final-source-map.json').write_text(json.dumps({'algorithm':'sha256','scope':'actual selected source/input bytes, including fixed reader and original codec resources','files':source},indent=2)+'\n')
report={'passed':not failed,'checks':len(checks),'failed':failed,'results':results,'kernelVerifiedSourcesIdentical':checks['kernel-verified-sources-identical'],'sourceMap':'final-source-map.json','scope':'selected deterministic/real-process regressions; no device/model or whole-product claim','assetFinalization':'separate finalization.json required before CLOSED','reviews':['integration-review-spec-v1.md','integration-review-standards-v1.md','../coverage-delivery/review-spec-v2.md','../coverage-delivery/review-standards-v2.md']}
(H/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'passed':report['passed'],'checks':len(checks),'failed':failed}))
if failed:sys.exit(1)
