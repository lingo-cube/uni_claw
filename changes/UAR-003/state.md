# UAR-003 — DSH 嵌入 R1 前置决议（推荐组合 + 五未决问题 + Tracer Bullet 授权边界）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: b4b6865c

## Intent（WHAT/WHY）

**WHAT**：对 `docs/analysis/dsh-integration-research.md`（2026-09-22 重写版）
做 R1 前置决议：批准（或修订）其 §8 推荐组合，裁决其 §9 五个未决问题，
并产出未来 DSH Tracer Bullet change 的预定授权边界。本 change 是纯决议，
零实现。

**WHY**：UAR baseline §9 与 ADR-0022 Decision 5 把 DSH Profile/plugin/package
切分、Kernel Bridge transport、Tracer Bullet、产品实现全部 deferred；研究
已完成带一手引用（`路径:行号` + [已核实] 标注）的 scoping，但全部结论停在
选项评估层，研究 §9 边界声明「进入实现需独立 change 走 uniflow 主干」。
DSH 轨道开工前必须先裁决；决议与 SIM-002 / RUN-004 并行不抢资源（零代码、
零共享层变更）。

## Scope

- 批准/修订研究 §8 推荐组合（D0）
- 裁决研究 §9 未决问题 1–5（D1–D5）
- 产出「Tracer Bullet 预定授权边界」（见下节；它是未来实现 change 立项时的
  裁决输入，**不构成对实现的直接授权**——实现 change 仍需独立立项并过人工门）
- 决议全程留痕：shadow 台账事件 + 本文件 Decisions 表

## Tracer Bullet 预定授权边界（D0–D5 裁决定稿 2026-09-23）

未来 DSH Tracer Bullet change 可直接引用本节作为裁决输入：

- **允许**：`platforms/dsh/package.json`（钉 `@deepseek-ai/dsh` npm 版本）+
  lock 入库；`dsh plugin --profile uniclaw-product add @deepseek-ai/dsh-headless`
  创建产品 profile；Kernel(C#) 每任务 spawn 一次性（`DSH_HOME` 指向仓库内
  隔离目录、凭证走 env、`DSH_CWD` 指向隔离工作目录）；C# 进程内 adapter
  （投影 / id 映射 / fail-closed 翻译）；禁用 coding tools 的 profile patch；
  研究 §7 验证表全部项作为 ACCEPTANCE 雏形。
- **禁止**：web-app 或开发 Harness 插件进产品 profile；`file:`/`link:`
  依赖作交付形态；profile-source 强制闭环；C8 approval 交互实现（只要求
  fail-closed 可观察）；baseline / ADR / 共享层（`model-routing.yaml` /
  SKILL / schemas）修改；JSON-RPC 常驻形态（启用需新裁决）。

## Out of Scope

- 一切实现：`platforms/dsh/` 创建、profile 实建、spawn、C# adapter 代码、
  绑定/settings 文件落盘、`model-routing.yaml` / 共享层变更
- UAR baseline v0.1 或任何 ADR 修订（如裁决触及冻结面，走 supersede 流程
  另立 change）
- SIM-002 / RUN-004 / SCN-* / PER-009 并行线
- Codex-backed Simulation Realization 侧设计
- R1 本体（Primary Run activation / session contract closure，Deferred ⑰
  维持未决）

## Decisions（Human adjudicated 2026-09-23）

| # | 问题 | 选项 | Leader 建议 | 状态 |
|---|---|---|---|---|
| D0 | 研究 §8 推荐组合：主轴 = `platforms/dsh/` 最小 JS 宿主（钉 `@deepseek-ai/dsh` + lock）→ `uniclaw-product` profile（base+headless）→ Kernel(C#) 每任务 spawn 一次性 → 仓库内隔离 `DSH_HOME` + env 凭证；第二选项 JSON-RPC 常驻保留不默认；明确不选 = web-app / 开发 Harness 插件 / `file:` 依赖 / profile-source 强制闭环 | 批准 / 修订 | 批准——逐条有一手证据（研究 §1–§8），一次 spawn 天然满足 `1 Session/1 Goal/1 Run` 与 resume 禁止 | **Human authorized 2026-09-23（按建议）** |
| D1 | Conformance adapter 形态与宿主（研究 §9.1） | a) C# 进程内翻译（Kernel 侧 adapter） b) Product profile 内 cordis 插件 c) 兼有分层 | a——headless 一次性形态下 DSH 侧只有 stdout+exit code+session 日志、无事件订阅面；投影/映射责任贴 Kernel（唯一 task authority，研究 §5.2 约束）；插件形态在 JSON-RPC 常驻启用时再议（与 §8.2 一致） | **Human authorized 2026-09-23（委托架构正确性裁定 → a，推导链见下）** |
| D2 | Product tier→provider/model 绑定文件与 Dev `.dsh/model-bindings.yaml` 的关系（研究 §9.2） | a) 独立 `platforms/dsh/bindings.yaml`（形状同构，校验工具改参复用） b) 同一文件两段 | a——产品绑定是 Product 侧事实，与 Dev Harness 互不干扰；共享层继续禁 provider/model 名（MRB-001 state.md:43） | **Human authorized 2026-09-23（按建议）** |
| D3 | 插件强制闭环（envelope/receipt）是否引入产品侧（研究 §9.3） | a) 不引入 b) 引入 | a——MRB-001「不建第二套 task system」+ baseline §2「Realization 不是 Host 平行 Authority」；SKILL/workflow 覆盖路径已足够 | **Human authorized 2026-09-23（按建议）** |
| D4 | `DSH_HOME` 隔离目录与凭证面（研究 §9.4） | a) `platforms/dsh/home/` + gitignore（sessions/logs/credentials/.env）；settings 最小（仅 `DEEPSEEK_API_KEY` env，不带 llm-pi-ai 多 provider） b) 仓库外路径 c) 多 provider settings 随 profile 交付 | a——一次完成 state/instruction/lifecycle 隔离（研究 §4.3 已核实锚点）；多 provider 无真实 buyer 前不预造（ADR-0026 buyer-driven 原则） | **Human authorized 2026-09-23（按建议）** |
| D5 | sandbox 面与 C8 approval 产品化路线（研究 §9.5） | a) v0 用 DSH 默认 workspace-write + `DSH_CWD` 指向隔离工作目录；收紧留待真实 buyer 证据；C8 保持 deferred，Tracer Bullet 只要求 fail-closed 可观察（退出非 0 / 无输出） b) 立即以 profile patch 收紧 sandbox 行 | a——缝形状由真实证据验证后冻结（ADR-0026）；C8 lifecycle vocabulary 是 baseline line 232 明文 deferred | **Human authorized 2026-09-23（按建议）** |

### D1 架构正确性推导链（用户裁决原话「哪种符合架构正确性」，委托裁定）

1. **baseline §4**：Adapter 位于 seam 内部、可翻译，但 Conformance Surface
   调用者不得依赖 Plugin/Profile/transport——C# 进程内方案只依赖进程边界
   （spawn 契约：exit code + stdout + session 文件），零依赖 Host 内部件；
   cordis 插件方案把 conformance 关键翻译耦合进 Plugin 生命周期。
2. **baseline §5 禁令**（"Host event/log 因物理保存 Product records 而取得
   语义 Owner/Authority"）：插件在 Host 进程内产出 Product record 投影，
   把投影的（物理）生产面放进 Host 进程，扩大该禁令的风险面；C# 方案的
   投影生产留在产品进程，语义 Owner 与物理生产同侧。
3. **ADR-0022 Decision 4**（transport 只属 realization 内部）：两方案都
   不向外泄漏 vocabulary，但 C# 方案让 Host 保持黑盒（研究 §4.2 主轴语义）。
4. **研究 §3.4/§8.5**：私有插件发布链路未解决、版本漂移已实际存在
   （web 1.1.0 vs headless 1.0.0）——把 conformance 翻译押在未决链路上
   不符合缝纪律（AGENTS.md 开发原则 5）。
5. **Kernel 唯一 task authority**（研究 §5.2 约束）：翻译贴 Kernel 侧，
   投影 / id 映射 / fail-closed 责任与 authority 同址。

结论：**a（C# 进程内）**。插件形态仅在 JSON-RPC 常驻启用且出现事件订阅
真实 buyer 时重开（届时新裁决）。veto 窗口开放至下次人工触点。

## Assumptions

- 研究文档的证据引用（`路径:行号`、[已核实] 标注）在本决议时点有效；
  DSH 侧版本事实以 `@deepseek-ai/dsh@0.1.1-rc.2` 为准。
- 实现排序不变：SIM-002（尤其 S1 Product Host 边界剥离）先于任何 DSH 实现
  落地；本决议只消除 DSH 轨道的决议欠账，不改变队列。

## Alternatives（含被拒）

1. **直接开 DSH 实现不做决议**：拒绝——违反 baseline §9 授权边界；被
   2026-09-23 用户裁决否决（选「开 R1 决议 change」）。
2. **等 SIM-002 / RUN-004 全闭后再决议**：拒绝——决议与实现线不抢资源，
   推迟只会把 DSH 轨道最大未知数（adapter 形态）留到最后。
3. **本 change 顺带冻结 baseline 修订或 mint 新 ADR**：拒绝——决议属
   realization detail（baseline §0 第 5 层），Change State 即长期真相；
   若某裁决日后证明为不可逆架构取舍，届时另走 ADR change。

## Owner-Authority impact

- 不新增 Product Owner / Authority；DSH 侧任何组件（profile / plugin /
  session / transcript）不取得 Product Authority（baseline §4/§5 维持）。
- Conformance adapter 定位为 seam 内翻译件：不 author Primary Goal /
  Contract、不重判 Runtime Outcome、不形成 Goal Evaluation（baseline §4）。
- Kernel 保持唯一 task authority；DSH session id → Product Run correlation
  只做显式投影映射。

## ADR refs

- ADR-0022：DSH-backed = Product Realization；其 Decision 5 的 deferred 边界
  由本 change 缩小但**不解冻实现**（实现 change 另立）。
- ADR-0026：buyer-driven 原则——D4/D5 最小化（不预造多 provider settings、
  不预收紧 sandbox）的依据。
- ADR-0019：Primary Run self-drive 语义不在本 change 范围，维持继承。

## Acceptance

1. D0–D5 每项有 Human 裁决留痕（Decisions 状态列 + shadow 台账事件）
2. 批准结果含明确不选清单（或修订后的等价物）
3. 「Tracer Bullet 预定授权边界」可判定：未来实现 change 能据它机械判断
   哪些动作在授权内、哪些仍禁止（ACCEPTANCE 雏形 = 研究 §7 验证表）
4. 零实现改动：无 `src/` `tests/` `platforms/` 共享层变更（仅 `changes/` +
   shadow 台账 + INDEX 再生）
5. 台账事件行八列完整（分类 + 推导链 + ruling + override + wait）

## Constraints

- 不修改 RUN-004 工作树既有改动（spec / state / 台账正在并行推进）
- 不修改 baseline / ADR / SKILL / schemas / `model-routing.yaml`
- 决议引用研究文档时保留其 §节/行号锚点，便于 Review 复核

## Verification

```yaml
verification:
  level: CONTRACT
  method: >
    决策引用与研究 §节一致性核对（D0–D5 ↔ 研究 §8 / §9.1–§9.5）；
    git 范围核对；INDEX 再生一致性；台账事件 #25 行列完整性
  expected: >
    Acceptance 1–5 全满足；改动仅限 changes/UAR-003/state.md（新增）、
    shadow 台账（追加 #25）、INDEX（再生）；RUN-004 既有工作树改动零触碰
  actual: >
    2026-09-23：D0–D5 六槽全部 Human authorized（5/5 按建议 + D1 委托
    架构裁定 a 且推导链留档）；git status 仅 INDEX / 台账 / UAR-003 为本
    change 触及面（RUN-004 两文件为既有改动，零触碰）；
    tools/gen-open-changes.py 再生输出「91 changes · 11 open」含 UAR-003；
    台账 #25 行按 '|' 分割为 9 列，与表头一致
  evidence: >
    git status --short 输出；gen-open-changes.py 输出；
    awk -F'|' 列数检查输出；前台裁决记录（D1 委托原文「哪种符合架构正确性」）
```

## Review（2026-09-23，Leader 自评 + 裁决留痕核对）

Result: `PASS`

- Acceptance 1：D0–D5 状态列全部 Human authorized，台账事件 #25 同步（✓）
- Acceptance 2：明确不选清单落在 Authorization Boundary「禁止」侧（✓）
- Acceptance 3：授权边界两侧均为可机械判断的动作清单，ACCEPTANCE 雏形
  指向研究 §7 验证表（✓）
- Acceptance 4：git 范围核对通过，零实现改动（✓）
- Acceptance 5：台账行九列完整（含 #），分类与推导链在裁决前落档（✓）
- 治理注意（非缺陷）：D1 为委托裁定（用户未逐选项裁决），缓解 = 推导链
  工件级留档 + veto 窗口至下次人工触点（先例：台账事件 #7）。

## Status log

- 2026-09-23 · created·persisted · 用户裁决开 R1 决议 change（前台对话选
  「开 R1 决议 change（推荐）」，前序判断：DSH 实现不提前、决议先行）；
  D0–D5 候选与 Leader 建议落档；grill 待人裁决（台账事件 #25）。
- 2026-09-23 · grill-round-1-done·persisted→resolved · 台账事件 #25：
  六槽裁决落定——D0/D2/D3/D4/D5 全按建议（5/5），D1 用户委托架构正确性
  裁定 → 依工件推导链定 a（C# 进程内），veto 窗口至下次触点；
  Authorization Boundary 由草案转定稿。
- 2026-09-23 · resolved→reviewed→verified · Review 自评 PASS（Acceptance
  1–5 逐项过，D1 委托裁定治理注意以推导链 + veto 窗口缓解）；CONTRACT
  四元组落 Verification。待人工 closure（P-D′：DECISION-HEAVY 保留人工）。
- 2026-09-23 · verified→closed·human-closure · 台账事件 #26：人批准关闭
  （与建议一致，零 override）。DSH 轨道决议欠账清零：Tracer Bullet 立项时
  直接引用本文件 Authorization Boundary（允许/禁止清单），实现排序仍以
  SIM-002 / RUN-004 队列为先。
