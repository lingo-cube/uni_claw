from pathlib import Path
import hashlib,json,os,subprocess,xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
entry=json.loads((HERE/'entry.json').read_text())
candidate=json.loads((HERE/'candidate-inputs-v1.json').read_text())['files']
for name,digest in candidate.items(): assert sha(ROOT/name)==digest,name
for name,digest in entry['inputs'].items():
 if name not in candidate: assert sha(ROOT/name)==digest,name
inputs={name:sha(ROOT/name) for name in entry['inputs']}
inputs.update({name:d for name,d in candidate.items() if name.endswith('.cs')})
previous=(HERE.parent/'slice4/reproduce.py').read_text()
host_filter=previous.split('host_filter = "',1)[1].split('"',1)[0]+'|FullyQualifiedName~RunResource|FullyQualifiedName~FileConsultationJournalTests|FullyQualifiedName~StorageBarrierFileTests'
commands=[('kernel','tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj',None),('host','tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj',host_filter),('dsh','tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj','FullyQualifiedName~SettingsCoverageEventSimulationTests|FullyQualifiedName~RuntimeToolHostProcedureTests')]
(HERE/'regression-selection.json').write_text(json.dumps(commands,indent=2)+'\n')
summary={};ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
env=dict(os.environ,UNICLAW_STORAGE_BARRIER_EXPORT=str(HERE/'host-final/kill'),UNICLAW_CONTROL_PLAN_ASYNC_EXPORT=str(HERE/'host-final/control-plan'),UNICLAW_WORLD_DELTA_EXPORT=str(HERE/'host-final/world-size'))
for name,project,selected in commands:
 command=['dotnet','test',project,'--no-restore','--logger','trx;LogFileName='+name+'-final.trx','--results-directory',str(HERE)]
 if selected: command+=['--filter',selected]
 assert not (HERE/(name+'-final.log')).exists()
 with (HERE/(name+'-final.log')).open('w') as output:
  result=subprocess.run(command,cwd=ROOT,env=env,stdout=output,stderr=subprocess.STDOUT)
 rows=ET.parse(HERE/(name+'-final.trx')).findall('.//t:UnitTestResult',ns)
 summary[name]={'exitCode':result.returncode,'total':len(rows),'passed':sum(r.attrib['outcome']=='Passed' for r in rows),'failed':[r.attrib['testName'] for r in rows if r.attrib['outcome']=='Failed']}
 print(json.dumps({name:summary[name]}),flush=True)
for name,digest in inputs.items(): assert sha(ROOT/name)==digest,name
assemblies={str(f.relative_to(ROOT)):sha(f) for project in ['src/UniClaw.Kernel','src/UniClaw.Host','tools/RunRecoveryProbe','tests/UniClaw.Kernel.Tests','tests/UniClaw.Host.Tests','tests/UniClaw.Host.Dsh.Tests'] for f in (ROOT/project/'bin/Debug/net10.0').glob('UniClaw*.dll')}
(HERE/'test-summary.json').write_text(json.dumps(summary,indent=2)+'\n')
(HERE/'candidate-verification-binding-v1.json').write_text(json.dumps({'candidate':'candidate-inputs-v1.json','sourceHashes':candidate,'effectiveBuildHashes':inputs,'assemblyHashes':assemblies,'testHashes':{name:sha(HERE/(name+'-final.trx')) for name,_,_ in commands}},indent=2)+'\n')
