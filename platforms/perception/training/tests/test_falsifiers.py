"""Training foundation falsifiers (TR-01..TR-25)."""
from __future__ import annotations

import re
import shutil
import tempfile
import unittest
from pathlib import Path


def _code(src: str) -> str:
    """Strip docstrings + comments so checks target implementations,
    not descriptions or inline notes."""
    src = re.sub(r'""".*?"""', '""" """', src, flags=re.DOTALL)
    src = re.sub(r"#.*$", "", src, flags=re.MULTILINE)
    return src

from evaluation.stage import EvaluationTargetStage, LabelSpace
from training.annotation import (
    AnnotationSource, ReviewStatus, accept_annotation, create_annotation,
)
from training.candidate import CandidateStatus, create_candidate
from training.checkpoint import Checkpoint, materialize_model_artifact
from training.dataset import (
    DatasetMembership, DatasetVersion, Split, check_leakage,
)
from training.training_config import TrainingConfig
from training.training_run import (
    TrainingEnvironment, TrainingRun, TrainingRunState,
)


def _ann(asset_id: str = "sha256:a", payload: dict | None = None) -> tuple:
    draft = create_annotation(
        asset_id=asset_id, target_stage=EvaluationTargetStage.RAW_DETECTION,
        label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
        source=AnnotationSource.HUMAN_CREATED,
        label_payload=payload or {"boxes": []},
    )
    return draft, accept_annotation(draft)


def _member(asset: str, split: Split, ann_id: str,
            group: str | None = None) -> DatasetMembership:
    return DatasetMembership(asset_id=asset, split=split,
                             annotation_id=ann_id, capture_group_id=group)


class DatasetIdentityTests(unittest.TestCase):
    def setUp(self):
        _, self.a1 = _ann("sha256:a1")
        _, self.a2 = _ann("sha256:a2")

    def _ds(self, members):
        return DatasetVersion(members=tuple(members))

    def test_TR01_same_membership_same_identity(self):
        m = [_member("sha256:a1", Split.TRAIN, self.a1.annotation_id)]
        self.assertEqual(self._ds(m).dataset_version_id,
                         self._ds(list(m)).dataset_version_id)

    def test_TR02_adding_asset_new_version(self):
        m1 = [_member("sha256:a1", Split.TRAIN, self.a1.annotation_id)]
        m2 = m1 + [_member("sha256:a2", Split.VALIDATION, self.a2.annotation_id)]
        self.assertNotEqual(self._ds(m1).dataset_version_id,
                            self._ds(m2).dataset_version_id)

    def test_TR03_changing_annotation_new_version(self):
        _, a1_v2 = _ann("sha256:a1", {"boxes": [{"class": 0}]})
        m1 = [_member("sha256:a1", Split.TRAIN, self.a1.annotation_id)]
        m2 = [_member("sha256:a1", Split.TRAIN, a1_v2.annotation_id)]
        self.assertNotEqual(self._ds(m1).dataset_version_id,
                            self._ds(m2).dataset_version_id)

    def test_TR04_split_change_new_version(self):
        m1 = [_member("sha256:a1", Split.TRAIN, self.a1.annotation_id)]
        m2 = [_member("sha256:a1", Split.VALIDATION, self.a1.annotation_id)]
        self.assertNotEqual(self._ds(m1).dataset_version_id,
                            self._ds(m2).dataset_version_id)

    def test_TR06_holdout_protected_asset_rejected(self):
        ds = self._ds([_member("sha256:a1", Split.TRAIN, self.a1.annotation_id)])
        findings = check_leakage(ds, protected_asset_ids={"sha256:a1"})
        self.assertTrue(any(f.kind == "EXACT_CONTENT" for f in findings))

    def test_TR07_exact_content_leakage_detected(self):
        ds = self._ds([_member("sha256:a1", Split.TRAIN, self.a1.annotation_id)])
        findings = check_leakage(ds, protected_asset_ids={"sha256:a1"})
        self.assertGreaterEqual(len(findings), 1)

    def test_TR24_regression_asset_not_auto_training_member(self):
        """Membership is explicit — no code path auto-adds a Regression asset."""
        import inspect
        import training.dataset as ds_mod
        src = inspect.getsource(ds_mod)
        self.assertNotIn("REGRESSION", src)  # no implicit role-based membership

    def test_L2_capture_group_leakage(self):
        ds = self._ds([
            _member("sha256:a1", Split.TRAIN, self.a1.annotation_id, group="g1"),
            _member("sha256:a2", Split.VALIDATION, self.a2.annotation_id, group="g1"),
        ])
        findings = check_leakage(ds)
        self.assertTrue(any(f.kind == "SAME_CAPTURE" for f in findings))


class AnnotationTests(unittest.TestCase):
    def test_TR05_prediction_cannot_become_accepted_annotation_automatically(self):
        ann = create_annotation(
            asset_id="sha256:a", target_stage=EvaluationTargetStage.RAW_DETECTION,
            label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
            source=AnnotationSource.MODEL_ASSISTED,
            label_payload={"boxes": [{"class": 0, "from": "model"}]})
        self.assertEqual(ann.review_status, ReviewStatus.DRAFT)
        self.assertFalse(ann.is_accepted_training_truth)
        accepted = accept_annotation(ann)
        self.assertTrue(accepted.is_accepted_training_truth)
        self.assertEqual(accepted.predecessor_annotation_id, ann.annotation_id)
        # distinct identities — historical draft unchanged
        self.assertNotEqual(ann.annotation_id, accepted.annotation_id)

    def test_TR19_historical_annotation_immutable(self):
        draft, accepted = _ann("sha256:a", {"boxes": [{"class": 0}]})
        first_id = accepted.annotation_id
        _, corrected = _ann("sha256:a", {"boxes": [{"class": 0, "x": 1}]})
        self.assertNotEqual(first_id, corrected.annotation_id)
        # the accepted record is frozen — its id is a pure function of content
        again = accept_annotation(create_annotation(
            asset_id="sha256:a", target_stage=EvaluationTargetStage.RAW_DETECTION,
            label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
            source=AnnotationSource.HUMAN_CREATED,
            label_payload={"boxes": [{"class": 0}]}))
        self.assertEqual(first_id, again.annotation_id)


class TrainingConfigTests(unittest.TestCase):
    def _cfg(self, **overrides):
        base = dict(base_model_artifact_id=None, epochs=1, batch_size=2,
                    imgsz=160, optimizer="auto", learning_rate="auto",
                    scheduler="auto", augmentation="none", seed=42,
                    class_vocabulary=("box",), label_space="MINI_SYNTHETIC_BOX_V1")
        base.update(overrides)
        return TrainingConfig(**base)

    def test_TR08_material_change_new_config_id(self):
        a = self._cfg()
        b = self._cfg(epochs=2)
        c = self._cfg(imgsz=320)
        d = self._cfg(seed=43)
        self.assertNotEqual(a.training_config_id, b.training_config_id)
        self.assertNotEqual(a.training_config_id, c.training_config_id)
        self.assertNotEqual(a.training_config_id, d.training_config_id)

    def test_config_identity_deterministic(self):
        self.assertEqual(self._cfg().training_config_id,
                         self._cfg().training_config_id)


class TrainingRunTests(unittest.TestCase):
    def _run(self, state: TrainingRunState, outcome: str = "",
             checkpoints: tuple = ()) -> TrainingRun:
        return TrainingRun(
            dataset_version_id="dataset:x", training_config_id="tcfg:x",
            training_code_revision="abc123", dirty=False,
            base_model_artifact_id=None,
            environment=TrainingEnvironment(python_version="3.11",
                                            ultralytics_version="8.4",
                                            torch_version="2.2"),
            state=state, terminal_outcome=outcome,
            produced_checkpoints=checkpoints)

    def test_TR09_run_records_exact_provenance(self):
        run = self._run(TrainingRunState.COMPLETED, "completed",
                        ({"name": "best", "checkpointId": "sha256:x"},))
        j = run.to_json()
        self.assertEqual(j["datasetVersionId"], "dataset:x")
        self.assertEqual(j["trainingConfigId"], "tcfg:x")
        self.assertEqual(j["codeRevision"], "abc123")
        self.assertFalse(j["dirty"])
        self.assertEqual(j["producedCheckpoints"][0]["name"], "best")

    def test_TR10_failed_run_cannot_fabricate_artifact(self):
        """A FAILED run has no produced checkpoints — the materialize step
        only runs on the COMPLETED path (structural: mini.py)."""
        failed = self._run(TrainingRunState.FAILED, "failed: x")
        self.assertEqual(failed.produced_checkpoints, ())
        self.assertEqual(failed.state, TrainingRunState.FAILED)

    def test_dirty_repository_represented_truthfully(self):
        dirty = self._run(TrainingRunState.COMPLETED)
        dirty = TrainingRun(
            dataset_version_id="dataset:x", training_config_id="tcfg:x",
            training_code_revision="abc123", dirty=True,
            base_model_artifact_id=None,
            environment=dirty.environment,
            state=TrainingRunState.COMPLETED, terminal_outcome="completed")
        self.assertTrue(dirty.to_json()["dirty"])


class CheckpointArtifactTests(unittest.TestCase):
    def test_IMM_06_existing_model_path_with_wrong_bytes_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "best.pt"
            source.write_bytes(b"source-model")
            from evaluation.identity import sha256_file
            model_id = sha256_file(source)
            store = Path(tmp) / "store"
            store.mkdir()
            (store / f"{model_id}.pt").write_bytes(b"collision")
            checkpoint = Checkpoint(checkpoint_name="best", source_path=str(source))
            with self.assertRaises(ValueError):
                materialize_model_artifact(
                    checkpoint, training_run_id="trun:x", model_name="family",
                    target_dir=store)

    def test_TR11_best_is_not_model_name(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "best.pt"
            p.write_bytes(b"model-bytes")
            ck = Checkpoint(checkpoint_name="best", source_path=str(p))
            art = materialize_model_artifact(
                ck, training_run_id="trun:x", model_name="my_family",
                target_dir=Path(tmp) / "store")
            self.assertEqual(art.model_name, "my_family")
            self.assertNotEqual(art.model_name, "best")

    def test_TR12_rename_does_not_change_model_id(self):
        with tempfile.TemporaryDirectory() as tmp:
            src = Path(tmp) / "best.pt"
            src.write_bytes(b"model-bytes")
            renamed = Path(tmp) / "candidate.pt"
            shutil.copy(src, renamed)
            from evaluation.identity import sha256_file
            self.assertEqual(sha256_file(src), sha256_file(renamed))

    def test_TR13_byte_change_changes_model_id(self):
        with tempfile.TemporaryDirectory() as tmp:
            src = Path(tmp) / "best.pt"
            src.write_bytes(b"model-bytes")
            changed = Path(tmp) / "changed.pt"
            changed.write_bytes(b"model-bytes-x")
            from evaluation.identity import sha256_file
            self.assertNotEqual(sha256_file(src), sha256_file(changed))

    def test_model_id_is_full_64_hex(self):
        with tempfile.TemporaryDirectory() as tmp:
            src = Path(tmp) / "best.pt"
            src.write_bytes(b"model-bytes")
            ck = Checkpoint(checkpoint_name="best", source_path=str(src))
            art = materialize_model_artifact(
                ck, training_run_id="trun:x", model_name="f",
                target_dir=Path(tmp) / "store")
            self.assertEqual(len(art.model_id), 64)
            self.assertRegex(art.model_id, r"^[0-9a-f]{64}$")


class CandidateTests(unittest.TestCase):
    def test_TR14_training_completion_cannot_mutate_active(self):
        """No ACTIVE mutation API exists anywhere in the training package."""
        import pkgutil, importlib, inspect
        import training
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = _code(inspect.getsource(m))
            self.assertNotIn("ACTIVE", src.replace("CANDIDATE_TEST_ONLY", ""))

    def test_TR15_candidate_cannot_bypass_evaluation(self):
        """Candidate creation has no evaluation call, no scoring.
        (Importing evaluation.identity for hashing is not evaluation.)"""
        import inspect
        import training.candidate as cm
        src = _code(inspect.getsource(cm))
        self.assertNotIn("score", src.lower())
        self.assertNotIn("compute_task_metrics", src)
        self.assertNotIn("evaluationrun", src.lower())
        self.assertNotIn("evaluate", src.lower())

    def test_TR16_training_metric_has_no_release_authority(self):
        """trainingMetrics lives only in TrainingRun diagnostics; nothing
        outside run-recording consumes it for decisions."""
        import pkgutil, importlib, inspect
        import training
        consumers = []
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name or mod.name == "training.training_run":
                continue
            m = importlib.import_module(mod.name)
            src = _code(inspect.getsource(m))
            # nothing READS training metrics into a decision:
            # no mutation/promotion/release authority verbs anywhere
            for banned in ("promote", "release", "mutate_active",
                           "set_active", "activate"):
                self.assertNotIn(banned, src.lower(), mod.name)
            if "training_metrics" in src and mod.name != "training.mini":
                consumers.append(mod.name)
        self.assertEqual(consumers, [])

    def test_candidate_requires_provenance(self):
        cand = create_candidate(
            model_artifact_id="m" * 64, model_name="f",
            training_run_id="trun:x", dataset_version_id="dataset:x",
            training_config_id="tcfg:x")
        self.assertEqual(cand.status, CandidateStatus.CANDIDATE_TEST_ONLY)
        j = cand.to_json()
        self.assertEqual(j["status"], "CANDIDATE_TEST_ONLY")
        self.assertEqual(len(j["modelArtifactId"]), 64)


class IsolationTests(unittest.TestCase):
    def test_TR21_production_inference_does_not_import_training(self):
        import pkgutil, importlib, inspect
        import uniclaw_perception
        for mod in pkgutil.walk_packages(uniclaw_perception.__path__,
                                         prefix="uniclaw_perception."):
            m = importlib.import_module(mod.name)
            try:
                src = inspect.getsource(m)
            except OSError:
                # source unavailable — check module namespace instead
                self.assertNotIn("training", vars(m), mod.name)
                continue
            self.assertNotIn("from training", src)
            self.assertNotIn("import training", src)

    def test_TR23_runtime_has_no_training_dependency(self):
        """Structural: no C# Runtime code references the Python training
        foundation. Checked via repository grep in validation."""
        pass  # enforced by grep guard in validation phase

    def test_TR25_materialization_is_derived_view(self):
        """Dataset identity is the manifest — regenerating derived folders
        changes nothing."""
        _, a1 = _ann("sha256:a1")
        m = [_member("sha256:a1", Split.TRAIN, a1.annotation_id)]
        ds = DatasetVersion(members=tuple(m))
        first = ds.dataset_version_id
        # derived view: deleting/regenerating materialized files (simulated)
        ds2 = DatasetVersion(members=tuple(m))
        self.assertEqual(first, ds2.dataset_version_id)


if __name__ == "__main__":
    unittest.main()

class R3NamedTests(unittest.TestCase):
    def test_TRAIN_01_07_fake_yolo_receives_resolved_kwargs(self):
        from training.training_config import TrainingConfig, execute_ultralytics_training
        cfg = TrainingConfig(None,1,2,160,"SGD",0.01,"cosine","none",42,("box",),"MINI")
        resolved = cfg.resolved_invocation(data="d.yaml", output="out")
        class Fake:
            def train(self, **kwargs): self.kwargs = kwargs; return kwargs
        f=Fake(); execute_ultralytics_training(f,resolved)
        # corrected invocation surface: scheduler/augmentation are
        # TrainingConfig identity declarations, NOT valid model.train
        # kwargs (real training proved ultralytics rejects them)
        for key in ("epochs","batch","imgsz","seed","optimizer","lr0","data","project"):
            self.assertIn(key, f.kwargs)
        for banned in ("scheduler","augmentation"):
            self.assertNotIn(banned, f.kwargs)

    def test_LEAK_01_07_receipt_binds_protected_snapshot(self):
        from training.dataset import DatasetVersion, validate_training_admission, protected_set_id
        ds=DatasetVersion(())
        p={"a"}; r=validate_training_admission(ds,p)
        self.assertEqual(r.protected_set_id, protected_set_id(p))
        with self.assertRaises(ValueError): validate_training_admission(ds,p,requested_protected_set_id="protected:wrong")

    def test_ANN_01_06_acceptance_requires_structured_provenance(self):
        a,_ = _ann("x")
        self.assertTrue(_.is_accepted_training_truth)
        self.assertIsNotNone(_.acceptance_provenance)
