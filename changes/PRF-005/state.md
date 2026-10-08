# PRF-005 — PRF-4：危险动作策略升格产品声明面

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

设计文档切片 PRF-4（Q14 裁决）：`forbidden-action-policy.json` 从 testset
fixture 升格为有 owner、有版本、可机械校验的产品 policy 工件——canonical
落 `product/policy/`，消费方加载点 fail-closed 执法（分类底线/修订号/
默认拒绝），不预造 Kernel 公共缝、不进 tools/。

## Scope

- canonical：`product/policy/android-settings-forbidden-actions.json`
  （policyRef=product/android-settings/forbidden-actions；
  **safetyPolicyRevision=1**；分类底线七类齐全）。
- loader（`SettingsActionPolicy.Load`，消费方=Host 加载点）新增执法：
  ① safetyPolicyRevision 必须为正整数；② **分类底线**
  （destructive/account-removal/credential-change/developer-debug/
  permission-grant/unknown-action）必须 ⊆ forbiddenActionClasses，缺失拒载
  （策略不得缩小危险面）；③ revision 入 digest canonical 串与
  AgentProjection（audit 面）。
- 记录类型增 `SafetyPolicyRevision` 字段（两处直接构造随迁）。
- 引用切换：`.dsh/profiles/settings-coverage.yaml` actionPolicy.path →
  product/policy/…；testset manifest fixtureRef 同步；删除
  `testsets/android-settings/forbidden-action-policy.json`（单一 canonical，
  无双副本）。
- 测试：canonical 加载/投影断言 + 缺 revision 拒载 + 缺底线类拒载。

## Out of Scope

- Kernel/Effect Guard 公共缝（buyer-first：等第二个消费者）。
- 其他平台 policy（仅 Android Settings 在册）。
- PRF-005 envelope 的 safetyPolicyRevision 落盘（下一片）。

## Decisions

1. 分类底线=六类危险词汇的机械清单（Q14「分类完备」的定义），不是可配置项。
2. digest 覆盖 revision（同内容不同 revision=不同指纹，审计可分辨）。

## Acceptance

1. 非法 policy（缺 revision/缺底线类）拒载（fail-closed 用例绿）。
2. Settings coverage 行为与既有测试零回归（Host.Tests 全绿）。
3. policy 带 safetyPolicyRevision 且进投影。
4. canonical 单副本：grep 旧路径仅历史 run 产物命中。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | SettingsActionPolicy/Coverage 套件 | 新三用例 + 既有全过 | PASS；85/85 | `tests/UniClaw.Host.Tests/SettingsActionPolicyTests.cs` |
| CONTRACT | canonical/路径/单副本 | product/policy 在册；旧路径仅 run 产物 | PASS | `.dsh/profiles/settings-coverage.yaml`；grep |
| SCENARIO | 全解 + 重封 | 全绿 0 违规 | PASS 1471/1471；certify 20 块 --change PRF-005 | `dotnet test UniClaw.Kernel.slnx` |

## Status log

- 2026-10-08 · UNDERSTAND → RESOLVE → PERSIST → IMPLEMENT → REVIEW →
  VERIFY → CLOSED · 一片内闭合：loader 已有相当 fail-closed 基础（AGT-013），
  本片补齐 revision/底线/位置三要素；Host.Tests 200/200、全解 1471 绿。
