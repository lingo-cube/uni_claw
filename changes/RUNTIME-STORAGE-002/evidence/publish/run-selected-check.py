#!/usr/bin/env python3
"""Verify the private publication source view with an external, source-bound AssetRun."""
import json,os,re,subprocess,sys,xml.etree.ElementTree as ET
from pathlib import Path
HERE=Path(__file__).resolve().parent
REPO=HERE.parents[3]
sys.path.insert(0,str(REPO/'tools'))
from test_assets import AssetRun,load_layout,fingerprints,atomic_json,digest,file_digest
label,mode=sys.argv[1:3]
if (HERE/(label+'.json')).exists():raise FileExistsError(label)
d=json.loads((HERE/'draft-run.json').read_text());view=Path(d['view']);env=dict(os.environ,GIT_INDEX_FILE=d['index'])
paths=subprocess.check_output(['git','diff','--cached','--name-only'],cwd=REPO,env=env,text=True).splitlines()
project={'host':'UniClaw.Host.Tests','kernel':'UniClaw.Kernel.Tests','dsh':'UniClaw.Host.Dsh.Tests'}.get(mode)
classes=[]
if project:
 for p in paths:
  f=view/p
  if p.startswith('tests/'+project+'/') and f.suffix=='.cs' and f.exists():
   classes += re.findall(r'public\s+(?:sealed\s+|partial\s+|abstract\s+)*class\s+(\w+)',f.read_text())
 if not classes:raise ValueError('empty test scope')
 selected='|'.join('FullyQualifiedName~'+c for c in sorted(set(classes)))
else:selected=mode
source,inputs,provenance=fingerprints(view,[],selected,label,False)
extra_paths=json.loads((HERE/'host-test-input-closure.json').read_text()).get('inputPaths',[])
def extra_inputs():return [{'path':p,'sha256':file_digest(view/p)} for p in extra_paths]
additional=extra_inputs()
provenance['additionalInputs']=additional
inputs=digest([inputs,additional])
provenance['publicationTree']=subprocess.check_output(['git','write-tree'],cwd=REPO,env=env,text=True).strip()
provenance['sourcePreservation']='private candidate index plus exact selected source map; source view external'
run=AssetRun(REPO,load_layout(REPO),'RUNTIME-STORAGE-002',label,source,inputs)
atomic_json(run.path('results','provenance.json'),provenance)
if project:
 args=['dotnet','test','tests/'+project+'/'+project+'.csproj','--no-restore','--filter',selected,'--logger','trx;LogFileName=tests.trx','--results-directory',str(run.directory('results')),'-m:1','-nr:false']
elif mode=='python':
 modules=['tools.tests.'+Path(p).stem for p in paths if p.startswith('tools/tests/') and p.endswith('.py')]
 args=[sys.executable,'-m','unittest',*sorted(set(modules)),'-v']
elif mode in ('build-world','build-replay'):
 build_project='tools/WorldStorageProbe/WorldStorageProbe.csproj' if mode=='build-world' else 'tools/EvolutionReplay/UniClaw.EvolutionReplay.Tests.csproj'
 args=['dotnet','build',build_project,'-p:NuGetAudit=false','-m:1','-nr:false']
else:raise ValueError(mode)
execution_env=os.environ.copy()
execution_env['PYTHONPATH']=str(view/'tools/tests')+os.pathsep+execution_env.get('PYTHONPATH','')
with run.path('logs','check.log').open('w') as output:code=subprocess.call(args,cwd=view,stdout=output,stderr=subprocess.STDOUT,env=execution_env)
counts={};trx=run.path('results','tests.trx');log=run.path('logs','check.log').read_text()
if trx.exists():counts=ET.parse(trx).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
elif mode=='python':
 m=re.search(r'Ran (\d+) tests?',log);counts={'total':int(m[1]) if m else 0,'passed':code==0}
elif mode.startswith('build-'):counts={'built':code==0}
after=fingerprints(view,[],selected,label,False)[0]
summary={'command':args,'exitCode':code,'counts':counts,'sourceFilesUnchanged':after==source and extra_inputs()==additional,'consumersCompleted':True,'scope':selected,'publicationTree':provenance['publicationTree']}
# Compiler diagnostics are also consumed by the Leader; compile failure stays a failure.
run.finish(summary)
result={'runId':run.run_id,'runRoot':str(run.root),**summary}
atomic_json(HERE/(label+'.json'),result)
print(json.dumps(result))
if code or source!=after or not counts:sys.exit(1)
