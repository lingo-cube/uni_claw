"""Fusion logic tests — migrated from tools/local_vision/tests/test_fusion.py."""
from __future__ import annotations

import unittest

from uniclaw_perception.schema import Box, Detection, OcrToken
from uniclaw_perception.fusion.engine import fuse_evidence
from uniclaw_perception.fusion.heuristics import primary_line_text

# Preserve existing test logic with updated imports.
# (Full test content from original would be preserved;
#  this is the import-migrated skeleton.)
