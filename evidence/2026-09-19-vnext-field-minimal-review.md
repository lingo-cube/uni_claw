# vNext.1 字段与最小候选模型审阅

日期：2026-09-19
级别：`CONTRACT + SCENARIO_BACKED`
审阅对象：`UniClaw_Core_Model_设计文档_vNext.1.md`

## 审阅结论

vNext.1 是语义参考设计，不要求所有列出的字段都进入第一版 Core。当前材料支持的
最小候选基线是五种核心记录：`Clause`、`Segment`、`Evidence`、`Claim`、`Effect`。
`Slice`、`Attempt`、`TargetBinding` 保留为条件性/内部语义结构；Event 的发生语义
必须保留，但独立 Event 记录暂不冻结。

这不是严格最小性证明。字段是否进入实现，按 vNext.1 §4.3 的 B/C/A/H/O 等级和用途
关口决定。

## 字段审阅矩阵

| 语义 | 第一版必须保留 | 用途条件必需 | 当前 UI 版处理 | 审阅决定 |
|---|---|---|---|---|
| Clause | 显式规范类别/表达、登记时间 | 权威、范围、有效期、时间规则、版本关系 | 现有 Clause 候选字段承载最小条款 | 保留语义，C 字段不强塞 |
| Segment | 持续引用、语义类别 | 身份/归属/生命周期判断依据 | UI container/page 投影为 Segment | 保留核心记录 |
| Slice | 所属 Segment 快照、固定 Evidence 依据、局部表示 | 坐标系、单位、标定、融合方法、重建方法 | UI viewport/occurrence 投影为 Slice | 保留内部结构，不强制独立实体 |
| Evidence | 来源、内容/记录引用、观察时间或明确 Unknown | 采集上下文、Schema、处理链、设备信息 | Kernel admitted Evidence 只读投影 | 保留核心记录 |
| Claim | 可修正命题、基本来源 | Basis 角色、适用期、冲突、更正、发生归属、因果 | WorldClaim 投影为 Claim | 保留核心记录 |
| Effect | 逻辑操作、目标主体、参数/契约位置 | 条款、预期、补偿/重试关系 | CanonicalBinding 投影为 Effect | 保留核心记录 |
| Attempt | 尝试身份、Effect/Binding 关联、登记时间、投递未知/完成/失败 | 请求快照、执行端、授权、dispatch log、receipt refs、协调状态 | 当前 receipt 可映射基础 Attempt；缺失字段不伪造 | 保留内部结构，按用途补充 |
| TargetBinding | 目标引用、定位材料、固定 basis | 依赖条件、有效期/上下文、资源版本解释 | 当前 UI 使用 Slice basis + opaque locator | 保留内部结构，Slice 非强制 |
| Event | 发生语义（归属、时间、次数、参与者、依据、更正） | 独立查询/演化确有不可替代表达时 | 当前用 Evidence + Claim 组合 | 语义保留，独立类型待反例 |

## 审阅采用的删除与演化测试

删除某个独立类型不能只看类数量。必须检查：

1. **表达保持**：对象、发生/投递、依据、时间、Unknown/Conflict、权限和责任边界仍可回答。
2. **演化保持**：加入新 Evidence、更正、撤销或迟到反馈后，先更新原模型再转换，和先
   转换再更新，在必要判断上保持一致。
3. **缺失局部阻塞**：缺少 C 字段只阻塞依赖它的用途，不将字段缺失变成全局失败。

## 对当前 UI 实现的审阅

- 现有 UI projection 已能表达滚动—点击 tracer 的五种核心记录和三个内部结构。
- Slice 的重叠、历史固定、旧 binding stale、Unknown delivery 和 receipt 不等于 World
  result 均有测试证据。
- 当前 UI projection 尚未提供请求快照、执行端、授权依据和非 Slice basis 的真实输入；
  这些属于 UI Runtime/projection 的后续 gate，不是新增 Core 类型的理由。
- UI locator、occurrence、DOM/ADB、设备驱动保留在 Kernel，不在本次最小基线中上提。

## 锁定范围

本审阅锁定：

- 五种核心记录的语义边界；
- Slice/Attempt/TargetBinding 的内部/条件性位置；
- Event 发生语义保留但独立类型待反例；
- 字段按用途等级裁剪的规则；
- UI realization 首版使用现有 projection seam。

本审阅不锁定：

- 严格最小性证明；
- 最终字段、继承、包名、序列化、存储、Runtime 恢复协议；
- 机器人、文件/API、仿真 realization；
- 旧 UI/Kernel 类的删除、替换或迁移完成。

审阅结果已由 `changes/CORE-007/state.md` 承载；UI 首版实现已在同一 Change 中完成并由
`evidence/2026-09-19-core-007-ui-realization.md` 验证。此记录仍只负责字段审阅，不扩展
为源码迁移结论。
