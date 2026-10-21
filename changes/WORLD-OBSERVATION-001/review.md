# WORLD-OBSERVATION-001 双轴评审

固定点：实施入口 `evidence/implementation-entry/`（HEAD bce0ec3a981a4598b889bb0bf37d2b3e389aa41c + 入口 tracked.patch + before 源码）。审查差异为 `evidence/implementation.patch`，不是共享脏树相对HEAD的总diff。当前冻结文件哈希见 `evidence/review-source-hashes.json`。

## Standards

最终复审未发现阻断项。慢感知沿既有 UniPerceptionCapability/ISemanticPerception 返回普通供货结果，没有World写权；high仅进入DSH隔离slow请求，未获实际应用诊断即Unaligned；取消按原requestId清理slow，不影响主session。Control失败保留原验证位置，记录原attempt rejection和Traversal gap/pause，通过既有Recovery facts交策略，无自动Back或Run失败。

初轮发现及处理：
- occurrence重绑范围过宽：已限定当前CaptureEvidenceId；当前hierarchy只在capture/cycle/session/time完全相同才补该basis；新revision重新mint occurrence ID。旧revision保持不变。
- adapter与World标题空白处理不一致：已统一当前观察文字与标题的trim比较。
- 可能扩大语义范围：沿现有SemanticObservationProjector→P2/P3辅助语义处理，语义结果不能授予身份、到达或Goal成功；无新增Owner或领域判断协议。Leader判定符合原供货接线范围。

## Spec

最终实现覆盖六步顺序、原receipt/attempt/位置/预算保留，以及当前capture隔离。high真实证据为DSH request/header及响应diagnostics，不能用Leader推理强度、初轮Jev或单独模型名称输出替代。

初轮疑问及裁决：
- 孤立identity无有效当前capture时不新建ObservationNeed：拒绝无源场景，不能凭模型孤立声明创造当前观察；当前合法capture仍按Need处理。
- Signature上下文线索：保留既有association策略和World执法；载荷不接受PageKey/预期/confidence作为实体身份，当前capture、lineage、观察标题与布局必须一致。通用来源防伪不在本Change新增范围。
- 首轮真实证据不足：已补实际设备+模型with-xml/no-xml成功及受控wrong-session失败；早期失败、Jev、无high旧二进制均保留且不计最终验收。
- 最终复跑一度因实验序列化异常遗留attachment导致握手拒绝：修正实验事件字段、finally detach，仅重启本任务自建服务后复跑。结果以report.md和最终result.json为准。
- 实机负向覆盖：仅wrong-session为受控真实链关键失败；timeout/late/duplicate/cancel由确定性与仿真证明。A4要求其不误恢复，A8要求有界真实成功与关键失败，未要求每个负向全实机化；必须明确披露此边界。

Leader：Standards无阻断；Spec最终关闭取决于最终部署的high样例、协议/仿真、A1–A8四元组，不能仅据初轮评审结论关闭。

最终Spec复审：最新部署XML与无XML均receipt=1/verified=1/depth=1，5个匹配真实请求header为high；受控错session与真实超时保持depth=0。完整负向矩阵的仿真/实机边界已披露，无剩余实现或验收阻断。Leader确认范围内A1–A8证据成立，可以CLOSED；全仓既有失败不冒充通过。
