# 限定样本的决策输入留存核验

状态：只读核验完成，证据有界；非回放运行结果。日期：2026-10-22。

## 结论

现有样本可以支持部分运行事实诊断；尚未证明可精确还原模型当时收到的完整输入。消息事件确有留存，不能说“没有messages字段，所以没有输入”。也不能仅凭context快照和当前提示构造源码宣称精确重放。缺少完整性证明的点不得用于比较模型或prompt优劣，更不能用于发布门槛。

本结论限定于`evidence/ordered-closeout-20261022/bounded-live-01/consults/01/`。Worker检查样本结构及事件类型，Leader核对源路径、现有决策入口和结论范围；没有把该结果推广为所有历史语料均缺失。02/03文件的存在不证明它们内容同等完整；旧RUN-006未在本轮核验。

## 逐项事实

下表路径均相对上述样本目录。

| 核验项 | 已见证据 | 可以证明 | 仍未证明 |
|---|---|---|---|
| 决策上下文 | `context.json`含decisionId、runId、contractVersion、contractViewRef、decisionBoundaryId、progress、budgetRemaining、executionOverview | 存在结构化快照及预算/进度/标识字段 | 与最终provider请求逐字一致；全部引用是否解析成当时可读内容 |
| 系统提示与统计 | `trajectory-summary.json`有systemPrompt（16110字符）、sections、turns、usageAttempts、submitCalls等 | 留存部分提示内容和交互统计，可供事实诊断 | 这些内容就是最终完整输入，或全部来源都已钉定版本 |
| 消息与工具事件 | `trajectory/dsh-session.raw.jsonl`共22条事件，包括system/message 1、user/message 1、assistant/message 2、tool/call 1、tool/result 1，另有request/header、request/context、agent/inbox/spliced | 有部分对话和一次工具往返的结构，可关联callId/seq | Host额外注入、历史截断、工具定义以及消息组合次序是否完整等于provider实际输入 |
| 同一会话导出 | `trajectory/dsh-consultation.raw.jsonl`含同类事件；`trajectory/dsh-session-export.zip`内session.v4.jsonl共22条、事件类型相同 | 有会话/咨询副本与导出材料 | 多个副本独立证明输入完整；不因副本数量多而提高真值等级 |
| 提示探针 | `trajectory/dsh-prompt-probe.raw.json`有sections 2、contexts 0、tools 1、variables 3，元素为字符串 | 有探针输出 | contexts=0表示模型没有上下文；字符串已含完整工具协议或全部Host内容 |
| 模型和配置 | 会话model/selection及request/context含model/provider/contextWindow | 存在模型/provider标识 | 原模型不可变版本、采样参数、profile/config/prompt版本或哈希均完整；当前配置可代替历史配置 |
| 关联性 | context的DecisionId/RunId、消息seq、工具callId与submit事件存在 | 有进一步关联的线索 | 本轮未做完整值级join，不能宣称全部预算、usage、submit与证据引用一一核对通过 |

## 正式入口及重建限制

`DshAgentAdapter.cs:153-160`构造DecisionRequest，`DshOpenedHttpPeer.cs:187-195`序列化context；`dsh/uniclaw-decision-channel/src/index.js:604-634`的consultationPrompt将动态字段及request.context拼入咨询文本。这证明正式构造路径存在，不证明旧版本与当前源码相同，也不涵盖DSH Host之后可能追加的上下文、工具、截断或provider序列化。DshSlowConsult是慢感知，不作为决策输入证明。

后续若要标为“同输入模型/prompt比较”，至少需核对：实际请求边界的完整输入（消息、工具定义及相关往返）、Host注入来源与顺序、历史选择/截断规则、原配置与版本标识，以及决策/预算/证据关联。这里列的是尚需证明的集合，不声称这些信息必然从所有现存日志中丢失。当前systemPrompt全文可辅助核验，不能替代其余输入。

## 已接受的设计影响（Q11=A）

Q3已接受首版B1为诊断报告，不是发布门槛；Q11于2026-10-22接受A，旧资料纳入有明确证据范围的辅助诊断。资料不足处不猜填、不将缺字段当零，不比较未证明同输入的结果，也不纳入该类对照的统计分母。后续新样本满足输入留存资格后才能用于正式对照；纳入/排除数量和理由应可追溯，不能把排除样本记为模型失败或成功。新动作造成的世界分叉仍由B2/C另证。本裁决不代表本轮开始采集或调用模型。

## 本轮验证四元组

method：限定文件清单、JSON键/类型/长度与JSONL事件结构核验、正式决策构造链静态阅读；未重放或调用模型。

expected：区分上下文、轨迹与实际最终请求；仅陈述可由留存物证明的内容。

actual：留存结构和局部消息可定位；最终输入完整性、历史截断及完整版本/值级关联尚未证明。未测试任何prompt/模型优劣。

evidence：以上样本文件与源码锚点；本轮快照与保护核验见`evidence/round-5-entry.json`、`evidence/round-5-verification.json`。
