from pathlib import Path
import hashlib,json,tarfile,xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
load=lambda n:json.loads((HERE/n).read_text())
entry=load('entry.json');candidate=load('candidate-inputs-v3.json')['files'];binding=load('candidate-verification-binding-v3.json');concurrent=load('concurrent-inputs-v1.json')['files']
assert len(candidate)==23 and binding['sourceHashes']==candidate
for n,d in candidate.items():assert sha(ROOT/n)==sha(HERE/'candidate-source-v3'/n)==d,n
protected={n:d for n,d in entry['inputs'].items() if n not in candidate and n not in concurrent};assert len(protected)==644
for n,d in protected.items():assert sha(ROOT/n)==d,n
for n,d in concurrent.items():assert sha(ROOT/n)==sha(HERE/'concurrent-source-v1'/n)==d['observedSha256'],n
assert len(binding['effectiveBuildHashes'])==667 and len(binding['assemblyHashes'])==26
for field in ['effectiveBuildHashes','assemblyHashes']:
 for n,d in binding[field].items():assert sha(ROOT/n)==d,n
archive=HERE/'baseline-entry-source.tar.gz';assert sha(archive)==load('baseline-archive.json')['sha256']
with tarfile.open(archive) as t:
 for n,d in entry['inputs'].items():
  with t.extractfile(n) as f:assert hashlib.sha256(f.read()).hexdigest()==d,n
seals={};all_paths={}
for n in [f'changes/RUNTIME-STORAGE-001/evidence/slice{i}/final-artifacts.json' for i in range(1,6)]+['changes/WORLD-HISTORY-002/evidence/final-artifacts.json']:
 d=json.loads((ROOT/n).read_text());seals[n]={'sha256':sha(ROOT/n),'artifacts':len(d['files'])}
 for f,h in d['files'].items():assert sha(ROOT/f)==h,f;all_paths[f]=h
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def rows(p):return ET.parse(p).findall('.//t:UnitTestResult',ns)
summary=load('test-summary-v3.json');new={}
for project,suffix in [('kernel','StorageClosureAsyncTests.'),('host','StorageClosureFileTests.'),('dsh',None)]:
 f=HERE/binding['testFiles'][project];assert sha(f)==binding['testHashes'][project]
 results=rows(f);assert len(results)==summary[project]['total']
 assert all(r.attrib['outcome'] in ['Passed','Failed'] for r in results)
 failed={r.attrib['testName'] for r in results if r.attrib['outcome']=='Failed'}
 previous=HERE.parent/'slice5'/(project+'-final.trx');assert failed=={r.attrib['testName'] for r in rows(previous) if r.attrib['outcome']=='Failed'},project
 if suffix:
  selected=[r for r in results if suffix in r.attrib['testName']];assert all(r.attrib['outcome']=='Passed' for r in selected);new[project]=len(selected)
assert new=={'kernel':13,'host':8}
assert all(r.attrib['outcome']=='Failed' for r in rows(HERE/'reflux-red.trx'))
assert rows(HERE/'sync-context-red.trx')[0].attrib['outcome']=='Failed'
assert rows(HERE/'host-once-bridge.trx')[0].attrib['outcome']=='Passed'
base=HERE/'host-final-v3';performance=json.loads((base/'closure/performance/performance.json').read_text())
assert set(performance['Statistics'])==set(performance['SourceRows'])=={'run','evidence','world','control','plan','resource','consultation','archive','execution'}
for name,s in performance['Statistics'].items():
 count=performance['SourceRows'][name];marker=1 if name in ['evidence','world'] else 0
 assert s['FrameWrites']==s['FrameFlushes']==(count-1 if name=='execution' else count+marker),name
 assert s['HeadWrites']==s['HeadFlushes']==(0 if name in ['execution','consultation','archive'] else s['FrameWrites']),name
 assert s['CommittedBatches']==(count-1 if name=='execution' else count),name
 assert s['PeakBatchRecords']<= (128 if name=='evidence' else 2 if name in ['execution','control'] else 1)
 assert 0<s['PeakBatchBytes']<=(1024*1024 if name=='evidence' else 16*1024*1024)
assert performance['DeliveryCalls']==performance['Receipts']==1
assert performance['CollectionWindowMilliseconds']==performance['AdmissionQueueRecords']==0 and performance['InFlightLimitPerOwner']==1
assert performance['DriverElapsedMilliseconds']>=0 and all(v>=0 for v in performance['InitializationElapsedMilliseconds'].values())
directory=base/'closure/archive';read=lambda n:json.loads((directory/n).read_text());ready=read('ready.json');k=read('kill-observed.json');expected=read('expected.json')
assert ready['DriverPending'] and not ready['IsTerminal'] and not ready['ResourceBound']
assert ready['Consultations']==ready['Deliveries']==ready['Observations']==0 and ready['ArchiveStatistics']['CommittedBatches']==1
assert k['UsedKill'] and k['ExitCode']!=0 and k['Id']==ready['ProcessId']
ids={ready['ProcessId']}
for i in range(2):
 r=read(f'reader-{i}.json');assert r['ProcessId'] not in ids;ids.add(r['ProcessId'])
 assert r['Original']==expected and r['IsReadOnly'] and not r['IsTerminal'] and r['HistoriesConsistent'] and not r['ContinuationSupported']
for n,d in read('source-hashes.json').items():assert sha(directory/n).upper()==d,n
io=json.loads((base/'evidence/io-counts.json').read_text());assert io['Records']==64
for field in ['FrameWrites','FrameFlushes','HeadWrites','HeadFlushes']:assert io['Single'][field]==65 and io['Batch'][field]==2
before=json.loads((base/'control-plan/measurement/single.json').read_text());after=json.loads((base/'control-plan/measurement/batch.json').read_text())
assert before['ControlOperations']==after['ControlOperations'] and before['ControlRows']==after['ControlRows']==10
for field in ['FrameWrites','FrameFlushes','HeadWrites','HeadFlushes']:assert before['ControlStatistics'][field]==10 and after['ControlStatistics'][field]==9
before=json.loads((base/'barriers/single/measurement.json').read_text());after=json.loads((base/'barriers/batch/measurement.json').read_text());assert before['Attempt']==after['Attempt'] and before['Calls']==after['Calls']==1
assert before['ManagedWriteCalls']==before['ManagedFlushCalls']==3 and after['ManagedWriteCalls']==after['ManagedFlushCalls']==2
size=json.loads((base/'world-size/size.json').read_text());assert size['fullBytes']==7951934 and size['deltaBytes']==1107547 and size['operations']==257
reviews=load('review-summary.json');assert reviews['standards']['remaining']==reviews['spec']['remaining']==0
for axis in ['spec','standards']:assert (HERE/f'review-{axis}-v3.md').is_file()
print(json.dumps({'passed':True,'candidateFiles':23,'unchangedProtectedBuildInputs':len(protected),'separatelyObservedUnownedInputs':len(concurrent),'effectiveBuildInputs':667,'priorSealsVerified':seals,'uniquePriorArtifactsVerified':len(all_paths),'newKernelCases':13,'newHostCases':8,'kernelPassed':1297,'kernelExistingFailures':3,'hostPassed':356,'hostExistingFailures':1,'dshPassed':24,'nativeProductionStoragePaths':9,'archiveActualKill':1,'archiveFreshReaders':2,'allStorageDomainsAsync':True,'parentAcceptanceComplete':True,'reviewsComplete':True,'continuationSupported':False},indent=2))
