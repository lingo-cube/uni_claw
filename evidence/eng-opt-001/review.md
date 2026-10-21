# ENG-OPT-001 独立评审汇总

日期：2026-10-21实施、2026-10-22归档。Review与Leader运行的Verify分开记录。复用两名独立只读审查者 `/root/wrc_standards`、`/root/wrc_spec`，简单导航与集合修复由Luna协助，Leader负责范围、整合和验证。

## 固定范围

集成基线使用 `baseline-local-only.patch`；产品优化以 `optimization-entry.tar.gz` / `optimization-entry-hashes.json` 为固定点，分别冻结 `ownership.patch`、`assurance.patch`、`coverage.patch` 及对应source-hashes。不以共享dirty树相对HEAD的全部差异冒充本轮改动。最终指纹复核见 `final-integrity-check.json`。

## Standards轴

| 切片 | 审查内容 | 最终结论 |
|---|---|---|
| 基线/导航 | Simulation产品依赖限Kernel/Agent，DSH消费测试迁入Host.Dsh；测试fixture单向复用、不成环；默认绑定断言和合法工件跟随当前Owner事实，负向校验保留；导航不创建第二架构权威 | 0阻塞 |
| 所有权 | Run/Evidence admission拥有输入快照；World六读取面仅给只读视图；不改ID/接纳/预算/去重规则；补审确认Frozen/IImmutable/Sorted集合比较器语义 | 0阻塞 |
| Assurance | 同一Assurance Owner拥有typed/页面时序/视觉定案；Kernel仅投影最小事实、提供期望和调度；不把World聚合传入Assurance，不增公有协议或第二Owner | 0阻塞 |
| Coverage | 当前生产链Kind/Target/Text分离；文本兼容解析集中；选择代码按同一Ledger partial归拢；步骤产物保留kind/target；旧调用面保留 | 0阻塞 |

## Spec轴

| 切片 | 审查内容 | 最终结论 |
|---|---|---|
| 基线/导航 | 保留原测试发现与负向拒绝；不改变默认模型与产品功能；保留World后续新增测试和共享源码链接 | 0阻塞 |
| 所有权 | 外部输入改写不能改变canonical；只读World仍实时展示Owner追加；忽略大小写等原比较器行为必须保持 | 初审1项已修复，复审0阻塞 |
| Assurance | 页面确认、dispatch时序、checked Partial/Unknown/Unsupported和视觉路径保持原可达行为与拒绝原因；缺dispatch原日志分支保留 | 0阻塞 |
| Coverage | `Volume > media`、`Scroll settings`准确归属原目标；目标文字不再控制当前种类判断；public旧text-only路径集中兼容，原提示表达与控制策略保留 | 0阻塞 |

## 实际发现及处置

Spec初审发现：只复制HashSet比较器仍会改变不可变集合/排序集合的比较语义。返回IMPLEMENT补 `IImmutableSet`保留、`SortedSet`按原比较器转不可变排序set，并扩充公开Admit回归；新增反例先RED，修复后Run集合4/4通过。Standards及Spec均复核最终版本，无未处置阻塞。不增加自造测试接口、反射猜测或新admission拒绝规则。

World只读测试最初假设矛盾新claim必胜，该假设不属于本次契约；Leader纠正测试为Current对应Owner实际最新revision，未改World调和算法。失败尝试保留。

## 前置独立Review与Verify入口

- WRC：`wrc-independent-review.md`，两轴最终0阻塞；原疑似JS空Space崩溃经真实短路路径复核撤回，未硬化不可达输入。当前19+5项由Leader验证。
- Identity Store：`identity-close-review.md`，原A1–A4两轴0阻塞及当前8项验证；不外推为HTTP/设备完整生命周期。
- World：原Owner关闭并停止写入后核对72文件交接指纹；后续声明范围的重叠变化见 `world-handoff-protection.json`。

## 评审与验证的界限

上表是独立代码审查结论，不替代运行证据。最终8项目2024/2024、场景28/28、协议生成检查、路径/指纹保护由Leader另行执行，见 `verification.json`。3个真实模型opt-in测试默认未执行内部逻辑，不能算真实模型重验。历史资料物理迁移、大Owner拆分、未完成全树覆盖没有被本轮Review宣告完成。
