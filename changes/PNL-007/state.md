# PNL-007 — Tool/Capability 概念边界与 Harness 工具暴露清单

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

PNL-006 的报告工具与规划中的结果诊断既服务研发态又需被产品消费面（Workbench/
前端模型）调用。若不先钉死概念，Tool 会退化为 ADR-0038 已拒绝的"同级 Registry
entry"形态。本 Change 固化概念边界并落地 Harness 层工具暴露清单与执法。

Owner 验收四标准（2026-10-06）：定义描述清晰、管理层级清晰、工程定位与
设计位置清晰、文档清晰。

## Scope

- ADR-0039（概念边界 / 单向映射 / skill 单源双路径 / 管理层级 / 字段最小集）。
- 根目录 `tool-registry.yaml`：登记 run-report（implemented）与
  run-diagnosis（planned）；头注含概念、层级、维护规则。
- `tools/validate-tool-registry.py`：确定性执法（词汇枚举、绑定字段、路径
  与 capability 引用存在性、consumes 目标、status 规则）。
- AGENTS.md：canonical surface 增补 + 真相表新增工具暴露清单行。
- CONTEXT.md：Capability 词条群后新增「Tool」词条。

## Out of Scope

- Workbench 调用面（ToolInvoke 类 capability / 工具面板）与 model-procedure
  的模型路由：后续 Change（无 buyer 不预造，ADR-0026）。
- capability-backed 工具的解析机制：本期只有词汇（backing + capabilityRef）。
- 权限审批流、工具版本与废弃治理。
- 产品侧任何代码改动。

## Decisions

1. Capability = 产品层能力语义（Kernel/Runtime，权威在 CapabilityHub）；
   Tool = Harness 层暴露单元；skill = procedure 载体（model-procedure 工具
   的说明书），三者不互换。
2. 单向映射：tool → capabilityRef 只持暴露元数据；语义真相永在
   CapabilityHub；ADR-0035 已预留 Development Harness 独立注册域，
   tool-registry 即该域的注册面，与 Product Runtime 注册域不共享可变状态。
3. skill 单源双路径：研发态经 catalog 直载（零改动）；产品态经 skillRef
   由 adapter 解析注入；产品代码不得硬编码 harness skill 路径；用法分化
   显式拆分条目，禁止静默 fork。
4. 字段最小集 = 十字段 + consumes（工具间依赖，由 run-diagnosis 真实需求
   引入，超出既定十字段已在此显式记录）；枚举词汇冻结于清单头注与校验器。
5. posture 词汇：read-only / local-write / external-effect。run-report 为
   local-write（读 run 目录、写报告产物），run-diagnosis 为 read-only。

## Alternatives rejected

- Tool 作为新 Capability 类型进 Kernel Hub（ADR-0038 已拒）。
- 产品直接读 .agents/skills/（目录耦合，无法独立演化）。
- 复制 skill 内容进产品资产（双份真相漂移）。
- 本期建 Workbench 调用面或 MCP 网关（无真实 buyer）。

## Owner / Authority impact

- 新增共享契约面 `tool-registry.yaml`（AGENTS.md canonical surface 已列）；
  无产品代码、无 Kernel 注册状态改动。
- Development Harness 获得独立于 Product Runtime 的工具注册域。

## Acceptance

1. 校验器正例通过。
   - method: `python3 tools/validate-tool-registry.py`
   - expected: PASSED, 2 tool(s)
   - actual: PASSED tool-registry.yaml (2 tool(s): run-diagnosis, run-report)
   - evidence: 本文件 verification 段命令 1
2. 校验器负例捕获全部违规类（未知 surface / 缺 entry / capability 缺
   capabilityRef / 未声明 requiredCapability / consumes 指向未登记工具），
   且文件恢复后复验通过。
   - method: 合成坏条目注入 → 校验 → 恢复 → 复验
   - expected: 5 violations, FAILED；恢复后 PASSED
   - actual: 5 violations 逐条列出，FAILED；恢复后 PASSED（2 tool(s)）
   - evidence: verification 段命令 2
3. 四清晰标准落位：定义（CONTEXT.md Tool 词条 + ADR-0039 §1）；
   管理层级（ADR-0039 §4 + AGENTS.md 真相表行）；工程定位（ADR-0039 与
   0035/0038/0026 的锚定 + canonical surface 列入）；文档（registry 头注
   维护规则 + 校验器 docstring + ADR）。
   - method: 逐项文件核对
   - actual: 全部落位（见 verification 段命令 3）
   - evidence: 各文件路径见 verification

## verification（2026-10-06，CONTRACT/DETERMINISTIC 级）

```text
1. python3 tools/validate-tool-registry.py
   → PASSED tool-registry.yaml (2 tool(s): run-diagnosis, run-report), exit 0
2. 注入坏条目 bad-tool（surfaces=[fax]、无 entry、backing=capability 无
   capabilityRef、requiredCapability=nonexistent、consumes=[ghost]）
   → 5 VIOLATION 逐条列出, FAILED, exit 1；恢复原文件后复验 PASSED
3. 四清晰落位核对：
   定义    → CONTEXT.md「Tool」词条（Capability 词条群后）；ADR-0039 §1
   层级    → ADR-0039 §4；AGENTS.md §2 真相表「工具暴露清单」行
   定位    → ADR-0039 头部与 0035/0038/0026 关系；AGENTS.md §1 canonical surface
   文档    → tool-registry.yaml 头注（概念/层级/维护规则）；校验器 docstring
```

## Status

- 2026-10-06 IMPLEMENT：ADR-0039 + tool-registry.yaml（2 buyer）+ 校验器
  （正负例验证）+ AGENTS.md（canonical surface / 真相表）+ CONTEXT.md Tool
  词条。待 REVIEW。

## Review / Verify / Closure（2026-10-06）

- REVIEW：Tool（Harness 暴露单元）、Capability（产品语义）与 skill（procedure）
  的单向边界已在 ADR-0039、CONTEXT、AGENTS 和 registry 头注同步；本 Change 没有
  把 Tool 注册进 Product CapabilityHub，也没有预造 Workbench 调用机制。
- VERIFY：`python3 tools/validate-tool-registry.py` 正例 PASS；坏条目覆盖五类违规
  并 FAILED，恢复后复验 PASS；四项清晰标准逐文件核对通过。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED · acceptance 1–3 全部有
  可复验证据；后续 Runtime Host 调用适配单独落 PNL-008。
