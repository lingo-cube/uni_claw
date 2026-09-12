# FSV-001 WI-3 — Replacement impl + B2 消融（transient WorkItem 载荷）

- status: dispatched → subagent e7d0b65c-519d-474b-9f39-15f704cc435a
- objective: detect impl screenparser/screenparser-mps + 变体
  fastscreen-replacement(.json/.mps.json)（B1：detector 换 OCR 留，
  Server-Timing yolo 段语义保持）+ bench/run_replacement_ablation.py
  （B2：OCR 关闭消融，进程内同构 envelope）+ pytest。
- acceptance: B1 响应与 baseline 同构且 yolo[] schema 一致；B2 ocr 恒空；
  默认回归锚仍绿。
- result: ✅ 完成（subagent e7d0b65c-519d-474b-9f39-15f704cc435a）
  - detect impl `screenparser`/`screenparser-mps` 注册（STAGE_IMPLS +
    DETECT_IMPL_DEVICE）；mps 可用性探测照 torch-mps 语义 fail-closed；
    screenparser 系与 screenparse 集成段互斥 lint（fail-closed）。
  - server.py：replacement 模式下 detect 阶段改调 screenparse provider +
    adapter（mapped re-id det_{n} 全量即 detect 输出；structural 进
    screenParse[]；rescue/corroboration 不适用）；screenparse 推理计入
    yolo;dur（t_sp=None，无新增分段，与 baseline 同段语义可对拍）；
    并行（detect ∥ full-image OCR）沿用 can_parallel 结构。
  - 变体：fastscreen-replacement(.json/.mps.json)。
  - bench/run_replacement_ablation.py（B2：OCR 恒空，响应同构 JSON 输出）。
  - pytest：58 passed（新增 tests/test_replacement.py 14 项：注册表 lint /
    B1 同构+schema / OCR retained / 确定性 / B2 子进程对拍；WI-2 默认回归锚
    未破坏）。live UDS + curl 实测两资产，详见完成报告。
  - Leader 抽验（2026-09-12）：pytest 58 绿；变体注册×5、detect impl×4、
    ablation 模块加载均 OK。实测信号：B1 settings-home yolo 22 vs baseline
    26 / cand 13 vs 9 / yolo 段 737ms vs 482ms（CPU）；MPS 反而慢（2233ms）；
    B2 cand 塌缩 11→4（文字关联消失的诚实差）。
