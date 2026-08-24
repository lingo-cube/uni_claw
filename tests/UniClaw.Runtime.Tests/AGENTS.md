# Test Area Map — UniClaw.Runtime.Tests

> 本文件是测试区域的 **map, 不是 manual**。
> **Tests are evidence, not obstacles** — 测试验证能力，不验证脚本。
> 本文件不定义架构规则；测试协议见 `.ai/development-protocol.md` §17.4 与 `.ai/reviews/change-review.md`。
> 上级入口: 根 [AGENTS.md](../../AGENTS.md)（Single Source of Truth）。

## 1. Responsibility

`UniClaw.Runtime.Tests` 是 Runtime 的验证证据层：

- Unit / Architecture / Scenario / Integration 测试
- 机械 Guard（`Architecture/ArchitectureGuardTests.cs`）+ 确定性场景（Fake Environment）
- 真机验证（RealDevice collection，串行化）

## 2. 允许 / 禁止

**允许：**

- 添加 scenario test — 验证**能力**（coverage / authorization / fail-closed / 一致性 / evidence sufficiency），
  不验证脚本（禁止固定点击数量 / 固定 ActionHistory / 固定页面路径 / 固定坐标 / 固定 UI 文案）
- 增加验证（新的断言、新的证据检查）
- 修复 test harness / fixture（仅当失败分类为 TEST_HARNESS，见 `.ai/development-protocol.md` §8）

**禁止：**

- 修改 assertion 隐藏失败
- 删除失败 case（先分类：IMPLEMENTATION vs TEST_HARNESS；架构/spec 问题不能当 implementation 修）
- 降低验证标准（不放宽 fail-closed 断言）
- 把测试写成脚本（固定行为序列）
- 向 production 泄漏 Fake / ScriptedEnvironment 内部状态

## 3. 入口

- 根: `../../AGENTS.md`（SSOT · Authority Order · Definition of Done）
- 测试协议: `.ai/development-protocol.md` §17.4（Validation Rules）· §11（Verification Rhythm）
- 评审: `.ai/reviews/change-review.md`（四象限：Authority / Evidence / Boundary / Testing）
- 调试方法: `.ai/skills/evidence-driven-debugging`（E0-E4 · Debugging Gate）

## 4. 验证

- `dotnet test src/UniClaw.Runtime.sln`（all green + guards）
- `scripts/check-consistency.sh` ALL PASS
