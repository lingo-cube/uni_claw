#!/usr/bin/env python3
"""Select storage/closed-prerequisite hunks in the private index only."""
import difflib
import hashlib
import json
import os
import subprocess
from pathlib import Path
HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
draft = json.loads((HERE / 'draft-run.json').read_text())
env = os.environ.copy(); env['GIT_INDEX_FILE'] = draft['index']
providers = ['src/UniClaw.Kernel/Effects/EffectDispatch.cs', 'src/UniClaw.Kernel/World/WorldModel.cs', 'src/UniClaw.Kernel/Runtime/RuntimePlanGraph.cs']
subprocess.run(['git', 'add', '--', *providers], cwd=REPO, env=env, check=True)
changes = {}; excluded = []
path = 'src/UniClaw.Kernel/Runtime/KernelRunDriver.cs'
original = (REPO / path).read_text(); selected = original
for line in original.splitlines(True):
    if any(marker in line for marker in ['private ExpectedEffect? _stepExpectedEffect;', 'Scope cursor for an ordinary Act,', 'private string? _actOriginContainerId;', 'private string? _actOriginOverlayState;', '_actOriginContainerId =', '_actOriginOverlayState =', '_stepExpectedEffect =', 'var failedStepIndex = _planItems is not null ? _planItemIndex : _stepIndex;']):
        selected = selected.replace(line, '')
start = selected.index('                    if (_adoptedDecision is AgentDecision.Act localAct && steps.Count > 1)')
end = selected.index('                    _activeGuardEvidenceRefs =', start)
selected = selected[:start] + selected[end:]
selected = selected.replace('_pendingFailedStepIndex = failedStepIndex;', '_pendingFailedStepIndex = _planItemIndex;')
selected = selected.replace('_inFlightReceiptId, _stepExpectedEffect)', '_inFlightReceiptId)')
selected = selected.replace('var overlayRecovery = _kernel.CurrentBelief?.ObservationNeed?.Kind == OverlayObservationCodec.NeedKind;', 'const string recoverySubject = "page-identity";')
selected = selected.replace('        var recoverySubject = overlayRecovery ? "overlay" : "page-identity";\n', '')
selected = selected.replace('        overlayRecovery = need?.Kind == OverlayObservationCodec.NeedKind;\n', '')
selected = selected.replace('        recoverySubject = overlayRecovery ? "overlay" : "page-identity";\n', '')
selected = selected.replace('need is null ? depth : overlayRecovery ? ObservationDepth.Normal : ObservationDepth.Slow', 'need is null ? depth : ObservationDepth.Slow')
selected = selected.replace('new[] { overlayRecovery ? ProductAssociationStrategy.PopupSubject : WorldModel.CurrentContainerSubject }', 'new[] { WorldModel.CurrentContainerSubject }')
assert not any(marker in selected for marker in ['_stepExpectedEffect', '_actOrigin', 'OverlayObservationCodec', 'overlayRecovery', 'failedStepIndex'])
changes[path] = (original, selected)
path = 'src/UniClaw.Kernel/UniKernel.cs'; original = (REPO / path).read_text(); selected = original
selected = selected.replace(', ExpectedEffect? authoredExpected = null', '')
selected = selected.replace('receiptId, authoredExpected)', 'receiptId)')
selected = selected.replace('var expected = authoredExpected ?? (isSwipe', 'var expected = isSwipe')
selected = selected.replace(': ExpectedEffect.OfState(target.Role, "present"));', ': ExpectedEffect.OfState(target.Role, "present");')
needle = ',\n                Overlay: expected.Changes.Contains(ChangePredicate.OverlayDismissed)\n                    ? BuildOverlayDismissalEvidence(preActionBelief, current, verifyingReceipt) : null'
assert needle in selected
selected = selected.replace(needle, '')
assert not any(marker in selected for marker in ['authoredExpected', 'BuildOverlayDismissalEvidence', 'ChangePredicate.OverlayDismissed'])
changes[path] = (original, selected)
for path, (original, selected) in changes.items():
    blob = subprocess.check_output(['git', 'hash-object', '-w', '--stdin'], input=selected.encode(), cwd=REPO).decode().strip()
    subprocess.run(['git', 'update-index', '--add', '--cacheinfo', '100644,' + blob + ',' + path], cwd=REPO, env=env, check=True)
    excluded += list(difflib.unified_diff(selected.splitlines(True), original.splitlines(True), fromfile='a/' + path, tofile='b/' + path))
    assert (REPO / path).read_text() == original
subprocess.run(['git', 'checkout-index', '--force', '--prefix=' + draft['view'] + '/', *providers, *changes], cwd=REPO, env=env, check=True)
tree = subprocess.check_output(['git', 'write-tree'], cwd=REPO, env=env, text=True).strip()
(HERE / 'excluded-parallel-hunks.patch').write_text(''.join(excluded))
(HERE / 'kernel-hunk-selection.json').write_text(json.dumps({'tree': tree, 'closedPrerequisites': providers,
    'selectedFiles': {p: hashlib.sha256(s.encode()).hexdigest() for p, (_, s) in changes.items()},
    'workingFilesUnchanged': True, 'authority': 'private submission view; excludes explicitly parallel Act/Overlay/ExpectedEffect business hunks'}, indent=2) + '\n')
print(tree)
