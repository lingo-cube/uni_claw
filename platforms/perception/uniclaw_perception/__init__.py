"""UniClaw Perception Platform — owned Python Vision service.

Production inference package: YOLO object detection + RapidOCR text recognition
+ spatial fusion → structured perception evidence.

This package is the canonical production perception service. It is launched
by VisionServiceHost (UniClaw.Vision.Host) as a separate OS process.

Version is the pipeline/service version, NOT model version.
"""
__version__ = "1.0.0"
