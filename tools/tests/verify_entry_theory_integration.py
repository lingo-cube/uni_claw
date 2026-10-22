#!/usr/bin/env python3
"""Run an isolated xUnit theory; prove exact list-tests/TRX joins with a real failure."""
import importlib.machinery
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
output = Path(sys.argv[1]).resolve()
output.mkdir(parents=True, exist_ok=True)
project = Path(tempfile.mkdtemp(prefix='verify-entry-theory-'))
csproj = ET.parse(ROOT / 'tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj')
for group in list(csproj.getroot()):
    if group.tag == 'ItemGroup' and not group.findall('PackageReference'):
        csproj.getroot().remove(group)
csproj.write(project / 'TheoryFixture.csproj')
shutil.copy2(ROOT / 'global.json', project / 'global.json')
source = r'''using Xunit;
namespace UniClaw.Verification.Fixture;
public class TheoryTests
{
    [Theory]
    [Trait("Scenario", "SCN-FIXTURE-001")]
    [InlineData("hello world", 1, true)]
    [InlineData("say \"hello\"", 2, false)]
    [InlineData("hello world", 3, true)]
    public void Case(string text, int count, bool expected) => Assert.True(expected);
}
'''
(project / 'TheoryTests.cs').write_text(source)
shutil.copy2(project / 'TheoryFixture.csproj', output / 'TheoryFixture.csproj')
(output / 'TheoryTests.cs').write_text(source)
commands=[]
def run(label,args):
    proc=subprocess.run(args,cwd=project,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    (output / (label+'.txt')).write_text(proc.stdout)
    commands.append({'label':label,'args':args,'rc':proc.returncode})
    return proc
assert run('build',['dotnet','build','TheoryFixture.csproj']).returncode == 0
listing=run('list-tests',['dotnet','test','TheoryFixture.csproj','--no-build','--no-restore','--list-tests','--filter','Scenario=SCN-FIXTURE-001'])
assert listing.returncode == 0
execution=run('execution',['dotnet','test','TheoryFixture.csproj','--no-build','--no-restore','--logger','trx;LogFileName=theory.trx','--results-directory',str(output)])
assert execution.returncode == 1, 'fixture must have an intentional failing case'
loader=importlib.machinery.SourceFileLoader('scenario_coverage', str(ROOT/'tools/scenario-coverage.py'))
spec=importlib.util.spec_from_loader(loader.name,loader); coverage=importlib.util.module_from_spec(spec);loader.exec_module(coverage)
with patch.object(coverage,'SIM_TESTS',project):
    mapped=coverage.scenario_test_map(['SCN-FIXTURE-001'],no_build=True)['SCN-FIXTURE-001']
trx=coverage.parse_trx(output/'theory.trx')
assert len(mapped)==3 and set(mapped)==set(trx), (mapped,trx)
assert sorted(trx.values())==['Failed','Passed','Passed']
assert coverage.derive_status([trx[n] for n in mapped])==('failing',None)
(output/'result.json').write_text(json.dumps({'project':str(project),'commands':commands,'mapping':mapped,'trx':trx,'derived':'failing','exactJoin':True},indent=2)+'\n')
print('PASS: real xUnit 3 display names exactly join TRX; 2 Passed + 1 Failed derives failing')
