# SIM-017 源码与录制审计

审计范围按 WorkItem：Change State、Host popup classifier/live feed/coverage director、World ProductAssociationStrategy、SIM-016 red output、recent-recordings replay manifest。只读源审计；未运行设备或模型。

## 观察到的调用链

1. `SettingsTraversalLiveFeed.Next` 为每周期生成 `captureId` 与 `settings-cycle-NNN`，截图与 XML 分开采集；截图可独立落盘，XML 成功时以 captureId 命名落盘。它用 Kernel RunId（缺省时 device 伪 session）构造页面观察 capture。
2. Popup 分类输入仅为 `xml + hostPackage + resourceIds`。`SettingsPopupClassifier.Classify` 读取 XML 节点 package 与资源 ID；返回 `present/absent/null`。这里没有窗口列表或 overlay relation 输入。
3. `PopupProposal` 对非 null 结果发出 `ui.overlay.popup`，provenance 含 producer、采集时间、cycle scope、capture lineage；null 时无 proposal。
4. `ProductAssociationStrategy.PopupSubject` 注释将其定位为普通世界证据，明确不参与 container association；该策略不会提供窗口归属补证。
5. `SettingsCoverageLedger.RecordPopupState` 只接受 present/absent，追加事件；`LatestPopupState` 取最后一条。`Snapshot` 将其作为 PopupState。无声明不会自行清空最新状态。
6. `SettingsCoverageDirector.Consult` 对 snapshot present 优先清障；absent 重置 obstacle attempts 并继续普通路径。unknown 没有单独分支。因而“没有本周期 XML”与“popup absent”目前严格不同，但历史 present 可能继续成为最新快照。

## 录制证据与局限

`recent-recordings.json` 声明素材来自 recorded layouts 与 controlled identity/delivery/agent substitutes；时间窗、分辨率与 API level 是 replay context。元数据明确指出 API level、capture time、cycle/session 是回放上下文，不证明原采集事实/新鲜度；legacy XML 使用 collapsed checked capability。每个案例的 `recordedAt` 是 run preregistration frozenAtUtc，不是逐帧 wall clock。

Default apps 案例提供 before/after XML 路径与 SHA-256、captureId 和 replayTime、route 变化、受控页面身份输入，以及一次 tap 的结果。`previousCaptureRelation` 是前一步最后记录的 post，没有显式 pre-action capture 字段。故它可以证明：XML的package跨包、原route退为android.settings，SIM-016独立oracle将该回放场景标为正常页面；当前包名规则会把跨包结果分类为present。它不能证明：原设备窗口栈、窗口边界、焦点/覆盖关系、逐帧真实墙钟及采集原子顺序（trace只能证明记录顺序）、采集时同屏截图/XML原子性、当前 session freshness，亦不能把 controlled perception substitute 说成真实模型输出。

`evidence/sim-016/classification-red.txt` 记录公开 Host replay 测试失败，错误为正常 Default apps 页面被断言为 popup 且实际输出 `present`。这是分类反例证据，证明 package mismatch 单独作为 popup 充分条件不成立；它不证明该录制帧实际覆盖关系，也不授权本任务修改分类。

## 缺口定位与设计约束

缺口分两层：分类层缺少 window/overlay 事实；生命周期层把最近一次 present/absent 作为持续状态，没有 unknown/stale/conflict 消费语义。修复第二层必须让状态的“当前性”依赖同一 Run、同一 cycle 的证据关联，且缺失不能被解释为 absent。相反，继续无限消费旧 present 也不安全，故须将其标 stale 并走有界补采与停止。

证据 Owner 建议：Host acquisition 负责窗口来源采集、capture/cycle/session 关联与原始材料保留；Evidence/World 负责来源、时间、支持/冲突及可追溯记录；SettingsCoverageLedger只维护coverage-facing派生镜像，当前性权威判断留在既有Owner及新鲜度机制；Director 只执行已确认状态的控制分支并管理 bounded recovery。分类器保持现有 package mismatch OR whitelist 逻辑，直到另一个 Change 明确授权。

候选协议字段未形成冻结结论；没有源码证据证明最终类型、命名、通用性或预算数值。任何公开 surface 与状态 enum 应在后续 Owner 裁决和详细设计后确定。
