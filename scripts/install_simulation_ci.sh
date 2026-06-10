#!/bin/bash
# Install simulation CI hooks
# Run: bash scripts/install_simulation_ci.sh

set -e

echo "=== Installing Simulation CI Integration ==="
echo ""

# Check if pre-commit is installed
if ! command -v pre-commit &> /dev/null; then
    echo "Installing pre-commit..."
    pip install pre-commit
fi

# Install pre-commit hooks
echo "Installing pre-commit hooks..."
pre-commit install

# Make run script executable
chmod +x scripts/run_simulation_ci.py

echo ""
echo "✅ Installation complete!"
echo ""
echo "Usage:"
echo "  1. Manual run: python scripts/run_simulation_ci.py"
echo "  2. With git hooks: git commit (will run automatically)"
echo "  3. Skip hooks: git commit --no-verify"
echo ""
echo "Files created:"
echo "  • .github/workflows/simulation-ci.yml (GitHub Actions)"
echo "  • .pre-commit-config.yaml (Local pre-commit hooks)"
echo "  • scripts/run_simulation_ci.py (CI runner)"
