# PROJECT_LEADER_PHASE26_PERCEPTION_FAILURE_STRATIFICATION_AND_SLOW_CALIBRATION_SHADOW_RESULT

> Gate：PHASE26_PERCEPTION_FAILURE_STRATIFICATION_AND_SLOW_CALIBRATION_SHADOW。
> 结论：**分层账本完成；Z6 5→0→6 归因=整帧感知消失（acquisition/detection，非 VLM 证据）；shadow 基准
> 21 次查询：VLM 定位能力强（校准后 14-35px）、**角色分类假阳性 4/13≈31%（一流失败）**；VLM 可买=视觉文本
> 重读类（OCR 串、短读）、不可买=采集消失类；**推荐 outcome D → PERCEPTION_ACQUISITION_STABILITY**
> 为下一架构门 + SLOW_CALIBRATION_CONTRACT（2B 角色精度不足为 blocker 前置）。Phase 2.6 STOPPED。

## 1. Failure-family ledger（真实证据分层）

| 族 | 代表案例 | ExpectedReality | ObservedReality | RealityGap | FDP / FirstDivergence | Owner | EvidenceRef | AssetRef |
|---|---|---|---|---|---|---|---|---|
| A CAPTURE_ACQUISITION | **Z6 seq5**（5→0→6）| 连续 root 帧 | 单帧 fused=0/structured=0（邻居 seq2/4/6 均健康 8 候选）| 整帧感知消失（连标题 OCR 也无）| 捕获/检测单帧瞬断；帧视觉有效性无图不可证 | Perception acquisition/detection | `runs/p26-refine-runZ6-classify.txt` + stage seq5 | **MISSING_ASSET**（无 PNG）→ SHADOW_NOT_EVALUABLE |
| B VISUAL_DETECTION_MISS | 'Wallpaper' 行同行 mi 缺失（I/M）| 行有 menu_item peer | 双通道均无（XML 截断 + 检测漏）| 对象未产出 | 通道同步漏检 | Fusion/detector | `probes/runM-seq7-wallpaper.json` | MISSING_ASSET |
| C OCR_RECOGNITION | 'Lou'/'LoO'（P/S/Z5）；'Bluetooth, pairing' 碎片 | 正确文本行 | 乱码/碎片字符串 | 字符串错 | OCR 模型对密集小字误读（en-v4 已改善但残余）| FAST OCR | `runs/p26-{push-runP,ocr2-runZ5}-classify.txt` | MISSING_ASSET（run 帧未留图）|
| D COMPOSITION_RELATION | Accessibility 'Audio'/'Captions' 无关系 | 节标题/描述有明确关系 | 无 ChildOf/DescriptionOf | 关系缺失 | 能力层缺标题/描述组合原语（Z4 gate 已停）| Semantic capability | `PROJECT-LEADER-*-RESULT.md` 系列 | 有：`uitars-bench/accessibility.png`（fresh）✓ |
| E SEMANTIC_ROLE_GAP | 节标题/长描述/page-title vs 内容行 | 角色可判 | 无强证据分类（PAGE_TITLE 已 defer）| 角色模糊 | 视觉事实足够但角色谓词不可靠 | Semantic capability | `runV-stages` | 有：accessibility/display-child.png ✓ |
| F TEMPORAL_STABILITY | Z6/Z6b 起步 stability 预算；Z6b normalizer unresolved | 连续稳定 | 8→0→8 抖动 / 2 窗 sparse | 时序抖动 → fail-closed | 帧级 dropout + 稀疏窗 | Perception/exploration | Z6/Z6b stages | MISSING_ASSET |
| G UNKNOWN | r5 偶发"滚底退回根" | 持续 Display | 返回根页 | 触发源未知 | — | Environment/系统 | `REAL-CONTAINER-EXIT-*.md` | MISSING_ASSET（无录屏）|

每个案例的 FDP ≠ terminal reason（reason 已在各 stage/classify 原文中区分）。

## 2. AssetRef 覆盖

- **Evaluable（fresh + 关联 truth）**：`uitars-bench/{root-top,root-scrolled,accessibility,display-child}.png` + `truth.json`
  （今天真机采集、uiautomator 同刻配对）。
- **SHADOW_NOT_EVALUABLE（无 fresh 图像）**：所有 run 逐帧（Z5 seq8 'LoO'、I/M seq7/8、Z6 seq5）——campaign
  未保留逐帧 PNG；MISSING_ASSET。

## 3. ContainerSemanticCalibrationProposal（只读 schema，shadow）

```
ObservationRef / AssetRef / runId / seq
ContainerRoleCandidate
PageTitleCandidate[] / NavigationRowCandidate[] / LocalControlCandidate[] /
SectionHeaderCandidate[] / DescriptionCandidate[] / SupportingCandidate[]
Relations: ChildOf | LabelOf | DescriptionOf | SupportingOf | ReturnControlOf
每 proposal: region(bounds) + semanticRole + evidenceClass + confidence + ambiguityFlag
禁止输出：StableKey authority / SourceIdentity proof / Completion / Action auth / Recovery / Route
```
VLM_PROPOSAL != WORLD_TRUTH；VLM_ROLE != SOURCE_IDENTITY；VLM_CONFIDENCE != COMPLETENESS_PROOF；
VLM_CALIBRATION != CONTAINER_TRANSITION_AUTHORITY；**shadow 开/关运行时行为字节级等价**。

## 4. Benchmark results（shadow，UI-TARS-2B q4 / llama-server Metal，fresh 资产 only）

| 家族 | 目标 | truth | Fast（运行时）| Slow-VLM role | 判定 |
|---|---|---|---|---|---|
| OCR 串重读 | （accessibility 长描述）| truth 文本不匹配（/见注）| Unknown 族 | （无 role token，lat 25.5s）| **不可评（truth 串不一致）→ 复数新查询再判** |
| WALLPAPER 短读 | 'Wallpaper' | Y row | Unknown（缺 peer）| **row ✓** | VLM 定位/角色=row 正确 |
| BLUETOOTH 碎片 | 'Bluetooth, pairing' | Y row | Unknown | **search ✗** | 假阳性角色 |
| SECTION_HEADER | 'Captions' | Y | Unknown | **section-header ✓** | 正确 |
| SECTION_HEADER2 | 'Audio' | 不在该页 truth | Unknown | （无 role）| 不可评（真值缺）|
| 深描述 | 'Will never turn on automatically' | Y | Unknown | **description ✓** | 正确 |
| 副值 | 'Not set' | Y | Unknown | **description ✓** | 正确 |
| 普通行 | 'Notifications' | Y row | Nav ✓ | **search ✗** | 假阳性 |
| 普通行2 | 'Security & privacy' | Y row | Nav ✓ | **section-header ✗** | 假阳性 |
| 普通行3 | 'Screen timeout' | Y row | Nav ✓ | **row ✓** | 正确 |
| toggle | 'On' | Y toggle | LocalControl | **description ✗** | 假阳性 |
| 根标题 | 'Settings' | Y title | Container | **title ✓** | 正确 |
| 搜索 | 'Search settings' | Y search | Search-role | **search ✓** | 正确 |
| 采集失败帧 | Z6 seq5 | — | 空帧 | — | **VLM_NOT_APPLICABLE**（无图可看）|

**指标**：角色精度 **7/13 正确（含 2 不可评）**；**假阳性语义提升 4/13≈31%**（Bluetooth→search、
Notifications→search、Security&privacy→section-header、On→description）——**假提升为一流失败，2B 不达标**；
定位精度（先前基准）校准后 14-35px；延迟=新图首调 ~26s / 缓存 0.1s；tokens 见 `shadow-bench.json`；
evaluable-frame rate=13/21。

## 5. Fast vs Slow 三向对比

- **Fast（运行时现役）**：frame→OCR/检测→fusion→语义（Unknown 面 = 本 gate 的第 1 段）。
- **Slow（VLM shadow）**：能**定位**（框）与**部分角色**（title/description/header/row 有时正确），
  但**角色判别不可靠**（31% 假提升）→ 尚无资格进 contract。
- **Human/truth**：`truth.json`（uiautomator 同刻树 + 目检页布局）为判据。
- 结论：VLM 的**定位层**可作未来 grounding 补充；**角色层需 7B/DPO 或 prompt 工程再证**。

## 6. Z6 5→0→6 归因（gate §7 焦点）

seq2/4/6 = 健康完整 root 帧（8 候选：Settings/搜索/Network&internet…）；**seq5 单帧 fused=0+structured=0**
（整帧消失，含标题 OCR）→ 帧级**捕获/检测瞬断**（A 或 F 族）。**无 PNG → 帧视觉有效性不可证**——
按 gate §7**不是 VLM 集成证据**（VLM 无法补采集消失）。

## 7. VLM 能买/不能买

- **可买（候选 buyer）**：视觉文本重读类——OCR 串（'Lou'/'LoO' 若帧可见）、短读（'Wallpaper' 视觉全形）
  ——**前置**：fresh 帧留存（否则 MISSING_ASSET）+ 角色精度达标。
- **不可买**：CAPTURE/DETECTION 整帧消失（Z6）、settle/normalizer 时序（F=fast 侧）。
- **待合同**：组合/角色缺口（D/E）需 SLOW_CALIBRATION_CONTRACT 且先解决 2B 假提升。

## 8. 推荐下一架构门（Outcome D：mixed → 分买分离，不叠堆）

1. **PERCEPTION_ACQUISITION_STABILITY**（next buyer，A/B/F 族：Z6 整帧 dropout、稀疏窗、起步抖动）——
   单帧完整性守卫/检测重取（fast 侧，证据已足）。
2. **SLOW_CONTAINER_SEMANTIC_CALIBRATION_CONTRACT**（VLM buyer；前置=2B 角色假提升修复 → 7B/DPO
   或角色提示工程 → 再基准）。
3. FAST_OCR_OWNER：en-v4 已改善（Z5 后 root-ish）；残余 'LoO' 族留 fast 侧继续。

## 9. Phase 2.6 exact first blocker / StableKey / 输出

- **First blocker**（分层后）：`ROOT_UNKNOWN_PERCEPTION_VARIANCE` 实为**混合**——主成因=**OCR 字符串
  串/短读 + 双通道单行漏检（MIXED：C+D）**→ 归入上列 buyer 1/2 分离，不叠堆。
- **StableKey = PARKED_NOT_GRADUATED**（注册重开条件：Runtime 在 observation correlation 边界暴露
  EXPECTED_CONTAINER_TRANSITION / AUTHORIZED_CHILD_ENTRY_OBLIGATION 只读事实 → 恢复 seam 检疫 →
  真实 child 验证 → 毕业评审）。
- 证据链：本文件 + `shadow-bench.json`/`shadow_bench.py`（archived under uitars-bench/）+ 分层家族证据 refs。
- 无任何 VLM→运行时接入；shadow 开/关等价（未触碰运行时代码）。Phase 2.6 STOPPED。