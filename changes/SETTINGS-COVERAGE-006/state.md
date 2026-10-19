# SETTINGS-COVERAGE-006 — 失败恢复优先与续额格式纠错
lifecycle_state: closed · disposition: none · depth: standard · base: 2225c642

## Intent（WHAT/WHY）
Owner 指令“诊断并修复 找出问题”。正式全遍历在 Quickstep 动作后唯一性验证失败，Director 仍要求执行旧目标，连续拒绝模型提出的 Navigate up 后停止。续额窗口虽开启21次，原生会话证明模型在 decision89 两次提交了错误放在 payload 顶层的 Act renewal，被通用 schema-invalid 拒绝后删除续额；并非完全没有申请，也不是 Kernel 丢失已授额度。

## Scope / Out of Scope
- SettingsCoverageDirector：StepRejected/VerificationFailed 及其有界重观察进入恢复选项，不以普通待办匹配拒绝安全恢复。保持相关性、可见目标、动作策略、Kernel grounding/verification 执法。
- DSH 工具返回真实字段诊断；Prompt 明确各决定种类的 renewal 精确路径；桥接审计保留 renewal，避免只看摘要误判模型未申请。
- Ledger 咨询上限标志与最终总上限一致，避免已续额后把无待办停止误报为咨询耗尽；当前授权仍由 Kernel 控制。
- 不放宽唯一性/权限/确认动作限制，不把 label 复制当作目标身份、不改变续额规则、不无条件自动加预算、不为 Settings label 特判。
- 保留共享 dirty checkout。入口快照：/var/folders/_3/lbphn3p95f3dj8ds3z1874q00000gn/T/uniclaw-recovery-entry-vzv_9r02。

## Decisions / Diagnosis
- E4 基线：evidence/settings-coverage-005/full-traversal-20261018-103122/runs/run-20261018-023125-401；DSH session-dd9b184c-81c8-4fec-870e-d6e21416e7fd。
- Expected：失败后模型可选择有界观察/安全返回；窗口内提案格式不合法应收到准确错误并在同一咨询纠正。
- Observed / FDP：decision109 的失败相位被普通 Quickstep directive 覆盖；decision89 的根层 renewal 两次被拒，反馈未暴露校验器已有的字段错误，第三次删除 renewal 后接收。
- Owner：恢复控制投影在 Host Director；输出纪律在 Product Prompt；格式反馈在 DSH adapter。原始 XML 中 Quickstep 标题被两个相互嵌套、均可点击的容器继承为同名目标，保留 Assurance fail-closed，不把恢复修复变成放宽验证。
- 两个公开反馈环已 RED：FailedStep_AcceptsObservedBackRecovery（2 cases）返回 null；Rejected misplaced renewal 测试仅得到通用 schema-invalid。

## Acceptance
1. 失败相位的已观测安全返回可以通过同号 Act 交给 Kernel；Defer→重新观察仍保持恢复上下文，不倒回旧目标；恢复完成才恢复普通遍历。
2. 不可见/不唯一/策略禁止的恢复动作不得成为 Effect；无恢复选项时有界停止。正常路径仍严格遵守 directive。
3. 错层 renewal 被严格拒绝且反馈具体字段；同一次咨询正确嵌入 proposal.renewal 后值不丢失。Prompt 路径明确；审计保留原提案与有效提案的 renewal。
4. 定向回归、全量与工件检查通过；公开工具路由确定性验证错误→纠正，真实 DSH 验证正确续额获批；真实失败分支验证恢复动作可采纳。整体覆盖结果如实记录，不把恢复成功写成全树完成。

## Constraints
沿用 ADR-0050、现有单一生命周期与 Owner 边界；旧 RUNTIME-CONSULTATION-001 的已通过字段传输能力保留。Direct 执行；不提交/推送共享改动。

## Verification
```yaml
verification:
  level: ENVIRONMENT
  method: "Director/Kernel and public DSH route regression RED→GREEN; real model + registered emulator; artifact and solution checks"
  expected: "failure recovery is reachable and bounded; malformed renewal can be corrected and granted without weakening schema"
  actual: "1651/1651 .NET、39/39插件通过；真实Quickstep失败→Defer→返回执行且验证成功，+2步/+3轮续额获批。字段纠错在确定性公开路由RED→GREEN；账本上限误报另已修复。定向计划仍有未覆盖项，不代表全设置树完成。"
  evidence: "evidence/settings-coverage-006/verification.md; live-focused/selected.json; .logs/"
```

## Status log
- 2026-10-18 · UNDERSTAND→RESOLVE · 原生输出与现有 E4 trace 核对，两个秒级公开入口测试已 RED；根因与修改边界明确。

- 2026-10-18 · IMPLEMENT · 恢复分支、字段反馈、续额路径提示和审计投影已改；Host 313/313 与插件 39/39 通过。清障 typed present→absent 保持原有恢复语义；滚动无效改为有界恢复耗尽停止。

- 2026-10-18 · REVIEW→VERIFY · Direct review 与验收分开完成。真实定向2次Effect/5次咨询：Quickstep唯一性失败后，Defer保持恢复，返回动作派发并验证成功；同一运行续额+2步/+3轮获批。方法细化：确定性公开路由证明错误→纠正，真实模型直接合法申请证明授予链路，不诱导非法输出、不混淆证据等级。
- 2026-10-18 · VERIFY→IMPLEMENT→REVIEW→VERIFY · 真实记录暴露 max-consult-rounds 误报。追代码确认并非续额后提前截停，而是无待办时理由优先使用旧初始上限；新增上限标志RED，按最终总上限修正后全量1651/1651通过。
- 2026-10-18 · VERIFY→CLOSED · 本修复范围闭合；插件39/39、prompt双份哈希、专用安装39文件、场景28/28、不变量47/47、diff检查通过。真实完整设置树仍未复验，不关闭SETTINGS-COVERAGE-005。见 verification.md 与 review.md；保留未提交修改。
