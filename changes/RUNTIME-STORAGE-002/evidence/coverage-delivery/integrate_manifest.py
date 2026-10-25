#!/usr/bin/env python3
"""Describe exact slice sources and shared-file integration; does not stage files."""
import difflib,hashlib,json,subprocess
from pathlib import Path
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[3]
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def load(path): return json.loads(path.read_text())
asset_path=ROOT/'changes/TEST-ASSET-AUTOMATION-001/evidence/integration-manifest.json'
asset=load(asset_path)
for row in asset['sources']: row['sha256']=sha(ROOT/row['path'])
asset_path.write_text(json.dumps(asset,ensure_ascii=False,indent=2)+'\n')
world=load(HERE.parent/'world-baselines/candidate.json')
rows=[]
for row in world['files']:
    path=row['path']; resolution=ROOT/path
    if path=='tools/RunRecoveryProbe/Program.cs': resolution=HERE/'before'/path
    rows.append({**row,'fixedEvidenceSource':str(resolution.relative_to(ROOT)), 'digestMatches':sha(resolution)==row['sha256']})
path='tests/UniClaw.Host.Tests/SettingsCoverageScenarioTests.cs'
before=subprocess.check_output(['git','show','HEAD:'+path],cwd=ROOT).decode()
after=before.replace('public sealed class SettingsCoverageScenarioTests','public sealed partial class SettingsCoverageScenarioTests',1)
assert before!=after
patch=''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True),fromfile='a/'+path,tofile='b/'+path))
(HERE/'existing-test-counterpart.patch').write_text(patch)
result={'coverageCandidate':'candidate.json','coveragePatch':'candidate.patch','runnerOwnedPatch':'runner-only.patch',
 'coverageEntry':'entry.json','worldCandidate':'../world-baselines/candidate.json','worldPatch':'../world-baselines/candidate.patch',
 'worldFixedSources':rows,'requiredFrozenOldCodec':'changes/RUNTIME-STORAGE-002/evidence/world-baselines/before/src/UniClaw.Host/Runtime/WorldRevisionDeltaCodec.cs',
 'oldCodecReason':'WorldStorageProbe.csproj Compile Include is a required reproducible v2 comparison dependency; retain in Git',
 'assetInventory':'changes/TEST-ASSET-AUTOMATION-001/evidence/integration-manifest.json',
 'assetLifecyclePatch':'changes/TEST-ASSET-AUTOMATION-001/evidence/integration-lifecycle.patch',
 'sharedCounterpart':{'path':path,'headSha256':hashlib.sha256(before.encode()).hexdigest(),'resultSha256':hashlib.sha256(after.encode()).hexdigest(),'patch':'existing-test-counterpart.patch','only':'add partial keyword; no shared later fixtures'},
 'sharedFiles':['SettingsCoverageRunner.cs: coverage entry delta only; parallel Overlay/ExpectedEffect/ObservationRecovery hunks excluded',
 'RunRecoveryProbe/Program.cs: World dispatcher first, then Coverage dispatcher; do not replace file from old snapshot',
 'RuntimeToolHost.cs: asset slice2 candidate-v2 plus Coverage entry delta; current full digest is combined, not ownership of all dirty hunks',
 'changes/INDEX.md: retain current unrelated entries, integrate only existing three asset tasks',
 'state/acceptance/continuation-handoff and WorkItems: append slice records, parent remains OPEN'],
 'evidenceAndStateRoots':['changes/RUNTIME-STORAGE-002/evidence/world-baselines','changes/RUNTIME-STORAGE-002/evidence/coverage-delivery',
 'changes/TEST-ASSET-LIFECYCLE-001','changes/TEST-ASSET-LAYOUT-001','changes/TEST-ASSET-AUTOMATION-001'],
 'exclude':['**/assets.lock','**/assets.*.lock','**/__pycache__/**','**/bin/**','**/obj/**'],
 'externalBindings':'local config and external rawRun roots retained in place, not Git release files',
 'integrationOwner':'original product Leader; no staging/commit/push performed by this chat'}
assert all(r['digestMatches'] for r in rows)
(HERE/'integration-manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print('World fixed sources verified:',len(rows),'asset sources:',len(asset['sources']))
