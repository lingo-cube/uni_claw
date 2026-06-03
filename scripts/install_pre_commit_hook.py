#!/usr/bin/env python3
"""
Installation script for pre-commit hooks.

Sets up the pre-commit hook for simulation testing
and refactoring verification.
"""

import os
import sys
import shutil
from pathlib import Path


def install_pre_commit_hook():
    """Install pre-commit hook for simulation testing."""
    project_root = Path(__file__).parent.parent
    hooks_dir = project_root / ".git" / "hooks"
    source_hook = Path(__file__).parent / ".git" / "hooks" / "pre-commit"

    if not hooks_dir.exists():
        print("ERROR: .git/hooks directory not found")
        print("This script must be run from a git repository.")
        return 1

    # Backup existing hook if it exists
    target_hook = hooks_dir / "pre-commit"
    if target_hook.exists():
        backup_path = hooks_dir / "pre-commit.backup"
        shutil.copy(target_hook, backup_path)
        print(f"Backed up existing pre-commit hook to {backup_path}")

    # Copy the pre-commit hook
    shutil.copy(source_hook, target_hook)
    os.chmod(target_hook, 0o755)  # Make executable

    print("Pre-commit hook installed successfully!")
    print(f"Location: {target_hook}")
    print("")
    print("The hook will:")
    print("  1. Run refactoring verification (blocking)")
    print("  2. Run simulation testing setup verification (non-blocking)")
    print("")
    print("To bypass: git commit --no-verify")
    return 0


def main():
    """Main installation function."""
    print("Installing pre-commit hook for simulation testing...")
    print("=" * 60)

    return install_pre_commit_hook()


if __name__ == '__main__':
    sys.exit(main())