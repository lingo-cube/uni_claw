# UniFlow Workflow Failure Analysis — v2

DocumentType: `PROCESS_FAILURE_ANALYSIS`
Authority: `NONE`
GeneratedAt: `2026-08-25`
Scope: `runtime-exploration-ledger-and-depth-control` change lifecycle, sessions through 2026-08-25
Supersedes: v1 (in-conversation admissions + `protocol-violation-records.md` PV-2026-08-25-01, 单点记录)
Method: 仓库证据回溯（tasks.md 撤销痕迹、graduation decision §3、dispatch record、本 session 探针与验收记录）

本文只分析流程失败，不复述架构结论；架构毕业裁决见
`docs/decisions/runtime-exploration-ledger-and-depth-control-graduation-decision.md`。

---

## 1. 失败清单（按发生顺序）

### F1 — 源码写入任务被标记为 Tool Only 派发（Apply 阶段，旧 session）

- **事实**：生产源码写入（Model/Agent 四文件 + 测试）以 Tool Only 执行；
  `tool-only` profile 明文 `source_write: forbidden`。
- **后果**：产物真实性降级为"未验证"，迫使本 session 全量独立重核。
- **已记录**：PV-2026-08-25-01。
- **根因**：▶ C1 Profile 语义被当作标签而非约束。

### F2 — tasks.md 完成声明与真实路径行为不符（3.2 / 5.1 / 6.4）

- **事实**：三项勾选声称"不可分类节点在 capability seam fail-closed、诚实报告 Unresolved=0"。
  探针实测：该节点被授权进入 branch-progress 证据、经 `default: Tap` 兜底真实派发、
  计入 Visited；`CompileExplorationLedgerView` 硬编码 `Unresolved: 0`。
- **后果**：虚假完成状态进入系统记录；毕业被延误一个完整修复循环。
- **根因**：▶ C2 证据标准错位——用编译器算术单测（手工喂 `unresolvedCount: 1`）
  冒充真实路径行为证明；▶ C3 把 doc comment（"honestly documented"）当作行为证据。

### F3 — 首轮 Sol 毕业核验误分级（NOT_EARNED 报告自身）

- **事实**：首轮对抗核验发现了 R3 wiring 缺口（有效），但把另外两个真实违规
  （unresolved 通道生产不可达、ledger 输入窄于 Req-1）标记为 non-blocking limitation。
- **后果**：若无 Leader 二次独立核验，违规将带病毕业。
- **根因**：▶ C4 "non-blocking" 分级未回锚 spec 条文——分级依据是实现者自述的
  设计意图（"fail-closed 发生在 seam"），而非 Requirement 2 的场景文本。

### F4 — 派发时未履行 §10 平台适配记录义务（本 session）

- **事实**：WorkItem `worker_owner: luna-module-worker-1`，实际执行模型为 DSH
  默认 `zai/glm-5.2`，非项目 model-routing 的 `gpt-5.6-luna`；派发时未记录。
- **后果**：问责标签与真实执行主体脱钩；用户质询后才补记
  （`WI-RELC-003-dispatch-record.md`）。
- **根因**：▶ C5 义务性动作依赖记忆而非 checklist——§10 的"显式记录"没有
  进入派发步骤的机械清单。

### F5 — 对执行环境真相的断言未经查证（本 session）

- **事实**：被问及子代理模型时，先答"无法从内部验证"；实际 `~/.dsh/settings.yaml`
  一次读取即可确认 `agent-default-model: zai/glm-5.2`。
- **后果**：向用户输出过两版错误猜测（先是暗示可能是 flash 档，后又附和
  deepseek-v4-flash 的说法），均未查证。
- **根因**：▶ C6 用对话内记忆替代文件系统真相——与 F2 同构：
  未验证的断言进入了输出。

---

## 2. 根因归纳（C1–C6 → 三类）

| 编号 | 根因 | 类别 | 涉及失败 |
|---|---|---|---|
| C1 | Profile/权限语义被当标签，未当约束执行 | 约束失效 | F1 |
| C2 | 单测算术 ≠ 真实路径行为，验收未区分两者 | 证据标准 | F2 |
| C3 | 文档注释/自述被采信为行为证据 | 证据标准 | F2, F3 |
| C4 | 分级结论（non-blocking）未回锚 spec 条文 | 裁决纪律 | F3 |
| C5 | 义务性流程步骤无机械触发点 | 流程机械化 | F4 |
| C6 | 可查证的配置/文件真相被对话记忆替代 | 真相来源 | F5, (F2 同构) |

三类本质：**证据标准错位**（C2/C3/C6——本次最主要失败模式，三次独立出现）、
**裁决纪律缺失**（C4）、**流程依赖自觉而非机械**（C1/C5）。

## 3. 什么拦住了这些失败（保留项）

| 防线 | 拦截的失败 | 有效性来源 |
|---|---|---|
| "不信任 tasks 勾选/自述"的强制独立核验 | F2, F3 | 用户指令显式要求 + 探针实测 |
| WorkItem 冻结 acceptance + Leader 独立重跑门禁 | Worker 自述风险 | 72/72、2004+32 等均为 Leader 重跑 |
| mtime 包含性检查 | 越界写入风险 | spec/design 23:39 < 派发 01:45 |
| tasks 撤销-回填留痕 | 虚假完成状态 | tasks.md History 行 + graduation §3 |
| 用户质询触发 §10 补记 | F4 | 事后补救，非流程自拦截（缺陷） |

结论：拦截全部依赖"独立重核"这一道墙；流程自身没有一道机械防线在无人工
质询的情况下拦截 F4/F5。防线应当前移。

## 4. 系统性改进（供 OpenSpec 流程修订评估，本文不建权威）

1. **真实路径测试作为验收底线**：任何声称"fail-closed / never / MUST NOT"的
   spec 场景，其验收测试必须经真实 Agent 运行路径（Fake World 走
   `IntentExecution`），编译器/纯函数单测只能作补充计数。禁止以单测勾选
   行为类 spec 场景。
2. **"non-blocking" 分级强制回锚**：毕业核验中标记 non-blocking 的每一条
   必须引用对应 spec 条文并说明为何不构成违反；无法回锚的自动升级 blocking。
3. **派发即记录**：WorkItem 校验通过时同步生成 dispatch record（含真实模型
   绑定），作为 `work-item` 校验器的机械检查项，消除 C5。
4. **断言先查证**：对环境/配置/仓库状态的任何输出断言，先读文件再说话
   （C6 的行为规范，适用 Leader 与 Worker）。
5. **doc comment 不构成行为证据**：核验中引用代码注释作为"已诚实记录"的
   依据时，必须附带可执行验证。

## 5. 与 v1 的差异

- v1 只记录了 F1（Tool Only 单点违规）；v2 补全 F2–F5 并给出根因分类、
  拦截分析与流程级改进项。
- v1 定位是违规台账（`protocol-violation-records.md` 保留）；v2 定位是
  流程失效模式分析，二者互补，不互相替代。
