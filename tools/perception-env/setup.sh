#!/usr/bin/env bash
# tools/perception-env/setup.sh — PER-005 ④ 环境拉起（幂等；Q10=A′ RapidOCR 最小集）
#
# 产物：
#   .perception/provider/ — uni-agent platforms/perception 运行子树（git blob 物化；
#     不含 evaluation/training/tests/cli——非运行面）
#   .perception/venv/     — Python 3.11 venv，版本保真 runtime.txt pin（torch 2.2.2 /
#     torchvision 0.17.2 / ultralytics 8.4.115 / rapidocr-onnxruntime 1.4.4 /
#     onnxruntime 1.23.2 / pillow / numpy 1.26.4 / fastapi 0.141.1 / uvicorn 0.52.1）
#     —— 跳过 paddleocr/paddlepaddle（rapidocr 是默认后端；paddle 被配置时服务
#     启动期 fail-closed，正是 D7 要的 fail-loud）。
# 不安装 paddle；不以「依赖安装成功」代替运行证明（运行验收在 C# ENVIRONMENT
# 测试 PerceptionLiveEnvironmentTests，A4/A5）。
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROVIDER="$ROOT/.perception/provider"
VENV="$ROOT/.perception/venv"
PYTHON="${UNICAW_PERCEPTION_PYTHON:-/opt/homebrew/bin/python3.11}"

command -v git >/dev/null || { echo "git 不在场" >&2; exit 2; }
[ -x "$PYTHON" ] || { echo "Python 3.11 不在场：$PYTHON（可用 UNICAW_PERCEPTION_PYTHON 覆盖）" >&2; exit 2; }

# 1) 物化 provider 运行子树（uni-agent 全程只读；--strip-components=2 去掉
#    platforms/perception 前缀，内容落 $PROVIDER 根）。整树物化、仅排除
#    training（21.6MB 训练产物，运行零依赖）：governance 存在跨包 import
#    （evaluation.identity / persistence / reports），逐目录点名单会漏——
#    实测先后踩 ModuleNotFoundError: evaluation、persistence。
if [ ! -f "$PROVIDER/requirements/runtime.txt" ]; then
  echo "[setup] 物化 provider 子树（platforms/perception 全树 − training）…"
  mkdir -p "$PROVIDER"
  git -C "$ROOT" archive uni-agent -- platforms/perception \
    | tar -x -C "$PROVIDER" --strip-components=2 --exclude='platforms/perception/training'
else
  echo "[setup] provider 已在场（幂等跳过）"
fi

# 1.5) provider 资产修复（对齐其自身注册记录）：en_PP-OCRv4_dict.txt 在
#      uni-agent git 里只有 94 行（=可打印 ASCII），但 governance manifest
#      注册为 "95-char en rec dictionary (PP-OCRv4 structure)"——缺一个
#      空格行。95+blank+space=97 = rec ONNX 输出维度；94 行时 rapidocr 解码
#      IndexError（实测）。幂等补上空格行；uni-agent 分支零改动。
DICT="$PROVIDER/ocr/models/en_PP-OCRv4_dict.txt"
if [ -f "$DICT" ] && [ "$(wc -l < "$DICT" | tr -d ' ')" = "94" ]; then
  echo "[setup] 修复 en_PP-OCRv4_dict.txt（94→95 行：补注册缺失的空格行）"
  printf ' \n' >> "$DICT"
  printf ' \n' >> "$DICT"
fi

# 2) venv + RapidOCR 最小集
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

# 3) 依赖闭包自检（导入级；运行级验收归 A4）
"$VENV/bin/python" - <<'PY'
import torch, torchvision, ultralytics, fastapi, uvicorn, PIL, numpy
import onnxruntime, rapidocr_onnxruntime
print(f"deps ok: torch={torch.__version__} ort={onnxruntime.__version__} "
      f"numpy={numpy.__version__} fastapi={fastapi.__version__}")
PY

echo "[setup] provider: $PROVIDER"
echo "[setup] venv:     $VENV"
echo "[setup] done. 运行验收（A4/A5）：DSH_TEST_PERCEPTION_LIVE=1 dotnet test --filter PerceptionLiveEnvironmentTests"
