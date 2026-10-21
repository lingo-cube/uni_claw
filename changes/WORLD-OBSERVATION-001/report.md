# WORLD-OBSERVATION-001 实施与验收

## 做了什么
页面身份不足时，执行链现在停在原导航动作的验证位置，消费World的ObservationNeed补证；World确认本次页面与元素归属后，Assurance独立验证到达，再允许Traversal推进。无XML时调用慢感知，实际DSH模型请求强度为high。

## 原来的问题
新控件可在页面身份未知时沿用旧页归属；Host材料启发式与World的信息缺失没有显式消费缝，动作送达、页面确认与遍历进度也没有完整串联。仅有owner=null不足以保护直接Act/grounding和旧current anchor消费。

## 这次修改
- `World/PageObservation.cs`、`WorldModel.cs`：当前capture的严格codec、ObservationNeed、当前场景相关性与派生证据检查。未知阶段保留结构但不挂旧页，确认只重绑当前capture basis的occurrence；新revision重新mint ID，历史revision不变。同capture身份冲突保持未知，新capture可重新确认。普通内容Insufficient不机械清空整页。
- `Control/ObservationRecoveryControl.cs`、`Runtime/KernelRunDriver.cs`、`UniKernel.cs`：有界请求数与单调时钟期限，跨Drive保留原StepVerify、receipt、attempt和预算。页面身份屏障覆盖UI binding/直接消费及导航投影；补证后仍通过Assurance。耗尽发既有Recovery facts，保持原位置，不自动Back或把Run一律终结。
- `Host/PageIdentityRecovery.cs`、`SettingsTraversalLiveFeed.cs`：XML或同截图OCR/YOLO结构进入同一观察协议；收到World Need才补身份。切场景重采，保留原动作的source上下文；共享已消费模型配额，避免旧启发式与新需求两路竞跑。
- `Agent.Dsh/DshSemanticPerception.cs`、`DshOpenedHttpPeer.cs`、`Host.Dsh/Program.cs`及decision-channel插件：复用原provider/model与既有Perception能力，隔离slow请求实际设置high。请求hook未应用high或回执无证明即拒绝；取消按slowRequestId清理，不触及主决策session。
- P27、CONTEXT、公开面白名单、README、生成物和场景认证同步。产品模型参数只在DSH adapter/plugin表达，World需求不含provider命令。

## 验证结果
| 方法 | 预期 | 实际 | 证据 |
|---|---|---|---|
| 真实XML、身份不足，生产live feed→Kernel | 未知暂停→补证→归属→Assurance→Traversal；一次点击 | verified=1、depth=1、receipt=1；high真实请求 | `evidence/live-high-xml-final/with-xml/result.json` |
| 真实截图、实际OCR/YOLO，XML在输入边界不提供 | 慢感知high，走同一World/Assurance/Traversal链 | verified=1、depth=1、receipt=1；无XML输入成功 | `evidence/live-high-final/no-xml/result.json` |
| 实际DSH模型请求头 | 实际模型参数为high | 匹配本任务5个请求的归档header记录high；响应diagnostics也为high | `evidence/slow-high-final-request-headers.json` |
| 真实设备+模型后受控篡改identity session | 拒绝错误相关性，不推进、不重点击 | faultInjected=true，VerificationFailed，verified=0、depth=0、receipt=1 | `evidence/live-high-verified/wrong-session/result.json` |
| 真实模型超过30秒补证期限 | 未知不误恢复 | 等待位置保留，verified=0、depth=0、receipt=1；匹配请求取消 | `evidence/live-high-final/with-xml/result.json`及`EVIDENCE-BOUNDARY.json` |
| 确定性/仿真负向矩阵 | 不足、歧义、错capture/cycle/session/time/title/lineage、超时、迟到、重复、取消隔离；旧revision不变，原attempt不复活 | Kernel相关17/17；Host相关6/6；共享执行场景7/7；high供货与取消6/6 | `evidence/test-results/` |
| 回归 | 仿真/Host/DSH Host/插件通过 | Simulation257/257、Host406/406、Host.Dsh51/51、插件51/51 | TRX与`host-dsh-final.txt`、`plugin-full-final.txt` |
| 协议生成与图审计 | 真相源与生成物一致、图有效 | 7 machines、37 transitions、45 edges、27 steps、12 cards fresh；20个可执行场景认证刷新 | `protocol-check-final.txt`、`scenario-certify-latest.txt` |

全量Kernel为1028/1029，Agent.Dsh为209/212。剩余失败经实施入口比对归因于既有现场：Simulation多出的Agent.Dsh引用与closure断言冲突；两个握手fixture固定旧promptHash；一个fixture仍期待deepseekFlash而现场绑定为commandcodeFlash。未改这些既有选择，也未称全仓全绿（`evidence/existing-suite-failures.json`）。

真实设备为本任务注册的emulator-5556 lease，仅执行Apps→All apps安全导航。成功后因MaxLocalOperations=1停在awaiting-decision，不是Goal完成。无XML样例主动不向post-action观察提供XML，证明可选XML输入，不声称设备XML采集物理失败。超时/迟到/重复/取消完整矩阵主要由确定性与仿真覆盖，实机仅有界成功、关键受控失败及模型超时。

过程中的Jev样例、未成功构建而运行旧二进制的invalid-high样例、实验日志字段冲突和残留attachment握手拒绝均保留，不能作为high成功证据。实验已加入构建失败即停止、唯一事件字段和finally detach。最终XML成功与无XML成功来自最终部署代码。

## 新发现
高强度真实模型有超过30秒预算的情况；当前行为安全停在未验证位置。此处不增加原预算或降低强度。全量既有fixture/Composition Root漂移独立处理。未发现需要扩大本Change范围的阻断项。Standards复审无阻断；Spec复审与Leader处置见`review.md`。

## 下一步
本Change范围完成后关闭；完整Settings目标、子页滚动穷尽及全量既有fixture漂移留后续独立工作。本次不提交、不推送。已清理本任务启动的设备与专用DSH服务；保留用户要求的同模型high支持配置，凭据、代理及主Agent默认强度未改。
