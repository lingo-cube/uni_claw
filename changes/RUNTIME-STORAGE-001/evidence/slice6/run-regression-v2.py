from pathlib import Path
import hashlib,json,os,subprocess,xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
entry=json.loads((HERE/'entry.json').read_text())
candidate=json.loads((HERE/'candidate-inputs-v2.json').read_text())['files']
for name,digest in candidate.items(): assert sha(ROOT/name)==digest,name
concurrent=json.loads((HERE/'concurrent-inputs-v1.json').read_text())['files']
expected=dict(entry['inputs']); expected.update({n:d['observedSha256'] for n,d in concurrent.items()})
for name,digest in expected.items():
 if name not in candidate: assert sha(ROOT/name)==digest,name
inputs={name:sha(ROOT/name) for name in expected}
inputs.update({name:d for name,d in candidate.items() if name.endswith('.cs')})
project_roots=[(ROOT/n).parent for n in inputs if n.endswith('.csproj')]
compile_files=lambda:{str(f.relative_to(ROOT)) for root in project_roots for f in root.rglob('*.cs') if 'obj' not in f.parts and 'bin' not in f.parts}
assert not compile_files()-inputs.keys(),compile_files()-inputs.keys()
commands=json.loads((HERE.parent/'slice5/regression-selection.json').read_text())
commands[1][2]+='|FullyQualifiedName~StorageClosureFileTests|FullyQualifiedName~HostFailClosedTests|FullyQualifiedName~HostClosureTests'
(HERE/'regression-selection.json').write_text(json.dumps(commands,indent=2)+'\n')
summary={};ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
env=dict(os.environ,UNICLAW_STORAGE_BARRIER_EXPORT=str(HERE/'host-final-v2/barriers'),UNICLAW_STORAGE_CLOSURE_EXPORT=str(HERE/'host-final-v2/closure'),EVIDENCE_BATCH_EVIDENCE_DIR=str(HERE/'host-final-v2/evidence'),UNICLAW_RUN_ASYNC_EXPORT=str(HERE/'host-final-v2/run'),UNICLAW_WORLD_ASYNC_EXPORT=str(HERE/'host-final-v2/world'),UNICLAW_CONTROL_PLAN_ASYNC_EXPORT=str(HERE/'host-final-v2/control-plan'),UNICLAW_WORLD_DELTA_EXPORT=str(HERE/'host-final-v2/world-size'))
for name,project,selected in commands:
 command=['dotnet','test',project,'--no-restore','--logger','trx;LogFileName='+name+'-final-v2.trx','--results-directory',str(HERE)]
 if selected: command+=['--filter',selected]
 assert not (HERE/(name+'-final-v2.log')).exists()
 with (HERE/(name+'-final-v2.log')).open('w') as output:
  result=subprocess.run(command,cwd=ROOT,env=env,stdout=output,stderr=subprocess.STDOUT)
 rows=ET.parse(HERE/(name+'-final-v2.trx')).findall('.//t:UnitTestResult',ns)
 summary[name]={'exitCode':result.returncode,'total':len(rows),'passed':sum(r.attrib['outcome']=='Passed' for r in rows),'failed':[r.attrib['testName'] for r in rows if r.attrib['outcome']=='Failed']}
 print(json.dumps({name:summary[name]}),flush=True)
for name,digest in inputs.items(): assert sha(ROOT/name)==digest,name
assert not compile_files()-inputs.keys(),compile_files()-inputs.keys()
assemblies={str(f.relative_to(ROOT)):sha(f) for project in ['src/UniClaw.Kernel','src/UniClaw.Host','tools/RunRecoveryProbe','tests/UniClaw.Kernel.Tests','tests/UniClaw.Host.Tests','tests/UniClaw.Host.Dsh.Tests'] for f in (ROOT/project/'bin/Debug/net10.0').glob('UniClaw*.dll')}
(HERE/'test-summary-v2.json').write_text(json.dumps(summary,indent=2)+'\n')
(HERE/'candidate-verification-binding-v2.json').write_text(json.dumps({'candidate':'candidate-inputs-v2.json','sourceHashes':candidate,'effectiveBuildHashes':inputs,'assemblyHashes':assemblies,'testHashes':{name:sha(HERE/(name+'-final-v2.trx')) for name,_,_ in commands}},indent=2)+'\n')
