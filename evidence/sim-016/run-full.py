import json,os,pathlib,subprocess,sys
root=pathlib.Path(__file__).resolve().parents[2]
meta=json.loads((root/'evidence/sim-016/final-snapshot.json').read_text());snap=pathlib.Path(meta['path'])
evidence=root/'evidence/sim-016'
env=dict(os.environ,NUGET_HTTP_CACHE_PATH=str(snap/'.nuget-http'),UNICLAW_REGRESSION_REPLAY_OUTPUT=str(evidence/'actual'))
with (evidence/'final-restore.txt').open('w') as out:
 restore=subprocess.run(['dotnet','restore','UniClaw.Kernel.slnx','--ignore-failed-sources','-p:NuGetAudit=false'],cwd=snap,env=env,stdout=out,stderr=subprocess.STDOUT)
if restore.returncode:
 (evidence/'full-exit.json').write_text(json.dumps({'restore':restore.returncode,'full':None})+'\n');sys.exit(restore.returncode)
with (evidence/'full.txt').open('w') as out:
 result=subprocess.run(['python3','tools/verify-change','SIM-016','--scope','full'],cwd=snap,env=env,stdout=out,stderr=subprocess.STDOUT)
(evidence/'full-exit.json').write_text(json.dumps({'restore':restore.returncode,'full':result.returncode})+'\n')
print({'snapshot':str(snap),'exit':result.returncode})
sys.exit(result.returncode)
