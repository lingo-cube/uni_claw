# World消费侧增量说明（implementation，2026-10-21）

已读取上游 `../JEV-SEMANTIC-CAP-001/execution-contract.md`。唯一观察subject沿用`ui.semantic.page`；PageKey是语义类别，不能单独铸造或合并ContainerIdentity；预期/符合性不进入该claim。

## 当前独立修复
- WorldModel对`ui.screen`使用Owner私有的页面观察时间标记，区分历史依据与当前适用性，Reaffirm也推进该标记。旧页面结果保留accepted evidence与basis，不能替换当前页面claim、identity、occurrences或追加关系。普通状态claim继续保留原有冲突/权威裁决语义，未将这一保护泛化为所有claim的last-capture-wins。
- 本次`ui.screen`claim产生冲突时，身份/关系接纳不能仅因strategy提议New/Matched而继续；冲突与依据仍留存。New候选同时执法support全部合法、显式contradicting basis为空，不仅检查“包含当前EvidenceId”。
- Host签名在真实receipt目标变化时使用已接纳且唯一的元素语义描述，不能把capture-local occurrence id作为稳定入口。现有live feed调用处显式核对当前描述唯一性；缺描述或重复名称时保留无法判别，不从opaque id猜语义，不合并不明入口。

## 与上游载荷的接缝
- `World/UiRealization`基线禁止依赖Capability/Perception模块，合法输入为accepted Evidence。不能为了读取SemanticPageClaim类型跨模块伸手，也不在World复制上游协议record。
- 语义观察应辅助已接纳的页面/当前观察解释，不将PageKey或ObservedTitle当作独立实体签名。现有签名和观察端点仍决定身份，成功导航继续走Assurance/World既有入口。
- 同帧关联至少需要指向已接纳当前身份观察的明确依据关联；仅“时间接近”“分类相同”不能证明同页。同帧capture/cycle/session来自上游唯一payload；当前ui.screen来自Host，provenance中未统一暴露这些字段，不能凭臆造格式补齐。
- 新semantic subject的首条结果也可能晚于切页到达；因此语义消费独立核对当前页面观察依据及exact capture，而不是仅看该subject是否已经有值。已验证同类不同实体、旧帧首结果晚到、typed session/cycle不符和缺当前身份依据等反例。
- 上游官方执行/投影代码已落盘。本会话直接使用该实现完成注册→L1调用→官方SemanticObservationProjector→P2→World→Slice确定性联测，未修改Capability/Perception或自造协议载荷。

## 已落下的Owner辅助消费（供上游联合验收读取）
- 复用上游唯一`Capability.SemanticPageClaim`，解释器位于World owner partial（不在UiRealization模块引用Capability）。不新增公开载荷或第二份语义record。
- 通过当前身份签名的accepted依据与exact capture关联，将有效语义作为`<currentContainerId>.semantic.page` scoped claim提供给既有Slice；原始value沿用上游JSON，PageKey不参与实体mint/merge。
- **当前最小必需关联**：语义proposal的DerivedFromEvidenceIds或canonical transitive leaf basis中包含当前身份观察EvidenceId；semantic capture time等于该观察的原capture time；该观察有typed hierarchy capture或现有live feed的明确`capture:<id>` lineage，且与SemanticPageClaim.CaptureId相等。typed来源另核对session/cycle。原始Observation的source evidence不满足时只保留历史依据并在AssociationLog标不足，不猜同帧。
- 因而上游调用时Current.EvidenceIds/叶依据应包含实际当前身份观察（不是plan、expected或目标ContainerId），并保持P2 parent/leaf closure正确。仅提供不相关YOLO/OCR叶依据时，World不会因类别或时间相近而给当前容器贴语义。
- 任何策略对该辅助subject提议New/Matched都由World拦截；有效语义只丰富既有容器，不改signature/当前identity，不产生导航边。关联不足、首次结果迟到、旧frame、同类不同实体不能绕过这一约束。
- Owner按上游唯一SemanticPageClaim严格读取字段，夹带expectedPageKey等未声明字段的原始JSON不能进入当前claim/Slice；accepted记录及其basis留存，不将Ledger admission等同于World接纳。
- 无typed来源时，session/cycle的完整性依赖上游L1输入核验；World不从legacy capture lineage臆造不存在的session元数据。此路径只通过明确当前身份证据、exact capture id与原capture time建立关联。

## 验证与冻结
World/关联/语义/导航/滚动/P23/Slice边界最终专项100/100；Host全量400/400；相关Simulation70/70。当前完整Kernel1009/1012，剩3项是旧analysis元数据、Simulation多引用和其他两个文件README登记，见report.md。已将12个负责源码/测试文件记录于evidence/source-freeze.json；本会话不再改源码，共享文档/公开面/scenario由主会话收尾。

## 并行编译说明
2026-10-21本轮World测试曾被上游暂未落盘类型和后续局部变量重名编译错误阻断。为不改动或临时删除他人文件，仓库外focused项目仅编译相同World回归源码并引用已构建产品程序集，取得真实冲突RED。临时排除尚未完成的上游测试仅用于中途定位；最终专项和Kernel全量已在当前完整源码上重跑，不再排除这些上游测试。临时项目清理，失败输出和TRX保留。
