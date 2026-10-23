# Tracer：default-apps 跨包误判（SIM-016 → SIM-017）

> 用途：验证 `uniclaw-evolution-loop` 的循环机制，不是宣称分类 bug 已修。
> 分类语义是否改、怎么改，仍由 SIM-017 的 Owner 裁决。

## 1. 来源

| 项 | 值 |
|---|---|
| 红例归档 | `evidence/sim-016/classification-red.cs.txt`（sha256 `3315f71a…b0e7c`） |
| 失败输出 | `evidence/sim-016/classification-red.txt` / `.trx` |
| 录制 case | `tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json` 的 `default-apps` |
| 触发帧 | `evidence/runtime-plan-006/depth2/epoch-009/run-20261020-162121-900/evidence/capture-08173a863262439ca1bac49827ad4e6e.xml`（sha256 `322c8b47…228599`） |
| 设计边界 | `changes/SIM-017/design.md` |
| 组件契约 | `changes/SIM-016/component-contracts.md` |

红例测试名：
`UniClaw.Host.Tests.RecordedPopupClassificationTests.RecordedDefaultAppsPage_IsNotProofOfPopupSolelyBecausePackageChanged`

## 2. 事实

- `Default apps` 页 XML 的根 package 是 `com.android.permissioncontroller`，
  与 host `com.android.settings` 不同；但页面结构是设置类内容页：
  `content_parent` / `preference_fragment_container` / `recycler_view`。
- 真实弹窗（e1 语料）的根 package 也是 `com.android.permissioncontroller`，
  但含 `android:id/parentPanel` / `android:id/alertTitle` /
  `android:id/buttonPanel`。
- 当前分类器 `src/UniClaw.Host/SettingsPopupClassifier.cs:18`：
  `windowPackage != hostPackage → "present"`。所以 Default apps 页被误判。

## 3. 独立预期

- 来源：SIM-017 §“正反例与影响映射”第一行——跨包正常页只支持
  “package 不同”，覆盖关系 unknown，不得判 present。
- 断言：`SettingsPopupClassifier.Classify(xml, "com.android.settings", ids) != "present"`。
- 禁止：用分类器实际输出、Runtime 输出或本次 trace 反推预期。

## 4. FDP / Owner（预期）

| 项 | 预期 |
|---|---|
| First Divergence | `SettingsPopupClassifier.Classify` 的 package 分支（line 18）先于任何覆盖关系判断返回 `present` |
| Owner | `UniClaw.Host`（Host 观察/分类 adapter） |
| Failure class | B（grounding/semantic classification）或 C（authorization 前的误清障诱因）；以实际 FDP 为准 |
| 证据等级 | E2（单测 + 录制 XML）；不需要 E3/E4 |

## 5. 候选方向（仅候选）

最小方向：**先判覆盖窗结构，再判包名**。

```text
isDialog = XML 出现 android:id/parentPanel 或 alertTitle 或 buttonPanel
if (isDialog) return "present"
if (windowPackage != hostPackage) return null   // 跨包本身不构成 present
... 其余保持现有白名单/absent 逻辑
```

为什么它可能成立：真实弹窗有 dialog 结构；Default apps 页没有；现有
`RealPopupXml_ClassifiesPresent`、`WhitelistedResourceId...` 等测试不受影响。

为什么它不等于修复：

- 仍是 XML-only 启发式，不是 SIM-017 要求的窗口/覆盖关系证据；
- 返回 `null`（无声明）不等于 `unknown`；旧 present 在缺证后仍可能延续；
- 可能漏掉无 `parentPanel` 的真实覆盖窗；
- SIM-017 的五项 Owner 裁决未做。

因此本候选预期 `gate.status = decision-required`，不得直接合并。

## 6. 复现命令

```powershell
# 先把归档红例恢复为可编译测试（candidate 的一部分）
Copy-Item evidence/sim-016/classification-red.cs.txt tests/UniClaw.Host.Tests/RecordedPopupClassificationTests.cs

dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj `
  --filter "FullyQualifiedName~RecordedDefaultAppsPage_IsNotProofOfPopupSolelyBecausePackageChanged"
```

预期：第一次 RED（`present`），候选后 GREEN。

## 7. Gate 裁决项（来自 SIM-017）

1. 允许的窗口证据提供者与权限边界（系统窗口管理 / Accessibility / Host API）。
2. “足以证明 absent”的最小证据组合与冲突优先级。
3. unknown 的补采次数/时限复用哪一层预算；耗尽时 stop 还是等待人工。
4. Late evidence 是否只审计；若允许回填，需定义 cycle 未关闭且场景未变的可验证条件。
5. stale/unknown/conflict 是否建模为 popup 专用生命周期，还是复用通用 freshness。

## 8. 这条 tracer 证明什么 / 不证明什么

证明：循环能从归档失败走到 RED、FDP、候选、同 case GREEN、证据包；
Agent 能在无人逐步指挥下完成一轮。

不证明：live 分类已修、窗口证据已就位、旧 present 不会延续、模型/设备行为。
