#!/usr/bin/env python3
"""
Documentation Structure Verification Script

This script verifies the documentation structure of the Uni-Claw project,
checking for:
1. CLAUDE modular files exist
2. Testing structure is correct
3. PRD structure is correct
4. No scattered files in root
5. temp/ directory exists and is gitignored
6. Broken markdown links (optional/best effort)

Exit codes:
    0: All checks pass
    1: Violations found
"""

import os
import re
import sys
from pathlib import Path
from typing import List, Tuple, Set


class Colors:
    """ANSI color codes for terminal output."""
    RED = '\033[91m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    BLUE = '\033[94m'
    BOLD = '\033[1m'
    END = '\033[0m'


class VerificationResult:
    """Container for verification results."""
    def __init__(self):
        self.errors: List[str] = []
        self.warnings: List[str] = []
        self.passed_checks: List[str] = []

    def add_error(self, message: str):
        """Add an error message."""
        self.errors.append(message)

    def add_warning(self, message: str):
        """Add a warning message."""
        self.warnings.append(message)

    def add_passed(self, message: str):
        """Add a passed check message."""
        self.passed_checks.append(message)

    def has_errors(self) -> bool:
        """Check if there are any errors."""
        return len(self.errors) > 0

    def print_summary(self):
        """Print the verification summary."""
        print(f"\n{Colors.BOLD}=== Documentation Structure Verification ==={Colors.END}\n")

        # Print passed checks
        if self.passed_checks:
            for check in self.passed_checks:
                print(f"{Colors.GREEN}[PASS]{Colors.END} {check}")

        # Print warnings
        if self.warnings:
            print()
            for warning in self.warnings:
                print(f"{Colors.YELLOW}[WARN]{Colors.END} {warning}")

        # Print errors
        if self.errors:
            print()
            for error in self.errors:
                print(f"{Colors.RED}[FAIL]{Colors.END} {error}")

        # Final summary
        print()
        if self.has_errors():
            print(f"{Colors.RED}{Colors.BOLD}FAILED:{Colors.END} {len(self.errors)} error(s) found")
            if self.warnings:
                print(f"{Colors.YELLOW}{len(self.warnings)} warning(s){Colors.END}")
        else:
            print(f"{Colors.GREEN}{Colors.BOLD}PASSED:{Colors.END} All checks passed")
            if self.warnings:
                print(f"{Colors.YELLOW}{len(self.warnings)} warning(s){Colors.END}")


def get_project_root() -> Path:
    """Get the project root directory."""
    # Script is in scripts/, so root is parent directory
    return Path(__file__).parent.parent.resolve()


def check_claude_files(root: Path, result: VerificationResult):
    """Check 1: CLAUDE modular files exist."""
    required_files = [
        'CLAUDE.md',
        'CLAUDE_STATUS.md',
        'CLAUDE_WORKFLOW.md',
        'CLAUDE_CONVENTIONS.md',
        'docs/INDEX.md',
    ]

    for file_path in required_files:
        full_path = root / file_path
        if full_path.exists():
            result.add_passed(f"CLAUDE file exists: {file_path}")
        else:
            result.add_error(f"Missing CLAUDE file: {file_path}")


def check_testing_structure(root: Path, result: VerificationResult):
    """Check 2: Testing structure is correct."""
    testing_dir = root / 'docs' / 'testing'

    # Check directory exists
    if not testing_dir.exists():
        result.add_error("Missing directory: docs/testing/")
        return

    result.add_passed("Directory exists: docs/testing/")

    # Check required files
    required_files = [
        'README.md',
        'STANDARDS.md',
        'WORKFLOWS.md',
        'QUICK_REFERENCE.md',
    ]

    for file_name in required_files:
        full_path = testing_dir / file_name
        if full_path.exists():
            result.add_passed(f"Testing doc exists: docs/testing/{file_name}")
        else:
            result.add_error(f"Missing testing doc: docs/testing/{file_name}")


def check_prd_structure(root: Path, result: VerificationResult):
    """Check 3: PRD structure is correct."""
    docs_dir = root / 'docs'
    prd_dir = docs_dir / 'prd'
    archive_prd_dir = docs_dir / 'archive' / 'prd'

    # Check docs/prd exists
    if prd_dir.exists():
        result.add_passed("Directory exists: docs/prd/")
    else:
        result.add_error("Missing directory: docs/prd/")

    # Check docs/archive/prd exists
    if archive_prd_dir.exists():
        result.add_passed("Directory exists: docs/archive/prd/")
    else:
        result.add_error("Missing directory: docs/archive/prd/")

    # Check for orphan PRD files in docs/ root
    # Only PRD_UNIFIED.md should be at docs/ level
    prd_pattern = re.compile(r'^PRD_V?\d+_.*\.md$|^PRD\.md$|^PRD_V?\d+\.md$')
    orphan_prds = []

    for item in docs_dir.iterdir():
        if item.is_file() and item.suffix == '.md':
            if prd_pattern.match(item.name) and item.name != 'PRD_UNIFIED.md':
                orphan_prds.append(item.name)

    if orphan_prds:
        for orphan in orphan_prds:
            result.add_error(f"Orphan PRD file in docs/ (should be in docs/prd/): docs/{orphan}")
    else:
        result.add_passed("No orphan PRD files in docs/ root")


def check_root_scattered_files(root: Path, result: VerificationResult):
    """Check 4: Root directory scattered files check."""
    allowed_patterns = [
        re.compile(r'^CLAUDE.*\.md$'),
        re.compile(r'^README\.md$'),
        re.compile(r'^\.gitignore$'),
        re.compile(r'^\.env\.example$'),
        re.compile(r'^\.env\.local$'),
        re.compile(r'^pyproject\.toml$'),
        re.compile(r'^setup\.py$'),
        re.compile(r'^setup\.cfg$'),
        re.compile(r'^requirements.*\.txt$'),
        re.compile(r'^poetry\.lock$'),
        re.compile(r'^LICENSE$'),
        re.compile(r'^CHANGELOG\.md$'),
        re.compile(r'^\.test_fix_log\.md$'),  # Allow test fix log
    ]

    allowed_dirs = {
        '.git',
        '.github',
        '.claude',
        '.pytest_cache',
        '.mypy_cache',
        'cli',
        'config',
        'dashboards',
        'docs',
        'examples',
        'openspec',
        'scripts',
        'src',
        'tests',
        'temp',
        'test_results',
    }

    scattered_files = []

    for item in root.iterdir():
        # Skip directories
        if item.is_dir():
            if item.name not in allowed_dirs:
                result.add_warning(f"Unusual directory in root: {item.name}/")
            continue

        # Check files
        is_allowed = False
        for pattern in allowed_patterns:
            if pattern.match(item.name):
                is_allowed = True
                break

        if not is_allowed:
            scattered_files.append(item.name)

    if scattered_files:
        for file_name in scattered_files:
            result.add_warning(f"Unusual file in root: {file_name}")
    else:
        result.add_passed("No scattered files in root (within allowed patterns)")


def check_temp_directory(root: Path, result: VerificationResult):
    """Check 5: temp/ directory check."""
    temp_dir = root / 'temp'

    # Check temp exists
    if not temp_dir.exists():
        result.add_error("Missing directory: temp/")
        return

    if not temp_dir.is_dir():
        result.add_error("temp/ exists but is not a directory")
        return

    result.add_passed("Directory exists: temp/")

    # Check temp is in .gitignore
    gitignore_path = root / '.gitignore'
    if not gitignore_path.exists():
        result.add_error("Missing .gitignore file")
        return

    gitignore_content = gitignore_path.read_text()
    if 'temp' in gitignore_content or '/temp' in gitignore_content:
        result.add_passed("temp/ is in .gitignore")
    else:
        result.add_error("temp/ is not in .gitignore")


def extract_markdown_links(content: str, file_path: Path) -> List[Tuple[str, int, str]]:
    """Extract markdown links from file content.

    Returns list of (link_target, line_number, link_text).
    """
    links = []
    lines = content.split('\n')

    # Markdown link patterns
    # 1. [text](path) - inline links
    # 2. [text](path "title") - inline links with title
    inline_pattern = re.compile(r'\[([^\]]+)\]\(([^)]+)\)')

    for line_num, line in enumerate(lines, 1):
        matches = inline_pattern.finditer(line)
        for match in matches:
            link_text = match.group(1)
            link_target = match.group(2).split()[0]  # Remove trailing "title" if present
            links.append((link_target, line_num, link_text))

    return links


def resolve_relative_link(link: str, source_file: Path, root: Path) -> Path:
    """Resolve a relative markdown link to an absolute path."""
    # Remove anchor fragments
    link_path = link.split('#')[0]

    if not link_path:
        return None  # Anchor-only link

    # Handle absolute paths (starting with /)
    if link_path.startswith('/'):
        return root / link_path.lstrip('/')

    # Handle relative paths
    source_dir = source_file.parent
    try:
        resolved = (source_dir / link_path).resolve()
        # Try to keep it within project root for validation
        return resolved
    except (OSError, RuntimeError):
        return None


def check_broken_links(root: Path, result: VerificationResult):
    """Check 6: Broken link check (optional/best effort)."""
    docs_dir = root / 'docs'

    if not docs_dir.exists():
        result.add_warning("Skipping link check: docs/ directory not found")
        return

    # Find all markdown files
    md_files = list(docs_dir.rglob('*.md'))
    md_files.extend(root.glob('*.md'))  # Include root level md files

    broken_links = []

    for md_file in md_files:
        try:
            content = md_file.read_text(encoding='utf-8')
            links = extract_markdown_links(content, md_file)

            for link_target, line_num, link_text in links:
                # Skip external links
                if link_target.startswith(('http://', 'https://', 'mailto:', 'ftp://')):
                    continue

                # Skip anchor-only links
                if not link_target or link_target.startswith('#'):
                    continue

                # Resolve the link
                resolved_path = resolve_relative_link(link_target, md_file, root)

                if resolved_path is None:
                    continue

                # Check if target exists
                # Try both with and without .md extension
                target_exists = resolved_path.exists()
                if not target_exists and not resolved_path.suffix:
                    # Try adding .md
                    target_exists = resolved_path.with_suffix('.md').exists()

                if not target_exists:
                    relative_path = md_file.relative_to(root)
                    broken_links.append(
                        f"{relative_path}:{line_num} - [{link_text}]({link_target})"
                    )
        except (OSError, IOError) as e:
            result.add_warning(f"Could not read {md_file.relative_to(root)}: {e}")

    if broken_links:
        result.add_warning(f"Found {len(broken_links)} potentially broken links:")
        for link in broken_links[:5]:  # Show first 5
            result.add_warning(f"  {link}")
        if len(broken_links) > 5:
            result.add_warning(f"  ... and {len(broken_links) - 5} more")
    else:
        result.add_passed("No broken links detected (best effort check)")


def main():
    """Main entry point."""
    root = get_project_root()
    result = VerificationResult()

    # Run all checks
    print(f"{Colors.BLUE}Checking documentation structure...{Colors.END}")

    check_claude_files(root, result)
    check_testing_structure(root, result)
    check_prd_structure(root, result)
    check_root_scattered_files(root, result)
    check_temp_directory(root, result)
    check_broken_links(root, result)

    # Print summary and exit
    result.print_summary()

    if result.has_errors():
        sys.exit(1)
    sys.exit(0)


if __name__ == '__main__':
    main()
