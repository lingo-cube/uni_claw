# Greenfield Agent Runtime — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_GREENFIELD_AGENT_RUNTIME` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-greenfield-agent-runtime/`
> Authority: Runtime Architecture Contract I-1..I-14 and Architecture v1 remain the governing baselines; this decision adds no architecture authority.

## 1. Buyer and exact claim boundary

**Buyer:** UniClaw 新 Agent Runtime 的独立 Greenfield 工程地基：`src/UniClaw.Runtime/` + `tests/UniClaw.Runtime.Tests/` 独立工程边界，让新 Runtime 从零生长出自己的 Agent → Container → Traversal → Environment Spine，旧 UniClaw.Core 只作为能力参考（per proposal.md 动机与目标；proposal.md 无显式 buyer 字段）。

This receipt claims only that:

1. 独立工程边界建立：`src/UniClaw.Runtime/` 与 `tests/UniClaw.Runtime.Tests/` 两个新工程创建并加入 `src/UniClaw.Core.sln`（proposal.md 目标 1/5；tasks.md 任务 3/4/5 全部完成）；
2. 第一阶段 `UniClaw.Runtime` **不引用** `UniClaw.Core`（Greenfield isolation，机械约束）：csproj 零 ProjectReference（design.md D2；spec `Isolated Runtime Foundation` SHALL 第 2 条）；
3. Architecture Contract（12 条 invariants）建立于 `docs/system/constitution/runtime-architecture-contract.md`（Tier 1 Constitution 语义，design.md D3；tasks.md 任务 6）；
4. 机械 Architecture Guards（Guard 1/2/3，失败信息包含违反内容 + 文档指针）建立于 `tests/UniClaw.Runtime.Tests/Architecture/ArchitectureGuardTests.cs`（design.md D4；tasks.md 任务 8）；
5. AGENTS.md 增加「Agent Runtime（新）— Greenfield」唯一导航入口，不做大规模重构（design.md D5；tasks.md 任务 7）；
6. 独立 worktree `uni-claw-agent-runtime/` + 分支 `feature/agent-runtime`（基于 `feature/refactor` HEAD af6c1ee），现有工作区脏改动不进入新 Runtime 工作区（design.md D1；tasks.md 任务 1）。

No claim is made for: 任何 Runtime 业务类型（Agent / Container / TraversalFSM / WorldBelief / Recovery …），且本阶段不创建任何 stub / NotImplementedException（proposal.md 非目标；design.md 顶部）；复用决策（IActionExecutor / PageAnalysis / UniBrain / Graph / SourceGen / Foundation project — Extract Foundation / Create Adapter / Reuse Contract 三选一，本阶段不预设答案，design.md D2）；Recovery Runtime / Memory / LLM-VLM / Android / Vision / DynamicMatch；旧代码迁移、Container 最终类名、ContainerFSM 是否存在、TraversalFSM 状态设计（proposal.md 非目标）。

## 2. Validation evidence

- `tasks.md` 记录全部 9 项任务完成（第 1–9 项均为 `[x]`），其中任务 9「验证：baseline build/test 记录 → 完成后 build/test 对比，无 New regression」标记完成（openspec/changes/greenfield-agent-runtime/tasks.md）。
- `proposal.md`「验收」记录验收标准：`dotnet build src/UniClaw.Core.sln` — 0 错误（基线同样 0 错误）；`dotnet test src/UniClaw.Core.sln` — 基线测试无回归、新 Guard 测试通过；新 Runtime Guard 验证：csproj 零 ProjectReference、源码零旧 Runtime namespace 引用、契约文档 + 导航存在（openspec/changes/greenfield-agent-runtime/proposal.md）。
- `design.md`「验证」记录验证计划：基线（HEAD）与完成后各跑一次 build/test，明确区分 Pre-existing failure 与 New regression；新 Guard 测试必须通过；旧测试不得因本 change 退化（openspec/changes/greenfield-agent-runtime/design.md）。
- `specs/runtime-foundation/spec.md` 定义机械一致性场景 `Mechanical foundation verification`：验证 csproj 零 ProjectReference、源码零旧 Runtime namespace 引用，以及契约文档 + 导航存在（openspec/changes/greenfield-agent-runtime/specs/runtime-foundation/spec.md）。

The change's files record no build/test-run evidence (no `evidence/` directory exists, and no run counts or test names are recorded anywhere); verification evidence here is limited to the all-tasks-complete record in tasks.md, the acceptance criteria recorded in proposal.md 验收, the verification plan recorded in design.md 验证, and the conformance scenario in specs/runtime-foundation/spec.md.

## 3. Scenario receipts and falsifiers

The change files record no explicit falsifier section (no `evidence/` directory; design.md contains no falsifier section); rejection/negative requirements are defined in `specs/runtime-foundation/spec.md`:

- `SHALL 第一阶段 UniClaw.Runtime 不引用 UniClaw.Core（Greenfield isolation，机械约束）` — 该 spec 唯一的禁止性（否定）需求。
- `SHALL 新 Runtime Guard 验证：csproj 零 ProjectReference；源码零旧 Runtime namespace 引用；契约文档 + 导航存在` — 一致性否定检查（缺省即为失败）。

design.md 另记录设计级否定约束（source: openspec/changes/greenfield-agent-runtime/design.md）：`UniClaw.Runtime.csproj` 不含任何 ProjectReference（D2）；本阶段不创建任何 stub / NotImplementedException（顶部 blockquote）；不建立第二套文档体系、不创建 `.ai/`（D3）。

## 4. Deferred scope

The following remain outside this graduation and require separate authorization (proposal.md 非目标 / tasks.md Deferred):

- 任何 Runtime 业务类型（Agent / Container / TraversalFSM / WorldBelief / Recovery …）——由下一个 Vertical Slice 从真实运行需求中自然产生，本阶段不创建任何 stub / NotImplementedException。
- 复用决策：IActionExecutor / PageAnalysis / UniBrain / Graph / SourceGen / Foundation project（Extract Foundation / Create Adapter / Reuse Contract 三选一，本阶段不预设答案）。
- Recovery Runtime / Memory / LLM-VLM / Android / Vision / DynamicMatch。
- 旧代码迁移、Container 最终类名、ContainerFSM 是否存在、TraversalFSM 状态设计。
- 后续每个 Vertical Slice 均以本 change 为根继续走 OpenSpec（proposal.md 目标 6）。

## 5. Final conclusion

**GRADUATED.** 本 change 按 proposal.md/design.md 的边界建立了独立 Greenfield 工程地基（独立工程边界、第一阶段零 ProjectReference 隔离、12 条 invariants 的 Architecture Contract、机械 Architecture Guards、AGENTS.md 唯一导航入口），tasks.md 记录全部 9 项任务完成，验收标准与机械一致性场景由 proposal.md 验收、design.md 验证与 specs/runtime-foundation/spec.md 定义；归档作为本批次独立生命周期操作于 2026-08-30 执行。