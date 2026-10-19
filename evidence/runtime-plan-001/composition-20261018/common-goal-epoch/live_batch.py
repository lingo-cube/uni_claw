"""Fixed serial composition epoch. Raw trials and historical gates stay immutable."""
import argparse, datetime, hashlib, importlib.util, json, os, pathlib, socket, subprocess, threading, time

HERE = pathlib.Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p / 'AGENTS.md').exists())
COMPARISON = ROOT / 'evidence/runtime-plan-001/comparison'
PREFIX = 'composition-r18-commongoal-'
TASKS = ('known-path', 'nested-scope')
MODES = ('ShortPlan', 'ComposedPlan')
CONDITION_KEYS = ('model', 'bounds', 'coverage', 'actionPolicyDigest', 'harnessAssemblySha256',
    'schemaHash', 'kernelAssemblySha256', 'hostAssemblySha256', 'decisionChannelSourceSha256',
    'profileRevision', 'promptHash', 'providerArtifactSha256', 'slowModelEnabled',
    'observationAndVerification', 'transportEvidenceSha256', 'targets', 'goal', 'device')
spec = importlib.util.spec_from_file_location('accounting', COMPARISON / 'collect_metrics.py')
accounting = importlib.util.module_from_spec(spec)
spec.loader.exec_module(accounting)

def now(): return datetime.datetime.now(datetime.timezone.utc).isoformat()
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def save(path, value):
    assert not path.exists(), f'never overwrite {path}'
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')
def paths(mode=None):
    return sorted(p for p in COMPARISON.glob(f'*/*/{PREFIX}*/trial.json')
        if mode is None or p.parent.parent.name == mode)
def metrics(path):
    item = accounting.trial_metrics(path)
    if item['task'] == 'nested-scope':
        steps_path = pathlib.Path(item.get('RunDir') or '') / 'coverage-steps.json'
        steps = json.loads(steps_path.read_text()) if steps_path.exists() else []
        verified = [s for s in steps if s.get('verified')]
        root = 'android.settings|rk1:Settings|src=homepage_title|up=0'
        item['verifiedNestedTransitions'] = sum(s.get('routeBefore') != root
            and s.get('routeAfter') != root and s.get('routeBefore') != s.get('routeAfter')
            and s.get('targetDescriptor') != 'Navigate up' for s in verified)
        item['validTrial'] = (item.get('driveStatus') == 'Completed'
            and item.get('coverage') == 'CoverageComplete' and item.get('modeConformant') is True
            and item.get('error') is None and item.get('cleanupError') is None
            and len(verified) >= 6 and item['verifiedNestedTransitions'] >= 1)
    return item
def condition(item): return {k: item['conditions'][k] for k in CONDITION_KEYS}
def accounted(item):
    return (item['tokenAccountingComplete'] and item['modelCalls'] > 0
        and item['callsWithReportedUsage'] == item['modelCalls']
        and isinstance(item['providerReportedTokens'].get('totalTokens'), (int,float))
        and item['providerReportedTokens']['totalTokens'] > 0
        and isinstance(item.get('formalElapsedMilliseconds'), (int,float))
        and item['formalElapsedMilliseconds'] > 0)
def artifact_fingerprints():
    compiled = ROOT / 'tests/UniClaw.Host.Dsh.Tests/bin/Debug/net10.0'
    files = list(compiled.glob('UniClaw.*.dll'))
    for relative in ('product/profiles', 'product/prompt/uniagent-prod', 'product/settings-coverage',
        'dsh/uniclaw-decision-channel', 'schemas/agt-002'):
        directory = ROOT / relative
        files += [p for p in directory.rglob('*') if p.is_file()
            and 'node_modules' not in p.parts and '__pycache__' not in p.parts]
    files += [ROOT / '.dsh/product/uniagent-prod-bindings.yaml', ROOT / 'product/tasks/settings-coverage.yaml']
    return {str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))}
def check_transport():
    transport = json.loads(pathlib.Path(os.environ['UNICLAW_PLAN_LIVE_TRANSPORT_EVIDENCE']).read_text())
    listeners = subprocess.check_output(['lsof','-nP','-tiTCP:3081','-sTCP:LISTEN'], text=True).split()
    assert listeners == [str(transport['servicePid'])], 'service identity changed'
    assert sha(pathlib.Path(transport['launchEnvironmentFile'])) == transport['launchEnvironmentFileSha256']
    assert sha(pathlib.Path(transport['providerArtifactPath'])) == transport['providerArtifactSha256']
    return transport
def run(task, mode, number):
    transport = check_transport()
    if mode != 'Step':
        frozen = json.loads((HERE / 'thresholds.json').read_text())
        assert artifact_fingerprints() == frozen['artifactFingerprints'], 'artifact/configuration drift; no Plan invoked'
    assert os.environ.get('UNICLAW_ANDROID_DEVICE') and os.environ.get('UNICLAW_ANDROID_RUN_DIR')
    repeat = PREFIX + str(number)
    out = COMPARISON / task / mode / repeat
    assert not out.exists(), 'never rerun an epoch attempt'
    out.mkdir(parents=True)
    env = dict(os.environ, DSH_TEST_ADB_ENV='1', DSH_TEST_PERCEPTION_LIVE='1',
        UNICLAW_PLAN_LIVE_TASK=task, UNICLAW_PLAN_LIVE_MODE=mode, UNICLAW_PLAN_LIVE_REPEAT=repeat)
    command = ['dotnet','test','tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj',
        '--no-build','--no-restore','-m:1','-p:BuildInParallel=false','--filter',
        'FullyQualifiedName~RuntimePlanComparisonLiveTests','--logger','console;verbosity=normal',
        '--','RunConfiguration.MaxCpuCount=1','xUnit.MaxParallelThreads=1','xUnit.ParallelizeTestCollections=false']
    stop = threading.Event()
    addresses = sorted({v[4][0] for v in socket.getaddrinfo('api.deepseek.com',443)})
    witnessed = set()
    def monitor():
        while not stop.is_set():
            result = subprocess.run(['lsof','-nP','-a','-p',str(transport['servicePid']),'-iTCP'],
                text=True, capture_output=True)
            for line in result.stdout.splitlines():
                for address in addresses:
                    if f'->{address}:443' in line:
                        witnessed.add(address)
            stop.wait(.25)
    thread = threading.Thread(target=monitor, daemon=True)
    thread.start()
    started = now()
    try:
        with (out / 'test.log').open('w') as log:
            result = subprocess.run(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
    finally:
        stop.set()
        thread.join()
    save(out / 'invocation.json', dict(started=started, finished=now(), exitCode=result.returncode, command=command))
    save(out / 'socket-witness.json', dict(servicePid=transport['servicePid'], dnsAddresses=addresses,
        directlyWitnessed443Addresses=sorted(witnessed), sampledOnly=True))
    item = metrics(out / 'trial.json')
    save(out / 'metrics.json', item)
    print(json.dumps(dict(task=task, mode=mode, repeat=repeat, exitCode=result.returncode,
        valid=item['validTrial'], consultations=item['consultations'], calls=item['modelCalls'])), flush=True)
    return item

def baseline():
    assert not paths(), 'baseline phase starts once; no automatic resume or added samples'
    for task in TASKS:
        valid = 0
        for number in range(1,4):
            item = run(task, 'Step', number)
            valid += item['validTrial']
            if valid == 2: break
        assert valid == 2, f'{task}: insufficient baseline; stop before Plan'
    save(HERE / 'baseline-metrics.json', [metrics(p) for p in paths('Step')])

def freeze():
    assert not any(p.parent.parent.name != 'Step' for p in paths()), 'Plan already started'
    baseline_items = json.loads((HERE / 'baseline-metrics.json').read_text())
    gates = {}
    for task in TASKS:
        items = [m for m in baseline_items if m['task'] == task]
        valid = [m for m in items if m['validTrial']]
        assert len(valid) == 2 and len(items) <= 3
        assert all(accounted(m) for m in items), 'missing or non-positive measured cost; no Plan gate'
        assert all(condition(m) == condition(items[0]) for m in items)
        compiled = ROOT / 'tests/UniClaw.Host.Dsh.Tests/bin/Debug/net10.0'
        for key, name in [('harnessAssemblySha256','UniClaw.Host.Dsh.Tests.dll'),
            ('kernelAssemblySha256','UniClaw.Kernel.dll'),('hostAssemblySha256','UniClaw.Host.dll')]:
            assert sha(compiled/name) == items[0]['conditions'][key], 'baseline artifact already drifted'
        assert (ROOT/'product/prompt/uniagent-prod/prompt-hash.txt').read_text().strip() == items[0]['conditions']['promptHash']
        assert (ROOT/'dsh/uniclaw-decision-channel/schema/schema-hash.txt').read_text().strip() == items[0]['conditions']['schemaHash']
        assert sha(ROOT/'dsh/uniclaw-decision-channel/src/index.js') == items[0]['conditions']['decisionChannelSourceSha256']
        gates[task] = dict(conditions=condition(items[0]), baselineAttempts=len(items), validBaseline=2,
            maxConsultations=min(m['consultations'] for m in valid)-1,
            maxModelCalls=min(m['modelCalls'] for m in valid)-1,
            maxTokens=sum(m['providerReportedTokens']['totalTokens'] for m in items)/2,
            maxFormalMilliseconds=sum(m['formalElapsedMilliseconds'] for m in items)/2)
    save(HERE / 'thresholds.json', dict(registeredAtUtc=now(), baselineSha256=sha(HERE/'baseline-metrics.json'),
        protocolSha256=sha(HERE/'live-registration.md'), artifactFingerprints=artifact_fingerprints(), gates=gates))

def plans():
    lock = json.loads((HERE / 'thresholds.json').read_text())
    assert not any(p.parent.parent.name != 'Step' for p in paths()), 'fixed batch starts once'
    for task in TASKS:
        for mode in MODES:
            for number in (1,2):
                item = run(task,mode,number)
                assert condition(item) == lock['gates'][task]['conditions'], 'conditions drift; stop'

def evaluate():
    items = [metrics(p) for p in paths()]
    lock = json.loads((HERE/'thresholds.json').read_text())
    groups = []
    for task in TASKS:
        for mode in MODES:
            group = [m for m in items if m['task']==task and m['mode']==mode]
            count = sum(m['validTrial'] for m in group)
            gate = lock['gates'][task]
            complete = len(group)==2 and count==2 and all(accounted(m) for m in group)
            values = dict(consultations=sum(m['consultations'] for m in group)/max(count,1),
                modelCalls=sum(m['modelCalls'] for m in group)/max(count,1),
                tokens=sum(m['providerReportedTokens']['totalTokens'] for m in group)/max(count,1)
                    if all(accounted(m) for m in group) else None,
                formalMilliseconds=sum(m['formalElapsedMilliseconds'] for m in group)/max(count,1)
                    if all(accounted(m) for m in group) else None)
            passes = dict(functional=complete,
                conditions=all(condition(m)==gate['conditions'] for m in group),
                consultations=complete and values['consultations']<=gate['maxConsultations'],
                modelCalls=complete and values['modelCalls']<=gate['maxModelCalls'],
                tokens=complete and values['tokens']<=gate['maxTokens'],
                formalMilliseconds=complete and values['formalMilliseconds']<=gate['maxFormalMilliseconds'])
            groups.append(dict(task=task,mode=mode,attempts=len(group),valid=count,values=values,gates=passes,passed=all(passes.values())))
    save(HERE/'live-metrics.json',items)
    save(HERE/'live-result.json',dict(evaluatedAtUtc=now(),groups=groups,passed=all(g['passed'] for g in groups)))
    print(json.dumps(groups,ensure_ascii=False),flush=True)

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('phase',choices=('baseline','freeze','plans','evaluate'))
    args=parser.parse_args()
    globals()[args.phase]()
