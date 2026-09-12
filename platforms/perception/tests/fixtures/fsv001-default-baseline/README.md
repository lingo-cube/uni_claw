# fsv001-default-baseline — FSV-001 WI-2 默认管道响应基线（实现前）

三资产经 **default 管道（无变体）**产出的响应 JSON 原文
（`json.dumps(evidence, ensure_ascii=False)` = `/v1/analyze` 非 capture 请求
的 body 字节）。代表「实现前服务」在该输入上的响应。

## 捕获过程（可重放）

与真实服务同构（`bench/run_l2.py` 的 lifespan 等价注入 + `server.lifespan`
的 OCR 配置——**必须**调用 `configure_ocr_models`，否则 rapidocr 用包默认
模型，OCR confidence 与 served baseline 不一致）：

1. `cfg = load_config()`；`default = load_default()`；`lint_against_config`。
2. `cfg.ocr_backend == "rapidocr"` → `_rapid_ocr_kwargs.update(
   configure_ocr_models(language=cfg.ocr_lang))`（受管 en PP-OCRv4 mobile
   rec + dict；缺文件 fail-closed）。
3. `server._config = cfg`；`server._pipelines` 身份注入 **实现前记录值**
   （见下）——感知面字节与实现状态无关（default 路径条件编译，零效果），
   首捕已核验；身份字段恢复实现前记录使 fixture 完整代表实现前服务。
4. 每资产 `Image.open(...).convert("RGB")` → `server._run_pipeline(img, w, h,
   pipeline=default, pipeline_key="default")` → `json.dumps(evidence,
   ensure_ascii=False)` 落盘。

基线身份（2026-09-12 首捕记录；重捕时校验 configId 未漂移）：

- configId: `config:9cbe76d62637a0a48295129a6c9e79ddb5ebc6f234e7ed59eba9025d6b205e8e`
- pipelineRevision: `prev:3d61cc0e7163734ef41209975bfcb52e7207474caacc02af7abb1f95fdf7a8d0`
- deploymentId: `deploy:326cff00fe2b6fc61e38925c7b3873221e9c8da55fc3fd6c720410eae9a66af5`

## 对拍语义（见 test_screenparse_integration.py + 实测记录）

- 感知面（yolo / ocr / candidates / summary / scrollHints / image）与全部
  additive 外字段与 served baseline **逐字节一致**（live uvicorn 对拍：三
  资产 body 与 fixture 逐字节一致，仅 identity 字段除外）。
- `metadata.configId` **逐字节一致**（identity_content 对默认管道条件包含，
  不引入 screenparse 键）。
- `metadata.pipelineRevision` / `deploymentId` 按 PER-007 R2 内容寻址实现
  模块源码哈希——引入 FastScreen 行为模块后必然不同，此二字段为唯一允许
  差异（测试显式断言）。