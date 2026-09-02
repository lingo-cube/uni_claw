# PROJECT_LEADER_SETTINGS_SUBPAGE_TITLE_ROLE_STABLEKEY_CONFLICT_REPAIR —— STOP → ARCHITECTURE_GATE

> Gate：PROJECT_LEADER_SETTINGS_SUBPAGE_TITLE_ROLE_STABLEKEY_CONFLICT_REPAIR_GATE（附录 Z4 STABLEKEY_ROLE_CONFLICT）。
> 结论：**按 gate 预设止损条件 STOP → ARCHITECTURE_GATE**（strict title-role 谓词无法不靠新跨层权威区分
> 标题与真实全宽顶部行）。生产代码已回滚至 gate 前（HEAD c8164f4，session 既有 8 项修复不受影响）；
> 语义套件复验 47/47。

## 1. 已完成的 gate 步骤（证据）

- **诊断**（Z4 run dup ambiguity）：工具栏标题（idx0, row_013, 全宽 X=[0,0.996], cy=0.089）與左缘行
  （idx2, row_013, X=[0.178,0.472], cy=0.138）共 `row_013|menu_item` → 同帧重复签名 → stability dup →
  预算耗尽 fail-closed。FDP：PAGE_TITLE 表示被 row 稳定键继承（TEXT_MATCH + ROW_STABILIZATION）。
- **实现**：'settings.page-title' 准入（strict 组合：文本 + 近全宽 + 顶带 cy≤0.15 + 同帧 back 证据 +
  EXACTLY-ONE 候选 + 排除交互形状/图标）。**RED→GREEN**：精确 Z4 falsifier + 6 反例 6/6（含
  title vs 同文本行、标题+双子行、顶部交互控件、无 back、半宽、顶行保持 Nav）。
- **回归发现**：`SettingsTreeCapstoneTests` TREE1-19 等 **19 失败** —— TREE fixtures 的 child 行
  **全宽（X=0→1）且首行 cy=0.13 ≤ 0.15**，与真实标题同形 → 谓词误认首行为标题 → 源丢失 → DFS/返回链断裂。
  （真实 Settings 行均为左缘；fixtures 用全宽行是简化，但"合法全宽顶部行"形态必须 fail-closed——gate 反例 B。）

## 2. 为什么 STOP（gate 止损条件自证）

判别"真实标题"vs"全宽顶行"所需的层内证据 = **标题的 aux 层级链（在含返回控件的工具栏节点内）**。
决定性证据（Z4 seq49 structured 全量）：**工具栏节点不在 XML 中**——back ImageButton 的
parentSourceNodeIdentity='0/0/0/0/0/0/0/0' 是一个**悬空 id**（该节点未被 dump）；标题 occurrence
的 aux = **零**（XML 连标题行都没有）。即：真实帧中标题的层内佐证恰恰是 structured 通道不可靠的部分
（已登记 uiautomator 漏枚举族）→ **strict 谓词在真实帧上既无法准入合法标题，也无法排除全宽行**。
任何"几何+框架级 back 存在"的组合都会误伤全宽顶行。→ 满足 gate：「如果 strict title-role 谓词无法
不借助新跨层权威区分……STOP → ARCHITECTURE_GATE」。

## 3. 归档状态（回滚后）

- `src/UniClaw.Semantic.Settings/SettingsSemanticCapability.cs` → HEAD（c8164f4，session 既有修复保留）
- 测试文件 → HEAD（移除本次 6 个 title 测试）
- 语义套件 47/47 ✓；工作树对该两文件 clean。

## 4. ARCHITECTURE_GATE 选项（供 Leader）

1. **跨层权威（推荐评估）**：允许 capability 消费 binding 已解析的子页身份/工具栏标题角色
   （ResolveStructuralChildTitle / ToolbarTitleRoleResourceLeaf）——须以可证明保证限定（
   "仅当 binding 身份=当前容器 + 工具栏标题角色存在"），即新 authority 契约，需架构裁决。
2. **稳定器侧守卫**（不同 Owner=感知稳定器）：ROW_STABILIZATION 不得把内容行 StableKey 赋给
   非重叠顶带标题（gate 冻结项已列此边界；修复落在 stabilizer 需其自身 gate）。
3. **接受残余**：Z4 dup 形态登记为漂移家族（V/Z4 族），维持现状。

## 5. 边界确认

未改：budget/retry/step/normalizer/completeness/OCR；未放宽 duplicate ambiguity；未改变任何
既有判定。冻结语义全部保持。Phase 2.6 维持 STOPPED。