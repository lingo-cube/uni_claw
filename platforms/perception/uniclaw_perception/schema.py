from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Any


@dataclass(frozen=True)
class Box:
    x1: float
    y1: float
    x2: float
    y2: float

    def normalized(self, width: int, height: int) -> dict[str, float]:
        if width <= 0 or height <= 0:
            raise ValueError("INVALID_GEOMETRY: source dimensions must be positive")
        if not all(math.isfinite(v) for v in (self.x1, self.y1, self.x2, self.y2)):
            raise ValueError("INVALID_GEOMETRY: coordinates must be finite")
        return {
            "x1": round(self.x1 / width, 6),
            "y1": round(self.y1 / height, 6),
            "x2": round(self.x2 / width, 6),
            "y2": round(self.y2 / height, 6),
        }

    def center(self) -> tuple[float, float]:
        return ((self.x1 + self.x2) / 2.0, (self.y1 + self.y2) / 2.0)

    def area(self) -> float:
        return max(0.0, self.x2 - self.x1) * max(0.0, self.y2 - self.y1)

    def intersects(self, other: "Box") -> bool:
        return not (
            self.x2 <= other.x1
            or other.x2 <= self.x1
            or self.y2 <= other.y1
            or other.y2 <= self.y1
        )

    def intersection_area(self, other: "Box") -> float:
        if not self.intersects(other):
            return 0.0
        x1 = max(self.x1, other.x1)
        y1 = max(self.y1, other.y1)
        x2 = min(self.x2, other.x2)
        y2 = min(self.y2, other.y2)
        return max(0.0, x2 - x1) * max(0.0, y2 - y1)

    def contains_center(self, other: "Box") -> bool:
        cx, cy = other.center()
        return self.x1 <= cx <= self.x2 and self.y1 <= cy <= self.y2

    @staticmethod
    def from_list(value: list[int | float]) -> "Box":
        if len(value) != 4:
            raise ValueError(f"box must have 4 numbers, got {value!r}")
        x1, y1, x2, y2 = [float(v) for v in value]
        return Box(x1, y1, x2, y2)


@dataclass(frozen=True)
class Detection:
    id: str
    label: str
    confidence: float
    box: Box
    raw_label: str | None = None
    raw_class_id: int | None = None
    # raw_label: raw model class name BEFORE YOLO_LABEL_ALIASES normalization
    # (additive internal fields — to_json unchanged; evidence schema frozen).

    def to_json(self, width: int, height: int) -> dict[str, Any]:
        cx, cy = self.box.center()
        return {
            "id": self.id,
            "label": self.label,
            "confidence": round(self.confidence, 6),
            "bounds": self.box.normalized(width, height),
            "boundsPx": [round(self.box.x1), round(self.box.y1), round(self.box.x2), round(self.box.y2)],
            "center": {"x": round(cx / width, 6), "y": round(cy / height, 6)},
            "centerPx": [round(cx), round(cy)],
        }


@dataclass(frozen=True)
class OcrToken:
    id: str
    text: str
    confidence: float
    box: Box

    def to_json(self, width: int, height: int) -> dict[str, Any]:
        cx, cy = self.box.center()
        return {
            "id": self.id,
            "text": self.text,
            "confidence": round(self.confidence, 6),
            "bounds": self.box.normalized(width, height),
            "boundsPx": [round(self.box.x1), round(self.box.y1), round(self.box.x2), round(self.box.y2)],
            "center": {"x": round(cx / width, 6), "y": round(cy / height, 6)},
            "centerPx": [round(cx), round(cy)],
        }
