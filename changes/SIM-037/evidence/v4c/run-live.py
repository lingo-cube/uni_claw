#!/usr/bin/env python3
"""One preregistered real-model simulation on an owned DSH service; no device."""
from pathlib import Path
import datetime,hashlib,json,os,signal,subprocess,tempfile,time,xml.etree.ElementTree as ET
START=time.monotonic()
WORK_DEADLINE=START+570  # Startup and test share one budget; reserve 30 seconds for cleanup.
HERE=Path(__file__).resolve().parent
ENTRY=json.loads((HERE/'candidate-entry.json').read_text());SOURCE=Path(ENTRY['snapshot'])
LABEL='normal-006'
OUTPUT=HERE/'live'/LABEL
assert not OUTPUT.exists(), 'Original trial already exists; no repeated sampling.'
assert not (HERE/'live-execution.json').exists(), 'Original execution record already exists.'
def save(name,data): (HERE/name).write_text(json.dumps(data,indent=2)+'\n')
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def check_source():
    for rel,expected in ENTRY['files'].items(): assert digest(SOURCE/rel)==expected,rel
    for rel,expected in ENTRY['externalServiceFiles'].items(): assert digest(Path(rel))==expected,rel
def listeners():
    p=subprocess.run(['lsof','-t','-iTCP:3081','-sTCP:LISTEN'],capture_output=True,text=True)
    return [int(x) for x in p.stdout.split()]
config_paths=[Path.home()/'.dsh/profiles/uniclaw-product'/name for name in ['cordis.yml','cordis.patch.yml','package.json','.env']] + [Path.home()/'.dsh/.env']
def config_hashes(): return {str(p):digest(p) for p in config_paths if p.exists()}
before=config_hashes();service=None;test=None
summary={'began':datetime.datetime.now(datetime.timezone.utc).isoformat(),'case':LABEL,'modelAttemptStarted':False,'passed':False,'deviceStarted':False}
cleanup={'ownedServiceStopped':True,'ownedTestStopped':True,'configurationUnchanged':False,'errors':[]}
def interrupted(signum,frame): raise KeyboardInterrupt('Owned simulation interrupted')
signal.signal(signal.SIGTERM,interrupted)
signal.signal(signal.SIGINT,interrupted)
try:
    check_source()
    prereg=json.loads((HERE/'preregistration.json').read_text())
    assert prereg['case']==LABEL
    assert digest(HERE/'candidate-entry.json')==prereg['sourceEntrySha256'], 'source preregistration mismatch'
    assert digest(HERE/'publication.patch')==prereg['candidateDiffSha256'], 'publication preregistration mismatch'
    review=json.loads((HERE/'prelive-review.json').read_text());assert review['blockingFindings']==0
    for rel,expected in review['evidenceSha256'].items():assert digest(HERE/rel)==expected,rel
    for rel,expected in review['binaries'].items():assert digest(SOURCE/rel)==expected,rel
    assert not listeners(), 'ENVIRONMENT_UNAVAILABLE: dedicated 3081 is already owned; do not stop it.'
    private=Path(tempfile.mkdtemp(prefix='uniclaw-sim037-dsh-'))
    argv=['node','--import','tsx/esm','apps/cli/src/bin.ts','--profile','uniclaw-product','--patch','/var/folders/_3/lbphn3p95f3dj8ds3z1874q00000gn/T/uniclaw-sim037-v4-o1o7n8n3/service-override-v4c.yml','--port','3081','--host','127.0.0.1','--no-open']
    with (private/'service.log').open('wb') as log:
        service=subprocess.Popen(argv,cwd='/Users/fran/Documents/Code/dk-harness',stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    cleanup['ownedServiceStopped']=False
    save('service-lease.json',{'owned':True,'pid':service.pid,'argv':argv,'privateLog':str(private/'service.log'),'configHashes':before})
    for _ in range(60):
        assert time.monotonic()<WORK_DEADLINE, 'ENVIRONMENT_UNAVAILABLE: shared time budget exhausted during startup'
        assert service.poll() is None, 'ENVIRONMENT_UNAVAILABLE: owned DSH startup failed'
        if service.pid in listeners():break
        time.sleep(1)
    assert service.pid in listeners(), 'ENVIRONMENT_UNAVAILABLE: owned service did not listen'
    import urllib.request,urllib.error
    route_ready=False
    for _ in range(30):
        assert time.monotonic()<WORK_DEADLINE, 'ENVIRONMENT_UNAVAILABLE: route startup time exhausted'
        try:
            req=urllib.request.Request('http://127.0.0.1:3081/api/uniclaw-agent/handshake',data=b'{}',headers={'Content-Type':'application/json'},method='POST')
            try:
                with urllib.request.urlopen(req,timeout=2) as response:status=response.status;body=response.read().decode()
            except urllib.error.HTTPError as error:status=error.code;body=error.read().decode()
            if status!=404 and body.startswith('{'):
                route_ready=True
                break
        except (OSError,ValueError):pass
        time.sleep(1)
    assert route_ready, 'ENVIRONMENT_UNAVAILABLE: Product handshake route did not activate'
    save('environment-preflight.json',{'status':'passed','modelAvailability':'not assumed; formal call will prove or fail','deviceStarted':False,'noVisualServicesRequired':True,'formalFlowStartedAfterPreflight':True,'ownedDshPid':service.pid,'port':3081,'sourceEntrySha256':digest(HERE/'candidate-entry.json'),'configHashes':before})
    results=HERE/'live-test-results';results.mkdir(exist_ok=True)
    env=dict(os.environ,UNICLAW_SIM037_LIVE='1',UNICLAW_SIM037_OUTPUT=str(OUTPUT),UNICLAW_SIM037_MODE='complete',UNICLAW_UNIAGENT_DSH_BINDINGS=str(SOURCE/'.dsh/product/uniagent-prod-bindings.yaml'))
    args=['dotnet','test',str(SOURCE/'tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj'),'--no-build','--no-restore','--filter','FullyQualifiedName=UniClaw.Host.Tests.RealAgentFiniteMenuSimulationTests.RealAgentTraversesTheThreeLevelMenu','--logger','trx;LogFileName=normal-006.trx','--results-directory',str(results),'-m:1','-nr:false']
    summary['modelAttemptStarted']=True
    remaining=WORK_DEADLINE-time.monotonic()
    assert remaining>0, 'Shared startup/test time budget exhausted'
    with (HERE/'normal-006.console.log').open('wb') as log:
        test=subprocess.Popen(args,cwd=SOURCE,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
        try:code=test.wait(timeout=remaining)
        except subprocess.TimeoutExpired:
            os.killpg(test.pid,signal.SIGTERM)
            try:test.wait(timeout=5)
            except subprocess.TimeoutExpired:os.killpg(test.pid,signal.SIGKILL);test.wait(timeout=5)
            code=124;summary['hardTimeout']=True
    summary.update({'testArgs':args,'testPid':test.pid,'testExitCode':code})
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    trx=ET.parse(results/'normal-006.trx').getroot()
    counters=trx.find('t:ResultSummary/t:Counters',ns).attrib
    summary['counters']=counters
    summary['passed']=code==0 and all(int(counters[k])==1 for k in ['total','executed','passed']) and int(counters['failed'])==int(counters['notExecuted'])==0
    check_source()
except BaseException as error:
    summary.update({'error':str(error),'classification':'FORMAL_FAILED' if summary['modelAttemptStarted'] else 'ENVIRONMENT_UNAVAILABLE','passed':False})
finally:
    if test is not None and test.poll() is None:
        try:
            os.killpg(test.pid,signal.SIGTERM)
            try:test.wait(timeout=5)
            except subprocess.TimeoutExpired:os.killpg(test.pid,signal.SIGKILL);test.wait(timeout=5)
        except Exception as error:cleanup['errors'].append('owned test cleanup: '+str(error))
    cleanup['ownedTestStopped']=test is None or test.poll() is not None
    if service is not None:
        try:
            if service.poll() is None:
                os.killpg(service.pid,signal.SIGTERM)
                try:service.wait(timeout=10)
                except subprocess.TimeoutExpired:os.killpg(service.pid,signal.SIGKILL);service.wait(timeout=5)
            cleanup['ownedServiceStopped']=service.poll() is not None and service.pid not in listeners()
        except Exception as error:cleanup['errors'].append(str(error))
    cleanup['configurationUnchanged']=config_hashes()==before
    summary['elapsedSeconds']=time.monotonic()-START
    summary['withinExternal600SecondBound']=summary['elapsedSeconds']<=600
    summary['cleanup']=cleanup;summary['ended']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    summary['passed']=summary['passed'] and summary['withinExternal600SecondBound'] and cleanup['ownedTestStopped'] and cleanup['ownedServiceStopped'] and cleanup['configurationUnchanged'] and not cleanup['errors']
    save('live-execution.json',summary)
print(json.dumps(summary),flush=True)
raise SystemExit(0 if summary['passed'] else 1)
