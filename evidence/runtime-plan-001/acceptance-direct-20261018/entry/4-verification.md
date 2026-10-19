# ADR-0053 实施验证（2026-10-18）

当前产品调用链为正式DSH决策 → AgentDecision.Plan/ContinuePlan → 既有KernelRunDriver → 当前动作guard → Control/grounding/Assurance/Effect → 独立post-action验证。ForEach只从Owner只读候选投影选择语义目标，不持有定位/权限/完成权威。原始提案、实际物化动作、候选来源、guard、位置与receipt分别留证。

| 行为 / method | expected | actual | evidence |
|---|---|---|---|
| 动态候选及复核续行：公开Driver受控输入A/B/C | A处理后发现C由原规则继续，B有Owner证据受阻；预算复核后精确ContinuePlan，不重发已确认动作 | 通过；2次独立验证及唯一receipt，来源revision与attempt关联 | evidence/runtime-plan-001/core/dynamic-green-attempt11.log；最终Kernel回归 |
| 版本、暂停、资源与在途反例：公开Driver | 新版本替换旧尾、错误位置/同版本拒绝；NoAction不因续额续跑；零Effect工作量不因恢复重置；在途再次Drive不重派发 | 通过；E4/聚焦/拒绝/缺根等分支对账同次attempt，ForEach旧step锚不可误证 | core/compact-plan-focused.log（38/38）；integration/kernel-compact-final.txt（937/937） |
| 不同结构记录买方：同Driver替换事实/动作实现 | 显式补证后核验记录状态；保留Owner局部受阻，无页面树/返回导航特判 | 通过；R17补证、独立结果claim与Run义务验证，R22保持受阻；未新增任意状态判定语义 | core/record-buyer.md；core/record-buyer-green.log；KernelRunDriverPlanTests.RecordBuyer |
| 正式模型通道codec/Schema/生成物/Prompt同步 | 动态Plan与位置准确往返，嵌套/未知结构拒绝；产品与插件副本同源 | Agent.Dsh184/184；Prompt/Profile revision17，schema9e9aa4a…，Prompt a1574ef2…；Node最终44/44、exit0；直接挂载夹具补清理登记，产品src不变 | integration/uniclaw.agent.dsh.tests-final17.txt；integration/plugin-tests-attempt1.txt；protocol/retry-failure-fence-green.txt；integration/plugin-tests-final44-green.txt；integration/product-prompt17-check.txt |
| Settings逐动作检查、Owner投影与receipt归属：两种Plan同Host | 不用咨询时旧页预放行；未来目标到达时检查；每个实际步骤独立归属 | 24/24接线回归，4步/2咨询贯通；新增终局停滞反例10/10，最终Host341/341，Agent29/29，Host.Dsh34/34（未启用现场开关的测试不算现场证据） | host/host-integration-attempt3.txt；host/terminal-stagnation-green3.txt；integration/uniclaw.host.tests-final17.txt；integration/uniclaw.agent.tests-final17.txt；integration/uniclaw.host.dsh.tests-final17.txt |
| 场景与workspace生成执法 | 原场景输入/期望不变，致因源hash认证同步；schema有效/非法反例维持 | Simulation188/188；20个执行场景只改认证元数据；workspace13组通过 | integration/scenario-recertification-compact.txt；integration/scenario-check-final.txt；integration/uniclaw.simulation.tests-compact-final.txt；integration/workspace-schemas-attempt1.txt；review/independent-review.md |
| 独立规格/规范审查 | 未参与实现的新Worker审查dirty入口增量；两轴分离 | 首次71项哈希核对，后续retry/紧凑投影/Host停滞/Provider实际产物经另一只读Worker增量复审，无剩余产品/规范阻断；新模型对照完成，性能门槛未全部通过，状态保持VERIFY | review/independent-review.md；review/post-live-fix-review.md；review/disposition.json；review/reviewed-inventory.json |
| Provider失败响应计量：真实完整usage先于非法输出拒绝 | usage已收齐时即使工具JSON无效仍保留消耗，无成功finish，不修复坏JSON/增加retry | stream/runtime/Loader组合209/209；实际built adapter本地SSE实测usage17+MALFORMED_RESPONSE+无finish；headless定向会话回放1/1，134无关用例跳过 | protocol/dsh-failed-usage-green.txt；protocol/dsh-provider-built-smoke.json；protocol/dsh-usage-session-snapshot2.txt；protocol/dsh-provider-usage-diagnosis.md |
| 注册环境与真实三组对照 | 同目标/模型/权限/观察验证；先Step基线/数值门槛，再ShortPlan与RulePlan；全部失败消耗入账 | Flash accounted六次Plan均功能完成且计量完整，四组中三个通过全部门槛，known短Plan调用未改善；Owner授权Pro后同模型3份Step、10份Plan均功能有效/usage完整；候选短/规则两组全门槛通过，known短调用/时间及known规则时间未过，仍未全通过 | comparison/comparison-result-accounted.json；comparison/comparison-result-pro.json；comparison/model-change-pro/results.md；comparison/model-change-pro/authorization-and-binding.json；comparison/procedure.md；integration/device-start.txt；integration/service-reload.json；comparison各trial原始产物 |

所有evidence相对路径以上述`evidence/runtime-plan-001/`为根。细分build/fixture失败日志保留，但不替代最终行为结果；默认不运行的ENVIRONMENT测试不能算设备验收。费用若provider未提供账单则记不可得，不能把token或咨询数直接冒充货币节省。

部署前已备份插件文件及profile包/锁。3081当前实际运行`uniclaw-product`独立profile，默认web部署误用已按部署前备份恢复41文件；失败启动因端口占用退出，原专线在确认空闲后才重载。3080未重启；不改家目录模型/代理配置。原试验正式绑定为deepseek-official/deepseek-flash。Owner明确授权更换模型并重跑后，对照期间binding选择deepseek-official/deepseek-v4-pro，之前binding完整副本及前后hash已保存；原provider thinking disabled保持。新模型必须有独立Step/门槛/Plan对照，不混用旧模型成绩。注册文档旧模型表不作为本次实际模型证据。

最终性能结论：Pro两个候选组通过，两个known-path组未全部通过。原fixed门槛不调低，首次及全部追加样本不删除。两份known Plan使用结果盲法late lock，另四份known复核是结果触发的有限扩展；独立评审与明确偏差记录均保存。因此本Change不CLOSED，不能宣称普遍提速、货币节省或全Settings覆盖。

Owner确认暂不采用Pro后，正式默认已恢复deepseek-official/deepseek-flash；Pro choice及实验保留，记录见comparison/model-change-pro/returned-to-flash.json。模型会话时间包络与网络占比的区别见comparison/model-change-pro/latency-envelope-audit.json；未新增真实运行，不改变既有性能判定。
