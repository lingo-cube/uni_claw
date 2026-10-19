"""Post-run factual audit; does not change trial/gate results."""
import datetime, hashlib, importlib.util, json, pathlib, subprocess

HERE = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('batch', HERE/'live_batch.py')
batch = importlib.util.module_from_spec(spec)
spec.loader.exec_module(batch)

def read(path): return json.loads(path.read_text())
def walk(value):
    if isinstance(value,dict):
        yield value
        for child in value.values(): yield from walk(child)
    elif isinstance(value,list):
        for child in value: yield from walk(child)

rows=[]
for path in batch.paths():
    item=batch.metrics(path)
    root=pathlib.Path(item['RunDir'])
    files=list(pathlib.Path('/Users/fran/.dsh/sessions').glob(f"*/{item['sessionId']}/session.v*.jsonl*"))
    assert len(files)==1
    data=subprocess.check_output(['zstd','-dc',str(files[0])]) if files[0].suffix=='.zstd' else files[0].read_bytes()
    events=[json.loads(line) for line in data.splitlines()]
    calls=[e for e in events if e.get('type') in ('assistant/message','assistant/attempt')]
    positive=0
    for event in calls:
        d=event.get('data',{})
        usage=[d['usage']] if isinstance(d.get('usage'),dict) else batch.accounting.nested_usage(d.get('stream',[]))
        positive += bool(usage and usage[-1].get('totalTokens',0)>0)
    assert positive == item['modelCalls']
    selections=[e['data'] for e in events if e.get('type')=='model/selection']
    assert selections and all(s['provider']=='deepseek-official' and s['model']=='deepseek-flash'
        and s.get('reasoningEffort')=='off' for s in selections)
    preflight=read(root/'environment-preflight.json')
    assert preflight.get('formalFlowStartedAfterPreflight') is True
    steps=read(root/'coverage-steps.json')
    execution=read(root/'plan-execution.json')
    projections=read(root/'plan-candidate-projections.json')
    decisions=read(path.parent/'model-decisions.json')
    first=(decisions[0].get('decision') or {}) if decisions else {}
    first_items=(first.get('proposal') or {}).get('items',[])
    kinds=sorted({v['kind'] for v in walk(first_items) if 'kind' in v})
    literal_targets=[v['targetDescriptor'] for v in walk(first_items) if v.get('kind')=='act'
        and v.get('targetDescriptor') not in (None,'$candidate')]
    projected_scopes=sorted({p['facts']['scopeKey'] for p in projections if p.get('source')=='settings.current-navigation'})
    evaluations=(execution or {}).get('conditionEvaluations') or []
    completed_scope=(item.get('driveStatus')=='Completed' and item.get('coverage')=='CoverageComplete'
        and item.get('error') is None and item.get('cleanupError') is None)
    unique_receipts={s['receiptId'] for s in steps if s.get('receiptId')}
    sockets=read(path.parent/'socket-witness.json')
    row=dict(task=item['task'],mode=item['mode'],repeat=item['repeat'],sessionId=item['sessionId'],valid=item['validTrial'],
        completedScope=completed_scope,modeConformant=item['modeConformant'],firstPlanKinds=kinds,
        actualCallsWithPositiveReportedUsage=positive,
        actualModelSelections=selections,accountingComplete=batch.accounted(item),
        formalAfterPreflight=True,verifiedTargets=[s['targetDescriptor'] for s in steps if s.get('verified')],
        routeTransitions=[dict(target=s['targetDescriptor'],before=s['routeBefore'],after=s['routeAfter']) for s in steps],
        uniqueReceiptCount=len(unique_receipts),receiptCount=sum(bool(s.get('receiptId')) for s in steps),
        clickedSwitches=[s['targetDescriptor'] for s in steps if s.get('targetRole')=='switch'],
        firstPlanLiteralTargets=literal_targets,currentPageProjectionScopes=projected_scopes,
        evaluatedConditions=sorted({v['condition'] for v in evaluations}),conditionEvaluationCount=len(evaluations),
        directlyWitnessed443Addresses=sockets['directlyWitnessed443Addresses'])
    if item['mode']=='ComposedPlan' and completed_scope:
        assert first.get('kind')=='plan' and evaluations
        assert all(s.get('targetRole')!='switch' for s in steps)
        if item['task']=='nested-scope':
            assert len(projected_scopes)>=2, 'nested traversal must actually consume multiple new-page candidate facts'
            assert not any(t in ('Internet','Pair new device') for t in literal_targets), 'future child names were prelisted'
    rows.append(row)
output=HERE/'formal-audit.json'
assert not output.exists()
output.write_text(json.dumps(dict(checkedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),trials=rows),ensure_ascii=False,indent=2)+'\n')
print(json.dumps(dict(trials=len(rows),valid=sum(r['valid'] for r in rows),allExpectedModel=True,
    allCostAccountingComplete=all(r['accountingComplete'] for r in rows),
    trialsWithDirectSocketWitness=sum(bool(r['directlyWitnessed443Addresses']) for r in rows))))
