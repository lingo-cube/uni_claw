# World 版本业界资料研究

日期：2026-10-23
Authority: NONE
用途：设计参考；不是已接受契约。本文整理 Android Navigation、Kubernetes、ROS 2 与 W3C PROV-DM 的一手资料，并把外部事实与针对本项目的设计推论分开。不能据此直接确定 World/Container/Slice 字段或接口。

后续裁决：2026-10-23用户已接受Q17行为规则，并要求实施时用更详细用例或仿真验证，见discussion.md及version-layer-design.md；本文仍仅提供外部参考，不因行为接受而冻结公共字段/接口。

## 可信事实

### Android Navigation：目的地与导航栈条目分开

- 同一目的地可在导航栈中有多个实例；按目的地查询时返回栈顶实例。每个NavBackStackEntry提供自身生命周期及状态存储，相关目的地出栈后销毁这些状态。[Android官方导航指南](https://developer.android.com/guide/navigation/use-graph/programmatic#reference-a-destination-using-navbackstackentry)
- 官方源码分别定义destination和entry的唯一id；entry的id标识栈条目，不是页面目的地本身。此处引用固定源码版本，不依赖滚动分支。[AndroidX NavBackStackEntry源码](https://android.googlesource.com/platform/frameworks/support/+/14f6a2fc7da3abc6209ea7187171314d48c66020/navigation/navigation-common/src/commonMain/kotlin/androidx/navigation/NavBackStackEntry.kt)
- 不能从上述事实推出“每次返回都创建新entry”。返回可能重新激活既有条目；本项目若把每次确认重新进入记为新的访问轮次，是本项目的语义选择，不能直接拿Android的entry id代替。

### Kubernetes：对象身份与变更标记

- Kubernetes `metadata.uid` 唯一标识对象实例；对象删除后以相同名称重建，仍是不同对象。它表达身份，不是版本计数。[Object Names and IDs](https://kubernetes.io/docs/concepts/overview/working-with-objects/names/)
- `metadata.resourceVersion` 是服务端的对象内部版本标记，可用于变化检测以及GET/LIST/WATCH一致性要求，必须原样传回。当前API Concepts明确允许在同API group和resource type等限定条件下排序，扩展API server另有格式检查条件；不能跨资源类型随意比较，也不能解读为产品自己的“每次业务变化加一”。[Kubernetes API Concepts — Resource versions](https://kubernetes.io/docs/reference/using-api/api-concepts/#resource-versions)
- `metadata.generation` 是期望状态的代际序号，与resourceVersion的变化检测/并发用途分开。具体何种更新会推进它，需要查看资源自身API约定及实现；上述定义不能推出所有对象任意变化都加一。[Kubernetes API Conventions — Metadata](https://github.com/kubernetes/community/blob/master/contributors/devel/sig-architecture/api-conventions.md#metadata)；[ObjectMeta官方类型定义](https://github.com/kubernetes/apimachinery/blob/master/pkg/apis/meta/v1/types.go)
- 官方类型注释仍称resourceVersion为opaque；当前API Concepts已给出上述受限排序能力。引用时保留这个范围差异，不能使用“一律无法比较”的旧概括。[ObjectMeta官方类型定义](https://github.com/kubernetes/apimachinery/blob/master/pkg/apis/meta/v1/types.go)；[当前排序规则](https://kubernetes.io/docs/reference/using-api/api-concepts/#resource-versions)

### ROS 2：观察采集时间与空间来源明确

- sensor_msgs/Image要求header timestamp为图像采集时间，frame_id为相机光学坐标系。这是输入观察的时间/空间来源约定，不是识别处理版本。[ROS 2官方Image.msg](https://github.com/ros2/common_interfaces/blob/rolling/sensor_msgs/msg/Image.msg)
- 可借鉴之处是保留“何时真正采到、在哪个坐标系”。“同张旧图换模型处理时保留原采集时间、另记处理依据”是据此作的本项目设计推论；该消息定义并未规定我们的模型版本或Slice修订算法。

### W3C PROV-DM：实体、活动与来源

- PROV-DM 将 Entity、Activity 及其时间/使用/生成关系作为核心结构；Derivation 表达一个实体由另一个实体派生。该模型是领域无关的基础模型，同时允许领域扩展。[W3C PROV-DM §2.1](https://www.w3.org/TR/prov-dm/#prov-core-structures)
- Revision 是一种专门化的 derivation，表示新实体是旧实体的修订版本；它仍保留“从何而来”的派生关系，而不是仅用新版本号覆盖旧状态。[W3C PROV-DM §5.2.2](https://www.w3.org/TR/prov-dm/#dfn-revision)
- Activity 可使用既有实体并生成新实体；因此可描述某次处理如何基于输入产生结果。PROV-DM 还定义 Agent 与责任关系，可描述谁或什么对活动/实体负责。[W3C PROV-DM §5.1](https://www.w3.org/TR/prov-dm/#component-1-entities-activities-and-time)；[§5.3](https://www.w3.org/TR/prov-dm/#component-3-agents-responsibility-and-influence)

## 对当前问题的有限启示

这些资料分别展示：Android区分目的地和带状态的栈条目；Kubernetes区分对象身份、服务端变更标记和期望状态代际；ROS保留图像采集时间/空间来源；PROV-DM表达修订、派生与产生活动。它们为“身份、访问、变化和依据各自回答什么”提供参考，没有提供统一的大小版本方案。

这并不构成“Container 每次导航就版本加一”或“Slice 每次观察/修正就版本加一”的业界通则。本项目后续已由用户接受Container身份稳定、访问轮次单独区分、World采信相关观察或修正后派生新Slice的行为；这仍是本项目的设计取舍，不能反称为外部规范的强制要求，具体公共表示也未冻结。

## 本项目映射（设计推论，行为现随Q17接受，具体表示待工程细化）

| 本项目问题 | 候选表达 | 适用边界 |
|---|---|---|
| 这是哪个页面 | 稳定Container身份 | 离开/返回不自动新铸身份；路径相同不自动证明身份相同 |
| 这是哪次进入 | 已确认导航的访问轮次 | 导航请求/点击/失败不当已发生切换；Android栈条目只作分离思路参考 |
| 这个范围目前认识到什么 | 从World一致快照派生的不可变Slice | 相关观察或修正经World采信后再派生；旧Slice保留，一观察与Slice非一一对应 |
| 凭什么得出、何时看到 | 采集来源与解释/处理来源 | 旧图重处理不刷新采集时间；模型版更新不自动成为World事实 |

Container的事实、归属边或状态可以在无导航时更新，因此访问轮次不能替代事实修订。现有WorldBelief Revision继续作为一致提交的来源锚，任何合法相关更新都可使其推进。具体实现优先复用既有来源关联，以上不要求四个新公开counter或新增Owner。

## 不能推出什么

- 不能把 Kubernetes 的 resourceVersion 当作产品自己维护的“每次业务变化加一”公共计数器；其排序也必须遵守当前文档规定的资源类型和服务端格式等条件。
- 不能把 Kubernetes generation 当作所有对象通用的“每次变化 +1”规则；语义依资源类型及其 API 约定。
- 不能由 Kubernetes 或 PROV 推出 Container、World、Slice 的具体字段、接口、提交时机、计数规则或不变量。
- 不能把 PROV 的 revision/derivation/activity 关系直接等同于产品中的 Slice 类型或修正流程。它是可映射的溯源模型，不是本项目已接受的对象模型。
- 不能据上述类比声称存在适用于所有系统的行业标准版本模型。

## 本轮验证范围

method：一名Worker研究Kubernetes/W3C官方资料，主Agent查读Android/ROS原始资料并校对引用与迁移限制；对照本线既有Slice/World/ADR只读核对结果。

expected：事实/类比/待确认选择分开；同页变化、返回旧页面、旧图重处理与多范围Slice四类例子均有明确含义。

actual：形成上述推荐；后续用户已接受Q17行为规则，Q18保持Accepted A，并补充实施时详细用例或仿真要求。公共形状尚未冻结，未改产品、协议或运行测试/设备/模型，不把资料研究当作产品验证。

evidence：本文官方来源，version-layer-design.md，evidence/version-industry-entry.json与evidence/version-industry-verification.json。
