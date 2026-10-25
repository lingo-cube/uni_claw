#!/usr/bin/env python3
"""Compile the exact Host persistence modules independently of parallel Coverage edits."""
import hashlib
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO / 'tools'))
from test_assets import AssetRun, load_layout, atomic_json, digest, file_digest
paths = ['src/UniClaw.Host/Runtime/' + p for p in ['RuntimeRunStore.cs', 'RuntimeRunStore.History.cs', 'ActivationRecordStore.cs', 'TaskInstanceStore.cs', 'ProductSessionStore.cs', 'ReversibleJsonDelta.cs', 'HistoryVersions.cs']]
paths += ['tests/UniClaw.Host.Tests/' + p for p in ['RuntimeRunProjectionVersionTests.cs', 'RuntimeRunStoreTests.cs', 'ActivationCommitTests.cs', 'ActivationRecordStoreTests.cs', 'RuntimeIdentityStoreTests.cs', 'RuntimeIdentityCommitTests.cs']]
before = {p: file_digest(REPO / p) for p in paths}
run = AssetRun(REPO, load_layout(REPO), 'RUNTIME-STORAGE-002', 'isolated exact Host persistence public contract checks', digest(before), digest(paths))
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk'); props = ET.SubElement(project, 'PropertyGroup')
for key, value in {'TargetFramework': 'net10.0', 'ImplicitUsings': 'enable', 'Nullable': 'enable', 'IsTestProject': 'true', 'EnableDefaultCompileItems': 'false', 'IsPackable': 'false'}.items(): ET.SubElement(props, key).text = value
refs = ET.SubElement(project, 'ItemGroup')
for name, version in [('Microsoft.NET.Test.Sdk', '17.9.0'), ('xunit', '2.6.2'), ('xunit.runner.visualstudio', '2.5.4')]: ET.SubElement(refs, 'PackageReference', Include=name, Version=version)
sources = ET.SubElement(project, 'ItemGroup')
for path in paths: ET.SubElement(sources, 'Compile', Include=str(REPO / path), Link=Path(path).name)
file = run.directory('scratch') / 'HostPersistence.Tests.csproj'; ET.ElementTree(project).write(file, encoding='unicode')
atomic_json(run.path('results', 'source-binding.json'), {'files': before, 'scope': 'exact real source modules/public tests only; no fake implementations or Kernel/parallel Coverage dependency'})
with run.path('logs', 'check.log').open('w') as log:
    code = subprocess.call(['dotnet', 'test', str(file), '-p:NuGetAudit=false', '-m:1', '-nr:false', '--logger', 'trx;LogFileName=tests.trx', '--results-directory', str(run.directory('results'))], cwd=REPO, stdout=log, stderr=subprocess.STDOUT)
trx = run.path('results', 'tests.trx'); counts = ET.parse(trx).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib if trx.exists() else {}
summary = {'exitCode': code, 'counts': counts, 'sourceFilesUnchanged': before == {p: file_digest(REPO / p) for p in paths}, 'consumersCompleted': bool(counts), 'scope': 'isolated actual Host modules; final integrated build remains separate'}
run.finish(summary); atomic_json(HERE / 'host-pure-final.json', {'runId': run.run_id, 'runRoot': str(run.root), **summary});print(json.dumps(summary, indent=2));print(run.path('logs', 'check.log').read_text()[-2500:]);sys.exit(code)
