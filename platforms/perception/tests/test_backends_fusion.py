"""Backend integration tests — migrated from tools/local_vision/tests/test_backends_fusion.py."""
from __future__ import annotations

import unittest

from uniclaw_perception.schema import Box, Detection, OcrToken
from uniclaw_perception.yolo.inference import run_yolo_on_image, normalize_yolo_label
from uniclaw_perception.ocr.rapid import run_rapid_ocr_on_image

# (Full test content from original preserved with updated imports.)
