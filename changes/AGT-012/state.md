# AGT-012 — Android Settings three-task real test set

lifecycle_state: closed · disposition: none · depth: standard · base: a81785d5

## Intent（WHAT/WHY）

建立一个可复用的真实 Android Settings 测试集，覆盖：找到指定菜单项、切换并复用
指定开关、带预生成禁止动作集合的安全菜单遍历。把“做不到”拆成 profile/context、
prompt adapter、UniAgent、runtime guard、perception/grounding 和 environment 六类
可定位结果。

## Scope / Out of Scope

- Scope：扩展 `testsets/android-settings/manifest.json` 三项任务和 fixtures；补齐
  任务目标、允许/禁止动作、幂等复用和失败归因的 README 契约；提供静态
  `forbidden-action-policy.json` fixture。
- Out of Scope：本 Change 不直接改 Host/Kernel 协议，不伪造真实运行证据，不把
  当前 coverage profile 误标为已支持 safeActionSet/forbiddenActionSet。

## Decisions

1. 任务一和任务二沿用现有 Android Settings traversal/live 链路。
2. 任务二的“复用”定义为重复执行不反向切换：已满足期望状态时必须只观察、不再点击。
3. 任务三要求 UniAgent 在首次咨询前拿到预生成的 safe/forbidden action policy；
   未接入前状态为 `PROFILE_CONTRACT_NOT_READY`。
4. 禁止动作由 runtime guard 最终执法；prompt 只负责传达，不拥有安全 authority。

## Acceptance

1. manifest 包含三项任务、逻辑 fixture 和 acceptance refs，manifest validator 通过。
2. README 对每项任务写清目标、允许动作、禁止动作、后置证据和失败归因。
3. 任务三明确记录当前 profile/context 缺口，不产生假 PASS。
4. 不修改 Product 源码或现有 live evidence。

## Verification

```yaml
level: CONTRACT
method: >-
  python3 tools/validate-testset-manifests.py;
  python3 -m json.tool testsets/android-settings/forbidden-action-policy.json >/dev/null;
  rg -n "find-network-menu|toggle-wifi-reuse|safe-full-traversal|safeActionSet|forbiddenActionSet|PROFILE_CONTRACT_NOT_READY" testsets/android-settings;
  git diff --check
expected: >-
  Android Settings manifest 通过；三项任务、禁止动作前置策略和失败归因均可检索；
  差异检查通过，且没有源码或 live evidence 改动。
actual: >-
  manifest validator 通过；三项任务、策略 fixture、失败归因和
  PROFILE_CONTRACT_NOT_READY 均可检索；git diff --check 通过，未修改源码或 live evidence。
evidence: >-
  testsets/android-settings/manifest.json；testsets/android-settings/README.md；
  testsets/android-settings/forbidden-action-policy.json；命令输出
```

## Status log

- 2026-10-05 · UNDERSTAND → RESOLVE · 复用现有 Android Settings testset、AGT-004/005/011 live 链路；确认当前 profile 没有禁止动作集合接缝。
- 2026-10-05 · RESOLVE → PERSIST → IMPLEMENT · 落地三项任务、fixtures、验收引用和失败归因契约；任务三保持 `PROFILE_CONTRACT_NOT_READY`。
- 2026-10-05 · IMPLEMENT → VERIFY → CLOSED · manifest validator 和差异检查通过；静态安全策略 fixture 已纳入测试集；任务三的 Runtime 接入另待独立 Change。
