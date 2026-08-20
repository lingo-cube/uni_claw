---
name: documentation-migration-safety
description: Safely organize documentation without changing historical facts, decisions, gate conclusions, lifecycle, or source authority.
metadata:
  type: Documentation Operation
  authority: NONE
---

# Documentation Migration Safety

Allowed operations are creating indexes or projections and adding references or metadata.

Do not delete history, modify Decision bodies, modify Gate conclusions, change lifecycle, or move files without a manifest.

- Before: confirm each source exists.
- During: keep facts unchanged.
- After: validate links.

A projection has `Authority: NONE` and never replaces its source.
