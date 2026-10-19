# RUNTIME-CONSULTATION-001 — 咨询输入、慢感知会话与预算续额修复
lifecycle_state: closed · disposition: none · depth: standard · base: 2225c642

## Intent（WHAT/WHY）
Owner 已批准合并提案：消除每轮重复附加静态提示词，回收内部慢感知会话，接通有上限的预算续额，并核验慢感知结果是否进入后续决策。本次真实基线为 SETTINGS-COVERAGE-005 live-2：同一主会话 112 次咨询、每轮 6423 字符相同前缀、renewalWindow 全 null；Slow 每次创建独立会话且没有归档。

## Scope / Out of Scope
- DSH decision-channel 的静态指令挂载及 Slow 请求生命周期；Agent.Dsh 的既有 renewal 字段传输；SettingsCoverage 配置与 policy 组合；对应产品工件、安装配置及公开入口回归。
- 核验 Slow 请求→结果→观察提案→证据→世界模型→下轮决策，只有证据确认断点才修改。
- 不扩大 Effect 权限、不新增感知能力、不重写遍历策略，不把完整设置覆盖作为本 Change 完成条件。
- 保留已有 SETTINGS-COVERAGE-005 和并行修改；入口快照：/var/folders/_3/lbphn3p95f3dj8ds3z1874q00000gn/T/uniclaw-consultation-entry-eej3n37s。

## Decisions
- 主咨询复用既有 session；静态指令须在真实模型可见的会话作用域完整挂载后才切换 section，禁止仅删除每轮内容。
- Slow 保留每次独立上下文，使用宿主 workspaceRegistry.archiveSession 归档，保留日志；完成/失败/超时/abort/detach 均须收口，不更改 Product 生命周期。
- 初始预算保持 96 步/112 次咨询，最终总额不超过 256 步/320 次咨询；按既有累计续额口径映射，UniAgent 提案、规则审批，拒绝或触顶诚实停止（ADR-0050）。
- 公开验证缝：DSH HTTP routes + submit_decision，ProductProtocolJson/schema，SettingsCoverageConfig→Runner→KernelRunDriver，真实 DSH/模拟器记录。Owner 对提案的批准包含这些验证场景。

## Acceptance
1. 连续咨询的 user/message 不重复附加静态正文；真实模型 system 输入仍含身份、输出约束和完整 Skill。
2. Slow 成功/失败/超时/abort/detach 后会话归档；活动请求不串结果；归档失败可观测，不能宣称已回收。
3. Act/NoAction/Defer 的 renewal 通过 JSON/schema/工具入口往返，未知字段仍被拒绝；任务配置驱动续额窗口；初始+累计续额不超最终总顶。
4. 有界运行证明临近预算时可申请续额、规则授予并留痕；越界拒绝的确定性回归通过。
5. Slow 结果逐次说明接纳/拒绝/过期/无有效提案，不以调用成功代替语义生效。

## Verification
```yaml
verification:
  level: ENVIRONMENT
  method: "public boundary RED→GREEN; full relevant tests and artifact checks; dedicated DSH and registered emulator bounded run"
  expected: "nonduplicated static inputs, archived Slow sessions, bounded renewal, traceable Slow outcomes"
  actual: "1645/1645 solution、38/38 plugin；真实单会话14轮用户输入无重复静态正文，system完整；两笔续额累计+8步/+8轮，最终12步；4个设备Slow会话全部归档。完整设置覆盖未完成，非本次完成条件。"
  evidence: "evidence/runtime-consultation-001/verification.md; live-selected.json; slow-live.json; .logs/"
```

## Status log
- 2026-10-18 · UNDERSTAND→RESOLVE→PERSIST→PLAN · Owner 批准提案；复验 dirty checkout 与既有证据，独立留档，Direct 执行。
- 2026-10-18 · PLAN→IMPLEMENT · 先后复现 renewal 三变体丢失/拒绝、已授额度被初始 MaxSteps 截断、scope-less 探针误判、审计未落盘；逐片 RED→GREEN。修复对应 adapter/Host 既有缝，工件修订同步；专用部署配置切回 section。
- 2026-10-18 · IMPLEMENT→REVIEW→VERIFY · Direct review 无阻塞；全量 1645/1645、插件38/38与场景/协议/工件检查通过。借用注册设备进行短轮验证，真实输入/审批/归档证据核对；新增审计 JSON 投影单独确定性验证。
- 2026-10-18 · VERIFY→CLOSED · 提案内 acceptance 已证明，见 verification.md。真实覆盖仅25%、consult-budget-exhausted，不关闭 SETTINGS-COVERAGE-005，不启动完整256步运行；保留未提交修改与环境原所有权。
- 2026-10-18 · 交付复验 · 跨轮次后专用3081进程退出，真实探针连接拒绝；已保留环境失败记录，恢复独立后台服务后 PromptProbeE2eTests 1/1 通过。源代码摘要无漂移，不重跑长遍历。
