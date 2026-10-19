"""Evaluate frozen numerical gates; retain every failed attempt in group costs."""
import pathlib,json,statistics,hashlib,argparse
p=argparse.ArgumentParser();p.add_argument("--name",default="");a=p.parse_args();suffix="-"+a.name if a.name else ""
root=pathlib.Path(__file__).resolve().parent
limits=json.loads((root/f'thresholds{suffix}.json').read_text())
assert hashlib.sha256((root/limits.get('baselineMetricsFile','baseline-metrics.json')).read_bytes()).hexdigest()==limits['baselineMetricsSha256']
trials=json.loads((root/'all-trial-metrics.json').read_text())
groups=[]
for task,g in limits['gates'].items():
 for mode in ['Step','ShortPlan','RulePlan']:
  samples=[m for m in trials if m['task']==task and m['mode']==mode and all(m['conditions'].get(k)==v for k,v in limits['conditions'].items())]
  same=all(all(m['conditions'].get(k)==v for k,v in limits['conditions'].items()) for m in samples)
  valid=[m for m in samples if m['validTrial']];n=len(valid)
  complete=bool(samples) and all(m['tokenAccountingComplete'] for m in samples)
  callsKnown=bool(samples) and all(m.get('sessionId') and m.get('modelCalls',0)>0 and not m.get('accountingError') for m in samples)
  totals={k:sum(m.get(k) or 0 for m in samples) for k in ['consultations','modelCalls','formalElapsedMilliseconds','elapsedMilliseconds','rejectedSubmissions','callsWithoutReportedUsage']}
  totals['reportedTotalTokens']=sum(m['providerReportedTokens'].get('totalTokens',0) for m in samples)
  normalized={k:v/n if n else None for k,v in totals.items()}
  gates={'plannedValidSampleCount':n>=g['requiredValidSamplesPerPlanMode'],'sameConditions':same,'fewerConsultations':n>0 and normalized['consultations']<=g['maxConsultationsPerCompletedTask'],'fewerActualModelCalls':n>0 and callsKnown and normalized['modelCalls']<=g['maxModelCallsPerCompletedTask'],'tokensNoRegression':n>0 and complete and normalized['reportedTotalTokens']<=g['maxReportedTotalTokensPerCompletedTask'],'formalTimeNoRegression':n>0 and all(m.get('formalElapsedMilliseconds') is not None for m in samples) and normalized['formalElapsedMilliseconds']<=g['maxFormalMillisecondsPerCompletedTask']}
  groups.append(dict(task=task,mode=mode,baselineTokenAccountingComplete=g.get('baselineTokenAccountingComplete',True),tokenProofBasis=g.get('tokenThresholdInterpretation','complete baseline accounting'),attempts=len(samples),validCompletions=n,tokenAccountingComplete=complete,actualCallAccountingComplete=callsKnown,totals=totals,perCompletedTaskIncludingFailures=normalized,successfulFormalMedian=statistics.median(m['formalElapsedMilliseconds'] for m in valid) if valid else None,gates=gates,allPlanGates=all(gates.values()) if mode!='Step' else None,samples=[dict(repeat=m['repeat'],valid=m['validTrial']) for m in samples]))
ledger={'attempts':len(trials),'validCompletions':sum(m['validTrial'] for m in trials),'modelCalls':sum(m['modelCalls'] for m in trials),'reportedTotalTokensLowerBound':sum(m['providerReportedTokens'].get('totalTokens',0) for m in trials),'missingUsageCalls':sum(m['callsWithoutReportedUsage'] for m in trials),'monetaryCost':None,'monetaryCostReason':'provider did not report charges; no monetary savings claim'}
result=dict(thresholdsRegisteredAt=limits['registeredAtUtc'],registrationTiming=limits.get('registrationTiming','before-any-plan'),registrationDeviationFile=limits.get('registrationDeviationFile'),groups=groups,wholeExperimentIncludingPriorEpochFailures=ledger,scope='exploratory known 2-action path and currently observed 2-candidate/4-action set; not full traversal, no statistical significance or general speedup claim')
(root/f'comparison-result{suffix}.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(result,ensure_ascii=False,indent=2))
