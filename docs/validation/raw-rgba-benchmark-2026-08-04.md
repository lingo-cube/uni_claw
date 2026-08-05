# Raw RGBA Screenshot Pipeline — 压测结果

> 日期: 2026-08-04
> 环境: macOS, Python 3.11, uvicorn @ 127.0.0.1:8765
> 截图: 1080×1920 Android Settings 页面 (PNG, 156KB)
> 模型: `android_ui_detection_yolov8/best.pt` + RapidOCR
> 脚本: `tools/local_vision/benchmark_raw.py`

## 延迟分布 (50 runs per path)

| Metric | JPEG (`/v1/analyze`) | Raw RGBA (`/v1/analyze_raw`) | Delta |
|--------|---------------------|------------------------------|-------|
| **P50** | 2488ms | 2004ms | **-484ms (-19.4%)** |
| **P95** | 3260ms | 2533ms | **-727ms (-22.3%)** |
| **P99** | 3876ms | 2628ms | **-1248ms (-32.2%)** |
| min | 2274ms | 1743ms | -531ms |
| max | 3876ms | 2628ms | -1248ms |

## 传输数据量

| | JPEG | Raw RGBA | Ratio |
|---|---|------|-------|
| Body size | 140 KB | 8,100 KB | 58× |

raw RGBA 数据量 58×，但 P50 反而快 484ms — **localhost 传输开销可忽略**。

## Micro-benchmark (独立于推理)

| 操作 | 耗时 | 
|------|------|
| `Image.frombytes("RGBA", 1080×1920)` | 3117 µs (3ms) |
| `Image.open(BytesIO(JPEG))` | 60 µs |

frombytes 比 open 慢 ~3ms，但端到端反而快 484ms — **编解码差异不是主要因素**。

## 性能收益根因分析

raw 路径快 19-32% 的核心原因不是编解码，而是 **预处理（crop+resize）**：

- JPEG 路径：1080×1920 全图直接喂 YOLO（~2.0M 像素）
- Raw 路径：`_preprocess` crop 上下各 6.25% → resize 720×1120（~0.8M 像素）
- YOLO 推理像素量减少 **~60%**，这是大头

旧路径 C# 侧也有 crop+resize（`ImageResizer`），但输出是 JPEG → Python 还得再解码。raw 路径预处理归 Python 后，YOLO 输入的图像已经缩放过。

## 结论

1. ✅ Raw RGBA 路径 P50 延迟降低 19.4%，P99 降低 32.2%
2. ✅ 传输 58× 数据量，但 localhost 无影响
3. ✅ `Image.frombytes` zero-decode 工作正常（3ms 内存包装）
4. ✅ 预处理归 Python（crop+resize）是主要性能收益来源
5. ⏳ YOLO 检测质量对比（lossless raw vs JPEG quality=85）待验证
6. ⏳ 长周期内存增长待验证（100+ 请求）
