# ScreenVLM exploratory probe（FSV-001 后置探针，M4 素材）

- 时点：FSV-001 主实验（v2 判定）收口后；不参与 Test A/B 变量（任务书 §18
  允许的后置 exploratory probe）。
- 模型：docling-project/ScreenVLM v2（Idefics3 0.3B），olragon/ScreenVLM-MLX-4bit
  量化，独立探针 venv（/tmp/screenvlm-venv，mlx-vlm 0.7.0；olragon
  tokenizer_config 补丁仅缓存内，不进仓）。M4 Metal，21.7 tok/s，~12.3s/整屏。
- 语料：uni-agent 分支导出（git ref uni-agent:）——71-query grounding
  （4 真机屏 + truth.json）、71 元素类型判别真值、9 核心题 dump 判型、
  旧 qwen/UI-TARS 基准原始结果。
- 结果：web 分布内正常（MDN 屏元素/文本/ScreenTag 合法）；Android 原生
  崩坏级 OOD——root-top 被幻觉成网站导航、devopts 206 元素全塌缩 "text"、
  grounding 8/71（11%）、9 核心题全错。
- 结论：ScreenVLM 作 Slow 层限 web 域；UniClaw Android 需域微调
  （nanoVLM+ScreenTag 管线官方现成，a11y 自采数据；FSV 39 帧验证集形态
  可复用）。佐证 dump+文本 LLM 判型在 Android 域的优势结论。
- 环境事件（完整披露）：探针过程曾污染 perception venv（mlx/transformers
  连带 opencv/scipy 升级，YOLO 字节回归锚 3 项失败），已外科还原至
  runtime.txt pin（numpy 1.26.4/torch 2.2.2/torchvision 0.17.2/opencv
  4.10.0.84/scipy 1.13.1，mlx/transformers 卸载）；Leader 独立复核
  （pip pin + pytest 136 全绿）。
