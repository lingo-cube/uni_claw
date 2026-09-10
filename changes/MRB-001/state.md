# MRB-001 — DSH adapter 模型绑定层(tier → provider/model,声明式薄 adapter)
lifecycle_state: closed · disposition: none · depth: standard · base: ab70f82d

## Intent(WHAT/WHY)
uni-harness 分支的 `model-routing.yaml` 只定义了 capability → tier 共享映射,
tier → 具体 provider/model 的最后一公里标注「未来引入」而无消费者:派发
SubAgent 时模型来自 DSH 会话默认值,属会话级巧合而非路由行为,
`silent_downgrade: forbidden` 无人执行。本 Change 落地 DSH 侧 adapter 绑定,
使 WorkItem 的 `required_capability` 经 tier 解析到具体模型。

不移植 uni-agent 的 `dsh_profile_adapter.py`(79KB router + events.jsonl/
sessions/dispatches 状态目录)——那是第二套 task system,违反本分支禁令。

## Scope / Out of Scope
In:
- `.dsh/model-bindings.yaml`(tier → primary/fallback 绑定,纯声明)
- uniflow SKILL.md B4 增补派发时解析规则(引用 binding,不复制语义)
- `tools/validate-model-bindings.py` 确定性校验脚本
Out:
- `.codex/` adapter(Codex 侧绑定,结构同构,另行落地)
- 修改 `model-routing.yaml` 共享映射、WorkItem schema
- 任何运行时 router/状态目录/事件日志

## Decisions
1. 声明式薄 adapter:绑定只消费 model-routing.yaml 的 tier 名,单向依赖;
   无运行时、无状态(B5 对称性靠同构文件形状保证)。
2. 路由执行 = DSH 原生能力:`workflow` 工具 `agent()` 支持 provider/model
   覆盖,Leader 派发时按 binding 填参;不引入自造 router。
3. 失败策略:primary 失败 → fallback;均不可用 → ROUTING_UNAVAILABLE
   显式报告,绝不静默落回会话默认模型。
4. 绑定(Human Decision 确认):leader zai/glm-5.2(fallback opencode-go/
   glm-5.2) · expert opencode-go/deepseek-v4-pro · standard opencode-go/
   deepseek-v4-flash · fast opencode-go/mimo-v2.5。

## Acceptance
- `.dsh/model-bindings.yaml` 覆盖 model-routing.yaml 全部 4 个 tier,含
  fallback(leader)。
- 校验脚本:tier 全覆盖、结构合法、tier 名与共享映射一致时退出 0。
- uniflow SKILL.md B4 描述派发时解析路径,不含 provider 具体名(共享层
  不绑定模型)。

## Constraints
- 共享层(model-routing.yaml / SKILL.md / schemas/)不出现 provider/model 名。
- binding 文件不出现状态、session、派发记录。

## Verification
level: CONTRACT
method: `python3 tools/validate-model-bindings.py` + 共享层泄漏 grep
expected: 退出 0,输出 4 tier 绑定校验通过;SKILL/model-routing/schemas 无 provider/model 名
actual: 4 tier OK(expert/fast/leader/standard),exit 0;grep 无命中(exit 1)
evidence ref: 本会话工具执行记录(2026-08-30)

## Status log
- 2026-08-30 · understanding→implemented · 方案经 Human Decision 确认(声明式 + 推荐绑定套)
- 2026-08-30 · implemented→verified · CONTRACT 校验通过
- 2026-09-10 · verified→closed · 提交前由另一会话按入口协议复验 CONTRACT:
  validate-model-bindings exit 0(4 tier OK);共享层(SKILL/model-routing/
  schemas)provider/model 泄漏 grep 无命中;精确提交 5 文件落盘
