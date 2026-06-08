#!/usr/bin/env python3
"""
Outdated Document Scanner

Scans documentation files for freshness issues:
1. Documents not updated in >N days (configurable)
2. Code-document synchronization checks
3. Deprecated/draft status docs older than threshold

Usage:
    python scripts/doc_freshness.py --days=90
    python scripts/doc_freshness.py --days=90 --path=docs/
    python scripts/doc_freshness.py --verbose
"""

import argparse
import os
import re
import sys
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Optional, Tuple


@dataclass
class DocMetadata:
    """Metadata extracted from a document file."""
    path: str
    last_updated: Optional[datetime] = None
    status: str = "unknown"  # draft, stable, deprecated, archived, unknown
    linked_modules: List[str] = field(default_factory=list)
    issues: List[str] = field(default_factory=list)


@dataclass
class FreshnessReport:
    """Report of document freshness issues."""
    outdated_docs: List[DocMetadata] = field(default_factory=list)
    stale_sync_issues: List[Tuple[DocMetadata, str, int]] = field(default_factory=list)
    deprecated_old: List[DocMetadata] = field(default_factory=list)
    summary: Dict[str, int] = field(default_factory=dict)


class DocFreshnessScanner:
    """Scanner for document freshness issues."""

    # Patterns to extract metadata from documents
    DATE_PATTERNS = [
        r"Last updated:\s*(\d{4}-\d{2}-\d{2})",
        r"Updated:\s*(\d{4}-\d{2}-\d{2})",
        r"更新日期:\s*(\d{4}-\d{2}-\d{2})",
        r"Date:\s*(\d{4}-\d{2}-\d{2})",
    ]

    STATUS_PATTERNS = [
        r"Status:\s*(\w+)",
        r"状态:\s*(\w+)",
    ]

    MODULE_LINK_PATTERNS = [
        r"`src/([^/]+)/`",
        r"`src/([^/]+/[^/]+\.py)`",
        r"\[.*\]\(src/([^/]+)\)",
    ]

    def __init__(self, days_threshold: int = 90, deprecated_threshold: int = 30, sync_threshold: int = 1):
        self.days_threshold = days_threshold
        self.deprecated_threshold = deprecated_threshold
        self.sync_threshold = sync_threshold  # Minimum days to report sync issue
        self.now = datetime.now()

    def scan_directory(self, root_path: str) -> List[DocMetadata]:
        """Scan all markdown documents in a directory."""
        docs = []
        root = Path(root_path)

        if not root.exists():
            print(f"Warning: Path {root_path} does not exist")
            return docs

        for md_file in root.rglob("*.md"):
            # Skip excluded paths
            if any(skip in str(md_file) for skip in [".git", "node_modules", "__pycache__"]):
                continue

            metadata = self._extract_metadata(md_file)
            docs.append(metadata)

        return docs

    def _extract_metadata(self, file_path: Path) -> DocMetadata:
        """Extract metadata from a markdown file."""
        metadata = DocMetadata(path=str(file_path))

        try:
            content = file_path.read_text(encoding="utf-8")

            # Extract last updated date
            for pattern in self.DATE_PATTERNS:
                match = re.search(pattern, content)
                if match:
                    try:
                        metadata.last_updated = datetime.strptime(match.group(1), "%Y-%m-%d")
                        break
                    except ValueError:
                        pass

            # If no date found, use file mtime
            if metadata.last_updated is None:
                metadata.last_updated = datetime.fromtimestamp(file_path.stat().st_mtime)
                metadata.issues.append("No explicit last_updated date in document")

            # Extract status
            for pattern in self.STATUS_PATTERNS:
                match = re.search(pattern, content, re.IGNORECASE)
                if match:
                    metadata.status = match.group(1).lower()
                    break

            # Extract linked modules
            for pattern in self.MODULE_LINK_PATTERNS:
                matches = re.findall(pattern, content)
                metadata.linked_modules.extend(matches)

        except Exception as e:
            metadata.issues.append(f"Error reading file: {e}")

        return metadata

    def check_freshness(self, docs: List[DocMetadata]) -> FreshnessReport:
        """Check all freshness criteria."""
        report = FreshnessReport()

        for doc in docs:
            days_since_update = (self.now - doc.last_updated).days if doc.last_updated else 999

            # Check outdated documents
            if days_since_update > self.days_threshold:
                report.outdated_docs.append(doc)

            # Check deprecated/draft docs that are old
            if doc.status in ["deprecated", "draft"] and days_since_update > self.deprecated_threshold:
                report.deprecated_old.append(doc)

            # Check code-document sync
            if doc.linked_modules:
                for module in doc.linked_modules:
                    module_path = self._find_module_path(doc.path, module)
                    if module_path and Path(module_path).exists():
                        module_mtime = datetime.fromtimestamp(Path(module_path).stat().st_mtime)
                        if doc.last_updated and module_mtime > doc.last_updated:
                            stale_days = (module_mtime - doc.last_updated).days
                            # Only report if code is significantly newer
                            if stale_days >= self.sync_threshold:
                                report.stale_sync_issues.append((doc, module, stale_days))

        # Build summary
        report.summary = {
            "total_docs": len(docs),
            "outdated_count": len(report.outdated_docs),
            "sync_issues": len(report.stale_sync_issues),
            "deprecated_old": len(report.deprecated_old),
        }

        return report

    def _find_module_path(self, doc_path: str, module_ref: str) -> Optional[str]:
        """Find the actual path to a linked module."""
        # Get project root
        doc_file = Path(doc_path)
        project_root = self._find_project_root(doc_file)

        if not project_root:
            return None

        module_ref = module_ref.strip()
        module_path = project_root / "src" / module_ref

        # Try exact path first
        if module_path.exists():
            return str(module_path)

        # Try as .py file
        module_path = project_root / "src" / f"{module_ref}.py"
        if module_path.exists():
            return str(module_path)

        # Try as directory
        module_path = project_root / "src" / module_ref
        if module_path.is_dir():
            # Use the most recent file in the directory
            py_files = list(module_path.glob("*.py"))
            if py_files:
                most_recent = max(py_files, key=lambda f: f.stat().st_mtime)
                return str(most_recent)

        return None

    def _find_project_root(self, start_path: Path) -> Optional[Path]:
        """Find project root by looking for common markers."""
        current = start_path
        markers = ["CLAUDE.md", "pyproject.toml", "setup.py", ".git"]

        for _ in range(10):  # Max 10 levels up
            if any((current / m).exists() for m in markers):
                return current
            if current.parent == current:  # Reached filesystem root
                break
            current = current.parent

        return None

    def _format_path(self, path: str) -> str:
        """Format path as relative to current directory if possible."""
        try:
            return str(Path(path).relative_to(os.getcwd()))
        except ValueError:
            return path

    def format_report(self, report: FreshnessReport, verbose: bool = False) -> str:
        """Format the report as readable text."""
        lines = []
        lines.append("=" * 80)
        lines.append("DOCUMENT FRESHNESS REPORT")
        lines.append("=" * 80)
        lines.append(f"Scan date: {self.now.strftime('%Y-%m-%d')}")
        lines.append(f"Threshold: {self.days_threshold} days for outdated, {self.deprecated_threshold} days for deprecated")
        lines.append("")

        # Summary
        lines.append("SUMMARY")
        lines.append("-" * 40)
        for key, value in report.summary.items():
            lines.append(f"  {key}: {value}")
        lines.append("")

        # Outdated documents
        if report.outdated_docs:
            lines.append(f"OUTDATED DOCUMENTS ({len(report.outdated_docs)} files)")
            lines.append("-" * 40)
            for doc in sorted(report.outdated_docs, key=lambda d: d.last_updated or datetime.min):
                days_old = (self.now - doc.last_updated).days if doc.last_updated else 999
                rel_path = self._format_path(doc.path)
                lines.append(f"  {rel_path}")
                lines.append(f"    Last updated: {doc.last_updated.strftime('%Y-%m-%d') if doc.last_updated else 'Unknown'} ({days_old} days ago)")
                lines.append(f"    Status: {doc.status}")
                if verbose and doc.issues:
                    for issue in doc.issues:
                        lines.append(f"    Issue: {issue}")
                lines.append("")

        # Code-document sync issues
        if report.stale_sync_issues:
            lines.append(f"CODE-DOCUMENT SYNC ISSUES ({len(report.stale_sync_issues)} issues)")
            lines.append("-" * 40)
            for doc, module, stale_days in sorted(report.stale_sync_issues, key=lambda x: x[2], reverse=True):
                rel_path = self._format_path(doc.path)
                lines.append(f"  {rel_path}")
                lines.append(f"    Module: src/{module}")
                lines.append(f"    Code is {stale_days} days newer than documentation")
                lines.append("")

        # Deprecated/draft old documents
        if report.deprecated_old:
            lines.append(f"OLD DEPRECATED/DRAFT DOCS ({len(report.deprecated_old)} files)")
            lines.append("-" * 40)
            for doc in sorted(report.deprecated_old, key=lambda d: d.last_updated or datetime.min):
                days_old = (self.now - doc.last_updated).days if doc.last_updated else 999
                rel_path = self._format_path(doc.path)
                lines.append(f"  {rel_path}")
                lines.append(f"    Status: {doc.status}")
                lines.append(f"    Last updated: {doc.last_updated.strftime('%Y-%m-%d') if doc.last_updated else 'Unknown'} ({days_old} days ago)")
                rec = "Review and archive" if doc.status == "deprecated" else "Publish or remove"
                lines.append(f"    Recommendation: {rec}")
                lines.append("")

        # Recommendations
        lines.append("RECOMMENDATIONS")
        lines.append("-" * 40)

        if report.outdated_docs:
            lines.append(f"1. Review {len(report.outdated_docs)} outdated documents")
            lines.append("   - Update last_updated date after review")
            lines.append("   - Add status markers (draft/stable/deprecated)")

        if report.stale_sync_issues:
            lines.append(f"2. Fix {len(report.stale_sync_issues)} code-document sync issues")
            lines.append("   - Update documentation to reflect code changes")
            lines.append("   - Or update code to match documented behavior")

        if report.deprecated_old:
            lines.append(f"3. Process {len(report.deprecated_old)} old deprecated/draft docs")
            lines.append("   - Move deprecated docs to archive/")
            lines.append("   - Finalize or remove draft docs")

        if not any([report.outdated_docs, report.stale_sync_issues, report.deprecated_old]):
            lines.append("No issues found! Documentation is fresh.")

        lines.append("")
        lines.append("=" * 80)

        return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(
        description="Scan for outdated documentation",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    parser.add_argument(
        "--days",
        type=int,
        default=90,
        help="Days threshold for outdated documents (default: 90)"
    )
    parser.add_argument(
        "--deprecated-days",
        type=int,
        default=30,
        help="Days threshold for deprecated/draft documents (default: 30)"
    )
    parser.add_argument(
        "--path",
        type=str,
        default="docs/",
        help="Path to scan for documentation (default: docs/)"
    )
    parser.add_argument(
        "--sync-days",
        type=int,
        default=7,
        help="Minimum days for code-doc sync issues (default: 7)"
    )
    parser.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Show detailed information including file-level issues"
    )

    args = parser.parse_args()

    scanner = DocFreshnessScanner(
        days_threshold=args.days,
        deprecated_threshold=args.deprecated_days,
        sync_threshold=args.sync_days
    )

    print(f"Scanning {args.path} for outdated documents...")
    docs = scanner.scan_directory(args.path)
    print(f"Found {len(docs)} documentation files")

    report = scanner.check_freshness(docs)
    print("\n" + scanner.format_report(report, verbose=args.verbose))

    # Exit with error code if issues found
    total_issues = (
        len(report.outdated_docs) +
        len(report.stale_sync_issues) +
        len(report.deprecated_old)
    )

    if total_issues > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
