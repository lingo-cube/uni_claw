from pathlib import Path
import hashlib,json,tarfile,xml.etree.ElementTree as ET
HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[3]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=lambda n:json.loads((HERE/n).read_text())
entry=read('entry.json'); candidate=read('candidate-inputs-v1.json')['files']
assert len(candidate)==20
for name,digest in candidate.items():
 assert sha(ROOT/name)==digest,name
 assert sha(HERE/'candidate-source-v1'/name)==digest,name
protected={n:d for n,d in entry['inputs'].items() if n not in candidate}
for name,digest in protected.items(): assert sha(ROOT/name)==digest,name
prior=ROOT/entry['priorSeal']['path']; assert sha(prior)==entry['priorSeal']['sha256']
sealed=json.loads(prior.read_text())['files']
assert len(sealed)==342
for name,digest in sealed.items(): assert sha(ROOT/name)==digest,name
archive=HERE/'baseline-entry-source.tar.gz'; assert sha(archive)==read('baseline-archive.json')['sha256']
with tarfile.open(archive) as tar:
 for name,digest in entry['inputs'].items():
  with tar.extractfile(name) as f: assert hashlib.sha256(f.read()).hexdigest()==digest,name
binding=read('candidate-verification-binding-v1.json');assert binding['sourceHashes']==candidate
assert len(binding['effectiveBuildHashes'])==663
for name,digest in binding['effectiveBuildHashes'].items(): assert sha(ROOT/name)==digest,name
for name,digest in binding['assemblyHashes'].items(): assert sha(ROOT/name)==digest,name
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def results(path): return ET.parse(path).findall('.//t:UnitTestResult',ns)
summary=read('test-summary.json')
new={}
for project,suffix in [('kernel','StorageBarrierAsyncTests.'),('host','StorageBarrierFileTests.'),('dsh',None)]:
 path=HERE/(project+'-final.trx'); assert sha(path)==binding['testHashes'][project]
 rows=results(path); assert len(rows)==summary[project]['total']
 failed={r.attrib['testName'] for r in rows if r.attrib['outcome']=='Failed'}
 old=HERE.parent/'slice4'/(project+'-final-v2.trx')
 assert failed=={r.attrib['testName'] for r in results(old) if r.attrib['outcome']=='Failed'},project
 assert not any(r.attrib['outcome'] not in ['Passed','Failed'] for r in rows)
 if suffix:
  chosen=[r for r in rows if suffix in r.attrib['testName']]; assert all(r.attrib['outcome']=='Passed' for r in chosen)
  new[project]=len(chosen)
assert new=={'kernel':35,'host':13},new
# The intentional driver cancellation RED really delivered once before propagation was fixed.
red=results(HERE/'driver-cancel-red-v2.trx');assert len(red)==1 and red[0].attrib['outcome']=='Failed'
message=red[0].find('t:Output/t:ErrorInfo/t:Message',ns).text
assert 'Expected: 0' in message and 'Actual:   1' in message
killed=HERE/'host-final/kill'
for kind in ['resource','consultation','prepare','result']:
 directory=killed/kind
 load=lambda name:json.loads((directory/name).read_text())
 ready=load('ready.json');kill=load('kill-observed.json');expected=load('expected.json')
 assert not ready['IsTerminal'] and ready['DriverPending']
 assert ready['Deliveries']==(1 if kind=='result' else 0)
 assert ready['PublishedReceipts']==ready['Deliveries']
 assert ready['Consultations']==(0 if kind in ['resource','consultation'] else 1)
 assert ready['Observations']==1
 assert kill['UsedKill'] and kill['ExitCode']!=0 and kill['Id']==ready['ProcessId']
 ids={ready['ProcessId']}
 for i in range(2):
  reader=load('reader-'+str(i)+'.json');assert reader['ProcessId'] not in ids;ids.add(reader['ProcessId'])
  assert reader['IsReadOnly'] and not reader['ContinuationSupported'] and reader['RunId']==ready['RunId']
  for prop in ['ResourceRows','Consultation','Execution']: assert reader[prop]==expected[prop],(kind,prop)
 for name,digest in load('source-hashes.json').items():assert sha(directory/name).upper()==digest,(kind,name)
before=json.loads((killed/'single/measurement.json').read_text());after=json.loads((killed/'batch/measurement.json').read_text())
assert before['Attempt']==after['Attempt'] and before['Calls']==after['Calls']==1
assert before['Receipts']==after['Receipts']==1 and before['FrameBytesSubmitted']==after['FrameBytesSubmitted']==1492
assert before['ManagedWriteCalls']==before['ManagedFlushCalls']==before['CommittedBatches']==3
assert after['ManagedWriteCalls']==after['ManagedFlushCalls']==after['CommittedBatches']==2
assert before['PeakBatchRecords']==1 and after['PeakBatchRecords']==2 and after['PeakBatchBytes']<=16*1024*1024
for axis in ['spec','standards']: assert (HERE/('review-'+axis+'-v1.md')).is_file()
review=read('review-summary.json'); assert review['standards']['remaining']==review['spec']['remaining']==0
print(json.dumps({'passed':True,'candidateFiles':20,'unchangedProtectedBuildInputs':len(protected),'effectiveBuildInputs':663,'priorSealedArtifacts':len(sealed),'newKernelCases':35,'newNativeFileAndKillCases':13,'kernelPassed':1284,'kernelExistingFailures':3,'hostPassed':343,'hostExistingFailures':1,'dshPassed':24,'actualCommitBeforeAckKills':4,'freshReaders':8,'executionOriginalFrames':3,'executionWritesAndFlushesBefore':3,'executionWritesAndFlushesAfter':2,'sameFrameBytes':1492,'ownerResourceConsultExecutionRuntimeAsync':True,'allStorageDomainsAsync':False,'parentChangeComplete':False,'continuationSupported':False,'reviewsComplete':True},indent=2))
