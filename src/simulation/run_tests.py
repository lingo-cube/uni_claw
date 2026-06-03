#!/usr/bin/env python3
"""单元测试脚本"""

import argparse
import subprocess
import sys
from pathlib import Path

def main():
    parser = argparse.ArgumentParser(description="单元测试")
    parser.add_argument("-c", "--coverage", action="store_true", help="覆盖率")
    args = parser.parse_args()
    
    test_path = Path(__file__).parent / "test"
    cmd = [sys.executable, "-m", "pytest", str(test_path), "-v"]
    if args.coverage:
        cmd.extend(["--cov=src." + Path(__file__).parent.name, "--cov-report=term-missing"])
    
    result = subprocess.run(cmd, cwd=Path(__file__).parent.parent.parent)
    sys.exit(result.returncode)

if __name__ == "__main__":
    main()
