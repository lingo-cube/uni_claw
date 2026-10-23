# EVO-001 · Cycle-01 证据包（人读摘要）

> Case：`default-apps-cross-package`（SIM-016 归档红例 → SIM-017 设计边界）
> 机器面：`packet.json`。本文件不新增结论。

## 打开 IDE 看什么

| 看哪 | 路径 |
|---|---|
| 出错的判定 | [SettingsPopupClassifier.cs](src/UniClaw.Host/SettingsPopupClassifier.cs#L17-L18)（base 版：跨包即 present） |
| 候选改法 | [candidate.diff](changes/EVO-001/evidence/cycle-01/candidate.diff) / [candidate-files/](changes/EVO-001/evidence/cycle-01/candidate-files) |
| 红例（逐字恢复） | [classification-red.cs.txt](evidence/sim-016/classification-red.cs.txt) → diff 中的新测试方法 |
| 设计边界 | [SIM-017 design](changes/SIM-017/design.md) |
| 循环方法 | [uniclaw-evolution-loop SKILL](.agents/skills/uniclaw-evolution-loop/SKILL.md) |

## 原来哪里错

`Classify` 第一步就拿"根节点 package ≠ hostPackage"当覆盖窗证明返回
`present`。Default apps 页由 `com.android.permissioncontroller` 提供但结构是
设置内容页（`preference_fragment_container`/`recycler_view`，无任何 dialog
id）→ 误判 present → 触发无谓清障。真实弹窗（e1）同包名但含
`parentPanel`/`alertTitle` —— 包名从来区分不了这两者。

## 这次候选怎么改（未应用，Gate 待裁）

白名单（dialog 结构 id）**先于**包名判定；跨包且无白名单命中 → 返回
`null`（无声明）而非 `present`。同一 case：红 → 绿；known-gap 测试期望随
致因 change 迁移（C8）。应用方式：`git apply …/candidate.diff`。

## 证明链（method/expected/actual/evidence）

| 步 | method | expected | actual | evidence |
|---|---|---|---|---|
| RED | 定向单测（见 packet.json command） | 失败，消息含 `asserted as popup: present`，与 SIM-016 归档一致 | exit 1，消息逐字一致 | red.trx / red.stdout.txt |
| FDP | 证据+源码核对 | 分歧在 package 分支早于结构证据 | line 18 先返回 | packet.json `fdp` |
| GREEN | 同 filter + 组件回归（红测 + known-gap + 分类器 9 例） | 全过；两次一致 | 18/18 × 2 次 | green.trx / green.stdout.txt |
| 对照 | 还原候选后跑原失败测试 | 若仍失败 → 扩大回归的红与候选无关 | 2/2 仍失败（hash-mismatch） | control-no-candidate.trx |
| 边界 | git status 两文件回 HEAD；无 commit/push/晋升 | 主树零污染 | 干净 | 见 state.md A7 |

## 新发现（2026-10-23 归因修正）

初判"56a75fd7 后 SIM-014 pins 未随迁、主树既有红"**有误**，复核后的事实链：

- HEAD 是绿的：HEAD 的 `WorldBeliefRevision.cs`（879ff136）与 PopupReplay pin 一致，56a75fd7 当时已正确随迁；
- 红来自工作区**在途未提交**的 WORLD-VISUALIZATION-001：改了 WorldBeliefRevision/WorldModel/UniKernel + 新增两个文件，重签了 ComponentReplay manifest（30/30 绿验证有效），**漏签** Host.Tests 的 PopupReplay manifest → 弹窗回放线只在含在途变更的工作区红；
- 承接方：该会话提交前补签（其自身 VERIFY 面），EVO-001 不接管。

过程缺陷（摩擦记录）：cycle-01 对照实验只排除了"本候选致红"，未排查工作区其他在途变更 —— 对照步骤应包含在途变更清单，第二次同类摩擦触发 A8 落进 skill。

## Gate（未清，Uniflow 不得 CLOSED）

1. SIM-017 五项 Owner 裁决。
2. 是否接受过渡语义（白名单优先 + 跨包无声明），还是等窗口证据链。
3. known-gap 期望迁移是否随本候选批准。
4. SIM-014 pins 漂移的承接 owner。

## 循环自身的摩擦记录（A8 候选素材）

- DSH 沙箱命名管道限制 → MSBuild 多节点/testhost 死锁；需 `-m:1 -nr:false`
  写进 skill 的运行注意事项（本轮已记录，未改 SKILL.md，等第二次同类摩擦触发 A8）。
- pwsh 管道合并原生命令输出在多项目构建下有死锁风险 → 用 `*> file` 替代。
