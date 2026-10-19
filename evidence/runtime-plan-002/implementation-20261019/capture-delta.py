"""Capture only this session's delta against the preserved dirty entry."""
import pathlib, json, hashlib, difflib, subprocess
root=pathlib.Path(__file__).resolve().parents[3]
base=pathlib.Path('/tmp/uniclaw-runtime-plan-002-entry-baseline')
entry=json.loads((pathlib.Path(__file__).parent/'entry.json').read_text())
pre={i['path']:i for i in entry['files']}
paths=[]
for pattern in ('src/**/*.cs','src/**/README.md','tests/**/*.cs','schemas/agt-002/*','product/prompt/uniagent-prod/*','dsh/uniclaw-decision-channel/**/*'):
 for p in root.glob(pattern):
  if p.is_file() and not any(x in p.parts for x in ('obj','bin','node_modules','.git')):paths.append(p.relative_to(root).as_posix())
paths+=['product/profiles/uniagent-prod.yaml','CONTEXT.md','docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md']
changes=[];diff=[]
for rel in sorted(set(paths)):
 p=root/rel;data=p.read_bytes();h=hashlib.sha256(data).hexdigest()
 if rel in pre:
  old=base/rel
  if h==pre[rel]['sha256']:continue
  if not old.exists():raise RuntimeError('missing preserved entry: '+rel)
  olddata=old.read_bytes()
 else:
  result=subprocess.run(['git','show','HEAD:'+rel],cwd=root,capture_output=True)
  olddata=result.stdout if result.returncode==0 else b''
  if data==olddata:continue
 changes.append({'path':rel,'entrySha256':hashlib.sha256(olddata).hexdigest(),'currentSha256':h,'new':not bool(olddata)})
 try:diff.extend(difflib.unified_diff(olddata.decode().splitlines(True),data.decode().splitlines(True),fromfile='entry/'+rel,tofile='current/'+rel))
 except UnicodeDecodeError:pass
out=pathlib.Path(__file__).parent
(out/'implementation-delta.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2)+'\n')
(out/'implementation-delta.patch').write_text(''.join(diff))
print(json.dumps({'files':len(changes),'patch':str(out/'implementation-delta.patch')}))
