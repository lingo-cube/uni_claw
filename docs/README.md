# docs/ 目录放置规则

> DocumentType: `DOCS_PLACEMENT_RULES`
>
> Status: `FROZEN`（修订必须经 change；由 ARCH-DOC-015 冻结，接替
> UWM-009 冻结迁移事件所立的旧两条规则）

## 1. 分类学（五层）

| 目录 | 语义 | 朝向 | 允许 | 不允许 |
|---|---|---|---|---|
| `prd/` | 需求：产品/能力级 WHAT & WHY（已接受即需求权威） | 用户价值 | 声明 `LOCKED`/`ACCEPTED` 的需求文档 | 草案（驻 `design/`）；实现细节 |
| `design/` | 方案：未裁决前瞻设计、迁移计划、PRD 草案 | 未来/意图 | `Authority: NONE`、状态属 DRAFT/CANDIDATE/PROPOSAL 族的候选 | 任何权威声明；已冻结内容 |
| `analysis/` | 分析：考察既有现实（考古/评估/调研/inventory） | 过去/事实 | `Authority: NONE` 的调查产物 | 任何权威声明；规范语义 |
| `architecture/` | 冻结架构权威：产品基线、协议、**组件基线** | 既成/已定 | FROZEN/CLOSED 基线与 COMPONENT-BASELINE | 未冻结候选稿 |
| `adr/` | 架构决策记录（不可逆 WHY） | 决策点 | ADR（文件名 `\d{4}-` 前缀） | 其他内容 |
| `agents/` | Development Harness 对 docs 的消费规则 | —— | agent 指南 | 产品域内容 |

## 2. 状态词汇表（头部体例）

1. **体例**：`design/`、`analysis/` 的每份文档必须在头部声明分行引用头
   `> Status:` 与 `> Authority:`（各占一行）。
2. **合法状态族**：
   - design：`DRAFT*` / `CANDIDATE*` / `PROPOSAL*`
   - analysis：`CANDIDATE*` / `DRAFT*` / `COMPLETE*`（调查产物）/
     `SPLIT / ROUTER`（拆分导航，ARCH-DOC-013 先例）
   - architecture：`FROZEN*` / `*CLOSED*` / `COMPONENT-BASELINE*`
   - prd：`LOCKED*` / `ACCEPTED*`
3. **驻留禁词**：`analysis/` 与 `design/` 的 Status 不得出现
   `\bFROZEN\b` / `\bCLOSED\b`（词边界判定；复合词如 `R0_CLOSED` 不受影响）。

## 3. 生命周期（升格迁移）

1. 文档在 `design/` 只能处于候选态；裁决接受后**必须迁入**对应权威目录
   （方案 → `architecture/` 组件基线；PRD 草案 → `prd/`），并同步跨文档
   引用与指向。判据是文档状态，不是作者意图。
2. `analysis/` 永非规范：分析结论要产生约束力，必须经 change 落入
   design/adr/architecture，而非原地升格。
3. 已建成的组件架构（有已落地代码 + ADR 支撑）可直接冻结为
   `architecture/` 组件基线（COMPONENT-BASELINE，版本化，修订经 change）。

## 4. 执法（有效性）

本规则由 `tests/UniClaw.Kernel.Tests/DocsMetadataTests.cs` 确定性执法
（全量回归的一部分）：五目录头部/状态/禁词/文件名规则逐项校验；各目录
`README.md`（索引本体）豁免。违规 = 测试 RED，不允许豁免提交。

## 5. 索引

`analysis/`、`design/`、`prd/` 各维护 `README.md` 索引：每份文档一句话
用途 + 状态（可由头部机器提取）。新文档入目录必须同步索引。

---

（历史：本规则初版由 UWM-009 冻结迁移事件补立、ARCH-DOC-013 收口执行，
仅两条驻留权规则；ARCH-DOC-015 重写为五层分类学 + 词汇表 + 执法测试并
冻结。）
