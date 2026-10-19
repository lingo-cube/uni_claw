"""Freeze numeric gates only after Step data, before any Plan trial exists."""
import pathlib,json,statistics,hashlib,datetime,argparse
p=argparse.ArgumentParser();p.add_argument("--name",default="");p.add_argument("--provider");p.add_argument("--model");p.add_argument("--provider-sha");p.add_argument("--late-lock-deviation");a=p.parse_args();suffix="-"+a.name if a.name else ""
root=pathlib.Path(__file__).resolve().parent
path=root/f'thresholds{suffix}.json';assert not path.exists(), 'never overwrite preregistered gates'
source=root/f'baseline{suffix}-metrics.json';metrics=json.loads(source.read_text())
keys=['model','bounds','coverage','actionPolicyDigest','harnessAssemblySha256','schemaHash','kernelAssemblySha256','hostAssemblySha256','decisionChannelSourceSha256','profileRevision','promptHash','providerArtifactSha256','slowModelEnabled','observationAndVerification']
if any([a.provider,a.model,a.provider_sha]):
 assert all([a.provider,a.model,a.provider_sha]), 'pin provider, model and provider artifact together'
 metrics=[m for m in metrics if m['conditions']['model']=={'Provider':a.provider,'Name':a.model} and m['conditions']['providerArtifactSha256']==a.provider_sha]
 assert metrics, 'no matching pinned Step baseline'
latest=max(metrics,key=lambda m:(root/m['task']/'Step'/m['repeat']/'experiment.json').stat().st_mtime_ns);conditions={k:latest['conditions'][k] for k in keys}
recovery=None
if a.late_lock_deviation:
 assert all([a.provider,a.model,a.provider_sha]), 'late lock requires explicit model and artifact'
 recovery=json.loads((root/a.late_lock_deviation).read_text())
 assert recovery['baselineSha256']==hashlib.sha256(source.read_bytes()).hexdigest()
 assert recovery['planResultReadBeforeRecord'] is False and not recovery.get('resultUnblindedAtUtc')
 assert not recovery.get('thresholdsLockedAtUtc'), 'late lock is single use'
 assert (root/'model-change-pro/blind-gate-calculation.json').exists()
allowed_started=[]
base=[m for m in metrics if all(m['conditions'].get(k)==v for k,v in conditions.items())]
assert all(m['providerReportedTokens'].get('totalTokens',0)>0 for m in base), 'every baseline attempt must report a nonzero token lower bound'
for plan in list(root.glob('*/ShortPlan/*/experiment.json'))+list(root.glob('*/RulePlan/*/experiment.json')):
 exp=json.loads(plan.read_text())
 if all(exp.get(k)==v for k,v in conditions.items()):
  assert recovery is not None and plan.parent.relative_to(root).as_posix() in recovery['affectedPlans'], 'cannot register gates after this epoch Plan results'
  allowed_started.append(plan.parent.relative_to(root).as_posix())
if recovery is not None: assert sorted(allowed_started)==sorted(recovery['affectedPlans']), 'late lock permits only the documented unread attempts' 
assert all(all(m['conditions'][k]==v for k,v in conditions.items()) for m in base)
gates={}
for task,n in [('known-path',1),('candidates',2)]:
 samples=[m for m in base if m['task']==task]; valid=[m for m in samples if m['validTrial']];assert len(valid)==n
 gates[task]={'baselineSamples':[dict(repeat=m['repeat'],sessionId=m['sessionId'],valid=m['validTrial'],tokenAccountingComplete=m['tokenAccountingComplete'],callsWithoutReportedUsage=m['callsWithoutReportedUsage'],reportedTotalTokens=m['providerReportedTokens']['totalTokens']) for m in samples],'requiredValidSamplesPerPlanMode':n,'baselineTokenAccountingComplete':all(m['tokenAccountingComplete'] for m in samples),'tokenThresholdInterpretation':'reported baseline lower bound; a Plan token pass requires complete Plan accounting <= this bound; no exact percentage if baseline usage is missing','maxConsultationsPerCompletedTask':min(m['consultations'] for m in valid)-1,'maxModelCallsPerCompletedTask':min(m['modelCalls'] for m in valid)-1,'maxReportedTotalTokensPerCompletedTask':sum(m['providerReportedTokens']['totalTokens'] for m in samples)/n,'maxFormalMillisecondsPerCompletedTask':sum(m['formalElapsedMilliseconds'] for m in samples)/n,'baselineMedianFormalMilliseconds':statistics.median(m['formalElapsedMilliseconds'] for m in valid)}
result={'registeredAtUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'baselineMetricsFile':source.name,'baselineMetricsSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'conditions':conditions,'gates':gates,'method':'Per mode/task, all actual attempts including failed/rejected/corrected trials contribute consultations, calls, tokens and formal time; divide by number of valid completed tasks. Require planned valid sample count, same conditions, every Plan attempt token usage complete for token gate. Baseline reported tokens are a lower bound when usage is missing: complete Plan total <= baseline lower bound <= true baseline total proves no regression, without an exact savings percentage. Missing usage is never zero consumption. Fewer consultations AND actual model calls than the smallest valid Step sample required; token/time thresholds include every failed Step attempt amortized per valid completion. Tokens/time independently require no regression vs Step current-epoch mean per completed task. Monetary charges unavailable; no cost-saving claim. Small exploratory sample, no statistical or full-tree claim. Earlier pre-fix epochs retained separately in whole-experiment ledger.'}
result['registrationTiming']='late-lock-before-unblinding' if recovery else 'before-any-plan'
result['selectedBaselineExperiments']=[{'task':m['task'],'repeat':m['repeat'],'experimentSha256':hashlib.sha256((root/m['task']/'Step'/m['repeat']/'experiment.json').read_bytes()).hexdigest()} for m in base]
if recovery:
 blind=json.loads((root/'model-change-pro/blind-gate-calculation.json').read_text())['gates']
 for task in gates:
  for key,value in blind[task].items(): assert gates[task][key]==value, 'cannot change blind numeric gates'
 result['registrationDeviationFile']=a.late_lock_deviation
 result['alreadyStartedUnreadAttempts']=allowed_started
path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
if recovery:
 recovery['thresholdsLockedAtUtc']=result['registeredAtUtc'];recovery['thresholdsFile']=path.name;recovery['thresholdsSha256']=hashlib.sha256(path.read_bytes()).hexdigest();recovery['disposition']='Explicit result-blind late lock, independently reviewed; first attempt retains ordinary functional validity and full costs; not strict pre-Plan registration.'
 (root/a.late_lock_deviation).write_text(json.dumps(recovery,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(gates,ensure_ascii=False))
