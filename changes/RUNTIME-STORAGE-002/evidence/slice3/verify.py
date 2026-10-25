#!/usr/bin/env python3
"""Read-only source/evidence verification; no device/model calls."""
import hashlib,json,tarfile
from pathlib import Path
from xml.etree import ElementTree as E
R=Path(__file__).resolve().parent
def load(p): return json.loads((R/p).read_text())
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def counts(p): return {k:int(v) for k,v in E.parse(R/p).getroot().find('{*}ResultSummary/{*}Counters').attrib.items()}
def run():
 b=load('build-binding-v4.json');c=load(b['candidate']);e=load('entry.json');checks={}
 checks['candidate']=all(sha(R/'candidate-v4'/p)==h for p,h in c['files'].items())
 checks['entry']=all(sha(R/'entry-source'/p)==h for p,h in e['files'].items())
 checks['archive']=sha(R/b['base_source_archive'])==b['base_source_archive_sha256']
 sources={}
 for layer in b['layers']:
  paths=layer.get('paths') or load(layer['manifest'])['files']
  for p in paths: sources[p]=R/layer['root']/p
 with tarfile.open(R/b['base_source_archive']) as a:
  checks['inputs1015']=len(b['inputs_sha256'])==1015 and all((sha(sources[p]) if p in sources else hashlib.sha256(a.extractfile(p).read()).hexdigest())==h for p,h in b['inputs_sha256'].items())
 for name,n in [('pure-host-v4.trx',181),('pure-dsh-v4.trx',50),('pure-kill-v4.trx',2)]:
  t=counts(name);checks[name]=t['passed']==t['total']==n and t['failed']==0
 checks['feature_red']=counts('public-tests/red-baseline/public-red.trx')['failed']==1
 checks['source_red']=counts('init-source-red/init-source-prefix-red.trx')['failed']==1
 checks['endpoint_red']=counts('integrity-tests/head-binding-red.trx')['failed']==1
 checks['malformed_red']=counts('integrity-tests/inspect-malformed-red.trx')['failed']==3
 for name in ['kill-settled','kill-pending']:
  p=R/'pure-kill-v4'/name;head=json.loads((p/'resource.journal.jsonl.head').read_text());k=json.loads((p/'kill-observed.json').read_text())
  checks[name]=k['UsedKill'] and k['ExitCode']!=0 and all(sha(p/'resource.journal.jsonl.snapshots'/ref['fileName']).upper()==ref['hash'] for ref in head['baselines'])
 comp=load('cost/comparison-v4.json')
 checks['same_input_cost']=len(comp['samples'])==3 and all(x['legacy']['inputRecordSha256']==x['versioned']['inputRecordSha256'] and x['legacy']['latest']==x['versioned']['latest'] and x['saved_bytes_ratio']<1 for x in comp['samples'])
 checks['reviews']=all((R/('review-'+axis+'-v4.md')).exists() for axis in ['spec','standards'])
 for folder,manifest in [('slice1','final-artifacts-v6.json'),('slice2','final-artifacts-v2.json')]:
  checks[folder+'_preserved']=all(sha(R.parent/folder/p)==h for p,h in load('../'+folder+'/'+manifest).items())
 if (R/'final-artifacts-v4.json').exists():checks['seal']=all(sha(R/p)==h for p,h in load('final-artifacts-v4.json').items())
 return {'passed':all(checks.values()),'checks':checks,'level':'DETERMINISTIC+SCENARIO','candidate':b['candidate'],'inputs':len(b['inputs_sha256']),'assemblies':len(b['assemblies_sha256']),'scope':'Resource first family only; no full-suite/live/model/device/parent-complete claim'}
if __name__=='__main__':
 result=run();print(json.dumps(result,ensure_ascii=False));raise SystemExit(0 if result['passed'] else 1)
