# WORLD-OBSERVATION-001 实施合同

工程细化，承接Q1–Q4，不改变Owner。入口基线为 evidence/implementation-entry/before/；本Change增量相对此目录审查，不把整个脏树diff归本任务。

## 观察与World消费缝
- 新增公开 `PageCaptureObservation`（CaptureId / ObservationCycleId / SessionCorrelation / CaptureTime / Space / Items），普通source proposal subject=`ui.page.capture`；Items为同capture结构/文字/布局观察，复用SemanticLayoutItem。严格JSON codec拒绝未知成员、空相关性、不合法坐标/尺寸、重复item。
- 新增 `PageIdentityObservation`（CaptureId / ObservationCycleId / SessionCorrelation / CaptureTime / Signature / ObservedTitle），普通derived proposal subject=`ui.page.identity`。Signature是观察adapter的上下文签名提示，不是ContainerId；PageKey、预期与confidence不入载荷。必须衍生自当前capture已入证record，ObservedTitle必须来自该capture的已观察Items；至少有一个额外非空文字或结构项提供布局依据。Fast有足够依据可直接提出，语义能力仅选择已观察title，不新增能力或provider直写World。
- World严格校验当前capture/cycle/session/time及派生依据，再调用既有IAssociationStrategy；ProductAssociationStrategy使用Signature，同既有ui.screen一样经World authority gates。旧ui.screen路径兼容；新capture路径只由匹配的ui.page.identity确认，普通内容Insufficient不撤销身份。
- WorldBeliefRevision新增不可变CurrentPageObservation与ObservationNeed；Need含capture record证据、相关性及revision，信息类型固定identity-disambiguation，不含provider命令。新capture先保留未归属occurrences；确认后在新revision归属同capture结构；历史revision不改。旧capture补证仅历史入证，不能恢复当前。
- 只有显式当前capture未确认才启用UI屏障；ResolveCurrent和DeriveBindingView不允许依赖该capture的UI effect；既有未启用capture的ownerless用途兼容。当前capture未确认时导航投影不能使用旧current claim推进。

## Control与验证
- ObservationDirective可携带World原样ObservationNeed及剩余等待预算；Control单独选择Slow深度。Host收到Need复用捕获材料，调用既有ISemanticPerception并产普通proposal，不制造新页面图。
- Control拥有每个待验证位置的有界观察恢复计数/时间，跨Drive等待保留；用原StepVerify、原receipt/attempt/预算位置恢复，不转StepAct、不再dispatch。零/超时/失败耗尽回既有验证失败/Recovery策略，保留未知。
- Assurance独立判断页面已确认、post-action证据时序及effect变化；确认不等于到达成功。Traversal仅接Assurance verified后的current container。
- 错capture/cycle/session、迟到、重复、取消不释放屏障；取消不等待模型。等待补证不新建attempt、不重置steps/rounds/local operations。

## 切片及验收
1. World/codec与反例RED→GREEN：当前观察、缺失信息、历史隔离、归属和直接Act屏障。
2. Control/Host接回同一链：有XML未知、无XML视觉文字充分，证明一次点击→等待→确认→独立验证→Traversal继续。
3. 双轴审查（相对入口快照）与协议生成/场景认证、相关回归；注册自有设备lease并预检，运行有界真实成功/失败样例；失败保留，不以回放代替设备执行。
4. Acceptance全部证明后更新state/协议/术语/索引；不提交推送。环境失败保持VERIFY，不将部分覆盖写CLOSED。


## 实施期所有者补充：慢感知high
无XML成功样例必须调用慢感知模型，并提供实际请求high的证据。原Jev实验保留为初轮对照。`DshSemanticPerception`沿已有SemanticPerceptionExecutor供货协议、既有DSH绑定的slow text模型调用`/slow`；请求参数high在DSH adapter/plugin隔离slow session的agent/request设置，未应用或供货不支持即失败，不静默降级。参数与provider词汇不进入共享Kernel载荷；不改主Agent默认effort或代理/凭据。新World身份需求替代旧NoXml/SemanticUnclear/StructuralVisualConflict材料启发式的并行模型调度，避免同一capture两条模型调用竞跑；旧popup观察保留。feed内共享已消费模型请求计数，Control子配额不重置既有预算。
