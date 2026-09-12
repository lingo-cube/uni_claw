#!/usr/bin/env bash
# tools/perception-env/setup.sh — PER-007 v2：感知 provider 环境拉起（幂等）
#
# PER-007 后 provider 树已入仓（platforms/perception，含模型与修复后 dict）；
# 本脚本只负责 venv + RapidOCR 最小集（Q10=A′：跳过 paddle；被配置时服务
# 启动期 fail-closed，正是 D7 要的 fail-loud）。
# 版本保真 platforms/perception/requirements/runtime.txt pin（torch 2.2.2 /
# torchvision 0.17.2 / ultralytics 8.4.115 / rapidocr-onnxruntime 1.4.4 /
# onnxruntime 1.23.2 / pillow / numpy 1.26.4 / fastapi 0.141.1 / uvicorn 0.52.1）。
# 不以「依赖安装成功」代替运行证明（运行验收 = C# PerceptionLiveEnvironment
# Tests A4/A5）。
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
VENV="$ROOT/.perception/venv"
PYTHON="${UNICAW_PERCEPTION_PYTHON:-/opt/homebrew/bin/python3.11}"

[ -f "$ROOT/platforms/perception/requirements/runtime.txt" ] || {
  echo "platforms/perception 不在场（PER-007 迁移树缺失）" >&2; exit 2; }
[ -x "$PYTHON" ] || { echo "Python 3.11 不在场：$PYTHON（可用 UNICAW_PERCEPTION_PYTHON 覆盖）" >&2; exit 2; }

# 1) venv + RapidOCR 最小集
if [ ! -x "$VENV/bin/python" ]; then
  echo "[setup] 创建 venv（$PYTHON）…"
  "$PYTHON" -m venv "$VENV"
fi
echo "[setup] 安装依赖（首次约数百 MB 下载）…"
"$VENV/bin/pip" install --quiet --upgrade pip
"$VENV/bin/pip" install --quiet \
  torch==2.2.2 torchvision==0.17.2 ultralytics==8.4.115 \
  rapidocr-onnxruntime==1.4.4 onnxruntime==1.23.2 \
  'pillow>=10.0.0' numpy==1.26.4 \
  fastapi==0.141.1 'uvicorn[standard]==0.52.1'

# 2) 依赖闭包自检（导入级；运行级验收归 A4/A5）
"$VENV/bin/python" - <<'PY'
import torch, torchvision, ultralytics, fastapi, uvicorn, PIL, numpy
import onnxruntime, rapidocr_onnxruntime
print(f"deps ok: torch={torch.__version__} ort={onnxruntime.__version__} "
      f"numpy={numpy.__version__} fastapi={fastapi.__version__}")
PY

echo "[setup] venv: $VENV"
echo "[setup] done. 运行验收（A4/A5）：DSH_TEST_PERCEPTION_LIVE=1 dotnet test --filter PerceptionLiveEnvironmentTests"
