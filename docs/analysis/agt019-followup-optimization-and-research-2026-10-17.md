# AGT-019 后续优化：融合 / 循环发现 / 坐标利用——四层研究与待办（v2，2026-10-17）

> Status: DRAFT / CANDIDATE
> Authority: NONE（研究整理 + 设计提案，供 owner 裁决与排期；不修改任何冻结面）
> 谱系：RUN-006 真机全遍历（六轮）暴露弹窗死循环 → owner 系列讨论 →
> v1 文档 → owner 对抗审查 + Leader 反驳 + 仲裁 → 本 v2（四层重构）。
> 证据锚点：changes/RUN-006/report.md + evidence/run-006/。
> v1→v2 变更：三处事实修正（viewportDigest/LocalView 分层/slow 能力源）；
> 三处语义边界收窄（L2 定性、skip⊥blocked、几何 authority）；结构改为
> "已证事实 / 待证假设 / 候选设计 / owner 裁决"四层，主张必附证明方法。

---

## 第一层：已证明的运行事实（只写 trace/代码能直接证明的）

| # | 事实 | 证据 |
|---|---|---|
| F1 | `SIM name & color` 被重派 **46 次**、`CANCEL` 清障 **45 次**（同一目标/同一弹窗） | coverage-steps 计数（3/6 轮一致） |
| F2 | 每一步 tap 均"验证通过"，覆盖进度零推进（37.5% 止步） | steps 全 verified + coverage-report |
| F3 | 该弹窗即目的地：XML 内容 = "SIM label and color" 对话框（Name/Color/CANCEL/SAVE） | capture-273b….xml 原文 |
| F4 | 对话窗周期 XML dump 只含对话框（焦点窗口所限），底页元素丢失 | 同上 XML |
| F5 | 弹窗周期路由回退 `android.settings`（无页面标题） | settings-trace routeKey |
| F6 | `viewportDigest` 在循环中不变 | trace |
| F7 | `viewportDigest` = **XML 可点击节点 text/resource-id 排序摘要**，不含像素、不含 bounds（v1"截图 digest"系误写） | DeriveViewportDigest 源码 |
| F8 | M5 双零判据在此类循环**不可达**（StepsVerified 恒涨）——miss 是直接观测，非假设 | F2 + 判据定义 |
| F9 | slow 全链真机闭环：6× `Landed\|projected=1\|late\|ms=850-1215`（真模型/真 DSH 链） | full-traversal-6 trace |
| F10 | consult-bridge.json 97/97 `localView: null`——**记录层原因已定位**：Runner 把装饰前的 `projected` 写入桥记录（Runner L233-242），Director 的装饰发生在其内部副本 | 源码 + 全轮 bridge |
| F11 | slow text 消费 fast/XML 依据；slow visual（像素）未接线（visualEnabled=false，无 buyer） | 配置 + BuildSlowRequest 源码 |

## 第二层：待证明的语义假设（每项附证明方法）

| # | 假设 | 证明方法 |
|---|---|---|
| H1 | 弹窗出现 = 目标已到达（目的地对话框） | 对照实验：目标弹窗 vs 无关弹窗（场景 S1/S2），到达后账本能否区分 |
| H2 | 底页可作为"历史锚点"（BasePageAnchor）而非当前页面 | 语义三分（Overlay/BasePage/CurrentContainer）+ 裁决 D1 |
| H3 | `CANCEL` 验证 = "动作已验证但覆盖无进展" | 验证类型分类回放（L2 候选 b） |
| H4 | 同一弹窗指纹复现 = 循环 | 场景 S5（重复 CANCEL 无新增覆盖）vs 合法重复操作 |
| H5 | titleless popup 必属对话窗 | 场景 S2/S3（有/无标题、部分遮挡）+ 分类器对照 XML |
| H6 | 几何零位移 = 到底/卡死 | 场景 S6（scroll 后文本同 bounds 变）——**L4 必须绑定**：同一元素 association、坐标空间、容差；否则 sticky/重排/缩放/异窗坐标均致误报 |
| H7 | 真实 DSH 传输边界的 LocalView 非空（Agent 实际收到装饰后 Context） | **实现验证项（前置）**：在 FileDecisionConsult.WriteRequest 或 HTTP peer 请求边界读最终载荷 |

## 第三层：候选设计（语义边界写清；只产生事实信号或升级信号）

### 片 1：融合（Overlay/BasePage/几何）
- 对话窗判定（弹窗 resource-id 集 + 无页面标题，H5 待证）；
- 底页保留为 **BasePageAnchor**（Option B，业界同款先例见 §引用）——语义归裁决 D1；
- 视觉当仲裁不当身份源；几何遮挡（中心点判据为主）→ 事实进 LocalView；
- 路由连续性：对话周期路由 = 底页路由（依赖 D1 结论）。

### 片 2：循环发现（触发 + 轨迹上报）
- 机械触发：L1 目标重派 / L1' 弹窗指纹复现 / **L2 覆盖指纹（修法待回放选择，F8 的 miss 本身已证）** / L3 XML 视口摘要（F7 语义）/ L4 bounds 零位移（H6 绑定条件为前提）；
- 心跳轨（轨迹摘要搭正常咨询便车，占比/窗口=W 待裁决 D-参数）；
- 五族轨迹摘要（事实无判语）；升级选项集含 **skip（独立语义，⊥blocked：blocked=终局申报、skip=跳过继续——两者非别名，B1 定形时已正交）**；
- 自动 skip 仅限预算耗尽或 Agent 连续无效响应（子问题见 D4）。

### 横切规则
- 置信度方言：数值=感知内部，枚举=决策跨层，阈值=翻译官；
- **几何 authority 边界**：几何差分只产生 displacement/zero-motion/occlusion **事实信号**，可触发升级/诊断/慢感知；覆盖记账仍走既有 post-action verification 与 occurrence 链——几何若要改变覆盖事实，须另立 Promotion 决策（D5）。

## 第四层：owner 裁决（五项，各配最小验证场景）

| # | 裁决 | 场景 |
|---|---|---|
| D1 | BasePageAnchor 是否入 Agent Context；执法点定位：Kernel fresh-observation 有效性（锚点元素无当前 fresh occurrence → Grounding 自然拒绝，无需伪装成新 truth）——收窄为"在哪执法" | S1/S2/S3 |
| D2 | "到达即完成"是否适用于弹窗目标；适用则覆盖率如何记录 | S1（目标弹窗）vs S2（无关弹窗） |
| D3 | M5 进度轴修法四选（回放比较误报/漏报）：a) 维持 StepsVerified；b) obstacle 验证从进度中分类排除；c) 独立 CoverageProgressFingerprint；d) 组合 | S4（合法长验证）+ S5 |
| D4 | skip 六问：改覆盖状态？零 Effect？必须 Agent 明确选择？自动仅限预算耗尽/连续无效响应？允许重进？独立 skipReason+目标证据？ | S5 + S7（Agent 无响应/非法决策） |
| D5 | 几何与 digest 只做升级信号，还是允许进覆盖判定 | S6 |

**七个最小验证场景**：S1 目标弹窗 vs S2 无关弹窗；S3 弹窗部分遮挡目标；
S4 合法长验证过程；S5 重复 CANCEL 无新增覆盖；S6 scroll 后 XML 文本同
bounds 变；S7 Agent 无响应/非法决策。

**参数类（先保守后标定）**：心跳占比/公式、流水窗口 W、摘要落点、滚动
差分归属、K/R/W/N 初值。

**实现验证项（先于裁决完成）**：
- [ ] H7：DSH 请求边界读最终 Context 载荷（判定桥空是记录层假象还是传输也丢）；
- [ ] 桥记录改记装饰后 Context（或并记两态）——审计真实性修复。

## 业界参照（"参考启发"级，除非另注；深挖任一篇可升证）

| 来源 | 启发 | 深度 |
|---|---|---|
| [MobileUse（NeurIPS 2025）](https://papers.neurips.cc/paper_files/paper/2025/hash/3994410d63ec68ce9a66011a34c9a2c4-Abstract-Conference.html) | 分层反思 + Reflection-on-Demand（片 2 双轨同构） | 摘要 |
| [Mobile-Agent-v2（NeurIPS 2024）](https://papers.neurips.cc/paper_files/paper/2024/file/0520537ba799d375b8ff5523295c337a-Paper-Conference.pdf) | 独立反思角色盯轨迹 | 标题 |
| [Reflexion（arXiv 2303.11366）](https://browse.arxiv.org:443/pdf/2303.11366) | 失败后读轨迹写教训；跨 run 记忆 Out-of-Scope | 摘要 |
| [AdaPlan-H（arXiv 2604.23194）](https://arxiv.org/abs/2604.23194) | 固定粒度两头错；自适应粗→细（抽象/具体决策流） | 摘要 |
| [Plan-and-Execute 对比](https://atlan.com/know/ai-agent/react-vs-plan-and-execute-agent-architecture/) + [resilient 版](https://browse-export.arxiv.org/pdf/2509.08646) | 抽象摊销 + 偏差重规划 | 文章/摘要 |
| [AutoDroid](https://ar5iv.labs.arxiv.org/html/2308.15272v4) / [V2](https://ar5iv.labs.arxiv.org/html/2412.18116) | 离线脚本 vs 在线交互（Policy 摊销同构） | 摘要 |
| [auto-mobile PR #1496](https://github.com/kaeawc/auto-mobile/pull/1496) | 浮层时保留应用层级（Option B 先例） | 标题 |
| [AppAgent v2](https://www.alphaxiv.org/zh/abs/2408.11824) / [综述](https://ar5iv.labs.arxiv.org/html/2504.13865v1) | 结构×视觉交叉验证；SoM 锚定 | 标题 |
| [Chromium a11y](https://github.com/chromium/chromium/blob/main/content/public/android/javatests/src/org/chromium/content/browser/accessibility/WebContentsAccessibilityTest.java) | 平台可见性轴（dump 不导出则几何自算） | 存在性 |
| [playwright-spatial-layout-mcp](https://www.npmjs.com/package/playwright-spatial-layout-mcp/v/0.2.0) | 空间关系一等输出 | 标题 |
| [Web Scraper 无限滚动](https://webscraper.io/blog/how-to-scrape-infinite-scroll-pages) | 滚动 diff 增量采集 | 文章 |
| [YOLO 阈值实践](https://journals.plos.org/plosone/article/file?type=supplementary&id=10.1371/journal.pone.0340640.s005) / [Appium 图像](https://docs.saucelabs.com/mobile-apps/automated-testing/appium/appium-images-plugin/) / [Safeguard](https://cognaptus.com/blog/2026-02-03-click-with-confidence-teaching-gui-agents-when-not-to-click/) | 置信度方言三例证 | 摘要/文档/文章 |
| 早前 grill-2（changes/AGT-019/grill-2-industry-compare.md） | 选项集/弹窗默认/硬停兜底 | 已归档 |

## 附：抽象计划 vs 具体指令（信任阶梯，待立项归属裁决）

词表已齐（Act/Plan/Policy = ReAct/微计划/规则摊销三级）；缺决策流。提案：
信任阶梯 + 事件驱动升降级（升级靠证据积累判断权归 Agent；降级全机械：
租约失效/验证失败/异常谓词/循环信号；不可逆动作恒单步）。
业界锚：AdaPlan-H（自适应粒度）、Plan-and-Execute（replan）、AutoDroid
（脚本摊销）。**归属建议：M6 v2 设计输入正式立项**。

## 待办汇总

- 等 owner：D1-D5 + 参数组 + 阶梯立项归属；
- 先行实现验证：H7 传输边界 + 桥审计修复；
- 已设计待实现：矩形工具箱 → 片 1 → 片 2（顺序按依赖）；
- RUN-006 旧挂账：deploy.sh preset 工具化、标定冻结（数据后）、slow 落地
  内容的决策消费验证；
- 未讨论留观：键盘浮层（无 buyer 不预造）、超视口列表重建、sticky 锚点、
  视口边缘策略、LocalView 空间关系族、跨 run 反思记忆。
