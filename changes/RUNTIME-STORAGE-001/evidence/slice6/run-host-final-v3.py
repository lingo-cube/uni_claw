from pathlib import Path
import hashlib,json,os,subprocess,xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
previous=json.loads((HERE/'candidate-verification-binding-v2.json').read_text());candidate=json.loads((HERE/'candidate-inputs-v3.json').read_text())['files']
changed={n for n,d in candidate.items() if d!=previous['sourceHashes'][n]}
assert changed=={'tests/UniClaw.Host.Tests/StorageClosureFileTests.cs'},changed
inputs=dict(previous['effectiveBuildHashes']); inputs.update({n:d for n,d in candidate.items() if n.endswith('.cs')})
for n,d in inputs.items():assert sha(ROOT/n)==d,n
# Only an independent Host.Tests case was added. Kernel/DSH source and test assemblies are unchanged.
for n,d in previous['assemblyHashes'].items():
 if n.startswith('tests/UniClaw.Host.Tests/') and n.endswith('/UniClaw.Host.Tests.dll'):continue
 assert sha(ROOT/n)==d,n
commands=json.loads((HERE/'regression-selection.json').read_text());_,project,selected=commands[1]
env=dict(os.environ,UNICLAW_STORAGE_CLOSURE_EXPORT=str(HERE/'host-final-v3/closure'),UNICLAW_STORAGE_BARRIER_EXPORT=str(HERE/'host-final-v3/barriers'),UNICLAW_CONTROL_PLAN_ASYNC_EXPORT=str(HERE/'host-final-v3/control-plan'),EVIDENCE_BATCH_EVIDENCE_DIR=str(HERE/'host-final-v3/evidence'),UNICLAW_WORLD_ASYNC_EXPORT=str(HERE/'host-final-v3/world'),UNICLAW_RUN_ASYNC_EXPORT=str(HERE/'host-final-v3/run'),UNICLAW_WORLD_DELTA_EXPORT=str(HERE/'host-final-v3/world-size'))
command=['dotnet','test',project,'--no-restore','--filter',selected,'--logger','trx;LogFileName=host-final-v3.trx','--results-directory',str(HERE)]
with (HERE/'host-final-v3.log').open('w') as output:result=subprocess.run(command,cwd=ROOT,env=env,stdout=output,stderr=subprocess.STDOUT)
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};rows=ET.parse(HERE/'host-final-v3.trx').findall('.//t:UnitTestResult',ns)
summary=json.loads((HERE/'test-summary-v2.json').read_text());summary['host']={'exitCode':result.returncode,'total':len(rows),'passed':sum(r.attrib['outcome']=='Passed' for r in rows),'failed':[r.attrib['testName'] for r in rows if r.attrib['outcome']=='Failed']}
for n,d in inputs.items():assert sha(ROOT/n)==d,n
assert not {str(f.relative_to(ROOT)) for n in inputs if n.endswith('.csproj') for f in (ROOT/n).parent.rglob('*.cs') if 'obj' not in f.parts and 'bin' not in f.parts}-inputs.keys()
assemblies={n:sha(ROOT/n) for n in previous['assemblyHashes']}
testFiles={'kernel':'kernel-final-v2.trx','host':'host-final-v3.trx','dsh':'dsh-final-v2.trx'}
(HERE/'test-summary-v3.json').write_text(json.dumps(summary,indent=2)+'\n')
(HERE/'candidate-verification-binding-v3.json').write_text(json.dumps({'candidate':'candidate-inputs-v3.json','sourceHashes':candidate,'effectiveBuildHashes':inputs,'assemblyHashes':assemblies,'testFiles':testFiles,'testHashes':{n:sha(HERE/f) for n,f in testFiles.items()},'reusedUnchangedResults':{'kernel':'v2: Kernel sources and test assembly unchanged','dsh':'v2: DSH dependency graph excludes Host.Tests; runtime and test assemblies unchanged'},'onlyV3SourceDelta':sorted(changed)},indent=2)+'\n')
print(json.dumps(summary,indent=2))
