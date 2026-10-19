"""Extract only trial-scoped accounting; never copy session content/credentials."""
import pathlib, json, subprocess, argparse, re
ROOT = pathlib.Path(__file__).resolve().parent

def nested_usage(value):
    found = []
    if isinstance(value, dict):
        if isinstance(value.get('usage'), dict): found.append(value['usage'])
        for k, v in value.items():
            if k != 'usage': found += nested_usage(v)
    elif isinstance(value, list):
        for v in value: found += nested_usage(v)
    return found

def trial_metrics(path):
    trial = json.loads(path.read_text()); sid = trial.get('sessionId')
    output = dict(trial); output['conditions'] = json.loads((path.parent/'experiment.json').read_text())
    output.update(modelCalls=0, failedOrInterruptedAttempts=0,
        callsWithReportedUsage=0, callsWithoutReportedUsage=0, providerReportedTokens={},
        submitDecisionCalls=0, acceptedSubmissions=0, rejectedSubmissions=0, rejectionReasons={}, submittedProposalKinds={},
        providerReportedCost=None, costAvailability='provider journal does not report monetary charges')
    if sid:
        files = list(pathlib.Path('/Users/fran/.dsh/sessions').glob(f'*/{sid}/session.v*.jsonl*'))
        if len(files) != 1:
            output['accountingError'] = f'expected one session generation; found {len(files)}'
        else:
            data = subprocess.check_output(['zstd','-dc',str(files[0])]) if files[0].suffix == '.zstd' else files[0].read_bytes()
            totals = {}
            for line in data.splitlines():
                try: event = json.loads(line)
                except ValueError: continue
                kind = event.get('type'); d = event.get('data',{})
                if kind == 'tool/call' and d.get('name') == 'submit_decision':
                    output['submitDecisionCalls'] += 1
                    try:
                        args = json.loads(d['arguments']) if isinstance(d.get('arguments'),str) else d.get('arguments',{})
                        proposal_kind = args.get('task',{}).get('payload',{}).get('kind','unknown')
                        if proposal_kind not in ('act','plan','continuePlan','policy','noAction','defer','skip','blocked'): proposal_kind='unknown'
                    except (ValueError,TypeError,AttributeError): proposal_kind='unknown'
                    output['submittedProposalKinds'][proposal_kind] = output['submittedProposalKinds'].get(proposal_kind,0)+1
                if kind == 'tool/result':
                    content = d.get('message',{}).get('content',[])
                    for block in content:
                        value=block.get('text','')
                        if value.startswith('submit_decision accepted:'): output['acceptedSubmissions'] += 1
                        match=re.match(r'submit_decision rejected: ([a-z0-9-]+)', value)
                        if match:
                            output['rejectedSubmissions'] += 1
                            reason=match.group(1); output['rejectionReasons'][reason]=output['rejectionReasons'].get(reason,0)+1
                if kind not in ('assistant/message','assistant/attempt'): continue
                output['modelCalls'] += 1
                d = event.get('data',{})
                if event['type'] == 'assistant/attempt' or d.get('interrupted'):
                    output['failedOrInterruptedAttempts'] += 1
                usages = [d['usage']] if isinstance(d.get('usage'), dict) else nested_usage(d.get('stream',[]))
                if usages:
                    output['callsWithReportedUsage'] += 1
                    for key, value in usages[-1].items():
                        if isinstance(value,(int,float)): totals[key] = totals.get(key,0) + value
                else: output['callsWithoutReportedUsage'] += 1
            output['providerReportedTokens'] = totals
    output['tokenAccountingComplete'] = bool(sid) and output['callsWithoutReportedUsage'] == 0 and 'accountingError' not in output
    bridge = pathlib.Path(trial.get('RunDir') or trial.get('runDir') or '')/'consult-bridge.json'
    if bridge.exists():
        exchanges = json.loads(bridge.read_text())['exchanges']; output['proposalKinds'] = {}
        for ex in exchanges:
            kind=(ex.get('decision') or {}).get('kind','no-response')
            output['proposalKinds'][kind]=output['proposalKinds'].get(kind,0)+1
        output['runtimeRejectedProposals'] = sum(ex.get('decision') is not None and ex.get('effectiveDecision') is None for ex in exchanges)
    trace_path = bridge.parent/'settings-trace.json'
    if trace_path.exists():
        trace = json.loads(trace_path.read_text())
        output['observationCycles'] = len(trace)
        output['fastPerceptionCalls'] = sum(e.get('fastInferenceSucceeded') is not None for e in trace)
        output['failedFastPerceptionCalls'] = sum(e.get('fastInferenceSucceeded') is False for e in trace)
        output['hierarchyCaptures'] = sum(e.get('hierarchyAvailable') is True for e in trace)
        output['slowTraceEntries'] = sum(bool(e.get('slow')) for e in trace)
        if output['slowTraceEntries']:
            output['accountingError'] = 'unexpected slow activity; inspect all slow sessions before claiming complete accounting'
    output['validTrial'] = (trial.get('driveStatus') == 'Completed' and trial.get('coverage') == 'CoverageComplete'
        and trial.get('modeConformant') is True and trial.get('error') is None and trial.get('cleanupError') is None
        and trial.get('verifiedSteps') == trial.get('expectedVerifiedSteps'))
    return output

if __name__ == '__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--baseline-only',action='store_true'); parser.add_argument('--output'); args=parser.parse_args()
    trials=[trial_metrics(p) for p in sorted(ROOT.glob('*/*/*/trial.json')) if not args.baseline_only or p.parent.parent.name=='Step']
    out=ROOT/(args.output or ('baseline-metrics.json' if args.baseline_only else 'all-trial-metrics.json'))
    out.write_text(json.dumps(trials,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({'output':str(out),'trials':len(trials),'validTrials':sum(t['validTrial'] for t in trials)}))
