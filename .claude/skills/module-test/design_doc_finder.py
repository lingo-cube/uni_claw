#!/usr/bin/env python3
"""
设计文档查找器 - Fallback工具

当AI无法判断需要读取哪些设计文档时使用这个工具。
它会从CLAUDE.md提取文档索引并生成建议。
"""

import sys
from pathlib import Path
import re


def main():
    """简单的文档查找建议工具"""
    if len(sys.argv) < 2:
        print("Usage: python design_doc_finder.py <module_name>")
        print("Example: python design_doc_finder.py graph")
        sys.exit(1)

    module = sys.argv[1]
    project_root = Path.cwd()
    claude_md = project_root / "CLAUDE.md"

    if not claude_md.exists():
        print(f"❌ CLAUDE.md not found at {claude_md}")
        sys.exit(1)

    print(f"Design Document Lookup Suggestions: {module} module")
    print("=" * 50)
    print()
    print("Step 1: Read documentation index")
    print(f"  Read CLAUDE.md")
    print()
    print("Step 2: Find relevant documents by module name")
    print(f"  Search for '{module}' keyword in CLAUDE.md")
    print()
    print("Step 3: Common design document paths")
    print("  General Architecture:")
    print("    - docs/ARCHITECTURE.md")
    print("    - docs/ARCHITECTURE_V6.md")
    print()
    print(f"  Module Specific:")
    print(f"    - src/{module}/README.md (if exists)")
    print(f"    - docs/modules/{module}_design.md (if exists)")
    print()
    print("Step 4: AI determines needed documents")
    print("  Based on test failure specifics, AI should determine:")
    print("  - Architecture issue? -> Read architecture docs")
    print("  - API issue? -> Read module README")
    print("  - Requirement issue? -> Read PRD docs")
    print()
    print("Recommendation: Let AI read CLAUDE.md first, then determine needed docs")

if __name__ == "__main__":
    main()