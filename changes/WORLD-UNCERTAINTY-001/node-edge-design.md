# World节点、边与局部Slice：继续设计核对

日期：2026-10-22。状态：DRAFT / EVIDENCE-BOUNDED。Authority NONE。沿World聚焦范围继续设计与随行记录，不实施产品，不运行构建/测试/设备/模型。本文是行为与缺口稿，不冻结新的公共模型、接口或枚举。

## 本轮所有者输入与既有定义的对应

用户补充：①按边和节点维护；②一组Slice可以共同构成页面表达，页面实体名为Container；③Slice语义修正与Container归属切换有关。现有基线允许采用图表达，复用WorldBelief中的Container/Relation，不另建平行可变图。UI页面是Container的一种，业务设备/每个几何区域不自动成为UI Container。

节点表达实体身份及自身世界语义；边表达实体间有依据、带语义类型的关联。包含、覆盖与经过独立验证的导航含义不同，不能因为都画成箭头就互相替代。导航允许返回/重访，不对整个图施加树或全局无环假设；涉及层级的具体约束由真实关系语义和消费需求决定。

Slice仍是World从指定revision派生的有界只读视图，不自己写入世界事实，也不因存在一个Slice就建立新Container。一组局部Slice可以共同呈现同一Container的已知部分，不以拼接数量证明页面看全。Slice的来源Container引用及scope不等于新的ContainerRelation类型；当前ContainerRelation端点是两个Container，不能直接把SliceId塞进去。

“完整由层级表达”指已知节点与关系能组合解释上下文，不承诺图已覆盖整个应用。当前页默认只投影本地语义；必要上层上下文由World提供最小只读关联，不复制整条祖先语义。

### 语义更新与归属纠正分开

| 新证据说明什么 | World应处理什么 | Slice表现 |
|---|---|---|
| 原来认作A页，现有充分依据表明应属B页 | 重新裁决观察/局部语义的Container归属及受影响关联，具体首期范围见Q15 | 派生新版本视图；旧Slice和旧依据仍可解释 |
| 同一页面的控件用途/状态被修正 | 更新原Container的相关语义/claim，冲突沿原规则保留或裁决 | 可以生成新Slice，Container不必变化 |
| 页面类别相同或仅有相似标题 | 不足以直接切换、合并或新铸身份 | 保留身份/归属缺口，不靠名称强行选择 |

归属切换是语义修正的一种情形，不能把所有语义变化都定义成换Container。页面类别、对象身份、观察归属、当前交互锚点是相关但不同的判断。

## 当前已有的路径

- WorldModel.cs:768-817：节点由既有association策略及World证据/矛盾gate建立或延续，CurrentContainer/签名由Owner派生；不让provider直接铸造身份。
- WorldModel.cs:820-838：非导航关系提案有已知端点和已接纳证据成员检查；导航另走事后验证路径。并非仅有enum或完全没有关系接纳入口。
- WorldModel.cs:128-218：导航边核验两端历史revision、页面观察与签名依据、scope/verification引用及新边的当前目标锚点；历史边继续作为历史世界事实。
- WorldModel.Viewports.cs：已验证同页滚动及部分可配准位移已有记录。无重叠、歧义或嵌套区域保持未知；对应锚不成为LogicalItem身份或当前可执行定位。
- Slice.cs及WorldModel.cs:1390-1430：按Container集合派生只读局部视图。默认仅root，已有范围过滤和revision引用；不是从头缺一套Slice。

## 仍未完成的缺口与具体反例

| 项目 | 已确认的当前行为 | 对本次需求还缺什么／拟验收反例 |
|---|---|---|
| 最小结构边的生产与消费 | ProductAssociationStrategy各分支Relations为空；DeriveGraphProjection只给同scope导航边。Contains/Overlays虽有内部接纳入口，产品路径及投影尚不完整 | 两个已识别上下文间的归属/包含/覆盖不能靠导航历史代替。先定位当前页上层关联的真实观察来源与消费者；再补相应最小关系，不预造所有关系类型。无依据时有明确关系缺口 |
| 同一页面的多个局部Slice | 当前DeriveSlice按Container集合选全部该scope内occurrence/claim，没有页内区域/内容子集的显式裁剪请求。Core投影的Scope也只是Container集合字符串 | 若同一Container分上半部、下半部/不同已观察内容范围表达，必须能分清范围、重复内容及未覆盖部分；不能把两个同页全量视图误当两个互补局部。具体范围请求形状仍待buyer推导 |
| 多范围Slice标识碰撞 | CoreSemanticProjection.cs:71-72只用revision+root生成SliceId，而ProjectSlice:90-91输出的Scope及ObservedRecordIds可随InScope集合变化 | 已确认源码级表达缺陷：同revision/root，InScope={A}与{A,B}都可被现有DeriveSlice接受，却产生相同Core Id及不同内容。CoreRecords.cs:170-171的历史依据比较仅比较Id。尚未运行回归，未证明正式Host有现场错绑；同revision下多局部视图必须区分，拟验收相同固定视图稳定、不同范围不混同 |
| 节点/边的纠正与失效 | 现有身份冲突会阻断关联；语义只丰富已有当前Container。关系路径主要追加，尚未核实一条可消费的通用归属纠正、结构边撤销/替换链 | A归属被新证据推翻、覆盖层关闭、上层上下文被替换时，不能只加新边同时继续把旧边当当前事实。需按已购最小关系说明何时仍未知、何时失效/替换，保留历史。Q15已接受A，首期纳入最小正式归属纠错；不承诺通用身份merge或全生命周期 |
| 组Slice的时间/空间一致性 | Slice只来自单个World revision；当前滚动关联能证明部分跨观察配准，但没有本轮运行证据证明多Slice即当前完整页面 | 同页上方观察后内容改变再滚动下方，不能把旧上方和新下方直接当同一时刻当前页。若纳入多次观察，必须由World按证据、时间与空间关系形成新的可解释revision；未能确认的对应/覆盖保持未知。Q14决定首期跨观察累计范围 |
| 四类缺口及实际Agent输入 | 通用四类记录、覆盖依据派生、持续更新及真实模型输入尚未交付，见world-gap-audit.md | 缺节点、缺关系、局部覆盖不足及归属纠正各自能进入当前相关摘要；类型重叠不重复补证，同因无新结果不反复请求决策。仅有引用、图节点或字段类型不算Agent已读到 |

SliceId问题是当前纯函数输入输出可直接判定的缺陷；其余多数是新明确需求对应的生产/消费缺口，不是声称现网已发生事故。修复位置应由后续实际设计和diff确定，不立即增加新Graph Owner或接口。

## 本轮没有升级为缺陷的现象

- 图投影保留全部该revision历史节点是WorldModel.cs:223-226明确的用途；scope当前过滤导航边，不是已承诺的全图租户/权限隔离。没有具体受支持隔离需求及受影响消费者时，不把“节点未按scope过滤”直接叫泄漏或bug。
- 导航边保留两端历史revision、目标历史revision不强制等于当前revision，并不自动错误。当前实现仍核验两端身份依据且新边的目标必须是当前锚点；记录历史关系不能被消费为当前可执行许可。Worker提出的两项推测均缺足够违约证据，本轮不列为已证缺陷。
- root/inScope存在于World并不自动说明二者为父子。当前DeriveSlice允许buyer请求局部多Container，不能擅自改成仅允许子树；本线所需“当前页局部”如何由Owner确定，应明确在消费请求和证据上，不靠通用API猜归属。
- 冲突新鲜窗现已定位：KernelRunDriver.cs:156固定3秒，act和终局路径:1072/:1795调用；ConflictResolver.cs:84使用capture时间差。未核实其标定和完整可观测性，不再描述为完全未找到机制，也不据常量存在判断其可靠性。
- 普通claim迟到：ApplyClaim:496-512允许同producer异scope修订，当前历史保护主要针对页面身份/capture等。较早capture迟到能否错误覆盖特定普通claim，仍须对照对应流与契约验证；不以revision时间单调证明所有claim时间语义正确，不重开已关闭语义exact-capture修复。

## 当前设计前沿

节点/边表达、World为唯一维护者、Slice只读局部视图、身份与语义分轴均由用户最新方向及既有协议确定，不重复提问。工程字段、稳定标识算法、codec同步、关系来源与测试位置由本会话核实和推导，不请用户猜。

Q14：多次滚动等观察是否纳入首期同一Container累计表达。推荐A：纳入有依据、可核对的累计；由World经reconciliation形成新revision，Slice仍来自单revision，未证明时序/配准/覆盖的部分保持未知。B：首期只做同一revision的局部视图组合，跨观察累计另切片。不是直接拼历史Slice改变当前世界，也不是全页穷尽承诺。

Q15：已有明确归属被新证据推翻时，首期是否做正式归属纠错。推荐A：纳入最小纠错路径，World据充分证据在新revision更新归属并处理受影响关联，冲突不足不强选；原Slice/历史引用保留，操作恢复仍由原消费Owner判断。B：首期仅未确认→确认，已明确归属的更正先保持冲突，正式纠错另切片。两项皆不包括通用身份合并/全生命周期。

所有者后续答案：Q14/Q15 Accepted A，首期纳入有据累计及正式归属纠错。Q14曾要求人话解释，随后明确答“按推荐”，并补充一组Slice计算页面表达；Q16再选择A，限定为结构、控件语义、状态及布局并可视化。现有Replay单Slice呈现可复用，多Slice有据供给与累计不是已有历史拼图自动证明，详见page-expression-design.md。

公共模型仍未冻结，不进入产品实现。原Q1-Q13及World聚焦保持有效。

## 验证四元组

method：一名Worker只读节点/边生产与gate，Leader读取Slice/组合缝/Core投影/消费关联；对照协议和实际caller，剔除不成立的bug推测，保存共享现场指纹。

expected：既有保证、尚缺能力、源码可判定缺陷及经验未证分开；不把历史图、导航或局部视图当执行许可或完整覆盖。

actual：上述源码路径与SliceId碰撞表达可定位；Q14/Q15/Q16已接受A。已有Replay呈现及限制经只读核验，未修改产品、构建、运行测试/设备/模型，未证明现场串页、错误投递或完整跨观察表达已通过。

evidence：本文路径行号，evidence/node-edge-entry.json、evidence/node-edge-verification.json，discussion.md及world-gap-audit.md。
