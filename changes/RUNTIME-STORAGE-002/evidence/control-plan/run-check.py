#!/usr/bin/env python3
"""One source-bound selected check, using the shared configured development asset layout."""
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO / 'tools'))
from test_assets import AssetRun, load_layout, fingerprints, atomic_json, file_digest, digest

label, mode, selected = sys.argv[1:4]
source, inputs, provenance = fingerprints(REPO, [], selected, label, False)
def golden_inputs():
    if mode != 'golden':
        return []
    catalog = json.loads(subprocess.check_output([sys.executable, str(REPO / 'tools/gen-run-report.py'), '--describe-inputs']))
    paths = [REPO / selected / item['fileName'] for item in catalog['files']]
    paths += [REPO / sys.argv[4] / name for name in catalog['outputFiles']]
    return [{'path': str(p.relative_to(REPO)), 'exists': p.exists(),
             'sha256': file_digest(p) if p.is_file() else None} for p in paths]
extra_inputs = golden_inputs()
if extra_inputs:
    provenance['goldenInputFiles'] = extra_inputs
    inputs = digest([inputs, extra_inputs])
run = AssetRun(REPO, load_layout(REPO), 'RUNTIME-STORAGE-002', label, source, inputs)
atomic_json(run.path('results', 'provenance.json'), provenance)
if mode in ('host', 'world', 'kernel'):
    project = 'tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj' if mode == 'kernel' else 'tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj'
    args = ['dotnet', 'test', project, '--no-restore',
            '--filter', selected, '--logger', 'trx;LogFileName=tests.trx', '--results-directory',
            str(run.directory('results')), '-m:1', '-nr:false']
elif mode == 'dsh':
    args = ['dotnet', 'test', 'tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj', '--no-restore',
            '--filter', selected, '--logger', 'trx;LogFileName=tests.trx', '--results-directory',
            str(run.directory('results')), '-m:1', '-nr:false']
elif mode == 'python':
    args = [sys.executable, '-m', 'unittest', 'discover', '-s', 'tools/tests', '-p', selected, '-v']
elif mode == 'build':
    args = ['dotnet', 'build', 'tools/RunRecoveryProbe/RunRecoveryProbe.csproj', '--no-restore', '-m:1', '-nr:false']
elif mode == 'growth':
    scenario, number = selected.split(':')
    args = ['dotnet', 'tools/RunRecoveryProbe/bin/Debug/net10.0/UniClaw.RunRecoveryProbe.dll',
            'measure-control-plan-growth', str(run.directory('raw') / 'source'), scenario, number]
elif mode == 'golden':
    args = [sys.executable, 'tools/gen-run-report.py', '--run-dir', selected,
            '--check', '--expect-dir', sys.argv[4]]
else:
    raise ValueError(mode)
environment = os.environ.copy()
if mode == 'world':
    environment['UNICLAW_WORLD_DELTA_EXPORT'] = str(run.directory('raw') / 'world-cost')
with run.path('logs', 'check.log').open('w') as output:
    code = subprocess.call(args, cwd=REPO, stdout=output, stderr=subprocess.STDOUT, env=environment)
log = run.path('logs', 'check.log').read_text()
trx = run.path('results', 'tests.trx')
counts = {}
if trx.exists():
    counts = ET.parse(trx).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
elif mode == 'python':
    match = re.search(r'Ran (\d+) tests?', log)
    counts = {'total': int(match[1]) if match else 0, 'passed': code == 0}
elif mode == 'build':
    counts = {'built': code == 0}
elif mode == 'growth':
    growth = run.directory('raw') / 'source/growth.json'
    if growth.is_file():
        item = json.loads(growth.read_text())
        counts = {'requested': item['RequestedCount'], 'mode': item['Mode'], 'completedWork': item['CompletedWork'], 'adoptions': item['Adoptions'], 'replacements': item['Replacements']}
elif mode == 'golden':
    counts = {'passed': code == 0 and 'CHECK-PASS' in log}
after_source, after_inputs, after = fingerprints(REPO, [], selected, label, False)
if extra_inputs:
    after_inputs = digest([after_inputs, golden_inputs()])
before_files = {x['path']: x['sha256'] for x in provenance['sourceFiles']}
after_files = {x['path']: x['sha256'] for x in after['sourceFiles']}
changes = sorted(p for p in before_files.keys() | after_files.keys() if before_files.get(p) != after_files.get(p))
owned = {'tools/RunRecoveryProbe/ControlPlanGrowthProbe.cs', 'tools/RunRecoveryProbe/Program.cs', 'src/UniClaw.Kernel/Runtime/KernelRunDriver.PlanHistory.cs', 'tests/UniClaw.Host.Tests/WorldHistoryDeltaTests.cs', 'src/UniClaw.Host/HostRunner.cs', 'src/UniClaw.Host/Runtime/AuxiliaryArtifacts.cs',
         'src/UniClaw.Host/Runtime/ConsultationArtifactLog.cs', 'tools/gen-run-report.py',
         'tools/tests/test_run_report_input_contract.py', 'tools/tests/test_run_report_consultation_log.py',
         'tests/UniClaw.Host.Tests/ConsultationArtifactLogTests.cs'}
summary = {'label': label, 'mode': mode, 'selection': selected, 'exitCode': code, 'counts': counts,
           'sourceDigest': source, 'inputDigest': inputs, 'sourceUnchangedDuringRun': source == after_source,
           'inputsUnchangedDuringRun': inputs == after_inputs,
           'changedSourcePathsDuringRun': changes, 'ownedSourcesUnchangedDuringRun': not owned.intersection(changes),
           'consumersCompleted': bool(counts), 'scope': 'shared working sources; selected checks only'}
run.finish(summary)
atomic_json(Path(__file__).parent / (label + '.json'), {'runId': run.run_id, 'runRoot': str(run.root), **summary})
print(json.dumps({'runId': run.run_id, 'runRoot': str(run.root), **summary}, indent=2))
print(log[-4000:])
sys.exit(code)
