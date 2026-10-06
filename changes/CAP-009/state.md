# CAP-009 — Perception 可执行契约与健康聚合（uni.perception 实例化 + 依赖图闭合）

lifecycle_state: closed · disposition: none · depth: standard · base: 63804f7a

## Intent（WHAT/WHY）

所有者裁决（2026-10-06，perception 纪律审计后）：

1. 设计意图确认：uni.perception 应实现 ISemanticPerception + IUiElementPerception
   双协议——当前零产品实现（审计 P1）。
2. 健康检查必要：fast 感知是真实服务（必须检查）；模型端用健康接口
   （ModelManagement.CheckHealth 已存在）；某源没有健康接口 → 诚实忽略（Unknown），
   不做假健康。
3. 依赖图必须画。
4. 相关配置（依赖锚点注册）必须补充。方向：配置管理 + 架构信息。

审计异议的化解：空 marker 实例注册是仪式（无买方）——本轮 façade 以**健康聚合
owner**身份落地（真实买方：真机运行时诊断），协议负载词汇
（SemanticObservationProposal/PerceptionAssessment）仍留给下一个感知买方驱动冻结。

## Scope

- Kernel：`UniPerceptionCapability : ISemanticPerception, IUiElementPerception,
  ICapabilityHealthCheckable`（canonical 声明 = 现组合根 descriptor 同值；
  WithDescription 同表视图先例；`PerceptionHealthSource` 命名健康源注入）。
  CheckHealth 聚合：Unhealthy > Degraded > 混合 Unknown（部分可观测）> Healthy；
  无源 = Unknown；诊断逐源列出。
- Host 组合根：`RegisterProductPerception` 升级——uni.perception **实例注册**
  （fast 资产探针 + 模型端探针注入；模型端经 IModelManagement.CheckHealth）；
  slow.visual 维持 description-only（理由：无运行时实例，visual 未接线）；
  新增 `fast.yolo` / `fast.ocr` 声明性依赖锚点注册（理由：确定性本地资产，
  无实例语义；协议词汇留待 fast 能力独立 change 冻结）。
- Program.cs：注入 fast/模型端健康源；capability-facts 落盘随之完整。
- 文档：seam 设计 §4.0.1 补 mermaid 能力依赖图（含 slow.text 不注册的设计
  决定边注 + model.management 供链）。
- skill 已知偏差同步：uni.perception 转实例注册；slow.visual 仍 description-only。

## Out of scope

- 协议负载词汇（SemanticObservationProposal / PerceptionAssessment）——独立
  change，由下一个感知买方驱动。
- slow.visual 实例化（visual 未接线，无实例可注册）。
- fast 感知服务级 ping（探针先做资产/配置可观测面；服务级健康接口留待服务
  化稳定后定义——"没有接口就诚实 Unknown"）。

## Acceptance

| ID | 行为 | 证据 |
|---|---|---|
| A1 | 双协议实现 | UniPerceptionCapability 实现两 marker + 注册经 ValidateImplementation（Composite 双协议 ↔ 双接口一致性执法在生产路径首次行使） |
| A2 | 健康聚合 | 契约测试：worst-of 聚合 / 混合 Unknown→Degraded / 无源 Unknown / 逐源诊断；模型端探针复用 ModelManagement.CheckHealth |
| A3 | 实例注册 | Resolve("uni.perception") 取回实例且健康源共享（无分叉）；slow.visual 维持 description-only 且理由在案 |
| A4 | 依赖闭合 | fast.yolo/fast.ocr 锚点注册；registry facts 含全部依赖端点；mermaid 依赖图入文档 |
| A5 | 兼容 | 全量测试绿；白名单 +2 型；场景再认证 |

## Verification（2026-10-06 回填）

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `dotnet build` | 0 error | 0 error、0 新增警告 | 本地构建 |
| DETERMINISTIC | Kernel.Tests 829（UniPerceptionCapabilityTests 8 新例：双协议实现/canonical 镜像/注册执法行使/聚合四态/探针异常诚实 Degraded/非法源拒绝/视图语义/模型端联动） | 全绿 | PASS 829/829 | `dotnet test tests/UniClaw.Kernel.Tests` |
| DETERMINISTIC | Host.Tests 175（组合 3 新例：实例+锚点+visual 形态、健康源注入与活状态共享、重复注册 fail-closed） | 全绿 | PASS 175/175 | `dotnet test tests/UniClaw.Host.Tests` |
| DETERMINISTIC | 全量 + 场景哈希 | 全绿 | 1386/1386（Simulation 188 含 CAP-009 再认证 20 块） | `dotnet test UniClaw.Kernel.slnx` |
| CONTRACT | 依赖图 | mermaid 入 §4.0.1（含 slow.text 不注册的语义承载边注 + 健康源关系） | 已落 | docs/design/runtime-capability-integration-seams-v0.1.md |

测试红队修正记录：白名单字典序（PerceptionHealthSource 先于 PerceptionProtocol）；
联动期望（唯一 profile 摘除 → Unhealthy 非 Degraded，诚实传播）；依赖闭合断言
尊重 ADR-0035（slow.text 有意不注册，由 uni.model.management 承载——测试改为
显式断言该设计决定，图以虚线边注表达）。

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 所有者四点裁决落定；仪式矛盾
  以健康 owner 化解；开工。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED（所有者验收同轮）· 落地：
  Kernel UniPerceptionCapability（双协议 marker + 健康聚合 owner + PerceptionHealthSource）
  → 白名单 +2 → 组合根实例注册 + fast 锚点 + visual 理由在案 → Program.cs 注入
  fast 资产探针与模型端 ModelManagement.CheckHealth 探针 → §4.0.1 mermaid 依赖图
  → skill 已知偏差同步 → 测试 11 新例 → 场景再认证 → 全量 1386/1386。
  协议-接口一致性执法（ValidateImplementation）首次在生产注册路径行使。
