# Leader 交接指令 — runtime-exploration-ledger-and-depth-control 毕业核验收尾

> 触发：`执行 UniFlow：验收 R3/R4 修复并完成毕业核验收尾`
> 会话主绑定：**zai/glm-5.2/high**（本会话即 Leader；启动时记录 Host 回执并断言 primary）
> Worker 显式绑定：`opencode-go/deepseek-v4-flash/high`（读 binding 配置，勿硬编码）
> 仓库：`/Users/fran/Documents/Code/spacex/uni_claw`（分支 `uni-agent`，**禁止提交 git**）
> 生成：2026-08-25（DSH UniFlow 机械强制闭环修复完成后）

## 一、当前真实状态（已由修复会话验证，勿重复实现）

修复 WorkItem `exploration-ledger-remediation-r3` **已落盘且全部验证通过**：

| 项目 | 结果 |
|---|---|
| `dotnet build src/UniClaw.Runtime.sln --nologo` | 0 errors / 0 warnings |
| 定向 ledger 测试（ExplorationLedger/DepthBoundary/AuthorityGuard） | **30/30 passed** |
| 全量确定性复跑（排除 RealDevice/RealEmulator/RealityBaseline） | **2033/2033 passed**（Runtime 2001 + Semantic 32），0 真实回归 |
| 上游 validator / adapter validator / unittest（103 条）/ check-consistency | 全绿 |

修复内容（已在工作树，未提交）：
- `src/UniClaw.Runtime/Model/ExplorationLedgerCompiler.cs`：`CompileScope` 中
  `visited = CompletedSiblingEvidence.Count + unknownFrontierCount`（R3 接线，
  boundary record-only 节点计 visited；doc 注明 frontier 为重叠注记）。
- `src/UniClaw.Runtime/Model/ExplorationLedger.cs`：ctor 不变量
  `visited+pending+unresolved ≤ discovered` 且 `unknownFrontier ≤ discovered`
  （R4 重叠注记；frontier 不再参与 disposition 和式）。
- 测试更新（断言修改均以 spec R3/R4 为依据，非掩盖）：
  `ScopeLedger_RejectsDispositionOverCount`（新不变量）、
  `Depth0_BoundedRecord_ContainersRecordedNotFailed_FrontierLedgered`
  （visited=2 且 frontier=2）、新增 R3 直证零 dispatch 用例。

## 二、剩余工作（按正确工作流执行，每步验证）

### Step 0 — Leader 绑定（七）
1. 读取 `~/.dsh/settings.yaml` 确认本会话 `agent-default-model` 为 zai/glm-5.2/high；
2. 用 `DshWorkflowRuntime.record_leader_receipt(receipt)` 记录并断言 primary；
3. 若 Host 回执不是 zai/glm-5.2 → 如实 fail-closed，不得静默降级，停止汇报。

### Step 1 — 修复 diff 独立验收（verification WorkItem）
生成合法 JSON WorkItem（`execution_profile=verification`，`module_profile=runtime-core`，
唯一 `worker_owner`）：
- `objective`：验收 R3/R4 修复 diff 与冻结 spec 一致性 + 定向与全量测试取证；
- `scope.write=[]`（只读；verification 不写源码/测试/文档）；
- `acceptance` 含：build 0 错误、定向 30/30、全量 2033/2033、失败分类、
  修复 diff 与 spec R3/R4 场景逐条对应；
- `forbidden`：禁止写文件、禁止改生产代码/测试/spec/OpenSpec；
- `leader_decisions_frozen=true`、`unresolved_architecture=[]`；
- `contract_refs` 指向 spec.md（R3/R4）。

派发：`DshWorkflowRuntime.dispatch_work_item()`（唯一入口）→ Host seam 显式
`provider/model=opencode-go/deepseek-v4-flash/high` 创建 verifier → 从 Host
会话日志读真实回执 → `accept_result`（缺 reasoning 时 Host 默认 high 自动补齐，
机制已实现并测试）→ 通过才接受。

### Step 2 — 毕业决策 + projection 同步（development WorkItem）
生成合法 WorkItem（`execution_profile=development`，`module_profile=engineering-governance`）：
- `tasks.md` §8：阻塞解除记录（接线证据 + 测试名 + 命令结果），更新为
  GRADUATION 状态与结论；
- `docs/work/active/current-gates.md` + `docs/snapshots/latest.md`：projection
  生命周期同步（与 OpenSpec source 一致）；
- 若需独立 Sol 复核：再派 fresh-context 只读 subagent（对抗式、仅凭仓库证据），
  不继承本会话上下文。

## 三、工作流约束（机械强制，非提示词）

1. 任何 Subagent 任务必须先有合法 JSON WorkItem 并经 `dispatch_work_item()` 派发；
   Markdown/自然语言任务说明直接派发会被 `DispatchGate` 拒绝。
2. model binding 由 `.dsh/profile-adapter/profile-source.yaml` 解析，不硬编码。
3. Host 创建 Subagent 必须显式传 provider/model/reasoning；能力不足写入前
   `ROUTING_CAPABILITY_LIMIT`。
4. 回执必须来自 Host（`request/header` 事件）；模型正文自述不算；缺回执/不一致
   拒绝结果与 Delta。
5. `tool-only` 不创建 Subagent、model=none。
6. 禁止提交、重置、清理、整文件回退或覆盖无关修改；不要动 `.ai/` 上游权威。

## 四、验证命令

```bash
python3 tools/agent_profile_validator.py validate
python3 tools/dsh_profile_adapter.py validate
python3 -m unittest discover -s tests/AgentWorkflow -p 'test_*.py'
bash scripts/check-consistency.sh
dotnet build src/UniClaw.Runtime.sln --nologo
dotnet test src/UniClaw.Runtime.sln --no-build \
  --filter "FullyQualifiedName!~RealDevice&FullyQualifiedName!~RealEmulator&FullyQualifiedName!~RealityBaseline"
```

## 五、汇报格式

修改文件与成员、接受的 WorkItem id、回执核对结果（requested vs actual）、
构建/测试计数、失败分类、tasks.md §8 更新后状态、projection 是否同步、
遗留限制（Unresolved 通道生产不可达等 4 条非阻塞项保留记录）。