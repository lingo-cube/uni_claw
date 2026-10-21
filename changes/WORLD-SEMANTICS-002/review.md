# World范围实现评审

性质：本会话自审，Standards与Spec两轴直接检查，不冒充独立agent评审。主会话仍负责共同协议/公开面/文档和最终联合验收。源码已冻结，哈希见evidence/source-freeze.json。

## Standards：职责、边界与真实增量
- 以evidence/entry/files为基线比较，保留进入时已有World滚动关系、occurrence依据与其他dirty修改；未从HEAD重建文件。增量见evidence/owned-incremental.patch。
- World拥有当前适用性、身份/关系接纳和scoped投影；上游唯一SemanticPageClaim由World owner partial读取，UiRealization没有增加Capability/Perception依赖。新增成员均private，公开关联接口与Slice形状未扩大。
- 页面时间标记用于当前`ui.screen`适用性，和Assurance freshness判断分开；普通状态claim仍可记录旧/异源矛盾，并交原权威域裁决。初次实现过宽导致既有功能回归，已收窄，保留失败TRX与最终全量结果。
- 当前身份观察索引只保存每个已关联容器的最近accepted record引用，不拥有第二套admission、跨Run状态或独立current truth；只在revision成功发布后更新。
- semantic PageKey不做实体匹配键；任何插件对该subject提议New/Matched均拦截，且不能产生relations。导航/滚动依然由既有独立验证入口接纳。
- Host仅将唯一的既有元素描述用作现有签名的入口区分信息；opaque occurrence id不进入签名。对缺描述/重复名称返回无法判别，现有feed显式提供唯一性，不另建业务接线。
- 没有修改Capability/Perception、UniKernel、共享CONTEXT/协议/生成索引；World README仅新增本partial文件登记与职责说明。未提交、推送或操作设备。

## Spec：缺陷与行为对照
| 要求 | 最终行为 / 证据 |
|---|---|
| 迟到不能回退身份 | A(t1)→B(t3)→A(t2)维持B；同页Reaffirm(t3)后旧异页也不mint，fresh返回仍可Matched；WorldIdentityReconciliationTests |
| 冲突不能推进当前页身份 | 同producer同scope不同ui.screen保留Conflict和原身份；New候选显式反证/非法support不能越过Owner gate |
| 重访稳定且不强合并 | 相同父上下文/观察route/唯一入口描述与不同occurrence id得到同签名；不同描述保持不同；缺失/重复描述不生成入口签名；SettingsPageIdentityTests |
| 新语义经正式链路消费 | Registry→ISemanticPerception→官方projector→Ledger→World→Slice；两个供货实现使用同一调用代码，Mismatch意图不污染实际meaning；SemanticCapabilityWorldIntegrationTests |
| 新语义不等于实体身份 | 同类别不同已观察容器保持两个身份；旧capture首个语义结果可admit但不贴当前页；typed capture/session/cycle/time不符均不贴；WorldSemanticEvidenceTests |
| 意图不能包装成观察事实 | 上游不投影意图；Owner也严格读取唯一claim字段；原始JSON多出的expectedPageKey反例RED后GREEN，不写当前claim/Slice |
| 原路径保持 | 最终100个World相关专项、Host400、Simulation70通过；完整Kernel1009/1012，剩3项均为下述共享工作区问题 |

## 评审发现及处置
R1：将所有claim的历史/冲突都阻断会破坏状态权威裁决和合法occurrence派生。已将页面当前适用性保护限定到既有`ui.screen`，新语义单独核对当前身份证据/capture；普通状态机制保持，原功能回归消失。中间失败见kernel-bounded-regression.trx，最终见kernel-reviewed-final.trx。

R2：稳定描述代替临时id后，重复描述可能强合并不同入口。已新增明确唯一性输入，由现有feed根据accepted前页occurrences计算；非唯一反例先RED，再GREEN。没有用字符串猜opaque id含义。

R3：默认JSON读取忽略额外字段，可能将夹带意图的raw value透传到Slice。已用唯一协议record严格读取未声明字段；semantic-extra-field-red.trx记录反例，world-reviewed-final.trx包含修复后结果。

## 当前未关闭的共享检查与残余限度
- DocsMetadata：旧docs/analysis/ui-automation-test-function-reuse-inventory-2026-10-20.md缺Status/Authority。
- ProductHostClosure：进入本轮时Simulation测试项目已额外引用Agent.Dsh，与原两引用断言冲突；本次未改该csproj。
- SourceReadmeOwnership：剩SettingsNavigationScope.cs与ProfileIdentityRenderer.cs登记，均不是本次World实现新增；WorldModel.Semantics.cs已登记。主会话正在处置共享文档，本会话不改它们。
- 当前语义只是有依据地丰富已识别容器，不能独立判定相同route/同名页面是否为新实体，也不新增Contains/Overlays生命周期或语义目的地Effect验证权威。
- 没有在本会话调用真实Jev或设备；确定性替换测试不能当模型准确率。主会话负责真实线路与scenario联合证据。本Change保留verified等待共同收尾，不以源码冻结声称所有共享检查已绿。
