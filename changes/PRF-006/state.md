# PRF-006 — Role 段补齐（①核心任务 + ③行为约束，对齐业界五件套）

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

所有者指出并经业界一手核对（ADK instruction 五件套：核心任务/身份/约束/
工具时机/输出格式；Anthropic system prompt role 指南）：01-identity 段只有
身份一句，缺「告诉 UniAgent 它是干什么的」（①核心任务）与「怎么对待任务」
（③行为约束=product-context 蓝本第 3/4 条）。本片按已过目草稿补齐；
措辞属所有者后续统一修正范围（本片只补结构，措辞可再改，走 revision 递增）。

## Scope

- `product/prompt/uniagent-prod/01-identity.txt` 扩写：task 段 + grounding
  rules 三条（当前上下文=唯一现实源；未知/冲突/危险不猜——safe action/
  defer/noAction；不发明元素、completion 需当前 typed evidence）。
- manifest `promptRevision` 1→2；hash 重算；包副本同步。
- 插件测试改读 manifest 动态 revision（未来 bump 不再破测试）。
- 部署产品 profile + live consult 冒烟。

## Out of Scope

- 措辞终稿（所有者统一修正）。
- PRF-5 assembly/envelope。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | plugin tests | 全过（revision 动态断言） | PASS；29/29 | `dsh/uniclaw-decision-channel/tests/plugin.test.mjs` |
| CONTRACT | hash 双侧 --check + 同步测试 | PASS | PASS | `tools/prompt-manifest-hash.py` |
| ENVIRONMENT | live consult 冒烟（3082 产品 profile） | 通道完整、决策捕获 | PASS（consult 2s + probe 双绿，revision 2 动态断言） | E2E 输出；plugin tests 29/29 |

## Status log

- 2026-10-08 · 全流程一片闭合（详见 Verification）。
