"""Image preprocessing: crop + resize pipeline.

Preserves exact behavior from server.py _preprocess().
"""
from __future__ import annotations

from PIL import Image


def preprocess(
    image: Image.Image,
    max_width: int,
    crop_top_ratio: float,
    crop_bottom_ratio: float,
) -> tuple[Image.Image, float, float, int]:
    """Crop top/bottom + resize to max width. PIL zero-decode path.

    Returns:
        (preprocessed_image, scale, top_px, orig_h)
        scale = orig_w / preproc_w (both axes, >1 = downscaled)
        top_px = pixels cropped from top in original coordinates
        orig_h = original full-screen height (before any crop)
    """
    orig_w, orig_h = image.size

    # Step 1: crop
    top_px = int(orig_h * crop_top_ratio)
    bottom_px = int(orig_h * crop_bottom_ratio)
    if top_px > 0 or bottom_px > 0:
        crop_h = orig_h - top_px - bottom_px
        if crop_h > 0:
            image = image.crop((0, top_px, orig_w, orig_h - bottom_px))
    else:
        top_px = 0

    # Step 2: resize
    scale = 1.0
    if max_width > 0 and image.width > max_width:
        scale = image.width / max_width
        new_h = int(image.height / scale)
        image = image.resize((max_width, new_h), Image.LANCZOS)

    return image, scale, top_px, orig_h
