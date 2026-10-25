#!/usr/bin/env python3
"""Read sealed v4 bytes with the current extracted codec; never writes the old source."""
import json
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO / 'tools'))
from test_assets import AssetRun, load_layout, fingerprints, atomic_json, file_digest, digest

source_dir = REPO / 'changes/RUNTIME-STORAGE-002/evidence/slice3/cost/final-v4/versioned-256'
source_path = source_dir / 'resource.journal.jsonl'
expected = json.loads((REPO / 'changes/RUNTIME-STORAGE-002/evidence/slice3/cost/versioned-final-v4-256.json').read_text())
def source_hashes():
    return {p.relative_to(REPO).as_posix(): file_digest(p) for p in source_dir.rglob('*') if p.is_file()}
before = source_hashes()
source, inputs, provenance = fingerprints(REPO, [], 'sealed-resource-v4-compat', 'core-version shared codec compatibility', False)
inputs = digest([inputs, before, expected['inputRecordSha256']])
run = AssetRun(REPO, load_layout(REPO), 'RUNTIME-STORAGE-002', 'sealed v4 resource codec compatibility', source, inputs)
atomic_json(run.path('results', 'provenance.json'), {**provenance, 'sealedInputs': before})
scratch = run.directory('scratch')
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(project, 'PropertyGroup')
for key, value in {'OutputType': 'Exe', 'TargetFramework': 'net10.0', 'ImplicitUsings': 'enable', 'Nullable': 'enable'}.items():
    ET.SubElement(props, key).text = value
refs = ET.SubElement(project, 'ItemGroup')
assemblies = {}
for name in ['UniClaw.Host', 'UniClaw.Kernel', 'UniClaw.Core']:
    path = REPO / 'tests/UniClaw.Host.Tests/bin/Debug/net10.0' / (name + '.dll')
    ET.SubElement(ET.SubElement(refs, 'Reference', Include=name), 'HintPath').text = str(path)
    assemblies[name] = file_digest(path)
ET.ElementTree(project).write(scratch / 'Compat.csproj', encoding='unicode')
program = '''using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Host.Runtime;
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
using var journal = new FileRunResourceJournal(args[0], readOnly: true);
var rows = journal.ReadRecords();
using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
foreach (var row in rows) hash.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row, json) + "\\n"));
foreach (var seq in new[] { 1, (rows.Count + 1) / 2, rows.Count })
    foreach (var direction in new[] { ResourceHistoryDirection.Forward, ResourceHistoryDirection.Reverse })
        if (JsonSerializer.Serialize(rows[seq - 1], json) != JsonSerializer.Serialize(journal.ReadVersion(seq, direction).Record, json))
            throw new InvalidDataException("sealed-v4-version-mismatch");
var latest = journal.ReadLatestVersion();
Console.WriteLine(JsonSerializer.Serialize(new { records = rows.Count, inputRecordSha256 = Convert.ToHexString(hash.GetHashAndReset()), latest.Sequence, latest.ReadCost, bidirectionalMatch = true }));
'''
(scratch / 'Program.cs').write_text(program)
with run.path('logs', 'check.log').open('w') as log:
    code = subprocess.call(['dotnet', 'run', '--project', str(scratch / 'Compat.csproj'), '--', str(source_path)], cwd=REPO, stdout=log, stderr=subprocess.STDOUT)
log = run.path('logs', 'check.log').read_text()
print(log[-2500:])
result = json.loads(log.strip().splitlines()[-1]) if code == 0 else {}
summary = {'exitCode': code, 'passed': code == 0 and result.get('inputRecordSha256') == expected['inputRecordSha256'] and before == source_hashes(),
           'counts': result, 'sealedSourceUnchanged': before == source_hashes(), 'inputRecordSha256Expected': expected['inputRecordSha256'],
           'compiledAssemblySha256': assemblies, 'consumersCompleted': True}
# Only this run's disposable build/project directory; sources and result/log consumers remain.
import shutil
scratch_bytes = sum(p.stat().st_size for p in scratch.rglob('*') if p.is_file())
shutil.rmtree(scratch)
summary['temporaryBuildRemoved'] = not scratch.exists()
summary['temporaryBuildBytesRemoved'] = scratch_bytes
run.finish(summary)
atomic_json(Path(__file__).parent / 'resource-v4-compat.json', {'runId': run.run_id, 'runRoot': str(run.root), **summary})
print(json.dumps(summary, indent=2))
sys.exit(0 if summary['passed'] else 1)
