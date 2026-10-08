# PRF-003 — PRF-2：静态产品 prompt 版本化 + scoped seam

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

ADR-0041/设计文档切片 PRF-2（Q10/Q11 裁决）：静态身份/输出纪律/payload-shape
不再每轮由 `consultationPrompt()` 内联合成；成为带 revision 与 hash 的
产品工件（`product/prompt/`），DSH 插件经包内副本消费；挂载主选 uniagent-prod
作用域 system-prompt section（探针已证实 `ctx.inject(['systemPrompt'])` +
`section({name, order, text})` 机制可用，与 MCP instructions 同款）。

## Scope

- canonical 工件：`product/prompt/uniagent-prod/`（manifest.json：schemaVersion/
  promptRevision/protocol 耦合声明/segments 清单；三个 .txt 分段：identity/
  output-discipline/payload-shapes——自 consultationPrompt() 逐行保真迁出）。
- 包副本：`dsh/uniclaw-decision-channel/prompt/`（整目录 + prompt-hash.txt；
  随既有 pnpm file: 快照部署，deploy.sh 零改动）。
- 哈希工具：`tools/prompt-manifest-hash.py`（manifest+segments 串联 sha256，
  写/校 prompt-hash.txt）。
- 插件：boot 时加载 prompt 工件并 fail-closed 自检（hash 不符/协议 schema
  耦合不符 → 拒载，Q10「schema bump 未 bump manifest → 校验失败」由此执法）；
  `{{protocolVersion}}/{{schemaVersion}}/{{schemaHash}}` 装配期令牌替换；
  PRESET 行注册 scoped system-prompt section（config.promptMount，默认
  'section'，'per-turn' 为回退）；consultationPrompt() 收缩为每轮动态
  （decisionId 两行 + allowedEffects 行 + context JSON）；consult-start 事件
  含 productPromptRevision。
- 测试：hash 篡改拒载、schema 耦合不符拒载、canonical↔包副本等值同步、
  section 注册断言、per-turn 回退、事件含 revision。

## Out of Scope

- 产品 profile assembly 引用块与 hash 钉扎执法（PRF-005）。
- 初始化审计 envelope（PRF-005）。
- slow 通道 prompt（DshSlowConsult 自有纪律，不动）。
- 危险动作 policy 升格（PRF-004）。
- src/ 零改动（无 scenario 重封需求）。

## Decisions

1. 分段三段制（identity/output-discipline/payload-shapes），文本自现有
   prompt 逐行迁出，语义零变；「Respond with the tool call only」保持在
   CRITICAL 块原位不复制。
2. section order=100（persona prefix 0 之后、plan policy 500 之前），
   interpolate: false（令牌已在装配期替换）。
3. 挂载可配（promptMount: section|per-turn）作为实现期回退通道，默认
   section；PRF-003 的 live 验证若发现 section 未进会话装配，无需改码即可
   切换，PRF-003 探针（会话 sections 枚举）顺带验证。

## Acceptance

1. 包 prompt 工件 hash 不匹配/协议耦合不符 → apply 拒载（fail-closed）。
2. canonical 与包副本逐字节相等（同步测试）。
3. PRESET 行注册 section（含三段静态文本）；HOST 行不注册。
4. 每轮 prompt 只含动态内容（section 模式）或 segments+动态（per-turn 模式）。
5. consult-start 事件携带 productPromptRevision。
6. 既有 plugin 测试全过；manifest 变更走 revision+1（工件内声明）。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | plugin tests（node --test） | 新增六用例 + 既有 22 全过 | PASS；28/28（含 4 个 B3 内容断言测试改跑 per-turn 模式保覆盖） | `dsh/uniclaw-decision-channel/tests/plugin.test.mjs` |
| CONTRACT | 工件结构与耦合声明 | manifest.protocol.schemaHash==schema-hash.txt；canonical↔包副本逐字节一致 | PASS（hash 双侧 PASS 1fe27fd9a721…；同步测试绿） | `product/prompt/uniagent-prod/`；`tools/prompt-manifest-hash.py` |

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 探针：scoped section 机制
  证实（server-context.ts 先例）；schema 双副本无等值测试先例 → 本片补
  canonical↔包副本同步测试；部署链（pnpm file: 快照）确认 prompt/ 随包走。
- 2026-10-07 · IMPLEMENT → REVIEW → VERIFY → CLOSED · canonical 工件 +
  hash 工具 + 包副本落地；插件 boot fail-closed（hash/协议耦合）+ preset
  section 注册（mock 扩 systemPrompt）+ promptMount 回退通道；四个 B3
  内容断言测试改跑 per-turn 保住覆盖；consult-start 事件带 promptRevision；
  plugin 28/28 绿；src/ 零改动（无重封需求）。live 验证项移交 PRF-004
  探针：部署后枚举会话 sections，确认 uniagent-prod:product-prompt 在场。
