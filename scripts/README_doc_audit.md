# Documentation Audit Script

## Overview

The `doc_audit.py` script performs a comprehensive audit of the Uni-Claw documentation system, checking structure integrity, content freshness, code-document coverage, and naming convention compliance.

## Features

The audit script checks four main areas:

1. **Structure Verification**
   - Validates expected documentation structure
   - Identifies missing files and directories
   - Detects orphan files that may be misplaced

2. **Freshness Analysis**
   - Checks last modified dates of documentation
   - Identifies stale documents (>180 days)
   - Flags documents needing attention (>90 days)
   - Tracks recently updated content

3. **Code-Document Coverage**
   - Maps source modules to their documentation
   - Identifies modules with incomplete documentation
   - Calculates coverage ratio across all modules

4. **Naming Convention Compliance**
   - Validates PRD naming patterns (PRD_V{major}_{minor}-{description}.md)
   - Checks design document naming (module-design.md)
   - Flags potential naming issues and warnings

## Usage

### Basic Usage

```bash
python scripts/doc_audit.py
```

This generates a report in `docs/reports/doc_audit_YYYY-MM-DD.md`.

### Custom Output

```bash
# Specify custom output location
python scripts/doc_audit.py --output docs/my_audit_report.md

# Output to JSON format
python scripts/doc_audit.py --json --output docs/audit_data.json
```

### Advanced Options

```bash
# Run from different directory
python scripts/doc_audit.py --project-root /path/to/project

# Show help
python scripts/doc_audit.py --help
```

## Exit Codes

- `0` - All checks passed (documentation is healthy)
- `1` - Critical issues found (failures)
- `2` - Warnings only (no failures, but improvements needed)

## Report Structure

The generated report includes:

1. **Executive Summary** - Overall status and quick statistics
2. **Structure Check** - Missing files, directories, and orphan detection
3. **Freshness Check** - Stale documents and recent updates
4. **Coverage Analysis** - Module-to-documentation mapping
5. **Naming Compliance** - Convention violations and warnings
6. **Recommendations** - Actionable improvement suggestions

## Integration with CI/CD

The script can be integrated into CI/CD pipelines:

```yaml
# Example GitHub Actions
- name: Documentation Audit
  run: python scripts/doc_audit.py
  
- name: Upload Audit Report
  if: always()
  uses: actions/upload-artifact@v3
  with:
    name: doc-audit-report
    path: docs/reports/doc_audit_*.md
```

## Configuration

The script's behavior can be customized by modifying the following constants in `doc_audit.py`:

- `EXPECTED_STRUCTURE` - Define required documentation structure
- `NAMING_PATTERNS` - Set naming convention regex patterns
- `SOURCE_TO_DOC_MAPPING` - Map source modules to documentation files
- Freshness thresholds (180 days for stale, 90 days for warning)

## Troubleshooting

### "Missing directory" errors
- Ensure the project structure follows the expected layout
- Check that required directories exist in `docs/`

### "Stale documents" warnings
- Review and update documentation that hasn't been touched in 6+ months
- Consider archiving outdated documents instead of deleting

### "Naming violations" 
- Follow PRD naming: `PRD_V{major}_{minor}-{description}.md`
- Use lowercase with hyphens for general docs: `my-guide.md`
- Design docs should follow: `module-design.md`

## Examples

### Run audit and view results

```bash
$ python scripts/doc_audit.py
======================================================================
Uni-Claw Documentation Audit
======================================================================
Date: 2026-06-08
Project: D:\space-x\uni_claw

[1/4] Checking documentation structure...
  Found 1 structure issues
[2/4] Checking documentation freshness...
  Found 0 stale, 2 warning docs
[3/4] Checking code-document coverage...
  Coverage: 95.0% (19/20 modules)
[4/4] Checking naming conventions...
  Found 0 violations, 3 warnings

Report generated: D:\space-x\uni_claw\docs\reports\doc_audit_2026-06-08.md
```

### JSON output for automation

```bash
$ python scripts/doc_audit.py --json
{
  "timestamp": "2026-06-08T10:30:00",
  "structure_check": {
    "status": "PASS",
    "issues": []
  },
  "statistics": {
    "structure_issues": 0,
    "stale_docs": 0,
    "coverage_ratio": "100.0%"
  }
}
```

## Contributing

When adding new documentation:

1. Follow the naming conventions
2. Update the expected structure in the script
3. Run the audit to verify compliance
4. Add module documentation mappings if applicable

## Related Scripts

- `verify/verify_docs.py` - Basic documentation verification
- `verify/doc_freshness.py` - Freshness checking only
- `verify/verify_refactor.py` - Code verification

## License

Part of the Uni-Claw project. See project LICENSE for details.
