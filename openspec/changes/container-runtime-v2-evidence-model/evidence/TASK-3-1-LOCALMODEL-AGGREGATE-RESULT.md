# Stage C1 Task 3.1 — LocalModel Aggregate 结果

> Change: `container-runtime-v2-evidence-model`
> Task: `3.1`（依赖后移裁决后的首个 C1 任务；2.6/2.7/2.8 状态见 tasks.md 内裁决注记）
> 日期: 2026-09（apply 会话）
> 结论: **3.1 完成，停在 Leader Gate**（不自动进入 3.2）

## 1. 修改文件

| 文件 | 变更 |
|---|---|
| `src/UniClaw.Runtime/Model/LocalModel.cs` | NEW：`NodeLocalModel`（per-Node 不可变聚合）、`CanonicalProjection`（只读骨架，仅 revision 标记，内容待 3.2/3.3）、`RegionCoverageProjection` / `ContainerCoverageProjection`（只读骨架，evidence refs + 可选 exhaustion，派生 `Exhausted` fail-closed） |
| `src/UniClaw.Runtime/Model/ContainerRuntimeV2.cs` | `ContainerRuntimeV2State` 增 `LocalModels`（`ImmutableArray<NodeLocalModel>`，每 Node 至多一个）；`LocalModelAppendInput`（append/archival 候选）；`ContainerRuntimeV2Reducer.PrepareLocalModelAppend`（同一 reducer 静态 seam 上的新入口，非第二 reducer） |
| `tests/UniClaw.Runtime.Tests/Unit/LocalModelAggregateTests.cs` | NEW：12 项聚焦测试 |
| `openspec/changes/.../tasks.md` | 记录 Human Gate 依赖裁决（2.6→3.3 后；2.7 待裁决；2.8 分解延后至 3.1/4.4/4.5）；勾选 3.1 |

## 2. 不可变状态所有权与 NET_NEW_MUTABLE_TRUTH

- **owner**：`NodeLocalModel` = Repository Mapping 预告的 NET_NEW_MUTABLE_TRUTH **+1 集中式** container-local canonical world owner。每 Graph node 恰好一个模型；无第二 inventory/canonical/coverage owner。
- **提交路径**：唯一 —— 现有 `ContainerRuntimeV2Reducer` 静态 seam 的 `PrepareLocalModelAppend`，一次不可变**整体替换**（构造完整 next state 后一次性 Accepted；拒绝返回 exact prior state reference，`Assert.Same` 验证）。
- **revision**：复用现有 `SemanticEvidenceRevision`（单一流）；stale/相等 revision 精确拒绝。
- **未新增**：第二 reducer、第二 revision 流、mutable cache、live handle、双写 owner、LogicalItem（3.2）、SemanticReconciler（3.3）、correlation 判断（2.6 延后）、Container 状态删除/迁移（2.8 延后）、admission/grounding/coverage/completion 消费（4.x）。生产 authority 零切换。
- Canonical/Coverage 投影为**只读骨架**：canonical 仅携带 revision 标记；coverage exhaustion 派生视图 **fail-closed**（空投影集 → NOT exhausted，测试锁定，防 3.1 阶段泄漏 completion 权威）。

## 3. Reducer 校验清单（无 dangling / append-only / 分层）

- node 必须已存在于 Graph；revision 严格递增。
- activation 引用必须存在于 accepted state 集合且未在任何层出现（append-only 去重）。
- occurrence 的 SliceRef、FastAssessment 的 SliceRef 必须属于本模型 slice 集（模型级无 dangling）。
- archive 仅允许 active → archived 移动（未知 active 引用拒绝；同 commit 既 activate 又 archive 拒绝）；archived 保留 relocation 锚，永不删除。
- 结果模型 IsValid（层间 disjoint + 层内 distinct）复验。

## 4. 测试结果

- 3.1 聚焦：**12/12 PASS**（append 创建/整体替换/前状态不变；重复 append 拒绝 + exact prior；archive 移层保锚；archive 未知拒绝；stale revision 拒绝；dangling occurrence/外模型 slice 的 occurrence 与 assessment 拒绝；未知 node 拒绝；同 commit 冲突拒绝；二次 append 替换不复制；coverage 骨架 fail-closed）。
- 聚焦门（B1+B2+3.1 + Model immutability + R8 live-state + ArchitectureGuard 全系）：**150/150 PASS**。
- `dotnet build`：0 error。
- `openspec validate --strict`：PASS。
- `scripts/check-consistency.sh`：**ALL PASS**。

## 5. 完整测试集分类（禁止以聚焦通过宣称毕业）

- Runtime：**2672 通过 / 4 失败** —— 全部为 B1/B2 证据已分类的既有/环境类，且较 B2 基线（6 失败）减少 2（在途工作变动），**无 3.1 新增失败**：
  - `ScrollStabilityConfirmationTests.TitleOff_...`（在途未提交工作，DecisionRecord 断言）
  - `CapstoneSingleAgentRunTests`（RealEmulator 环境依赖）
  - `ExternalBoundaryRealDeviceTests`（RealDevice 环境依赖）
  - `HarnessSourceShapeGuardTests.ScenarioKnowledgeTokens_...`（在途工作，白名单 token）
- Semantic（独立项目）：153 通过 / 5 失败 —— 与 B2 基线完全相同（SemanticProfile V2/V3 qualification，关联 platforms/perception 在途修改）。
- 仓库整体未毕业状态不变；3.1 触碰面（ContainerRuntimeV2 state/reducer + LocalModel）被 150 项聚焦门全覆盖。

## 6. Leader Gate（停止点）

3.1 已完成并留证。**未进入 3.2**（LogicalItem）/3.3（EvidencePolicy+Reconciler）。等待 Leader 对 3.1 产物与 2.6/2.7/2.8 依赖裁决执行情况的检查后再授权继续。
