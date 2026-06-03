#!/bin/bash
# E2E仿真测试执行脚本 (Unix/Linux/Mac)
# 使用方法: ./run_e2e.sh

set -e  # 遇到错误立即退出

echo "========================================================"
echo "  E2E仿真测试执行器"
echo "========================================================"
echo ""

# 检查Python是否可用
if ! command -v python3 &> /dev/null; then
    echo "错误: Python3未找到，请确保Python3已安装"
    exit 1
fi

# 显示Python版本
echo "使用Python版本: $(python3 --version)"
echo ""

# 运行E2E测试
echo "运行E2E测试..."
echo ""

python3 run_e2e_simple.py
EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo ""
    echo "========================================================"
    echo "  测试成功完成"
    echo "========================================================"
else
    echo ""
    echo "========================================================"
    echo "  测试失败 (退出码: $EXIT_CODE)"
    echo "========================================================"
fi

exit $EXIT_CODE