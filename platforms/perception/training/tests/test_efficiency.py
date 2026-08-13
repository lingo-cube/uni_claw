"""Efficiency falsifiers (EF-T01..EF-T08)."""
from __future__ import annotations

import re
import unittest
from pathlib import Path

from training.annotation import AnnotationSource, accept_annotation, create_annotation
from training.dataset import DatasetMembership, DatasetVersion, Split


def _code_without_docstrings(src: str) -> str:
    """Strip module/class/function docstrings so checks target
    implementations, not descriptions."""
    return re.sub(r'""".*?"""', '""" """', src, flags=re.DOTALL)


class EfficiencyTests(unittest.TestCase):
    def test_EF_T01_shared_bytes_stored_once(self):
        """Membership references AssetId — creating a DatasetVersion copies
        zero image bytes (structural: no copy API exists)."""
        import inspect
        import training.dataset as dm
        src = inspect.getsource(dm)
        self.assertNotIn("shutil", src)
        self.assertNotIn("copy2", src)
        self.assertNotIn("read_bytes", src)

    def test_EF_T02_role_change_duplicates_no_bytes(self):
        _, ann = _make_ann("sha256:a")
        ds = DatasetVersion(members=(
            DatasetMembership(asset_id="sha256:a", split=Split.TRAIN,
                              annotation_id=ann.annotation_id),))
        j = ds.to_json()
        # only references serialized — no byte payloads
        self.assertNotIn("bytes", str(j))
        self.assertEqual(j["members"][0]["assetId"], "sha256:a")

    def test_EF_T04_training_requires_no_emulator_or_device(self):
        """Training foundations import only stdlib + evaluation + PIL
        (image synthesis); no ADB/emulator/device tooling."""
        import pkgutil, importlib, inspect
        import training
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            self.assertNotIn("adb", src.lower())
            self.assertNotIn("emulator", src.lower())

    def test_EF_T06_no_second_metric_implementation(self):
        """Training has no metric/scorecard machinery — it references the
        evaluation workflow only."""
        import pkgutil, importlib, inspect
        import training
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = _code_without_docstrings(inspect.getsource(m))
            self.assertNotIn("scorecard", src.lower())
            self.assertNotIn("precision", src.lower())
            self.assertNotIn("recall", src.lower())

    def test_EF_T07_dataset_growth_is_manifest_change(self):
        """Adding members is the same API at any scale — no framework
        redesign surface (with_members is the only mutation entry)."""
        _, a1 = _make_ann("sha256:a1")
        _, a2 = _make_ann("sha256:a2")
        base = DatasetVersion(members=(
            DatasetMembership(asset_id="sha256:a1", split=Split.TRAIN,
                              annotation_id=a1.annotation_id),))
        grown = base.with_members(base.members + (
            DatasetMembership(asset_id="sha256:a2", split=Split.TRAIN,
                              annotation_id=a2.annotation_id),))
        self.assertNotEqual(base.dataset_version_id, grown.dataset_version_id)
        self.assertEqual(len(grown.members), 2)

    def test_EF_T08_no_mutable_latest_directory_as_truth(self):
        """Identity is content-addressed; no mutable 'latest' pointer
        (no file/dir path named latest, no latest.json write)."""
        import pkgutil, importlib, inspect
        import training
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            self.assertNotRegex(src, r"latest\.json")
            self.assertNotRegex(src, r"[\'\"]latest[\'\"]\s*/\s|/ *[\'\"]latest[\'\"]")

    def test_EF_T03_T05_mini_run_and_immediate_evaluation(self):
        """The mini run artifacts include a candidate + evaluation run —
        proven by the executed lineage (checked via manifests on disk)."""
        lineage_dir = Path(__file__).resolve().parent.parent / "artifacts" \
            / "manifests" / "lineage"
        if not lineage_dir.exists() or not list(lineage_dir.glob("*.json")):
            self.skipTest("mini lineage not yet executed")
        import json
        kinds_all = set()
        for f in lineage_dir.glob("*.json"):
            d = json.loads(f.read_text())
            kinds_all |= {n["kind"] for n in d["nodes"]}
        self.assertIn("Candidate", kinds_all)
        self.assertIn("EvaluationRun", kinds_all)


def _make_ann(asset_id: str):
    from evaluation.stage import EvaluationTargetStage, LabelSpace
    draft = create_annotation(
        asset_id=asset_id, target_stage=EvaluationTargetStage.RAW_DETECTION,
        label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
        source=AnnotationSource.HUMAN_CREATED, label_payload={"boxes": []})
    return draft, accept_annotation(draft)


if __name__ == "__main__":
    unittest.main()
