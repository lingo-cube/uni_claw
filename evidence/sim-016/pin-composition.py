import json,hashlib,pathlib
root=pathlib.Path(__file__).resolve().parents[2];fixture=root/'tests/UniClaw.Host.Tests/Fixtures/PopupReplay'
pins=json.loads((root/'tests/UniClaw.Simulation.Tests/Fixtures/ComponentReplay/composition.json').read_text())['pins'];known={p['path'] for p in pins}
paths={'product-implementation':['src/UniClaw.Host/SettingsPopupClassifier.cs','src/UniClaw.Host/SettingsTraversalLiveFeed.cs','src/UniClaw.Host/SettingsCoverage/SettingsCoverageRunner.cs','src/UniClaw.Host/UiAutomatorDump.cs','src/UniClaw.Kernel/Perception/UiHierarchy/TypedHierarchyProposalProjector.cs'],'product-contract':['src/UniClaw.Host/SettingsCoverage/SettingsCoverageConfig.cs'],'simulation-adapter':['tests/UniClaw.Host.Tests/PopupReplay/RecordedPopupEpisodes.cs','tests/UniClaw.Host.Tests/RecordedPopupReplayTests.cs','tests/UniClaw.Host.Tests/RecordedPopupRuntimeReplayTests.cs','tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj','tests/UniClaw.Host.Tests/RecordedPopupClassificationTests.cs'],'replay-input':['tests/UniClaw.Host.Tests/Fixtures/PopupReplay/sources.json'],'oracle':['tests/UniClaw.Host.Tests/Fixtures/PopupReplay/oracle.json']}
# Explicit module scope for the real Runner buyer, not a universal dependency resolver.
for module in ['src/UniClaw.Kernel/Runtime','src/UniClaw.Kernel/Effects','src/UniClaw.Kernel/Run','src/UniClaw.Kernel/Control','src/UniClaw.Kernel/Assurance','src/UniClaw.Host/SettingsCoverage']:
 paths['product-implementation'] += [str(p.relative_to(root)) for p in (root/module).rglob('*.cs')]
paths['product-implementation'] += [str(p.relative_to(root)) for p in (root/'src/UniClaw.Kernel').glob('UniKernel*.cs')]
paths['product-implementation'] += ['src/UniClaw.Host/UiHierarchyOccurrenceStrategy.cs']
paths['oracle'] += [f'scenarios/SCN-REPLAY-{n:03d}.json' for n in [10,11,12,13]]
for e in json.loads((fixture/'sources.json').read_text())['episodes']:
 paths['replay-input'] += [e['trace']['path'],e['steps']['path']]+[f[k]['path'] for f in e['frames'] for k in ['xml','png']]
for kind,names in paths.items():
 for name in sorted(set(names)):
  if name not in known:
   pins.append({'kind':kind,'path':name,'sha256':hashlib.sha256((root/name).read_bytes()).hexdigest()});known.add(name)
(fixture/'composition.json').write_text(json.dumps({'schemaVersion':'component-replay-composition.v1','causalChange':'SIM-016','pins':pins},indent=2)+'\n')
print('popup pins',len(pins))
