#!/usr/bin/env python3
"""
Comprehensive Documentation Audit Script

This script performs a complete audit of the Uni-Claw documentation system:
1. Structure verification (docs directory integrity)
2. Freshness checks (last modified dates, outdated content)
3. Code-document coverage (modules vs their documentation)
4. Naming convention compliance (file naming standards)

Usage:
    python scripts/doc_audit.py
    python scripts/doc_audit.py --output docs/reports/doc_audit_2024-06-08.md

Output:
    Generates comprehensive report to docs/reports/doc_audit_YYYY-MM-DD.md
"""

import os
import re
import sys
from pathlib import Path
from datetime import datetime, timedelta
from collections import defaultdict
from typing import Dict, List, Set, Tuple, Optional
import argparse
import json


class DocumentationAuditor:
    """Comprehensive documentation auditor for Uni-Claw project."""

    # Expected documentation structure
    EXPECTED_STRUCTURE = {
        "docs/": {
            "required_files": [
                "INDEX.md",
                "SETUP.md",
                "README.md",
                "PRD_UNIFIED.md",
                "DEVELOPMENT_WORKFLOW.md",
            ],
            "required_dirs": [
                "architecture",
                "architecture/modules",
                "architecture/concepts",
                "archive",
                "testing",
                "validation",
                "prd"
            ]
        },
        "docs/architecture/modules/": {
            "required_files": [
                "ai-design.md",
                "adb-design.md",
                "traversal-design.md",
                "graph-design.md",
                "simulation-design.md",
                "state-design.md",
                "exception-design.md",
                "trace-design.md",
                "analysis-design.md",
                "vision-design.md",
                "safety-design.md",
                "context-design.md",
                "config-design.md",
                "utils-design.md",
                "models-design.md",
                "state-machine-design.md",
                "README.md"
            ]
        },
        "docs/architecture/concepts/": {
            "required_files": [
                "hierarchical-state-machine.md",
                "state-machine-design.md",
                "exception-handling.md",
                "observability.md",
                "graph-model.md"
            ]
        }
    }

    # Naming convention patterns
    NAMING_PATTERNS = {
        "prd": r"^PRD_V\d+(_\d+(\.\d+)?)*-[a-z-_]+\.md$",
        "design": r"^[a-z-]+-design\.md$",
        "guide": r"^[a-z-]+-guide\.md$",
        "concept": r"^[a-z-]+\.md$",
        "readme": r"^README\.md$",
        "test": r"^test-.*\.md$",
        "general": r"^[A-Z_\-0-9a-z]+\.md$"
    }

    # Source modules and their expected docs
    SOURCE_TO_DOC_MAPPING = {
        "src/ai/": ["docs/architecture/modules/ai-design.md", "src/ai/README.md"],
        "src/adb/": ["docs/architecture/modules/adb-design.md"],
        "src/traversal/": ["docs/architecture/modules/traversal-design.md"],
        "src/graph/": ["docs/architecture/modules/graph-design.md"],
        "src/simulation/": ["docs/architecture/modules/simulation-design.md"],
        "src/state/": ["docs/architecture/modules/state-design.md"],
        "src/exception/": ["docs/architecture/modules/exception-design.md"],
        "src/trace/": ["docs/architecture/modules/trace-design.md", "src/trace/README.md"],
        "src/analysis/": ["docs/architecture/modules/analysis-design.md"],
        "src/vision/": ["docs/architecture/modules/vision-design.md"],
        "src/safety/": ["docs/architecture/modules/safety-design.md"],
        "src/context/": ["docs/architecture/modules/context-design.md"],
        "src/config/": ["docs/architecture/modules/config-design.md"],
        "src/models/": ["docs/architecture/modules/models-design.md"],
        "src/state_machine/": ["docs/architecture/modules/state-machine-design.md"]
    }

    def __init__(self, project_root: Path):
        """Initialize the auditor with project root path."""
        self.project_root = project_root
        self.docs_dir = project_root / "docs"
        self.src_dir = project_root / "src"
        self.reports_dir = self.docs_dir / "reports"
        self.audit_date = datetime.now().strftime("%Y-%m-%d")

        # Audit results
        self.structure_issues = []
        self.freshness_issues = []
        self.coverage_gaps = []
        self.naming_violations = []
        self.statistics = defaultdict(int)

    def run_full_audit(self) -> Dict:
        """Execute complete documentation audit."""
        print("=" * 70)
        print("Uni-Claw Documentation Audit")
        print("=" * 70)
        print(f"Date: {self.audit_date}")
        print(f"Project: {self.project_root}")
        print()

        results = {
            "timestamp": datetime.now().isoformat(),
            "structure_check": self._check_structure(),
            "freshness_check": self._check_freshness(),
            "coverage_check": self._check_coverage(),
            "naming_check": self._check_naming_conventions(),
            "statistics": dict(self.statistics)
        }

        return results

    def _check_structure(self) -> Dict:
        """Verify documentation structure integrity."""
        print("[1/4] Checking documentation structure...")
        results = {
            "status": "PASS",
            "issues": [],
            "missing_files": [],
            "missing_dirs": [],
            "orphan_files": []
        }

        for base_path, requirements in self.EXPECTED_STRUCTURE.items():
            full_path = self.project_root / base_path

            # Check required directories
            for req_dir in requirements.get("required_dirs", []):
                dir_path = full_path / req_dir
                if not dir_path.exists():
                    results["missing_dirs"].append(str(dir_path))
                    results["issues"].append(f"Missing directory: {dir_path}")
                    self.structure_issues.append(f"MISSING_DIR: {dir_path}")

            # Check required files
            for req_file in requirements.get("required_files", []):
                file_path = full_path / req_file
                if not file_path.exists():
                    results["missing_files"].append(str(file_path))
                    results["issues"].append(f"Missing file: {file_path}")
                    self.structure_issues.append(f"MISSING_FILE: {file_path}")

        # Check for orphan files (files not in expected structure)
        self._check_orphan_files(results)

        if results["issues"]:
            results["status"] = "FAIL"

        self.statistics["structure_issues"] = len(results["issues"])
        print(f"  Found {len(results['issues'])} structure issues")
        return results

    def _check_orphan_files(self, results: Dict):
        """Check for files that don't follow expected structure."""
        expected_files = set()
        for base_path, requirements in self.EXPECTED_STRUCTURE.items():
            full_path = self.project_root / base_path
            for req_file in requirements.get("required_files", []):
                expected_files.add(str(full_path / req_file))

        # Find all .md files in docs
        for md_file in self.docs_dir.rglob("*.md"):
            file_str = str(md_file)
            if file_str not in expected_files and not self._is_valid_orphan(md_file):
                results["orphan_files"].append(file_str)
                self.structure_issues.append(f"ORPHAN_FILE: {md_file}")

    def _is_valid_orphan(self, file_path: Path) -> bool:
        """Check if file is a valid orphan (e.g., archive, temporary, testing, validation)."""
        path_str = str(file_path)
        valid_locations = ["archive", "temp", "draft", "superpowers", "testing", "validation", "prd"]
        return any(x in path_str for x in valid_locations)

    def _check_freshness(self) -> Dict:
        """Check documentation freshness and last modified dates."""
        print("[2/4] Checking documentation freshness...")
        results = {
            "status": "PASS",
            "stale_docs": [],
            "recent_updates": [],
            "warning_docs": []
        }

        now = datetime.now()
        stale_threshold = timedelta(days=180)  # 6 months
        warning_threshold = timedelta(days=90)  # 3 months

        # Check all documentation files
        for md_file in self.docs_dir.rglob("*.md"):
            # Skip archive and temp files
            if "archive" in str(md_file) or "temp" in str(md_file):
                continue

            try:
                mtime = datetime.fromtimestamp(md_file.stat().st_mtime)
                age = now - mtime

                if age > stale_threshold:
                    results["stale_docs"].append({
                        "file": str(md_file.relative_to(self.project_root)),
                        "last_modified": mtime.strftime("%Y-%m-%d"),
                        "days_old": age.days
                    })
                    self.freshness_issues.append(f"STALE: {md_file} ({age.days} days)")
                elif age > warning_threshold:
                    results["warning_docs"].append({
                        "file": str(md_file.relative_to(self.project_root)),
                        "last_modified": mtime.strftime("%Y-%m-%d"),
                        "days_old": age.days
                    })
                else:
                    results["recent_updates"].append({
                        "file": str(md_file.relative_to(self.project_root)),
                        "last_modified": mtime.strftime("%Y-%m-%d"),
                        "days_old": age.days
                    })
            except Exception as e:
                results["stale_docs"].append({
                    "file": str(md_file.relative_to(self.project_root)),
                    "error": str(e)
                })

        if results["stale_docs"]:
            results["status"] = "WARNING"

        self.statistics["stale_docs"] = len(results["stale_docs"])
        self.statistics["warning_docs"] = len(results["warning_docs"])
        self.statistics["recent_updates"] = len(results["recent_updates"])
        print(f"  Found {len(results['stale_docs'])} stale, {len(results['warning_docs'])} warning docs")
        return results

    def _check_coverage(self) -> Dict:
        """Check code-documentation coverage."""
        print("[3/4] Checking code-document coverage...")
        results = {
            "status": "PASS",
            "missing_module_docs": [],
            "coverage_ratio": 0.0,
            "covered_modules": [],
            "uncovered_modules": []
        }

        total_modules = len(self.SOURCE_TO_DOC_MAPPING)
        covered_modules = 0

        for module_path, expected_docs in self.SOURCE_TO_DOC_MAPPING.items():
            full_module_path = self.project_root / module_path

            if not full_module_path.exists():
                continue

            module_covered = True
            missing_docs = []

            for doc_path in expected_docs:
                full_doc_path = self.project_root / doc_path
                if not full_doc_path.exists():
                    missing_docs.append(doc_path)
                    module_covered = False
                    self.coverage_gaps.append(f"MISSING_DOC: {doc_path} (for {module_path})")

            if module_covered:
                covered_modules += 1
                results["covered_modules"].append(module_path)
            else:
                results["uncovered_modules"].append({
                    "module": module_path,
                    "missing_docs": missing_docs
                })

        results["coverage_ratio"] = covered_modules / total_modules if total_modules > 0 else 0.0

        if results["uncovered_modules"]:
            results["status"] = "WARNING"

        self.statistics["coverage_ratio"] = f"{results['coverage_ratio']:.1%}"
        self.statistics["covered_modules"] = covered_modules
        self.statistics["total_modules"] = total_modules
        print(f"  Coverage: {results['coverage_ratio']:.1%} ({covered_modules}/{total_modules} modules)")
        return results

    def _check_naming_conventions(self) -> Dict:
        """Check file naming convention compliance."""
        print("[4/4] Checking naming conventions...")
        results = {
            "status": "PASS",
            "violations": [],
            "compliant_files": [],
            "warnings": []
        }

        # Check PRD naming (allow for variations in version numbers)
        prd_files = list((self.docs_dir / "prd").glob("PRD_*.md")) if (self.docs_dir / "prd").exists() else []
        archive_prd = list((self.docs_dir / "archive" / "prd").glob("PRD_*.md")) if (self.docs_dir / "archive" / "prd").exists() else []

        for prd_file in prd_files + archive_prd:
            # More lenient PRD pattern check
            if not (re.match(r"^PRD_V\d+(_\d+(\.\d+)?)*", prd_file.name) and
                    prd_file.name.endswith(".md") and "-" in prd_file.name):
                results["warnings"].append({
                    "file": str(prd_file.relative_to(self.project_root)),
                    "issue": "PRD naming could be improved",
                    "expected_pattern": self.NAMING_PATTERNS["prd"]
                })
            else:
                results["compliant_files"].append(str(prd_file.relative_to(self.project_root)))

        # Check design document naming (allow state-machine and test- patterns)
        design_dir = self.docs_dir / "architecture" / "modules"
        if design_dir.exists():
            for design_file in design_dir.glob("*design*.md"):
                # Allow state-machine-design and test-*-design patterns
                if (not re.match(self.NAMING_PATTERNS["design"], design_file.name) and
                    design_file.name != "state-machine-design.md" and
                    not design_file.name.startswith("test-")):
                    results["warnings"].append({
                        "file": str(design_file.relative_to(self.project_root)),
                        "issue": "Design document naming could be improved",
                        "expected_pattern": self.NAMING_PATTERNS["design"]
                    })
                else:
                    results["compliant_files"].append(str(design_file.relative_to(self.project_root)))

        # Check for uppercase in non-README files (should be lowercase with hyphens)
        for md_file in self.docs_dir.rglob("*.md"):
            if "archive" in str(md_file):
                continue

            # Skip README.md and specific patterns
            if md_file.name == "README.md" or md_file.name.startswith("PRD_"):
                continue

            # Check if filename contains uppercase (excluding allowed cases)
            if any(c.isupper() for c in md_file.stem) and not md_file.stem.isupper():
                results["warnings"].append({
                    "file": str(md_file.relative_to(self.project_root)),
                    "issue": "Potential uppercase in filename (use lowercase-hyphen)",
                    "suggested": md_file.stem.lower().replace("_", "-")
                })

        if results["violations"]:
            results["status"] = "FAIL"
        elif results["warnings"]:
            results["status"] = "WARNING"

        self.statistics["naming_violations"] = len(results["violations"])
        self.statistics["naming_warnings"] = len(results["warnings"])
        print(f"  Found {len(results['violations'])} violations, {len(results['warnings'])} warnings")
        return results

    def generate_report(self, results: Dict, output_path: Optional[Path] = None) -> str:
        """Generate comprehensive audit report."""
        if output_path is None:
            output_path = self.reports_dir / f"doc_audit_{self.audit_date}.md"

        # Ensure reports directory exists
        self.reports_dir.mkdir(parents=True, exist_ok=True)

        # Build report
        report_lines = [
            "# Documentation Audit Report",
            "",
            f"**Generated**: {self.audit_date}",
            f"**Project**: Uni-Claw",
            f"**Version**: V6.3",
            "",
            "---",
            "",
            "## Executive Summary",
            "",
        ]

        # Calculate overall status
        all_statuses = [
            results["structure_check"]["status"],
            results["freshness_check"]["status"],
            results["coverage_check"]["status"],
            results["naming_check"]["status"]
        ]

        if all(s == "PASS" for s in all_statuses):
            overall_status = "PASS"
        elif any(s == "FAIL" for s in all_statuses):
            overall_status = "FAIL"
        else:
            overall_status = "WARNING"

        report_lines.extend([
            f"**Overall Status**: {overall_status}",
            "",
            "### Quick Statistics",
            "",
            f"- **Structure Issues**: {self.statistics['structure_issues']}",
            f"- **Stale Documents**: {self.statistics['stale_docs']}",
            f"- **Warning Documents**: {self.statistics['warning_docs']}",
            f"- **Recent Updates**: {self.statistics['recent_updates']}",
            f"- **Module Coverage**: {self.statistics['coverage_ratio']}",
            f"- **Naming Violations**: {self.statistics['naming_violations']}",
            f"- **Naming Warnings**: {self.statistics['naming_warnings']}",
            "",
            "---",
            "",
            "## 1. Structure Check",
            "",
            f"**Status**: {results['structure_check']['status']}",
            ""
        ])

        # Structure details
        if results["structure_check"]["missing_files"]:
            report_lines.append("### Missing Files")
            for file in results["structure_check"]["missing_files"]:
                report_lines.append(f"- {file}")
            report_lines.append("")

        if results["structure_check"]["missing_dirs"]:
            report_lines.append("### Missing Directories")
            for dir in results["structure_check"]["missing_dirs"]:
                report_lines.append(f"- {dir}")
            report_lines.append("")

        if results["structure_check"]["orphan_files"]:
            report_lines.append("### Orphan Files (potentially misplaced)")
            for file in results["structure_check"]["orphan_files"]:
                report_lines.append(f"- {file}")
            report_lines.append("")

        # Freshness section
        report_lines.extend([
            "---",
            "",
            "## 2. Freshness Check",
            "",
            f"**Status**: {results['freshness_check']['status']}",
            ""
        ])

        if results["freshness_check"]["stale_docs"]:
            report_lines.append("### Stale Documents (>180 days)")
            for doc in results["freshness_check"]["stale_docs"]:
                report_lines.append(f"- **{doc['file']}** (last modified: {doc.get('last_modified', 'unknown')}, {doc.get('days_old', '?')} days)")
            report_lines.append("")

        if results["freshness_check"]["warning_docs"]:
            report_lines.append("### Warning Documents (>90 days)")
            for doc in results["freshness_check"]["warning_docs"]:
                report_lines.append(f"- **{doc['file']}** (last modified: {doc['last_modified']}, {doc['days_old']} days)")
            report_lines.append("")

        if results["freshness_check"]["recent_updates"]:
            report_lines.extend([
                "### Recently Updated (<90 days)",
                f"Count: {len(results['freshness_check']['recent_updates'])}",
                ""
            ])

        # Coverage section
        report_lines.extend([
            "---",
            "",
            "## 3. Code-Document Coverage",
            "",
            f"**Status**: {results['coverage_check']['status']}",
            f"**Coverage Ratio**: {results['coverage_check']['coverage_ratio']:.1%}",
            ""
        ])

        if results["coverage_check"]["uncovered_modules"]:
            report_lines.append("### Modules with Missing Documentation")
            for module in results["coverage_check"]["uncovered_modules"]:
                report_lines.append(f"- **{module['module']}**")
                for doc in module["missing_docs"]:
                    report_lines.append(f"  - Missing: {doc}")
            report_lines.append("")

        # Naming section
        report_lines.extend([
            "---",
            "",
            "## 4. Naming Convention Compliance",
            "",
            f"**Status**: {results['naming_check']['status']}",
            ""
        ])

        if results["naming_check"]["violations"]:
            report_lines.append("### Naming Violations")
            for violation in results["naming_check"]["violations"]:
                report_lines.append(f"- **{violation['file']}**")
                report_lines.append(f"  - Issue: {violation['issue']}")
                report_lines.append(f"  - Expected: {violation.get('expected_pattern', 'N/A')}")
            report_lines.append("")

        if results["naming_check"]["warnings"]:
            report_lines.append("### Naming Warnings")
            for warning in results["naming_check"]["warnings"]:
                report_lines.append(f"- **{warning['file']}**")
                report_lines.append(f"  - {warning['issue']}")
                if 'expected_pattern' in warning:
                    report_lines.append(f"  - Expected: {warning['expected_pattern']}")
                if 'suggested' in warning:
                    report_lines.append(f"  - Suggested: {warning['suggested']}")
            report_lines.append("")

        # Recommendations
        report_lines.extend([
            "---",
            "",
            "## Recommendations",
            "",
        ])

        recommendations = self._generate_recommendations(results)
        for i, rec in enumerate(recommendations, 1):
            report_lines.append(f"{i}. {rec}")

        report_lines.extend([
            "",
            "---",
            "",
            f"*End of Report - Generated by doc_audit.py*"
        ])

        report_content = "\n".join(report_lines)

        # Write report
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write(report_content)

        print(f"\nReport generated: {output_path}")
        return str(output_path)

    def _generate_recommendations(self, results: Dict) -> List[str]:
        """Generate actionable recommendations based on audit results."""
        recommendations = []

        # Structure recommendations
        if results["structure_check"]["missing_files"]:
            recommendations.append(
                f"**Create missing documentation files**: {len(results['structure_check']['missing_files'])} "
                "files are missing from the expected structure."
            )

        # Freshness recommendations
        if results["freshness_check"]["stale_docs"]:
            recommendations.append(
                f"**Update stale documentation**: {len(results['freshness_check']['stale_docs'])} "
                "documents haven't been updated in over 6 months."
            )

        # Coverage recommendations
        if results["coverage_check"]["uncovered_modules"]:
            recommendations.append(
                f"**Improve module documentation**: {len(results['coverage_check']['uncovered_modules'])} "
                "source modules lack complete documentation."
            )

        # Naming recommendations
        if results["naming_check"]["violations"]:
            recommendations.append(
                f"**Fix naming violations**: {len(results['naming_check']['violations'])} "
                "files don't follow naming conventions."
            )

        if not recommendations:
            recommendations.append("Documentation is in good health. Continue maintaining current standards.")

        return recommendations


def main():
    """Main entry point for the documentation audit script."""
    parser = argparse.ArgumentParser(
        description="Comprehensive documentation audit for Uni-Claw project"
    )
    parser.add_argument(
        "--output",
        "-o",
        type=str,
        help="Output file path for the report (default: docs/reports/doc_audit_YYYY-MM-DD.md)"
    )
    parser.add_argument(
        "--project-root",
        "-p",
        type=str,
        default=".",
        help="Project root directory (default: current directory)"
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Output results in JSON format instead of markdown"
    )

    args = parser.parse_args()

    # Determine project root
    project_root = Path(args.project_root).resolve()

    # Create auditor and run audit
    auditor = DocumentationAuditor(project_root)
    results = auditor.run_full_audit()

    # Output results
    if args.json:
        output_path = args.output or f"docs/reports/doc_audit_{auditor.audit_date}.json"
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(results, f, indent=2)
        print(f"\nJSON report generated: {output_path}")
    else:
        output_path = Path(args.output) if args.output else None
        report_path = auditor.generate_report(results, output_path)

    # Return exit code based on overall status
    all_statuses = [
        results["structure_check"]["status"],
        results["freshness_check"]["status"],
        results["coverage_check"]["status"],
        results["naming_check"]["status"]
    ]

    if any(s == "FAIL" for s in all_statuses):
        return 1
    elif any(s == "WARNING" for s in all_statuses):
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
