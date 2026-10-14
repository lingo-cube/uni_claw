---
title: Settings 全遍历决策者验证与前沿报告 v2
date: 2026-10-15
status: VERIFIED_BOUNDED_BLOCKED
authority: NONE (analysis evidence only; not product/runtime authority)
---

> Status: VERIFIED_BOUNDED_BLOCKED
> Authority: NONE (analysis evidence only; not product/runtime authority)

# Settings 全遍历决策者验证与前沿报告

## 做了什么

这次把“完整遍历”从静态 `targetPages` 扩展成真实 observation 驱动的安全前沿：根页动态入口、非根页动态子入口、已访问入口、结构性跳过原因和 identity-only root 回退都进入账本。每轮由 Leader-file 生成一个精确到当前层级元素的单步决策，Host 继续执行 guard、Kernel grounding/effect、真实 ADB 和 post-action verification；语言检查继续按 capture/observationCycle 持久化。

代码入口：

- `src/UniClaw.Host/SettingsCoverage/SettingsCoverageConfig.cs`
- `src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.cs`
- `src/UniClaw.Host/SettingsCoverage/SettingsCoverageDirector.cs`
- `src/UniClaw.Host/SettingsCoverage/SettingsActionPolicy.cs`
- `product/tasks/settings-coverage.yaml`

## 原来的问题

`SETTINGS-COVERAGE-002` 的报告只完成了静态名单和有限二级页面。它不能回答根页中未列入配置的入口是否被发现，也不能区分“未观察到”“安全跳过”“已尝试但验证失败”。上轮把 Host policy 拒绝写成 bridge no-response，也把 Kernel terminal 和 coverage complete 混在同一叙述里。

## 这次修改

1. `dynamicRootEntries: all-visible` 从 canonical root observation 形成安全根入口 frontier；排除 switch、返回键、滚动容器、framework control、显式排除和静态已知入口。
2. `dynamicNestedEntries` 在已观察非根页内按一层 DFS 产生子入口 frontier；switch 只留在 skip ledger，不自动产生 toggle effect。
3. `CoverageReport` 增加 `DynamicRootFrontier/Visited/SkipReasons` 与 `DynamicNestedFrontier/Visited/SkipReasons`。
4. identity-only `android.settings` 只可作为安全 root fallback，不再产生新的根候选；未知页面且没有可用 scroll/待进入目标时由 Director 重观察，耗尽后 `bounded-stop:unknown-page`。
5. Host policy 删除“只要声明目标开关可见就冻结所有导航”的过宽规则；只有动作目标本身是 switch 时仍要求 typed `desiredState`。
6. 任务预算第一轮提高到 `96 steps / 112 consultations / 12 scrolls`，每个动态子页先限制 1 个入口，避免同层无界展开。

## 验证结果

相关测试：Settings coverage/action policy/ledger focused suites 通过（关键轮次 103、16、29、20 tests；最终构建 0 errors）。

真实运行摘要见 [manifest.json](/Users/fran/Documents/Code/spacex/uni_claw/evidence/settings-coverage-003/manifest.json)。关键结果：

| 轮次 | 实际结果 | 证据 |
|---|---|---|
| DSH characterization | DeepSeek 回合未调用 `submit_decision`，Host fail-closed | `dsh-pass1` |
| Leader pass 1 | 19/19 verified，有限 coverage set 100% | `leader-pass1` |
| Leader pass 2 | 23/23 verified；静态一级 7/7、动态根入口 3、二级路由 10/8、语言 findings 24 | `leader-pass2` |
| Leader pass 3 | Host 真实拒绝 `Fix connectivity`，原因是过宽 target-switch freeze | `leader-pass3` consult/facts |
| Leader pass 4 | 7 steps 后 identity-only route 导致错误深度递归 | `leader-pass4` |
| Leader pass 5 | 96/96 verified，但同一 fallback route 重复 90 次，未形成 coverage | `leader-pass5` |
| Leader pass 6–8 | 修复后连续重观察；hierarchy/scroll container 仍不可用，最终 `bounded-stop:unknown-page` | `leader-pass6`–`leader-pass8` |

因此，当前可以确认的是：声明的有限验收集可在真实设备上走通，动态前沿和语言检查链真实工作；不能确认 Android Settings 整棵可达树已经遍历完。报告中的 100% 只对该轮声明的有限集合成立，不是全树结论。

## 当前 gap 与阻碍

### 已修复

- 动态根入口以前完全依赖静态名单。
- 滚动/深度候选没有跨观察持久化。
- Host 在可点击导航目标旁有 switch 时错误拒绝导航。
- identity-only route 被当成普通子页，造成重复递归。

### 仍阻碍完整树闭包

- 真实设备在若干页面返回 `android.settings` identity-only observation；没有可用的 scroll container 或唯一导航目标时，Kernel 无法合法 grounding，Host 必须 safe-stop。
- 当前 dynamic nested 只支持一层、每页一个入口；更深层需要新的预算/恢复/路径唯一性证据，不能把无限 DFS 预先写成产品语义。
- 当前运行没有完成 checkpoint resume；重启后不能从 frontier 继续而只能重新启动 Settings。
- Slow perception 本轮被启用但没有产生可用于决策的独立 Slow 结果；它的能力注册和持久化链存在，但本报告不把“未触发”写成 Slow 通过。
- visual capability 没有被替代；视觉服务快路径产生了证据，但本轮不作视觉质量结论。

## 下一步工程动作

先补一个 Host/Perception recovery slice：在 identity-only observation 保留 route/viewport/capture checkpoint，只有重新取得可 grounding 的 hierarchy 才恢复 frontier；否则持久化 `blocked` 和待恢复 frontier。随后再把 `maxDepth` 从 1 提升到 2，先跑一轮小预算验证路径唯一性，再扩大预算。跨 Host resume 和完整现场恢复仍需独立 buyer/Change，不能由本轮测试结果自动收口。
