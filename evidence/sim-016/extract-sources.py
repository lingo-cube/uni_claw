"""Explicit extraction index; does not infer popup truth or execute recorded commands."""
import pathlib,json,hashlib,struct
root=pathlib.Path(__file__).resolve().parents[2]
def asset(path):
 p=root/path;return {'path':str(path),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
def episode(id,base,indices,stepindex,date):
 trace=json.loads((root/base/'settings-trace.json').read_text());steps=json.loads((root/base/'coverage-steps.json').read_text())
 frames=[]
 for n,i in enumerate(indices):
  capture=trace[i]['captureId'];xml=base/'evidence'/f'{capture}.xml';png=base/'evidence'/f'{capture}.png'
  raw=(root/png).read_bytes();width,height=struct.unpack('>II',raw[16:24]);assert width>0 and height>0
  frames.append({'traceIndex':i,'captureId':capture,'role':'previous-recorded-post' if n==0 else 'trigger-post' if n==1 else 'subsequent-observation','xml':asset(xml),'png':asset(png),'width':width,'height':height})
 assert steps[stepindex]['postCaptureId']==frames[1]['captureId']
 return {'id':id,'date':date,'trace':asset(base/'settings-trace.json'),'steps':asset(base/'coverage-steps.json'),'triggerStepIndex':stepindex,'frames':frames,'missing':['independent pre-action capture association','recorded CANCEL action/receipt','recorded post-CANCEL frame','original per-frame capture timestamps'],'recordedClearance':False}
base=pathlib.Path('evidence/settings-coverage-004/live-fix/run-20261014-232314-431')
true=episode('sim-dialog',base,[5,6,7,8],5,'2026-10-14')
corpus=json.loads((root/'tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json').read_text());c=next(c for c in corpus['cases'] if c['id']=='default-apps')
base=pathlib.Path(c['settingsTrace']['path']).parent
trace=json.loads((root/c['settingsTrace']['path']).read_text());steps=json.loads((root/c['sequence']['path']).read_text())
indices=[next(i for i,r in enumerate(trace) if r['captureId']==c[k]['captureId']) for k in ['before','after']]
stepindex=next(i for i,s in enumerate(steps) if s.get('postCaptureId')==c['after']['captureId'])
false=episode('default-apps-cross-package',base,indices,stepindex,'2026-10-20')
fixture=root/'tests/UniClaw.Host.Tests/Fixtures/PopupReplay'
(fixture/'sources.json').write_text(json.dumps({'schemaVersion':'recorded-popup-source.v1','episodes':[true,false]},indent=2)+'\n')
print([(e['id'],len(e['frames'])) for e in [true,false]])
