# Local Vision Analyzer — 知识索引

## knowledge.md
- L1: LocalVisionProvider item 提取管线（C# + Python）
- L2: Item 质量维度（类型/坐标/文本/去重/滚动）
- L3: 诊断方法论（analysis.jsonl / 截图 / 回归对比）

## lessons.md
- 2026-08-05: OCR 副标题误标为 menuItem（Storage "28%used" 案例）
- 2026-08-05: OCR 文本空格变体导致重复点击（"Bluetooth, pairing" vs "Bluetooth,pairing"）
- 2026-08-05: YOLO 搜索框 type 波动（text ↔ button）
- 2026-08-05: endOfList 从未触发（6/6 run 全部 false）
- 2026-08-05: 坐标错位模式（副标题幻影 item 坐标偏到相邻行）

## scripts/
- item_quality_check.py: analysis.jsonl 批量 item 质量检查
- coord_validation.py: 坐标 vs 截图验证
- type_distribution.py: item type 分布统计
- text_variant_detect.py: OCR 文本变体跨帧检测
- benchmark_ocr_stability.py: OCR 稳定性压测
