"""Run one serial live trial against an already registered device lease."""
import argparse, pathlib, subprocess, os, json, datetime
p=argparse.ArgumentParser();p.add_argument("task", choices=["known-path","candidates"]);p.add_argument("mode",choices=["Step","ShortPlan","RulePlan"]);p.add_argument("repeat");a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[3]
out=pathlib.Path(__file__).resolve().parent/a.task/a.mode/a.repeat
assert not (out/"trial.json").exists(), "never overwrite a trial"
assert os.environ.get("UNICLAW_ANDROID_DEVICE") and os.environ.get("UNICLAW_ANDROID_RUN_DIR"), "registered device lease is required"
assert os.environ.get("UNICLAW_DSH_PROVIDER_ARTIFACT"), "loaded provider artifact is required"
out.mkdir(parents=True,exist_ok=True)
env=dict(os.environ,DSH_TEST_ADB_ENV="1",DSH_TEST_PERCEPTION_LIVE="1",UNICLAW_PLAN_LIVE_TASK=a.task,UNICLAW_PLAN_LIVE_MODE=a.mode,UNICLAW_PLAN_LIVE_REPEAT=a.repeat)
command=["dotnet","test","tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj","--no-build","--no-restore","-m:1","-p:BuildInParallel=false","--filter","FullyQualifiedName~RuntimePlanComparisonLiveTests","--logger","console;verbosity=normal","--","RunConfiguration.MaxCpuCount=1","xUnit.MaxParallelThreads=1","xUnit.ParallelizeTestCollections=false"]
started=datetime.datetime.now(datetime.timezone.utc).isoformat()
with (out/"test.log").open("w") as log:r=subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
(out/"invocation.json").write_text(json.dumps(dict(started=started,finished=datetime.datetime.now(datetime.timezone.utc).isoformat(),exitCode=r.returncode,command=command),indent=2)+"\n")
print(json.dumps(dict(task=a.task,mode=a.mode,repeat=a.repeat,exitCode=r.returncode)))
raise SystemExit(r.returncode)
