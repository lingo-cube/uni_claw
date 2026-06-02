#!/bin/bash
# Convenience wrapper for verify_refactor.py
# This script ensures Python path is correct and runs verification

# Get project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_ROOT" || exit 1

# Run the verification script
python scripts/verify_refactor.py "$@"
