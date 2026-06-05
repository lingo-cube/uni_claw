@echo off
REM Setup verification script for Uni-Claw development environment (Windows)

echo 🔧 Uni-Claw Development Environment Verification
echo ==============================================
echo.

REM Check Python version
echo 📋 Checking Python version...
python --version
if %errorlevel% neq 0 (
    echo ❌ Python not found. Please install Python 3.10+
    exit /b 1
)

REM Check pip version
echo 📋 Checking pip version...
pip --version
if %errorlevel% neq 0 (
    echo ❌ pip not found. Please ensure pip is installed
    exit /b 1
)

REM Check if in project root
echo 📋 Checking project structure...
if not exist "pyproject.toml" (
    echo ❌ pyproject.toml not found. Please run from project root
    exit /b 1
)

REM Check if pytest-asyncio is installed
echo 📋 Checking critical test dependencies...
python -c "import pytest_asyncio" 2>nul
if %errorlevel% neq 0 (
    echo ❌ pytest-asyncio not found. Installing dev dependencies...
    pip install -e ".[dev]"
    if %errorlevel% neq 0 (
        echo ❌ Failed to install dependencies
        exit /b 1
    )
    echo ✅ Dependencies installed successfully
) else (
    echo ✅ pytest-asyncio found
)

REM Verify AI module tests
echo 📋 Running AI module tests...
python -m pytest src/ai/test/ --tb=no -q
if %errorlevel% equ 0 (
    echo ✅ All AI tests passing - environment ready!
) else (
    echo ⚠️  Some tests failing, check output above
    exit /b 1
)

echo.
echo 🎉 Setup verification complete!
echo Your development environment is properly configured.
echo.
echo Quick test command:
echo   python -m pytest src/ai/test/ --tb=no -q
