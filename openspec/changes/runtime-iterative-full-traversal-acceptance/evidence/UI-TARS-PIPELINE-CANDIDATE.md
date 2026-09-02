# UI-TARS 管道候选评估（PIPELINE-CANDIDATE：感知通道门 A 的候选模型）

> 状态：**可行性已确认（本机可跑）· 离线评估第 1 步就绪（截图 + 脚本）· 等待模型端点就位**。
> 候选目的：替代/并行走既有 OCR+uiautomator 视觉通道，针对 Phase 2.6 残余感知方差三大家族：
> OCR 短读（'Wallpaper & style'→'Wallpaper'）、uiautomator 漏枚举/坐标偏移、textless 图标/节标题 & 长描述。

## 1. 可行性（已核实，非假设）

| 项 | 事实 |
|---|---|
| 本机 | Apple M4 · 24GB 统一内存 · Metal 4 · 109GB 空闲 |
| 推理栈 | **llama.cpp 已装**（`/opt/homebrew/bin/llama-server`）→ Metal GGUF，OpenAI 兼容端点 |
| 模型形态 | UI-TARS（Qwen2.5-VL 系架构，7B/2B/72B）；GGUF+mmproj 需在 HF 社区确认具体仓库（web_search 余额不足，未能在线核对——待核）|
| 预期延迟 | M4 上 7B q4 ≈ 10–25s/帧 → **离线评估 OK（批处理）**；真机 A/B 需降帧率或接受慢速（先测）|

## 2. 待测病种 KPI（对照现有 19 轮基线，判定是否值得上车）

| 病种 | 现有表现 | UI-TARS 判定标准 |
|---|---|---|
| 'Wallpaper & style' 短读为 'Wallpaper' → root Unknown | I/M/P/S 5 轮 | 正确框出 'Wallpaper & style' == 命中 |
| uiautomator 漏枚举底行（XML 截于 0.95）| L/U/W 等 | 截图框出全部行（含底行标题）|
| 节标题/长描述（Accessibility 页）Unknown | V/X 2 轮 | 能区分"标题（不可点）"与"行（可点）"|
| textless 图标（root 顶部）Unknown | O 1 轮 | 图标框 + 描述 |

## 3. 阶段（成本递增；每步有独立证据闸）

1. **离线 Ground-Truth**（现流程）：截图 4 张已采集
   `artifacts/uitars-eval/{root-top,root-scrolled,display-child,accessibility}.png` +
   `eval_uitars.py` 脚本；端点就绪即跑 → 病种命中表。**零运行时改动。**
2. **Socket Host 适配器**：`UniClaw.Vision.Host` 加 UI-TARS provider（PNG→HTTP→elements），
   平行现有通道；fusion/稳定器复用。
3. **A/B 真机**：同 recipe 各若干轮 vs 19 轮基线，用 `artifacts/tools/classify.py` 对比
   epoch 完整性/失败类分布（**先测每帧延迟**再定采样策略）。

## 4. 就绪待办（等你点头/授权）

- [ ] HF 下载 UI-TARS GGUF + mmproj（约 5GB；仓库候选需在线确认——我当前 web_search 余额不足，
      标注这一核对缺口）
- [ ] `llama-server -m <model>.gguf --mmproj <mmproj>.gguf --port 8000`
- [ ] `python3 eval_uitars.py --endpoint http://127.0.0.1:8000 artifacts/uitars-eval/*.png`
      → 病种命中表 → 决定是否进入阶段 2/3


## 6. 首次实验（2026-08-30 · 2B q4 · llama-server(Metal) on M4）—— 结果：阳性（带条件）

- **部署**：bartowski/UI-TARS-2B-SFT-GGUF（Q4_K_M + mmproj-f16，hf-mirror 下载 ~2.4GB）→
  `llama-server -m … --mmproj … -ngl 99 -p 8000`；`/health` ok。
- **任务形态发现**：UI-TARS 是**指令式定位模型**（"列举所有元素"会退化输出幻框）；
  改为单目标定位提示（"locate '<txt>'"）后全部命中。
- **延迟**：每图首调 ~25s（图片编码），此后 ~0.3s/目标。
- **命中表**（`artifacts/uitars-eval/targeted-results.json`）：
  'Wallpaper & style'✓、'Will never turn on automatically'✓、'Dark theme'✓、'Screen timeout'✓、
  'Interaction controls'✓、'Captions'✓、'Audio'✓、'Audio description'✓、'Flash notifications'✓、
  长描述✓。**覆盖三个病种家族**（OCR 短读 / 副文本标题 / 节标题+长描述）。
- **条件/未决**：① 位置精度未用真值表严格核对（本模型无图像输入，无法对图验框；
    目标 y 值与页面布局记忆同量级但需人工/工具复核）；② 'Accessibility' 一次返回 "not visible"
    （可能真在折叠线下或假阴性）；③ 2B 保真度 < 7B/DPO，建议精度核验时用 7B-DPO（需带 mmproj 的
    GGUF 仓库，FelisDwan 仅有纯模型——待找）；④ 真机 A/B 的帧率可行性未测。
- **结论**：UI-TARS 作为感知供给候选**有实质价值**（覆盖本 session 三大家族残余），
  值得进入第 1 步严格核验（真值表位置判定）后再定第 2/3 步。

## 5. 边界

- UI-TARS 仅作**感知供给**假设；运行时判定/fail-closed/completeness 一律不因候选而改动（感知通道门 A 的边界不变）。