@echo off
REM E2E仿真测试执行脚本 (Windows)
REM 使用方法: run_e2e.bat

echo ========================================================
echo   E2E仿真测试执行器
echo ========================================================
echo.

REM 检查Python是否可用
python --version >nul 2>&1
if errorlevel 1 (
    echo 错误: Python未找到，请确保Python已安装并在PATH中
    exit /b 1
)

REM 运行简单的E2E测试
echo 运行E2E测试...
echo.
python run_e2e_simple.py

if errorlevel 1 (
    echo.
    echo ========================================================
    echo   测试失败 (退出码: %ERRORLEVEL%)
    echo ========================================================
    exit /b %ERRORLEVEL%
) else (
    echo.
    echo ========================================================
    echo   测试成功完成
    echo ========================================================
    exit /b 0
)