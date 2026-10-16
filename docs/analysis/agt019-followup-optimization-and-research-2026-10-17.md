# AGT-019 后续优化：融合 / 循环发现 / 坐标利用——研究与待办（2026-10-17）

> Status: DRAFT / CANDIDATE
> Authority: NONE（研究整理 + 设计提案，供 owner 裁决与排期；不修改任何冻结面）
> 谱系：RUN-006 真机全遍历（三轮）暴露的弹窗死循环 → owner 系列讨论
> （XML×视觉融合 / 循环发现 / 置信度表示 / 坐标利用）→ 本文。
> 证据锚点：changes/RUN-006/report.md + evidence/run-006/full-traversal-6/
> （46× 同目标重派 + 45× 同弹窗清除，SIM 对话框即目的地被自清除）。

---

## 1. 病根链（一段话）

"SIM name & color" 设置项本身是对话框：点它 → 对话框打开（= 到达）；uiAutomator
按焦点窗口 dump → **XML 只剩对话框、底页元素全部丢失**；组合规则以 XML 为
权威 → 世界模型丢失底页与路由身份（退化"未知页"）；弹窗分类器只见"有带
CANCEL 的对话框" → 清障 → **把目的地关掉**；进入验证靠路由变化（对话框不改
路由）→ 永不成立 → 账本重派同一目标 → ×46。每步局部"验证成功"与全局零
推进并存。

## 2. 设计收敛（两片 + 公共地基）

### 片 1：XML × YOLO+OCR 融合（两层模型 + 几何）

- **对话窗判定**：dump 含 alertTitle/parentPanel/buttonPanel（复用弹窗分类器
  resource-id 集）且无页面标题节点 → 判定对话窗 dump；
- **底页保留（Option B，业界同款先例）**：不拿对话 dump 替换页面——保留上
  一页面周期 XML（路由未变即有效）：底页路由身份、元素、bounds 全保留；
- **视觉当仲裁不当事实源**：YOLO+OCR 负责"画面与预期差异大"报警，不持有
  元素身份（跨源身份匹配出错面大，业界共识：结构身份以树为准）；
- **几何遮挡**：矩形工具箱（交集/包含/中心点）——元素中心点落入对话框矩形
  → occluded；遮挡事实进 LocalView（AGT-019 自有面，零冻结成本）；
- **路由连续性**：对话周期路由 = 保留的底页路由（不再退化未知页；附带红利：
  慢感知错屏丢弃大幅减少）；
- **触发词语义**：视觉/结构冲突（StructuralVisualConflict）改为同层内比较。

### 片 2：循环发现（机械触发 + 轨迹上报，Reflection-on-Demand 同构）

- **机械触发轨（四层，命中即强制升级 UniAgent）**：
  L1 同一进入目标重派 ≥K 次（跨交错计数，只数 enter 指令）；
  L1' 同一弹窗指纹清除后复现 ≥R 次；
  L2 覆盖指纹（已进入数 + frontier）连续 W 轮不变——**必须剔除 StepsVerified**
  （RUN-006 教训：每步 tap"验证成功"使其恒涨，M5 双零判据在弹窗循环不可达）；
  L3 连续 N 轮截图 digest 相同（viewportDigest 现成）；
  L4 **滚动零位移**（滚动前后元素位置差分 = 0 → 到底/卡死，比 L3 快一拍）；
- **心跳轨（兜底）**：每 K 步轨迹摘要随正常咨询搭车（不另耗轮次），K 与
  预算挂钩（≈ maxSteps/8、占咨询预算 ≤1/6，取大）；
- **轨迹摘要（五族，只给事实不给判语）**：① 最近 W 步指令流水（kind/目标/
  verified——交错模式肉眼可见）；② 目标重派计数 top3；③ 弹窗指纹复发计数；
  ④ 覆盖进度差分曲线；⑤ 画面 digest 变化；
- **升级选项集**（不动回答词汇表）：{跳过该目标（Agent 选 back → Director
  顺势标记 skipped+原因）/ 返回 / 申报卡死（Blocked 已在词表）/ 再观察}；
  Agent 连续两轮无有效回答 → 机械自动跳过并留痕（防升级自身死循环）；
- **A 层到达语义**：点完后出现的弹窗不自动清障——标题≈目标快路径或升级
  问 Agent（慢感知已能落地读弹窗内容，正好供判断）；
- **滚动差分三产出**：滚动几何验证（替代纯步验证）/ 零位移信号（L4）/
  驱动 scroll-discovered 增量记账（爬虫同款）。

### 横切规则：置信度的"方言"

**数值是感知的方言，枚举是决策的方言，阈值是翻译官**：数值只在感知管线
内部（IoU、检测置信度、位置差分），过阈值后以类别事实跨层（occluded/
Observed/Partial/触发升级）。ElementEpistemic 枚举形态正确，不改。

### 坐标工具箱（两片公共地基）

矩形交/并/包含/中心点判定 + 滚动位移差分——遮挡（片1）、L4/边缘策略/
sticky 锚点/LocalView 空间上下文（后续）全部复用。

## 3. 业界参照（本讨论引用汇总）

| 来源 | 验证/启发了什么 | 核对深度 |
|---|---|---|
| [MobileUse（NeurIPS 2025）](https://papers.neurips.cc/paper_files/paper/2025/hash/3994410d63ec68ce9a66011a34c9a2c4-Abstract-Conference.html) | 分层反思 + Reflection-on-Demand：多时间尺度自我监控、按需反思保效率——片 2 双轨制（触发为主、心跳兜底）的同构 | 摘要级 |
| [Mobile-Agent-v2（NeurIPS 2024）](https://papers.neurips.cc/paper_files/paper/2024/file/0520537ba799d375b8ff5523295c337a-Paper-Conference.pdf) | 规划/执行之外单设反思角色盯轨迹——"独立判断者看完整轨迹"的架构位 | 标题级 |
| [Reflexion（arXiv 2303.11366）](https://browse.arxiv.org:443/pdf/2303.11366) | 失败后读轨迹写教训——升级裁决即反思产物；跨 run 记忆为 AGT-019 Out-of-Scope | 摘要级 |
| [auto-mobile PR #1496](https://github.com/kaeawc/auto-mobile/pull/1496) | "键盘打开时保留应用视图层级"——浮层致 dump 丢底页的同款病、同款修法（Option B 先例） | 标题级 |
| [AppAgent v2（arXiv 2408.11824）](https://www.alphaxiv.org/zh/abs/2408.11824) | XML 结构 × OCR 文本/位置交叉验证一族 | 标题级 |
| [GUI Agent 综述（arXiv 2504.13865）](https://ar5iv.labs.arxiv.org/html/2504.13865v1) | SoM（视觉打标锚定）一支；融合路线图 | 检索摘要级 |
| [Chromium a11y 测试](https://github.com/chromium/chromium/blob/main/content/public/android/javatests/src/org/chromium/content/browser/accessibility/WebContentsAccessibilityTest.java) | 平台自带 per-node 可见性轴（isVisibleToUser）——dump 不导出则几何自算（我们的取舍） | 文件级（存在性） |
| [playwright-spatial-layout-mcp](https://www.npmjs.com/package/playwright-spatial-layout-mcp/v/0.2.0) | 元素间空间关系作为一等输出供 Agent 消费 | 标题级 |
| [Web Scraper：无限滚动采集](https://webscraper.io/blog/how-to-scrape-infinite-scroll-pages) | 滚动→diff→增量采集：滚动差分的标准用法 | 文章级 |
| [zoom-consistency-routing](https://github.com/omxyz/zoom-consistency-routing/blob/main/paper.md) | 多尺度/缩放一致性校验（sticky 不变性的远亲） | 标题级 |
| [YOLO 置信度阈值实践](https://journals.plos.org/plosone/article/file?type=supplementary&id=10.1371/journal.pone.0340640.s005) | 数值置信 + 阈值过滤（感知方言） | 检索摘要级 |
| [Appium 图像插件（Sauce Labs）](https://docs.saucelabs.com/mobile-apps/automated-testing/appium/appium-images-plugin/) | 相似度数值 + 阈值（同上） | 文档级 |
| [Safeguard：何时不要点](https://cognaptus.com/blog/2026-02-03-click-with-confidence-teaching-gui-agents-when-not-to-click/) | 风险闸是类别判定不是分数外传（决策方言） | 文章级 |
| 早前 grill-2（changes/AGT-019/grill-2-industry-compare.md） | MCP elicitation/ACP options（有界选项集）、Playwright 默认 dismiss（弹窗）、LangGraph recursion_limit（硬停兜底） | 已归档 |

**诚实声明**：上表多数核到标题/摘要级；深挖任何一篇可拉全文补证。

## 4. 待办列表（未裁决 / 未实现 / 未讨论）

### 4.1 等 owner 裁决的参数
- [ ] 心跳轨占咨询预算比例（提案 ≤1/6）与 K 公式（maxSteps/8）；
- [ ] 轨迹摘要指令流水窗口 W（提案 12）；
- [ ] 摘要落点：LocalView 结构化（提案）vs RecentCycles 字符串；
- [ ] 滚动差分进片 2 还是独立小片（提案：进片 2）；
- [ ] 升级阈值初值：K=3/R=2/W=5/N=3（保守起步，数据后冻结——沿用"阈值后调"）。

### 4.2 已设计待实现
- [ ] 片 1（融合：对话窗判定/底页保留/几何遮挡/路由连续/视觉仲裁/触发词同层化）；
- [ ] 片 2（四层触发 + 心跳 + 轨迹摘要 + 选项集升级 + 自动跳过兜底 + A 层到达语义）；
- [ ] 矩形工具箱（片 1 前置，一次立起多方复用）。

### 4.3 RUN-006 挂账（已记录未修）
- [ ] 桥 DTO 缺 LocalView 映射（Director 写了、DSH 桥载荷 null——真机复现实锤）；
- [ ] M5 判据加轴（被片 2 L2 吸收）；
- [ ] 标定数字冻结（等片 2 落地后的真机数据）；
- [ ] deploy.sh 不管 preset 行（本轮手工镜像，建议工具化防再踩）；
- [ ] slow 落地内容的**决策消费**验证（已 Landed×6，但是否改变 Agent 决策未证）。

### 4.4 未讨论 / 留观察的问题
- [ ] 到达快路径的弹窗标题管道（XML alertTitle → 账本快照，A 层机械比对依赖）；
- [ ] "到达即完成"对覆盖语义的影响（对话框目的地如何计入覆盖率证据）；
- [ ] blocked 申报频率观察（词表新成员是否被滥用——预算封顶但需数据）；
- [ ] 键盘浮层（EditText 场景）：与对话框同几何，但当前无文本输入 buyer——
      不预造，出现 buyer 再上（auto-mobile PR 先例在手）；
- [ ] 超视口列表重建（滚动 diff 驱动的增量世界模型——scroll-discovered 已有账本位）；
- [ ] sticky/固定区识别与滚动后再锚定（AGT-010 只做了剔除未做利用）；
- [ ] 视口边缘点击策略（先滚再点；settings 列表短收益小，有 buyer 再上）；
- [ ] LocalView 增加空间关系族（相邻/包含/方位——playwright-spatial-layout-mcp 同款）；
- [ ] 跨 run 反思记忆（Reflexion 式教训库）——AGT-019 明确 Out-of-Scope，独立通道再议。
