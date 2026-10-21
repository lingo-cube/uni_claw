"""Prepare/apply only the reviewed baseline fixes; preserve concurrent edits."""
from pathlib import Path
import difflib
import sys

root = Path(__file__).resolve().parents[2]
updates = {}

def edit(name, before, after):
    value = updates.get(name, (root / name).read_text())
    if after and after in value:
        return
    if before not in value:
        if after in value:
            return
        raise RuntimeError(f'baseline context changed: {name}')
    if value.count(before) != 1:
        raise RuntimeError(f'ambiguous baseline context: {name}')
    updates[name] = value.replace(before, after, 1)

sim = 'tests/UniClaw.Simulation.Tests/UniClaw.Simulation.Tests.csproj'
edit(sim, '    <Compile Include="..\\UniClaw.Host.Dsh.Tests\\PlanGenerationSemanticAssertions.cs" Link="PlanGenerationSemanticAssertions.cs" />\n', '')
edit(sim, '    <ProjectReference Include="..\\..\\src\\UniClaw.Agent.Dsh\\UniClaw.Agent.Dsh.csproj" />\n', '')
edit(sim, '\n</Project>', '\n  <ItemGroup>\n    <!-- DSH codec integration consumes the existing deterministic simulation seam. -->\n    <InternalsVisibleTo Include="UniClaw.Host.Dsh.Tests" />\n  </ItemGroup>\n</Project>')
host = 'tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj'
line = '    <ProjectReference Include="..\\..\\src\\UniClaw.Host.Dsh\\UniClaw.Host.Dsh.csproj" />'
edit(host, line, line + '\n    <ProjectReference Include="..\\UniClaw.Simulation.Tests\\UniClaw.Simulation.Tests.csproj" />')
old = 'tests/UniClaw.Simulation.Tests/PlanGenerationKernelConsumerTests.cs'
new = 'tests/UniClaw.Host.Dsh.Tests/PlanGenerationKernelConsumerTests.cs'
if not (root / new).exists():
    updates[new] = (root / old).read_text().replace('using UniClaw.Host.Dsh.Tests;', 'using UniClaw.Simulation.Tests;').replace('namespace UniClaw.Simulation.Tests;', 'namespace UniClaw.Host.Dsh.Tests;')
    updates[old] = None
edit('docs/analysis/ui-automation-test-function-reuse-inventory-2026-10-20.md', '[生成计划仿真消费](../../tests/UniClaw.Simulation.Tests/PlanGenerationKernelConsumerTests.cs)', '[DSH 协议与计划仿真消费](../../tests/UniClaw.Host.Dsh.Tests/PlanGenerationKernelConsumerTests.cs)')
observer = 'tests/UniClaw.Agent.Dsh.Tests/ObserverProjectionTests.cs'
edit(observer, '// 2026-10-06 所有者指令：默认模型 deepseek-flash（deepseek-official\n        // 直连）；CAP-012 D9：专线按 docs/agents/test-emulator.md 注册 3081。', '// 2026-10-20 所有者授权：默认选择 CommandCode Flash；\n        // CAP-012 D9：专线按 docs/agents/test-emulator.md 注册 3081。')
edit(observer, 'Assert.Equal("deepseekFlash", bindings.SelectedModelKey);', 'Assert.Equal("commandcodeFlash", bindings.SelectedModelKey);')
edit(observer, 'Assert.Equal(new ModelConfiguration("deepseek-official", "deepseek-flash"), bindings.Model);', 'Assert.Equal(new ModelConfiguration("commandcode", "deepseek/deepseek-v4.1-flash"), bindings.Model);')
peer = 'tests/UniClaw.Agent.Dsh.Tests/DshOpenedHttpPeerTests.cs'
edit(peer, '"promptHash":"28dda7e96ffde4eb44c45677f8a103f20305d85ef1a13eb076596a2b17d09e6e"', '"promptHash":"<prompt>"')
value = updates.get(peer, (root / peer).read_text())
needle = '.Replace("<manifest>", expected.Protocol.CapabilityManifestHash)'
if '.Replace("<prompt>"' not in value:
    if value.count(needle) != 2:
        raise RuntimeError('handshake fixture count changed')
    updates[peer] = value.replace(needle, needle + '\n                .Replace("<prompt>", UniAgentProfileYaml.LoadDefault().Assembly!.PromptManifest!.Hash)')
patch = []
for name, value in updates.items():
    current = (root / name).read_text() if (root / name).exists() else ''
    patch.extend(difflib.unified_diff(current.splitlines(True), (value or '').splitlines(True), fromfile='a/' + name if current else '/dev/null', tofile='b/' + name if value is not None else '/dev/null'))
(root / 'evidence/eng-opt-001/baseline-local-only.patch').write_text(''.join(patch))
if '--apply' in sys.argv:
    for name, value in updates.items():
        if value is None:
            (root / name).unlink()
        else:
            (root / name).write_text(value)
print(('applied' if '--apply' in sys.argv else 'prepared'), len(updates), 'scoped baseline files')
