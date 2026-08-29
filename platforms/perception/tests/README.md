# Perception Platform — Tests

`platforms/perception` 的测试套件（`tests/`）覆盖感知输入、融合引擎、行合成、
还原修复与 HTTP server 的 contract。

## 运行前置

测试需要 **runtime 依赖 + dev 依赖**（`requirements/dev.txt` 已通过 `-r runtime.txt`
引用运行时依赖；只装 dev 文件会因缺 `fastapi`/`pillow`/`numpy` 等在收集期失败）。

**canonical Python 版本：3.11**（Intel-macOS 栈，见 runtime.txt 头注）。全量测试
请用 python3.11 运行；python3.12（PEP 701 f-string 解析器差异）下
`test_engine_relation_head_integration.py` 会因
`f"{(gen_expr)}"` 语法边界报 SyntaxError——这是解释器版本问题，不是代码 bug。

```bash
# 完整安装（含 torch 2.2.2 Intel-macOS pin，见 runtime.txt 头注）
cd platforms/perception
python -m pip install -r requirements/runtime.txt -r requirements/dev.txt

# 运行全部测试
cd <repo-root>
PYTHONPATH=platforms/perception python -m pytest platforms/perception/tests -q
```

## 轻量快速路径（不装完整 ML 栈）

仅依赖 `pytest` 的 gate 类测试可用 uv 隔离运行：

```bash
UV_CACHE_DIR="$PWD/.uv-cache" uv run --with pytest --with fastapi \
  --with pillow --with numpy --with requests \
  python -m pytest platforms/perception/tests/test_spacing_verifier_title_column.py \
  platforms/perception/tests/test_composition_validity_veto_repair.py -q
```

涉及 `uniclaw_perception.yolo` / `ocr.rapid` / `fusion.engine` 的用例
（`test_backends_fusion`、`test_engine_relation_head_integration`、
`test_navigation_row_composition`、`test_reality_repair`、`test_server` 等）
需要按 runtime.txt 安装完整 ML 依赖后运行——这是全量门闭合的前提。

## 变更纪律

- 新增/修改感知算子时，先跑相关 contract 测试（上例快速路径），再跑全量。
- 全量收集失败（ModuleNotFoundError）先查依赖安装，再查 `PYTHONPATH`。