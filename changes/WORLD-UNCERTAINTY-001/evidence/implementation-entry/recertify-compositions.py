"""Explicit WORLD-UNCERTAINTY-001 recertification; never called from a test."""
import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
def digest(path):return hashlib.sha256((ROOT/path).read_bytes()).hexdigest()
new_sources=['src/UniClaw.Host/PageSemanticReadSupply.cs', 'src/UniClaw.Kernel/Assurance/RuntimeAssurance.WorldImpact.cs', 'src/UniClaw.Kernel/Control/ControlLoop.WorldConsumption.cs', 'src/UniClaw.Kernel/Control/WorldRecoveryControl.cs', 'src/UniClaw.Kernel/Perception/UiHierarchy/NativeContextObservationProjector.cs', 'src/UniClaw.Kernel/UniKernel.WorldConsumption.cs', 'src/UniClaw.Kernel/World/ContainerContextObservation.cs', 'src/UniClaw.Kernel/World/PageInterpretationCorrection.cs', 'src/UniClaw.Kernel/World/SemanticVerificationObservation.cs', 'src/UniClaw.Kernel/World/SliceContentPresentation.cs', 'src/UniClaw.Kernel/World/WorldAccumulatedContent.cs', 'src/UniClaw.Kernel/World/WorldConsumptionBasis.cs', 'src/UniClaw.Kernel/World/WorldKnowledgeView.cs', 'src/UniClaw.Kernel/World/WorldModel.Content.cs', 'src/UniClaw.Kernel/World/WorldModel.Context.cs', 'src/UniClaw.Kernel/World/WorldModel.Corrections.cs', 'src/UniClaw.Kernel/World/WorldModel.History.cs', 'src/UniClaw.Kernel/World/WorldModel.Knowledge.cs', 'src/UniClaw.Kernel/World/WorldModel.SemanticKnowledge.cs', 'src/UniClaw.Kernel/World/WorldPageView.cs']
audit=[]
for relative,full in [('tests/UniClaw.Simulation.Tests/Fixtures/ComponentReplay/composition.json',False),('tests/UniClaw.Host.Tests/Fixtures/PopupReplay/composition.json',True)]:
 path=ROOT/relative; data=json.loads(path.read_text()); before=hashlib.sha256(path.read_bytes()).hexdigest()
 changed=[]
 for pin in data['pins']:
  actual=digest(pin['path'])
  if actual!=pin['sha256']:
   assert pin['kind'] not in ['oracle','replay-input'],pin['path']
   changed.append(dict(pin,newSha256=actual));pin['sha256']=actual
 pinned={p['path'] for p in data['pins']}
 added=[]
 for source in new_sources:
  if source in pinned:continue
  if not full and not (source.startswith('src/UniClaw.Kernel/World/') or source.endswith('NativeContextObservationProjector.cs')):continue
  pin={'kind':'product-implementation','path':source,'sha256':digest(source)}
  data['pins'].append(pin);added.append(source)
 data['pins'].sort(key=lambda p:(p['kind'],p['path']))
 data['causalChange']='WORLD-UNCERTAINTY-001'
 path.write_text(json.dumps(data,indent=2)+'\n')
 audit.append({'manifest':relative,'beforeSha256':before,'afterSha256':hashlib.sha256(path.read_bytes()).hexdigest(),'changedPins':changed,'addedImplementationPins':added,'oracleAndReplayInputUnchanged':True})
(ROOT/'changes/WORLD-UNCERTAINTY-001/evidence/implementation-entry/composition-recertification.json').write_text(json.dumps(audit,indent=2)+'\n')
