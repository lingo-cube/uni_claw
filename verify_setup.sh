#!/bin/bash
# Setup verification script for Uni-Claw development environment

echo "🔧 Uni-Claw Development Environment Verification"
echo "=============================================="

# Check Python version
echo "📋 Checking Python version..."
python --version
if [ $? -ne 0 ]; then
    echo "❌ Python not found. Please install Python 3.10+"
    exit 1
fi

# Check pip version
echo "📋 Checking pip version..."
pip --version
if [ $? -ne 0 ]; then
    echo "❌ pip not found. Please ensure pip is installed"
    exit 1
fi

# Check if in project root
echo "📋 Checking project structure..."
if [ ! -f "pyproject.toml" ]; then
    echo "❌ pyproject.toml not found. Please run from project root"
    exit 1
fi

# Check if pytest-asyncio is installed
echo "📋 Checking critical test dependencies..."
python -c "import pytest_asyncio" 2>/dev/null
if [ $? -ne 0 ]; then
    echo "❌ pytest-asyncio not found. Installing dev dependencies..."
    pip install -e ".[dev]"
    if [ $? -ne 0 ]; then
        echo "❌ Failed to install dependencies"
        exit 1
    fi
    echo "✅ Dependencies installed successfully"
else
    echo "✅ pytest-asyncio found"
fi

# Verify AI module tests
echo "📋 Running AI module tests..."
python -m pytest src/ai/test/ --tb=no -q
if [ $? -eq 0 ]; then
    echo "✅ All AI tests passing - environment ready!"
else
    echo "⚠️  Some tests failing, check output above"
    exit 1
fi

echo ""
echo "🎉 Setup verification complete!"
echo "Your development environment is properly configured."
echo ""
echo "Quick test command:"
echo "  python -m pytest src/ai/test/ --tb=no -q"
