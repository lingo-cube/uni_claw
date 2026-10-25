#!/usr/bin/env python3
"""Run one selected Host test scope and retain a source-bound result via the shared asset adapter."""
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO / 'tools'))
from test_assets import AssetRun, load_layout, fingerprints, atomic_json, file_digest

label, selected = sys.argv[1:3]
source, inputs, provenance = fingerprints(REPO, [], selected, label, False)
run = AssetRun(REPO, load_layout(REPO), 'RUNTIME-STORAGE-002', label, source, inputs)
atomic_json(run.path('results', 'provenance.json'), provenance)
args = ['dotnet', 'test', 'tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj', '--no-restore',
        '--filter', selected, '--logger', 'trx;LogFileName=tests.trx', '--results-directory',
        str(run.directory('results')), '-m:1', '-nr:false']
with run.path('logs', 'test.log').open('w') as output:
    code = subprocess.call(args, cwd=REPO, stdout=output, stderr=subprocess.STDOUT)
trx = run.path('results', 'tests.trx')
counts = {}
if trx.exists():
    counts = ET.parse(trx).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
after_source, after_inputs, _ = fingerprints(REPO, [], selected, label, False)
assemblies = {str(p.relative_to(REPO)): file_digest(p)
              for base in ('src/UniClaw.Host/bin/Debug/net10.0', 'tests/UniClaw.Host.Tests/bin/Debug/net10.0',
                           'tools/RunRecoveryProbe/bin/Debug/net10.0')
              for p in (REPO / base).glob('UniClaw.*.dll')}
summary = {'label': label, 'filter': selected, 'exitCode': code, 'counts': counts,
           'sourceDigest': source, 'inputDigest': inputs, 'sourceUnchangedDuringRun': source == after_source,
           'inputsUnchangedDuringRun': inputs == after_inputs, 'assembliesSha256': assemblies,
           'consumersCompleted': trx.exists(), 'scope': 'shared working sources; targeted only'}
run.finish(summary)
atomic_json(Path(__file__).parent / (label + '.json'), {'runId': run.run_id, 'runRoot': str(run.root), **summary})
print(json.dumps({'runId': run.run_id, 'runRoot': str(run.root),
                  **{k: v for k, v in summary.items() if k != 'assembliesSha256'}}, indent=2))
print(run.path('logs', 'test.log').read_text()[-2500:])
sys.exit(code)
