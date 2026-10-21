# 语义能力首版执行契约（2026-10-21 implementation slice）

用户已接受Q1-Q5及插件替换修订，并于2026-10-21授权进入实现，语义感知能力与World关联联合验收。以下是据已接受边界细化的实现合同，不将工程细节伪称新的用户裁决。

## 公开消费缝
- 既有`ISemanticPerception`补兼容默认实现的`ObserveSemanticsAsync(SemanticPerceptionRequest, CancellationToken)`；旧marker实现返回Unsupported，不伪装为真实执行。
- 既有`UniPerceptionCapability`仍是L2及同一注册实例，通过可替换`SemanticPerceptionExecutor`注入执行实现；Jev Adapter只实现该供货缝，不直接实现L1。
- 请求：RequestId、CapabilityCorrelation、当前SemanticObservationContext（capture/cycle/session/time/space、节点id/kind/text/bounds/confidence、有界EvidenceIds及叶依据）、候选页面语义目录、可选前页观测、实际元素/操作引用、可选SemanticEvaluationIntent（预期页目录key和说明，仅意图）、预算。
- 当前语义目录key是调用方的语义分类标识，不是World ContainerId、路径身份或已到达声明。缺目录外页面仍Unknown；不新增硬编码Android页面白名单。
- 当前布局必须同capture/cycle/session/space且fresh、有证据引用。前页保留自身旧capture，不强行与当前同帧；动作明确关联前页capture。意图与观测单列，不扩P22或RequiredClaim，不把新载荷塞旧SlowConsultationRequest.Reason。
- 当前上下文禁止PageName/PagePurpose/PageKey先验，避免预期伪装成当前观察。前页名称/用途只在独立的caller-supplied-previous-context分区供参考，不写入current。完整请求按System.Text.Json Web默认编码的UTF-8 JSON不得超过128KiB（含布局、目录、意图、动作、相关性与依据）；超出不调用供货方，不截断后假装信息完整。

## 输出与投影
- `SemanticPerceptionResult`独立表达执行状态、当前语义`SemanticObservationProposal`、前后SamePage/DifferentPage/Unknown、预期Match/Mismatch/Unknown/Conflicted和`PerceptionAssessment`。
- 页类型与预期匹配矛盾时保留有效观察，符合性标Conflicted；HTTP/JSON/超时/取消/缺配置失败不产生观察。模型provider confidence只诊断，不授权，不成为阈值真值。
- `SemanticObservationProjector`将有效当前语义候选转为普通ObservationProposal，唯一subject **`ui.semantic.page`**。value是`SemanticPageClaim`的JSON：**PageKey, PageName, PagePurpose, ObservedTitle, CaptureId, ObservationCycleId, SessionCorrelation**（camelCase序列化）。ObservedTitle只能来自当前已输入文字项，不是预期名称。
- projector不发`ui.screen`、current-container或NavigatesTo；不把预期/符合性结果写成观察。不带目标ContainerId。P2继续检查immediate parents和leaf closure，只有accepted记录才能到World。
- World团队可消费accepted `ui.semantic.page`作为辅助语义依据；PageKey仅类别，不能单独成为实体等价标准或mint canonical identity。现有ui.screen、端点证据、操作及验证入口继续执法；需要更强身份判别时标明不足而非按类别强合并。
- 如果语义结果补充了同帧快观察，身份当前适用性与异步迟到保护由World现有Owner执法；不能因为期望页或低层HTTP成功创造导航边。

## 分工与联合验收
- 本会话：Capability/Perception载荷、L2、Jev Host Adapter、显式组合入口、健康探测与限制说明、契约/真实线路/替换测试、公共面和基线文档；共享文件先保存baseline再定点改动。不新增可选的剖面mixin。
- World会话：World/关联策略及测试，已复现迟到/冲突/签名问题；不要另定义上游载荷，也不修改Capability/Perception/共享协议文件。
- 接口测试与Host组合、Jev HTTP及P2→World消费闭包是本次已授权验收缝。确定性替换件与Jev执行件使用同一公开L1调用闭包，调用方不包含provider判断。
- 联合场景：实际进入/返回、同页更新、不符预期、材料不足、取消/超时、坏响应、跨帧、迟到/冲突、同类不同实体；真实API回放只证明该批历史布局的模型响应，不能冒充真实设备动作。
- PER-022已定Fast strategy接口收internal与载荷补充同批完成；保留产品公开构造可用性，必要时提供只依赖公开live realization的兼容构造，不把内部strategy再暴露。
