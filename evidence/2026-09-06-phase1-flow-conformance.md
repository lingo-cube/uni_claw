# Phase 1 — Minimal Flow Conformance 运行记录

> Evidence for `FLOW_V2_CONFORMANCE_PASS`（Next Phase Directive Phase 1）。
> 每条路径记录：Gate transitions / Direct-Delegate decision / Skills invoked /
> WorkItem presence / Review result / Verification evidence / Completion /
> lifecycle bypass 观察。

## 控制面加载证据

- uniflow skill 经**标准发现机制**加载（catalog 自动出现于 `.agents/skills/
  uniflow/SKILL.md` 创建后）——ADR-0005 的运行时验证。

## Path A — Feature + Direct：CONTEXT.md 术语表

| 记录项 | 内容 |
|---|---|
| Gate 1 | 10 项逐项明确（Intent=固化已裁决术语；Type=Feature/doc；In/Out scope 显式；假设=仅 harness 术语；无阻塞 Human Decision）→ `EXPLORE_RESOLVED` |
| Leader Execution Decision | Direct：术语知识热在 Leader 上下文、单文件、委派无增益 |
| Gate 2 | 单文件可执行 / 验收可验证（格式对照 CONTEXT-FORMAT + 逐术语溯源）/ 验证策略已定 / 停止条件已知 → `EXECUTION_READY` |
| Skills invoked | `uniflow`（控制面，标准机制加载）；`domain-modeling`（CONTEXT.md 作者 skill，加载后按其 CONTEXT-FORMAT 执行） |
| WorkItem | **无**（Direct 路径不产生 WorkItem）——`workitems/` 无新增文件 |
| Implementation | 根 `CONTEXT.md`（16 术语，3 簇；glossary-only，无实现细节） |
| Review | Built Right?：符合上游格式（紧凑定义、_Avoid_、簇分组、无通用编程概念混入）✓。Built The Right Thing?：全部术语均可溯源到已裁决文档（uniflow SKILL.md / ADR-0001..0005 / provenance inventory），无新造语义 ✓ → APPROVE |
| Gate 3 | Acceptance 满足（文件存在+格式符合）；行为观察=skill 消费者按 docs/agents/domain.md 将读到该词汇表；diff 限单文件 scope；无 review 阻断项 → `VERIFIED` |
| Gate 4 | 请求范围完成；无未授权改动；domain docs 约定的 lazy 创建条件满足（首个术语已定型）→ `COMPLETE` |
| Bypass 观察 | 无。domain-modeling skill 明确「Update CONTEXT.md inline when resolved」——本次即该语义的首次真实执行；skill 未越权进入其他生命周期 |

## Path C — Bug + Direct：WorkItem schema 缺 status 属性（真实回归）

| 记录项 | 内容 |
|---|---|
| 发现方式 | Path B 编译 WorkItem 时暴露：README 约定「状态内嵌」但 schema 无该字段（bug 由真实使用发现，非构造） |
| 可靠 RED | 最小合规 WI（README 约定字段 status=pending）被 schema 拒绝：`additionalProperties` 未含 status，检查命令 exit=1（命令全文见本文件附录） |
| 诊断 | Expected=README 约定 status 内嵌；Observed=schema 拒绝。**FDP**=delegation 重构提交（e5a41dbc）改 README 未改契约。**Owner**=schemas/work-item.schema.json。**Root Cause**=契约与使用约定在独立提交中演进、无机械绑定 |
| Gate 1 | Root Cause 明确，修复方向唯一（schema 补 status enum）→ `EXPLORE_RESOLVED`（**修复前 STOP**：诊断未穿透到实现） |
| Gate 2 | 单文件契约修复、Direct → `EXECUTION_READY` |
| Fix | schema 新增 `status`（enum×5 + default pending）；属 UniFlow 内实现 |
| GREEN | 同一检查命令通过（exit=0，enum 全覆盖）；schema JSON 语法校验通过 |
| 回归应用 | WI-P1-B-001 即刻以 status=in_progress 使用新字段（真实消费验证） |
| Review | Built Right：最小 diff、enum 与 README 逐字一致 ✓；Built Right Thing：修的是契约缺字段而非删 README 约定 ✓ → APPROVE |
| Gate 3/4 | RED→GREEN 证据 + 真实使用验证 → `VERIFIED` → `COMPLETE` |
| Optimization candidate | schema-vs-README 一致性机械检查（记入 dogfood friction 候选，不在本阶段建） |

## 附录：Path C 检查命令

```bash
python3 - <<'EOF'
import json
schema = json.load(open('schemas/work-item.schema.json'))
wi = {"id":"WI-RED-001","objective":"red check","scope":{"write":["x"]},
      "semantic_brief":{"summary":"s"},"acceptance":["a"],"forbidden":[],
      "status":"pending"}
unknown = [k for k in wi if k not in set(schema['properties'])]
assert not unknown, f"still rejects: {unknown}"
assert wi['status'] in schema['properties']['status']['enum']
EOF
```
修复前 exit=1（RED）；修复后 exit=0（GREEN）。

## Path B — Feature + Delegate：兼容矩阵刷新（WI-P1-B-001）

| 记录项 | 内容 |
|---|---|
| Gate 1 | Intent=矩阵对齐重定位后现状；未知项（当前发现行为）恰为可委派求解的问题 → `EXPLORE_RESOLVED` |
| Gate 2 | **Delegate** 判据成立：上下文卸载 + 独立验证价值（Leader 自证不如新上下文复验）+ 验收可独立核对；停止条件=CLI/网络不可用即 BLOCKED → `EXECUTION_READY` |
| WorkItem | `workitems/WI-P1-B-001.json`（schema 校验通过后派发；执行中以 status=in_progress、完成后 done——status 字段即 Path C 修复的真实消费验证） |
| SubAgent | fresh context（`subagent` 工具，prompt=WorkItem 全文，零 Leader 会话依赖） |
| Skills | Worker 无必需工程 skill（documentation capability） |
| Worker 返回 | STATUS: DONE + 逐条 EVIDENCE（命令/文件）+ UNRESOLVED（DSH catalog 为 leader-attested 引证，Worker 不可观测——诚实申报，未越权宣称） |
| Leader 核验 | 独立复跑 `npx skills ls`：uniflow（Source: local, Agents: Codex/GitHub Copilot）可复现 ✓；矩阵 §0.1 重定位核对表 8/8 与 repo 一致 ✓；抽查引用计数 8 处证据标注 ✓ |
| Review | Built Right：单文件、header 风格保留、证据内联 ✓；Built Right Thing：过期主张清除、uniflow 覆盖、Gap 如实 ✓ → APPROVE |
| Gate 3/4 | 验收四条全满足 + Leader 复现 → `VERIFIED` → `COMPLETE`（由 Leader 判定，Worker 未宣称） |
| Bypass 观察 | 无。Worker 无 COMPLETE 宣称权；DSH 不可观测项如实转引而非编造 |
