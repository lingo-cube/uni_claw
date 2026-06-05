#!/usr/bin/env python3
"""
Migration script to standardize validation document names.

This script renames existing validation documents to follow the
validation-documentation skill naming conventions.
"""

import os
import shutil
from pathlib import Path
from datetime import datetime

# Project root
project_root = Path(__file__).parent.parent
validation_dir = project_root / "docs" / "validation"

# File mappings based on validation-documentation skill standards
FILE_MAPPINGS = {
    # Current name → Standard name
    "progress_report_2026-06-04.md": "progress_report.md",
    "V6_UNIMPLEMENTED_FEATURES.md": "planned_features.md",
    "simulation_infrastructure_analysis.md": "system_infrastructure_analysis.md",
    "fixture_dataset_quality.md": "test_data_quality.md",
}

def log(message):
    """Print log message with timestamp."""
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{timestamp}] {message}")

def dry_run_rename():
    """Show what would be renamed without actually doing it."""
    log("DRY RUN MODE - No actual changes will be made")
    log("=" * 60)

    if not validation_dir.exists():
        log(f"[ERROR] Validation directory not found: {validation_dir}")
        return 1

    log(f"Scanning: {validation_dir}")
    log("")

    rename_count = 0
    for old_name, new_name in FILE_MAPPINGS.items():
        old_path = validation_dir / old_name
        new_path = validation_dir / new_name

        if old_path.exists():
            log(f"[OK] {old_name} -> {new_name}")
            rename_count += 1
        else:
            log(f"[WARN] {old_name} (not found, may already be renamed)")

    log("")
    log(f"Total files to rename: {rename_count}")
    log("=" * 60)
    return 0

def perform_rename():
    """Actually perform the renaming."""
    log("VALIDATION DOCUMENT STANDARDIZATION")
    log("=" * 60)

    if not validation_dir.exists():
        log(f"[ERROR] Validation directory not found: {validation_dir}")
        return 1

    # Create backup note
    backup_note = validation_dir / "_RENAMING_BACKUP_NOTE.md"
    with open(backup_note, "w") as f:
        f.write(f"# Validation Document Renaming Backup\n")
        f.write(f"# Date: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write(f"# \n")
        f.write(f"# Files were renamed to follow validation-documentation skill standards:\n")
        f.write(f"# \n")
        for old_name, new_name in FILE_MAPPINGS.items():
            f.write(f"# - {old_name} -> {new_name}\n")

    log(f"Created backup note: {backup_note}")

    # Perform renaming
    rename_count = 0
    for old_name, new_name in FILE_MAPPINGS.items():
        old_path = validation_dir / old_name
        new_path = validation_dir / new_name

        if old_path.exists():
            try:
                shutil.move(str(old_path), str(new_path))
                log(f"[OK] Renamed: {old_name} -> {new_name}")
                rename_count += 1
            except Exception as e:
                log(f"[ERROR] Failed to rename {old_name}: {e}")
        else:
            log(f"[WARN] {old_name} (not found, may already be renamed)")

    log("")
    log(f"Summary: {rename_count} files renamed")
    log("=" * 60)

    if rename_count > 0:
        log("\nSuggested git command:")
        log('git add docs/validation/')
        log('git commit -m "standardize validation document naming to generic overwrite mode"')
        log('  - Applied validation-documentation skill standards')
        log('  - Renamed files to use generic, version-independent names')
        log('  - Enables consistent tracking across sessions and projects')

    return 0

def verify_standards():
    """Verify current files follow naming standards."""
    log("VERIFYING VALIDATION DOCUMENT NAMING STANDARDS")
    log("=" * 60)

    if not validation_dir.exists():
        log(f"[ERROR] Validation directory not found: {validation_dir}")
        return 1

    log(f"Scanning: {validation_dir}")
    log("")

    # Check for compliant files
    compliant_files = []
    non_compliant_files = []

    for file_path in validation_dir.glob("*.md"):
        if file_path.name.startswith("_"):
            continue  # Skip metadata files

        # Check if follows naming standards
        is_compliant = True
        violations = []

        # Check for version-specific patterns
        if "V6" in file_path.name or "V5" in file_path.name:
            violations.append("contains version prefix")
            is_compliant = False

        # Check for date patterns
        if "2026-" in file_path.name or "2025-" in file_path.name:
            violations.append("contains date")
            is_compliant = False

        # Check for numbered patterns
        if "_v2" in file_path.name.lower() or "_v1" in file_path.name.lower():
            violations.append("contains version number")
            is_compliant = False

        if is_compliant:
            compliant_files.append(file_path.name)
        else:
            non_compliant_files.append((file_path.name, violations))

    log(f"Compliant files ({len(compliant_files)}):")
    for fname in compliant_files:
        log(f"   [OK] {fname}")

    if non_compliant_files:
        log("")
        log(f"Non-compliant files ({len(non_compliant_files)}):")
        for fname, violations in non_compliant_files:
            log(f"   [ISSUE] {fname}")
            for violation in violations:
                log(f"        - {violation}")

    log("")
    log("=" * 60)
    return 0 if not non_compliant_files else 1

def main():
    """Main entry point."""
    import sys

    print("Validation Document Standardization Tool")
    print("=====================================")
    print()
    print("This tool standardizes validation document names to follow")
    print("the validation-documentation skill naming conventions.")
    print()

    if len(sys.argv) > 1:
        command = sys.argv[1]

        if command == "dry-run":
            return dry_run_rename()
        elif command == "verify":
            return verify_standards()
        elif command == "rename":
            return perform_rename()
        else:
            print(f"Unknown command: {command}")
            print("Available commands: dry-run, verify, rename")
            return 1
    else:
        print("Usage: python standardize_validation_docs.py <command>")
        print()
        print("Commands:")
        print("  dry-run  - Show what would be renamed without doing it")
        print("  verify  - Check if current files follow standards")
        print("  rename  - Actually perform the renaming")
        print()
        print("Example:")
        print("  # First check what would change")
        print("  python standardize_validation_docs.py dry-run")
        print()
        print("  # Then verify current state")
        print("  python standardize_validation_docs.py verify")
        print()
        print("  # Finally perform the renaming")
        print("  python standardize_validation_docs.py rename")
        return 0

if __name__ == "__main__":
    import sys
    sys.exit(main())