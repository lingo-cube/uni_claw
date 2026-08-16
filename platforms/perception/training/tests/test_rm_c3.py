"""RM-LEAK-01..10 + RM-ANN-01..12 + RM-TRAIN-01..10 (regression)
+ FINAL-LEAK-01..10 (GAP-006 executed-bytes ↔ admitted-manifest binding)
+ FINAL-TRAIN-01..14 (GAP-008 derivation/commit boundary)."""
from __future__ import annotations

import inspect
import json
import random
import struct
import tempfile
import unittest
import zlib
from pathlib import Path

from evaluation.identity import canonical_hash, sha256_file
from evaluation.stage import EvaluationTargetStage, LabelSpace

from training.annotation import (
    Annotation, AnnotationSource, ReviewStatus, AcceptanceProvenance,
    AnnotationAcceptanceEvent, accept_annotation, accept_and_persist,
    acceptance_event_for, acceptance_stance, create_annotation,
    save_annotation, validate_acceptance_chain,
)
from training.dataset import (
    DatasetMembership, DatasetVersion, Split, TrainingAdmissionReceipt,
    TrainingDataBindingError, admit_dataset_for_training,
    load_training_admission_receipt, protected_set_id,
    resolve_training_input_binding, save_training_admission_receipt,
)
from training.training_config import (
    TrainingConfig, TrainingExecutionSession, execute_training,
    load_training_config, save_training_config,
)
from training.training_run import (
    TrainingEnvironment, TrainingRun, TrainingRunState, commit_execution_run,
    save_training_run, training_run_from_execution,
)


def _draft(asset_id: str = "sha256:a"):
    return create_annotation(
        asset_id=asset_id, target_stage=EvaluationTargetStage.RAW_DETECTION,
        label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
        source=AnnotationSource.HUMAN_CREATED,
        label_payload={"boxes": [{"class": 0}]})


def _cfg(**over) -> TrainingConfig:
    base = dict(base_model_artifact_id=None, epochs=1, batch_size=2, imgsz=160,
                optimizer="auto", learning_rate="auto", scheduler="auto",
                augmentation="none", seed=42, class_vocabulary=("box",),
                label_space="MINI_SYNTHETIC_BOX_V1")
    base.update(over)
    return TrainingConfig(**base)


# ── deterministic synthetic images + labels + data.yaml (GAP-006) ──────

def _png_bytes(size: int, seed: int) -> bytes:
    """Minimal valid PNG (solid color) — deterministic from seed, no PIL."""
    rng = random.Random(seed)
    r, g, b = rng.randint(0, 255), rng.randint(0, 255), rng.randint(0, 255)
    row = b"\x00" + bytes((r, g, b)) * size
    raw = row * size

    def chunk(tag: bytes, data: bytes) -> bytes:
        c = tag + data
        return (struct.pack(">I", len(data)) + c
                + struct.pack(">I", zlib.crc32(c) & 0xFFFFFFFF))

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


def _boxes_for(seed: int, size: int) -> list[dict]:
    """Deterministic YOLO boxes (class cx cy w h, rounded to 6dp)."""
    rng = random.Random(seed * 7919 + 17)
    boxes = []
    for _ in range(rng.randint(1, 2)):
        w = rng.randint(6, 14)
        h = rng.randint(5, 12)
        cx = rng.randint(w, size - w) / size
        cy = rng.randint(h, size - h) / size
        boxes.append({"class": 0, "cx": round(cx, 6), "cy": round(cy, 6),
                      "w": round(w / size, 6), "h": round(h / size, 6)})
    return boxes


class Materialized:
    """Content-materialized training input whose image hashes EXACTLY match
    the returned membership (asset ids are computed from the written bytes —
    the canonical pattern used by training.mini)."""

    def __init__(self, root: Path):
        self.root = root
        self.data_yaml = root / "data.yaml"
        self.members: list[DatasetMembership] = []
        self.boxes_by_asset: dict[str, list[dict]] = {}
        self.path_by_asset: dict[str, Path] = {}
        self.label_by_asset: dict[str, Path] = {}

    def build(self, n_train: int = 2, n_val: int = 1, seed_base: int = 100,
              size: int = 32) -> "Materialized":
        idx = 0
        for split, name, n in ((Split.TRAIN, "train", n_train),
                               (Split.VALIDATION, "val", n_val)):
            img_dir = self.root / "images" / name
            lbl_dir = self.root / "labels" / name
            img_dir.mkdir(parents=True, exist_ok=True)
            lbl_dir.mkdir(parents=True, exist_ok=True)
            for k in range(n):
                seed = seed_base + idx
                img_path = img_dir / f"img_{idx:03d}.png"
                img_path.write_bytes(_png_bytes(size, seed))
                asset_id = f"sha256:{sha256_file(img_path)}"
                boxes = _boxes_for(seed, size)
                lbl_path = lbl_dir / f"img_{idx:03d}.txt"
                lbl_path.write_text("\n".join(
                    f"{b['class']} {b['cx']} {b['cy']} {b['w']} {b['h']}"
                    for b in boxes) + ("\n" if boxes else ""),
                    encoding="utf-8")
                self.members.append(DatasetMembership(
                    asset_id=asset_id, split=split, annotation_id=""))
                self.boxes_by_asset[asset_id] = boxes
                self.path_by_asset[asset_id] = img_path
                self.label_by_asset[asset_id] = lbl_path
                idx += 1
        self.data_yaml.write_text(
            f"path: {self.root}\ntrain: images/train\nval: images/val\n"
            f"names: {{0: box}}\n", encoding="utf-8")
        return self


class FakeResults:
    def __init__(self, save_dir, metrics=None):
        self.save_dir = str(save_dir)
        self.results_dict = metrics if metrics is not None else {
            "mAP50": 0.5, "fitness": 0.4}


class FakeModel:
    """Writes an ACTUAL checkpoint file + results during train (GAP-008)."""
    def __init__(self, path):
        self.path = path
        self.ckpt_bytes = b"fake-ckpt-bytes-v1"

    def train(self, **kwargs):
        save_dir = Path(kwargs["project"]) / kwargs["name"]
        weights = save_dir / "weights"
        weights.mkdir(parents=True, exist_ok=True)
        (weights / "best.pt").write_bytes(self.ckpt_bytes)
        return FakeResults(save_dir)


class FakeModelFail:
    def __init__(self, path):
        self.path = path

    def train(self, **kwargs):
        raise RuntimeError("boom")


class Store:
    def __init__(self, tmp: Path):
        self.ann_dir = tmp / "annotations"
        self.ev_dir = tmp / "events"
        self.ann_dir.mkdir()
        self.ev_dir.mkdir()
        self.receipt_dir = tmp / "admissions"
        self.receipt_dir.mkdir()
        self.session_evidence_dir = tmp / "execution-sessions"
        self.session_evidence_dir.mkdir()
        self.config_dir = tmp / "configs"
        self.config_dir.mkdir()

    def accept(self, ann, by="reviewer"):
        save_annotation(ann, self.ann_dir)     # draft via public writer
        return accept_and_persist(
            ann, by, annotation_dir=self.ann_dir, event_dir=self.ev_dir)

    def admit(self, ds, protected=()):
        receipt = admit_dataset_for_training(
            ds, set(protected), annotation_dir=self.ann_dir,
            event_dir=self.ev_dir)
        save_training_admission_receipt(receipt, self.receipt_dir)
        return receipt

    def save_config(self, cfg):
        save_training_config(cfg, self.config_dir)
        return cfg

    def annotate_and_admit(self, mat: Materialized, protected=()):
        """Bind canonical accepted annotations to the materialized content
        and admit — the canonical GAP-006/007 chain."""
        bound: list[DatasetMembership] = []
        for m in mat.members:
            ann = create_annotation(
                asset_id=m.asset_id,
                target_stage=EvaluationTargetStage.RAW_DETECTION,
                label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
                source=AnnotationSource.HUMAN_CREATED,
                label_payload={"boxes": mat.boxes_by_asset[m.asset_id]},
                provenance="synthetic-bindable")
            accepted = self.accept(ann)
            bound.append(DatasetMembership(
                asset_id=m.asset_id, split=m.split,
                annotation_id=accepted.annotation_id))
        ds = DatasetVersion(members=tuple(bound))
        return ds, self.admit(ds, protected=protected)


def _bindable(tmp: Path, *, n_train=2, n_val=1) -> tuple[
        Store, Materialized, DatasetVersion, TrainingAdmissionReceipt]:
    store = Store(tmp)
    mat = Materialized(tmp / "data").build(n_train=n_train, n_val=n_val)
    ds, receipt = store.annotate_and_admit(mat)
    return store, mat, ds, receipt


class RmLeakTests(unittest.TestCase):
    def test_RM_LEAK01_forged_in_memory_receipt_rejected(self):
        """No receipt object parameter exists — a forged instance cannot be
        passed at all."""
        sig = inspect.signature(execute_training)
        self.assertNotIn("admission_receipt", sig.parameters)
        self.assertIn("admission_receipt_id", sig.parameters)

    def test_RM_LEAK01a_lying_receipt_loader_cannot_be_injected(self):
        """Receipt authority is a canonical directory, not a caller callback."""
        sig = inspect.signature(execute_training)
        self.assertIn("receipt_dir", sig.parameters)
        self.assertNotIn("receipt_loader", sig.parameters)

    def test_RM_LEAK02_valid_looking_never_persisted_receipt_rejected(self):
        """A caller-minted receipt whose content does NOT match the
        canonical re-derived admission (wrong protected set) is rejected —
        the claimed id cannot be minted to match reality."""
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            accepted = store.accept(_draft("sha256:a"))
            ds = DatasetVersion(members=(
                DatasetMembership(asset_id="sha256:a", split=Split.TRAIN,
                                  annotation_id=accepted.annotation_id),))
            forged = TrainingAdmissionReceipt(
                dataset_version_id=ds.dataset_version_id,
                protected_set_id=protected_set_id({"sha256:extra"}))
            with self.assertRaises(ValueError) as e:
                execute_training(
                    config=_cfg(),
                    admission_receipt_id=forged.receipt_id,
                    dataset=ds, declared_protected_set=set(),
                    annotation_dir=store.ann_dir, event_dir=store.ev_dir,
                    receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir,
                    data_path="/x", project_path="/y", base_model_path="/z")
            self.assertIn("TRAINING_ADMISSION_MISMATCH", str(e.exception))

    def test_RM_LEAK03_wrong_content_under_claimed_id_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            accepted = store.accept(_draft("sha256:a"))
            ds = DatasetVersion(members=(
                DatasetMembership(asset_id="sha256:a", split=Split.TRAIN,
                                  annotation_id=accepted.annotation_id),))
            other_receipt = store.admit(ds)   # canonical id for THIS dataset
            # claim the canonical id but execute a DIFFERENT dataset
            other_ds = DatasetVersion(members=(
                DatasetMembership(asset_id="sha256:b", split=Split.TRAIN,
                                  annotation_id=accepted.annotation_id),))
            with self.assertRaises(ValueError) as e:
                execute_training(
                    config=_cfg(),
                    admission_receipt_id=other_receipt.receipt_id,
                    dataset=other_ds, declared_protected_set=set(),
                    annotation_dir=store.ann_dir, event_dir=store.ev_dir,
                    receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir,
                    data_path="/x", project_path="/y", base_model_path="/z")
            self.assertIn("TRAINING_ADMISSION_MISMATCH", str(e.exception))

    def test_RM_LEAK04_unpersisted_but_recomputable_receipt_rejected(self):
        """Re-derivation alone is insufficient: receipt authority requires
        a content-addressed, write-once record retrievable by its identity."""
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            accepted = store.accept(_draft("sha256:a"))
            ds = DatasetVersion(members=(DatasetMembership(
                asset_id="sha256:a", split=Split.TRAIN,
                annotation_id=accepted.annotation_id),))
            receipt = admit_dataset_for_training(
                ds, set(), annotation_dir=store.ann_dir, event_dir=store.ev_dir)
            with self.assertRaises(ValueError) as error:
                execute_training(
                    config=_cfg(), admission_receipt_id=receipt.receipt_id,
                    dataset=ds, declared_protected_set=set(),
                    annotation_dir=store.ann_dir, event_dir=store.ev_dir,
                    receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir, data_path="/x",
                    project_path="/y", base_model_path="/z",
                    model_factory=FakeModel)
            self.assertIn("PERSISTENCE_MISMATCH", str(error.exception))

    def test_RM_LEAK05_protected_set_mismatch_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            accepted = store.accept(_draft("sha256:a"))
            ds = DatasetVersion(members=(
                DatasetMembership(asset_id="sha256:a", split=Split.TRAIN,
                                  annotation_id=accepted.annotation_id),))
            receipt = store.admit(ds)          # protected set = {}
            with self.assertRaises(ValueError) as e:
                execute_training(
                    config=_cfg(), admission_receipt_id=receipt.receipt_id,
                    dataset=ds, declared_protected_set={"sha256:holdout"},
                    annotation_dir=store.ann_dir, event_dir=store.ev_dir,
                    receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir,
                    data_path="/x", project_path="/y", base_model_path="/z")
            self.assertIn("TRAINING_ADMISSION_MISMATCH", str(e.exception))

    def test_RM_LEAK07_protected_leakage_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            accepted = store.accept(_draft("sha256:a"))
            ds = DatasetVersion(members=(
                DatasetMembership(asset_id="sha256:a", split=Split.TRAIN,
                                  annotation_id=accepted.annotation_id),))
            with self.assertRaises(ValueError) as e:
                store.admit(ds, protected={"sha256:a"})
            self.assertIn("leakage", str(e.exception))

    def test_RM_LEAK08_canonical_admitted_receipt_accepted(self):
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=2, n_val=1)
            session = execute_training(
                config=_cfg(), admission_receipt_id=receipt.receipt_id,
                dataset=ds, declared_protected_set=set(),
                annotation_dir=store.ann_dir, event_dir=store.ev_dir,
                receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir,
                data_path=str(mat.data_yaml), project_path=str(Path(tmp) / "proj"),
                base_model_path="/z", model_factory=FakeModel)
            self.assertTrue(session.congruent)
            self.assertEqual(session.admission_receipt_id, receipt.receipt_id)
            # GAP-006 positive evidence: executed bytes == admitted membership
            binding = session.training_input_binding
            self.assertEqual(binding["datasetVersionId"], ds.dataset_version_id)
            self.assertEqual(binding["resolvedMemberCount"], 3)
            self.assertEqual(len(binding["imageContentIds"]), 3)
            self.assertEqual(len(binding["labelAnnotationBindings"]), 3)

    def test_RM_LEAK09_training_run_records_verified_receipt_identity(self):
        tmp, store, session = RmTrainTests()._executed()
        self.addCleanup(tmp.cleanup)
        run = training_run_from_execution(
            config=_cfg(), session=session,
            code_revision="abc", dirty=False,
            receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)
        self.assertEqual(run.training_admission_receipt_id,
                         session.admission_receipt_id)

    def test_RM_LEAK10_no_alternate_execution_api(self):
        import pkgutil, importlib
        import training
        callers = []
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name or "config" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            if "model.train(" in src:
                callers.append(mod.name)
        self.assertEqual(callers, [])


class RmAnnTests(unittest.TestCase):
    def _forged_accepted(self, store, asset_id="sha256:forged"):
        draft = create_annotation(
            asset_id=asset_id, target_stage=EvaluationTargetStage.RAW_DETECTION,
            label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
            source=AnnotationSource.MODEL_ASSISTED,
            label_payload={"boxes": [{"class": 0}]})
        pred_id = draft.annotation_id
        event = AnnotationAcceptanceEvent(
            review_event_id=f"review:{canonical_hash((pred_id, 'forger'))}",
            predecessor_annotation_id=pred_id,
            accepted_annotation_id="annotation:FORGED",
            accepted_payload_hash=canonical_hash(draft.label_payload),
            reviewer_identity="forger", decision="ACCEPT",
            asset_id=draft.asset_id, target_stage=draft.target_stage.value,
            label_space=draft.label_space.value)
        forged = Annotation(
            asset_id=draft.asset_id, target_stage=draft.target_stage,
            label_space=draft.label_space, source=draft.source,
            review_status=ReviewStatus.ACCEPTED,
            label_payload=draft.label_payload, provenance="forged",
            predecessor_annotation_id=pred_id,
            acceptance_provenance=AcceptanceProvenance(
                review_event_id=event.review_event_id,
                reviewer_identity="forger", predecessor_annotation_id=pred_id))
        return draft, event, forged

    def test_RM_ANN01_no_public_event_writer(self):
        self.assertFalse(hasattr(
            __import__("training.annotation", fromlist=["x"]),
            "save_acceptance_event"))

    def test_RM_ANN02_public_save_refuses_accepted(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            draft = _draft("sha256:a")
            accepted = store.accept(draft)     # canonical path works
            self.assertEqual(accepted.review_status, ReviewStatus.ACCEPTED)
            with self.assertRaises(ValueError):
                save_annotation(accepted, store.ann_dir)

    def test_RM_ANN03_direct_construction_has_zero_authority(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            _, _, forged = self._forged_accepted(store)
            ds = DatasetVersion(members=(
                DatasetMembership(asset_id="sha256:forged",
                                  split=Split.TRAIN,
                                  annotation_id=forged.annotation_id),))
            with self.assertRaises(ValueError) as e:
                store.admit(ds)
            self.assertIn("annotation chains", str(e.exception))

    def test_RM_ANN04_deserialization_has_zero_minting_authority(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            draft = _draft("sha256:a")
            accepted = store.accept(draft)
            loaded = Annotation.from_json(accepted.to_json())
            # loaded round-trip ok, but it cannot be re-saved via public API
            with self.assertRaises(ValueError):
                save_annotation(loaded, store.ann_dir)

    def test_RM_ANN05_invented_event_id_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            _, event, forged = self._forged_accepted(store)
            tampered = Annotation(
                asset_id=forged.asset_id, target_stage=forged.target_stage,
                label_space=forged.label_space, source=forged.source,
                review_status=forged.review_status,
                label_payload=forged.label_payload,
                provenance=forged.provenance,
                predecessor_annotation_id=forged.predecessor_annotation_id,
                acceptance_provenance=AcceptanceProvenance(
                    review_event_id="review:INVENTED",
                    reviewer_identity="forger",
                    predecessor_annotation_id=forged.predecessor_annotation_id))
            ok, reason = validate_acceptance_chain(
                tampered, annotation_dir=store.ann_dir, event_dir=store.ev_dir)
            self.assertFalse(ok)

    def test_RM_ANN06_invented_predecessor_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            forged = Annotation(
                asset_id="sha256:x",
                target_stage=EvaluationTargetStage.RAW_DETECTION,
                label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
                source=AnnotationSource.HUMAN_CREATED,
                review_status=ReviewStatus.ACCEPTED,
                label_payload={"boxes": []},
                predecessor_annotation_id="annotation:NOPE",
                acceptance_provenance=AcceptanceProvenance(
                    review_event_id="review:x", reviewer_identity="r",
                    predecessor_annotation_id="annotation:NOPE"))
            ok, _ = validate_acceptance_chain(
                forged, annotation_dir=store.ann_dir, event_dir=store.ev_dir)
            self.assertFalse(ok)

    def test_RM_ANN07_self_consistent_hashes_alone_no_authority(self):
        """A fully self-consistent forged chain (valid hashes, valid JSON)
        still has no minting authority — it cannot be PERSISTED through
        public APIs, so admission cannot see it as canonical storage."""
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            _, _, forged = self._forged_accepted(store)
            with self.assertRaises(ValueError):
                save_annotation(forged, store.ann_dir)
            self.assertFalse(hasattr(
                __import__("training.annotation", fromlist=["x"]),
                "save_acceptance_event"))
            self.assertFalse(hasattr(
                __import__("training.annotation", fromlist=["x"]),
                "_persist_acceptance_event"))
            self.assertFalse(hasattr(
                __import__("training.annotation", fromlist=["x"]),
                "_persist_annotation_record"))

    def test_RM_ANN07a_forged_event_identity_or_reviewer_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            draft, event, _ = self._forged_accepted(store, "sha256:a")
            save_annotation(draft, store.ann_dir)
            accepted = accept_annotation(draft, corrected_by="reviewer-a")
            forged_event = AnnotationAcceptanceEvent(
                review_event_id="review:ATTACKER_CHOSEN",
                predecessor_annotation_id=draft.annotation_id,
                accepted_annotation_id=accepted.annotation_id,
                accepted_payload_hash=canonical_hash(accepted.label_payload),
                reviewer_identity="reviewer-b",
                decision="ACCEPT", asset_id=accepted.asset_id,
                target_stage=accepted.target_stage.value,
                label_space=accepted.label_space.value)
            expected_event_id = accepted.acceptance_provenance.review_event_id
            (store.ev_dir / f"{expected_event_id.replace('review:', '')}.json").write_text(
                json.dumps(forged_event.to_json()), encoding="utf-8")
            ok, reason = validate_acceptance_chain(
                accepted, annotation_dir=store.ann_dir, event_dir=store.ev_dir)
            self.assertFalse(ok)
            self.assertIn("review event", reason)

    def test_RM_ANN07b_unpersisted_predecessor_cannot_be_accepted(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            with self.assertRaises(ValueError) as error:
                accept_and_persist(
                    _draft("sha256:unpersisted"), "reviewer",
                    annotation_dir=store.ann_dir, event_dir=store.ev_dir)
            self.assertIn("predecessor", str(error.exception))

    def test_RM_ANN08_canonical_accept_and_persist_produces_admissible_chain(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            accepted = store.accept(_draft("sha256:a"))
            ds = DatasetVersion(members=(
                DatasetMembership(asset_id="sha256:a", split=Split.TRAIN,
                                  annotation_id=accepted.annotation_id),))
            receipt = store.admit(ds)
            self.assertEqual(receipt.admission_result, "ADMITTED")

    def test_RM_ANN09_predecessor_remains_immutable(self):
        draft = _draft("sha256:a")
        pre_id = draft.annotation_id
        accepted = accept_annotation(draft)
        self.assertNotEqual(accepted.annotation_id, pre_id)
        self.assertEqual(draft.annotation_id, pre_id)

    def test_RM_ANN10_legacy_accepted_inadmissible(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            legacy = Annotation(
                asset_id="sha256:a",
                target_stage=EvaluationTargetStage.RAW_DETECTION,
                label_space=LabelSpace.MINI_SYNTHETIC_BOX_V1,
                source=AnnotationSource.HUMAN_CREATED,
                review_status=ReviewStatus.ACCEPTED,
                label_payload={"boxes": []}, provenance="legacy")
            stance = acceptance_stance(
                legacy, annotation_dir=store.ann_dir, event_dir=store.ev_dir)
            self.assertEqual(stance, "LEGACY_ACCEPTANCE_PROVENANCE")

    def test_RM_ANN11_no_public_save_bypass(self):
        import pkgutil, importlib
        import training
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name:
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            # production code must not call the internal event writer
            if mod.name != "training.annotation":
                self.assertNotIn("_persist_acceptance_event", src, mod.name)

    def test_RM_ANN12_admission_reloads_canonical_records(self):
        """admission loads records from canonical locations by identity —
        it never accepts caller-supplied record objects."""
        sig = inspect.signature(admit_dataset_for_training)
        self.assertIn("annotation_dir", sig.parameters)
        self.assertIn("event_dir", sig.parameters)
        self.assertNotIn("annotation_loader", sig.parameters)
        self.assertNotIn("event_loader", sig.parameters)


class RmTrainTests(unittest.TestCase):
    def _executed(self, cfg=None, model_factory=FakeModel):
        tmp = tempfile.TemporaryDirectory()
        store = Store(Path(tmp.name))
        mat = Materialized(Path(tmp.name) / "data").build(n_train=1, n_val=0)
        ds, receipt = store.annotate_and_admit(mat)
        cfg = cfg if cfg is not None else _cfg()
        session = execute_training(
            config=cfg, admission_receipt_id=receipt.receipt_id,
            dataset=ds, declared_protected_set=set(),
            annotation_dir=store.ann_dir, event_dir=store.ev_dir,
            receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir,
            data_path=str(mat.data_yaml),
            project_path=str(Path(tmp.name) / "proj"),
            base_model_path="/z", model_factory=model_factory)
        return tmp, store, session

    def _commit(self, store, session, out_dir=None, **over):
        kwargs = dict(
            session_evidence_id=session.session_evidence_id,
            config_dir=store.config_dir,
            receipt_dir=store.receipt_dir,
            session_evidence_dir=store.session_evidence_dir,
            code_revision="abc", dirty=False, out_dir=out_dir)
        kwargs.update(over)
        return commit_execution_run(**kwargs)

    def test_RM_TRAIN01_direct_completed_run_cannot_be_persisted(self):
        run = TrainingRun(
            dataset_version_id="dataset:x", training_config_id="tcfg:x",
            training_code_revision="abc", dirty=False,
            base_model_artifact_id=None, environment=_env(),
            state=TrainingRunState.COMPLETED, terminal_outcome="completed")
        with self.assertRaises(ValueError):
            save_training_run(run, Path(tempfile.mkdtemp()))

    def test_RM_TRAIN02_direct_failed_run_cannot_impersonate_history(self):
        run = TrainingRun(
            dataset_version_id="dataset:x", training_config_id="tcfg:x",
            training_code_revision="abc", dirty=False,
            base_model_artifact_id=None, environment=_env(),
            state=TrainingRunState.FAILED, terminal_outcome="failed: x",
            invocation_args={"epochs": 999})
        with self.assertRaises(ValueError):
            save_training_run(run, Path(tempfile.mkdtemp()))

    def test_RM_TRAIN03_unrelated_config_cannot_be_saved(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        store.save_config(_cfg(epochs=2))      # NOT the executed config
        with self.assertRaises(Exception):
            self._commit(store, session)

    def test_RM_TRAIN04_arbitrary_invocation_cannot_be_saved(self):
        """commit path derives invocation from the session — no caller
        invocation/hash parameters exist."""
        sig = inspect.signature(commit_execution_run)
        for banned in ("invocation_args", "invocation_hash"):
            self.assertNotIn(banned, sig.parameters)

    def test_RM_TRAIN05_training_run_from_execution_still_works(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        cfg = _cfg()
        run = training_run_from_execution(
            config=cfg, session=session,
            code_revision="abc", dirty=False,
            receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)
        self.assertEqual(run.training_config_id, cfg.training_config_id)
        self.assertEqual(run.invocation_hash,
                         canonical_hash(run.invocation_args))

    def test_RM_TRAIN06_identity_derives_from_session(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        cfg = _cfg()
        store.save_config(cfg)
        run = self._commit(store, session, out_dir=Path(tempfile.mkdtemp()))[0]
        self.assertEqual(run.dataset_version_id, session.dataset_version_id)
        self.assertEqual(run.training_admission_receipt_id, session.admission_receipt_id)

    def test_RM_TRAIN07_receipt_identity_retained(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        run = training_run_from_execution(
            config=_cfg(), session=session,
            code_revision="abc", dirty=False,
            receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)
        self.assertEqual(run.training_admission_receipt_id, session.admission_receipt_id)

    def test_RM_TRAIN08_failed_real_execution_preservable(self):
        tmp, store, session = self._executed(model_factory=FakeModelFail)
        self.addCleanup(tmp.cleanup)
        cfg = _cfg()
        store.save_config(cfg)
        run, path = self._commit(store, session, out_dir=Path(tempfile.mkdtemp()))
        self.assertEqual(run.state, TrainingRunState.FAILED)
        self.assertTrue(path.exists())

    def test_RM_LEAK06_forged_session_cannot_mint_terminal_history(self):
        """Caller-created dataclasses have no authority even if their visible
        fields claim congruence: commit accepts ONLY a persisted, content-
        addressed session evidence id."""
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        with self.assertRaises(Exception) as error:
            commit_execution_run(
                session_evidence_id="execution:" + "0" * 64,
                config_dir=store.config_dir,
                code_revision="abc", dirty=False,
                receipt_dir=store.receipt_dir,
                session_evidence_dir=store.session_evidence_dir)
        self.assertIn("INVOCATION_MISMATCH", str(error.exception))

    def test_RM_LEAK06a_session_with_invented_evidence_id_cannot_mint(self):
        """Even an exact-looking session has no terminal-run authority until
        its content-addressed execution evidence has been persisted under
        the exact identity claimed."""
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        forged_id = "execution:" + "f" * 64
        with self.assertRaises(Exception) as error:
            commit_execution_run(
                session_evidence_id=forged_id,
                config_dir=store.config_dir,
                code_revision="abc", dirty=False,
                receipt_dir=store.receipt_dir,
                session_evidence_dir=store.session_evidence_dir)
        self.assertIn("INVOCATION_MISMATCH", str(error.exception))

    def test_RM_LEAK06b_no_public_session_evidence_writer(self):
        """A caller cannot persist a forged session and turn it into terminal
        record authority through a module-level writer."""
        import training.training_config as tc
        self.assertFalse(hasattr(tc, "save_execution_session_evidence"))

    def test_RM_TRAIN09_legacy_loader_has_no_write_authority(self):
        """The module's ONLY write_once_json CALL site is inside
        commit_execution_run — no other write surface exists."""
        import training.training_run as tr
        src = inspect.getsource(tr)
        self.assertNotIn("write_text", src)
        # import line + exactly one call inside commit_execution_run
        self.assertEqual(src.count("write_once_json"), 2)
        commit_body = src.split("def commit_execution_run")[1]
        self.assertIn("write_once_json(", commit_body)

    def test_RM_TRAIN10_no_alternate_terminal_writer(self):
        """save_training_run is used NOWHERE; commit_execution_run is used
        only by the canonical consumer (training.mini) and the runner
        module itself."""
        import pkgutil, importlib
        import training
        save_users, commit_users = [], []
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name or mod.name == "training.training_run":
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            if "save_training_run" in src:
                save_users.append(mod.name)
            if "commit_execution_run" in src and mod.name != "training.mini":
                commit_users.append(mod.name)
        self.assertEqual(save_users, [])
        self.assertEqual(commit_users, [])


def _env() -> TrainingEnvironment:
    return TrainingEnvironment(python_version="3.11", ultralytics_version="8",
                               torch_version="2", seed="42")


class FinalLeakTests(unittest.TestCase):
    """GAP-006 FINAL: executed training bytes must bind to the admitted
    DatasetVersion manifest — content identity, fail closed."""

    def _session_or_error(self, store, mat, ds, receipt, data_path, tmp):
        try:
            return execute_training(
                config=_cfg(), admission_receipt_id=receipt.receipt_id,
                dataset=ds, declared_protected_set=set(),
                annotation_dir=store.ann_dir, event_dir=store.ev_dir,
                receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir,
                data_path=data_path, project_path=str(Path(tmp) / "proj"),
                base_model_path="/z", model_factory=FakeModel), None
        except TrainingDataBindingError as exc:
            return None, exc

    def test_FINAL_LEAK_01_unrelated_data_path_rejected(self):
        """A data.yaml whose images belong to a DIFFERENT dataset (zero
        content overlap) fails closed — data_path is location, never
        semantic identity."""
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=2)
            other = Materialized(Path(tmp) / "other").build(n_train=1, n_val=0,
                                                            seed_base=500)
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(other.data_yaml), tmp)
            self.assertIsNone(session)
            self.assertIn("UNRELATED_DATA_PATH", str(err))

    def test_FINAL_LEAK_02_extra_image_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=1, n_val=0)
            # drop an extra image not in the admitted membership
            (mat.root / "images" / "train" / "extra.png").write_bytes(
                _png_bytes(32, 9999))
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(mat.data_yaml), tmp)
            self.assertIsNone(session)
            self.assertIn("EXTRA_IMAGE", str(err))

    def test_FINAL_LEAK_03_missing_required_image_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=2, n_val=0)
            first = sorted(mat.path_by_asset.values())[0]
            first.unlink()
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(mat.data_yaml), tmp)
            self.assertIsNone(session)
            self.assertIn("MISSING_REQUIRED_IMAGE", str(err))

    def test_FINAL_LEAK_04_changed_bytes_rejected(self):
        """Same file count but altered image content → CHANGED_BYTES."""
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=2, n_val=0)
            first = sorted(mat.path_by_asset.values())[0]
            first.write_bytes(_png_bytes(32, 4242))
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(mat.data_yaml), tmp)
            self.assertIsNone(session)
            self.assertIn("CHANGED_BYTES", str(err))

    def test_FINAL_LEAK_05_wrong_label_content_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=1, n_val=0)
            label = sorted(mat.label_by_asset.values())[0]
            label.write_text("0 0.5 0.5 0.2 0.2\n", encoding="utf-8")
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(mat.data_yaml), tmp)
            self.assertIsNone(session)
            self.assertIn("LABEL_CONTENT_MISMATCH", str(err))

    def test_FINAL_LEAK_06_label_of_another_annotation_rejected(self):
        """The membership binds an annotation that belongs to a DIFFERENT
        asset — the YOLO label filename is never trusted; the canonical
        record's asset identity is."""
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            mat = Materialized(Path(tmp) / "data").build(n_train=1, n_val=0)
            # accept an annotation for a DIFFERENT asset than the member
            other_ann = store.accept(_draft("sha256:other"))
            ds = DatasetVersion(members=(DatasetMembership(
                asset_id=mat.members[0].asset_id, split=Split.TRAIN,
                annotation_id=other_ann.annotation_id),))
            receipt = store.admit(ds)
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(mat.data_yaml), tmp)
            self.assertIsNone(session)
            self.assertIn("LABEL_ANNOTATION_MISMATCH", str(err))

    def test_FINAL_LEAK_07_unresolvable_data_yaml_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=1)
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(Path(tmp) / "nope.yaml"), tmp)
            self.assertIsNone(session)
            self.assertIn("DATA_YAML_UNRESOLVABLE", str(err))

    def test_FINAL_LEAK_08_ambiguous_materialization_rejected(self):
        """The same image content materialized under more than one split is
        an ambiguous execution input — fail closed."""
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=1, n_val=1)
            train_img = sorted(mat.path_by_asset.values())[0]
            (mat.root / "images" / "val" / "dup.png").write_bytes(
                train_img.read_bytes())
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(mat.data_yaml), tmp)
            self.assertIsNone(session)
            self.assertIn("AMBIGUOUS_MATERIALIZATION", str(err))

    def test_FINAL_LEAK_09_label_file_missing_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=1, n_val=0)
            sorted(mat.label_by_asset.values())[0].unlink()
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(mat.data_yaml), tmp)
            self.assertIsNone(session)
            self.assertIn("LABEL_FILE_MISSING", str(err))

    def test_FINAL_LEAK_10_content_identity_not_path_identity(self):
        """The SAME content relocated to a different directory + data.yaml
        still binds with the SAME content-derived binding id — and the
        API surface exposes data_path (location) only, never a
        caller-supplied semantic identity."""
        sig = inspect.signature(execute_training)
        self.assertIn("data_path", sig.parameters)
        for banned in ("dataset_identity", "data_content",
                       "data_content_ids"):
            self.assertNotIn(banned, sig.parameters)
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=1, n_val=1)
            # relocate the whole tree to a different directory
            moved = Path(tmp) / "relocated"
            import shutil
            shutil.copytree(mat.root, moved)
            moved_yaml = moved / "data.yaml"
            moved_yaml.write_text(
                f"path: {moved}\ntrain: images/train\nval: images/val\n"
                f"names: {{0: box}}\n", encoding="utf-8")
            session, err = self._session_or_error(
                store, mat, ds, receipt, str(moved_yaml), tmp)
            self.assertIsNone(err)
            self.assertIsNotNone(session)
            # binding identity is content-derived: relocating cannot change it
            binding = resolve_training_input_binding(
                str(moved_yaml), ds, annotation_dir=store.ann_dir)
            self.assertEqual(binding.binding_id,
                             session.training_input_binding["bindingId"])

    def test_FINAL_LEAK_10b_resolver_direct_positive(self):
        """Direct resolver: exact set equality, label↔annotation binding,
        stable content-addressed binding id."""
        with tempfile.TemporaryDirectory() as tmp:
            store, mat, ds, receipt = _bindable(Path(tmp), n_train=2, n_val=1)
            binding = resolve_training_input_binding(
                str(mat.data_yaml), ds, annotation_dir=store.ann_dir)
            self.assertEqual(binding.dataset_version_id, ds.dataset_version_id)
            self.assertEqual(binding.resolved_member_count, 3)
            self.assertEqual(len(binding.label_annotation_bindings), 3)
            self.assertEqual(binding.split_counts,
                             {"train": 2, "val": 1})
            self.assertTrue(binding.binding_id.startswith(
                "training-input-binding:"))
            again = resolve_training_input_binding(
                str(mat.data_yaml), ds, annotation_dir=store.ann_dir)
            self.assertEqual(again.binding_id, binding.binding_id)


class FinalTrainTests(unittest.TestCase):
    """GAP-008 FINAL: commit_execution_run is a DERIVATION/COMMIT boundary,
    not a second data-entry API."""

    def _executed(self, cfg=None, model_factory=FakeModel):
        tmp = tempfile.TemporaryDirectory()
        store = Store(Path(tmp.name))
        mat = Materialized(Path(tmp.name) / "data").build(n_train=1, n_val=1)
        ds, receipt = store.annotate_and_admit(mat)
        cfg = cfg if cfg is not None else _cfg()
        session = execute_training(
            config=cfg, admission_receipt_id=receipt.receipt_id,
            dataset=ds, declared_protected_set=set(),
            annotation_dir=store.ann_dir, event_dir=store.ev_dir,
            receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir,
            data_path=str(mat.data_yaml),
            project_path=str(Path(tmp.name) / "proj"),
            base_model_path="/z", model_factory=model_factory)
        store.save_config(cfg)
        return tmp, store, session

    def _commit(self, store, session, out_dir=None, **over):
        kwargs = dict(
            session_evidence_id=session.session_evidence_id,
            config_dir=store.config_dir,
            receipt_dir=store.receipt_dir,
            session_evidence_dir=store.session_evidence_dir,
            code_revision="abc", dirty=False, out_dir=out_dir)
        kwargs.update(over)
        return commit_execution_run(**kwargs)

    def test_FINAL_TRAIN_01_commit_has_no_terminal_data_entry_params(self):
        """The commit boundary exposes ONLY the canonical session evidence
        id + storage locations + non-authoritative context.  No caller can
        declare state / terminal_outcome / base model / environment /
        checkpoints / metrics."""
        sig = inspect.signature(commit_execution_run)
        self.assertIn("session_evidence_id", sig.parameters)
        self.assertIn("config_dir", sig.parameters)
        for banned in ("state", "terminal_outcome", "base_model_artifact_id",
                       "environment", "produced_checkpoints",
                       "training_metrics", "invocation_args",
                       "invocation_hash", "session", "config",
                       "training_run", "metrics"):
            self.assertNotIn(banned, sig.parameters)

    def test_FINAL_TRAIN_02_failed_terminal_state_derived(self):
        """session.terminal_error non-null → FAILED — derived, not chosen."""
        tmp, store, session = self._executed(model_factory=FakeModelFail)
        self.addCleanup(tmp.cleanup)
        self.assertTrue(session.terminal_error)
        run = self._commit(store, session)[0]
        self.assertEqual(run.state, TrainingRunState.FAILED)

    def test_FINAL_TRAIN_03_terminal_outcome_derived(self):
        tmp, store, session = self._executed(model_factory=FakeModelFail)
        self.addCleanup(tmp.cleanup)
        run = self._commit(store, session)[0]
        self.assertIn("failed:", run.terminal_outcome)
        self.assertIn(session.terminal_error, run.terminal_outcome)
        self.assertIn("boom", run.terminal_outcome)

    def test_FINAL_TRAIN_04_completed_terminal_state_derived(self):
        """session.terminal_error null → COMPLETED / 'completed' — derived,
        not chosen."""
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        self.assertEqual(session.terminal_error, "")
        run = self._commit(store, session)[0]
        self.assertEqual(run.state, TrainingRunState.COMPLETED)
        self.assertEqual(run.terminal_outcome, "completed")

    def test_FINAL_TRAIN_05_base_model_derived_from_persisted_config(self):
        cfg = _cfg(base_model_artifact_id="sha256:base")
        tmp, store, session = self._executed(cfg=cfg)
        self.addCleanup(tmp.cleanup)
        run = self._commit(store, session)[0]
        self.assertEqual(run.base_model_artifact_id, "sha256:base")

    def test_FINAL_TRAIN_06_environment_derived_from_captured_execution(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        run = self._commit(store, session)[0]
        captured = session.captured_environment
        self.assertTrue(captured)   # captured during execution, never empty
        self.assertEqual(run.environment.python_version,
                         captured["pythonVersion"])
        self.assertEqual(run.environment.ultralytics_version,
                         captured["ultralyticsVersion"])
        self.assertEqual(run.environment.torch_version,
                         captured["torchVersion"])
        self.assertEqual(run.environment.seed, captured["seed"])
        self.assertEqual(run.environment.seed, "42")   # config.seed captured
        self.assertEqual(run.environment.os_name, captured["osName"])

    def test_FINAL_TRAIN_07_checkpoints_from_actual_execution_output(self):
        """The produced checkpoint id is the sha256 of the ACTUAL file the
        training wrote — never a caller-declared checkpoint list."""
        from evaluation.identity import content_id
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        expected_id = content_id(FakeModel(None).ckpt_bytes)
        self.assertEqual(session.produced_checkpoints,
                         ({"name": "best", "checkpointId": expected_id},))
        run = self._commit(store, session)[0]
        self.assertEqual(run.produced_checkpoints,
                         ({"name": "best", "checkpointId": expected_id},))

    def test_FINAL_TRAIN_08_checkpoint_revocation_fails_commit(self):
        """A produced checkpoint that disappears or changes after execution
        revokes the lineage — the commit re-verifies the produced files."""
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        weights = (Path(session.execution_location["project"])
                   / session.execution_location["name"] / "weights")
        (weights / "best.pt").unlink()
        with self.assertRaises(Exception) as error:
            self._commit(store, session)
        self.assertIn("INVOCATION_MISMATCH", str(error.exception))

    def test_FINAL_TRAIN_09_metrics_from_actual_execution_output(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        self.assertEqual(session.training_metrics,
                         {"mAP50": 0.5, "fitness": 0.4})
        run = self._commit(store, session)[0]
        self.assertEqual(run.training_metrics,
                         {"mAP50": 0.5, "fitness": 0.4})

    def test_FINAL_TRAIN_10_metrics_omitted_when_not_captured(self):
        """When the framework exposes no metrics, the record omits them —
        nothing is invented."""
        class EmptyResults(FakeResults):
            def __init__(self, save_dir):
                super().__init__(save_dir, metrics={})

        class EmptyModel(FakeModel):
            def train(self, **kwargs):
                save_dir = Path(kwargs["project"]) / kwargs["name"]
                weights = save_dir / "weights"
                weights.mkdir(parents=True, exist_ok=True)
                (weights / "best.pt").write_bytes(self.ckpt_bytes)
                return EmptyResults(save_dir)

        tmp, store, session = self._executed(model_factory=EmptyModel)
        self.addCleanup(tmp.cleanup)
        self.assertEqual(session.training_metrics, {})
        run = self._commit(store, session)[0]
        self.assertEqual(run.training_metrics, {})

    def test_FINAL_TRAIN_11_persisted_session_evidence_required(self):
        """Commit loads the session BY its content-addressed id — a forged
        or absent evidence id cannot mint terminal history."""
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        with self.assertRaises(Exception) as error:
            self._commit(store, session,
                         session_evidence_id="execution:" + "1" * 64)
        self.assertIn("INVOCATION_MISMATCH", str(error.exception))

    def test_FINAL_TRAIN_12_persisted_config_required(self):
        """Commit loads the TrainingConfig BY the session's config identity —
        no caller-supplied config object exists."""
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        # remove the persisted config → commit cannot derive base model
        cfg = _cfg()
        (store.config_dir
         / f"{cfg.training_config_id.removeprefix('tcfg:')}.json").unlink()
        with self.assertRaises(Exception) as error:
            self._commit(store, session)
        self.assertIn("INVOCATION_MISMATCH", str(error.exception))

    def test_FINAL_TRAIN_13_persisted_run_content_derived_identity(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        out = Path(tempfile.mkdtemp())
        run, path = self._commit(store, session, out_dir=out)
        body = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual(body["trainingRunId"], run.training_run_id)
        # identity derives from the persisted content (minus the id itself)
        identity_body = {k: v for k, v in body.items() if k != "trainingRunId"}
        self.assertEqual(
            run.training_run_id, f"trun:{canonical_hash(identity_body)}")
        self.assertEqual(body["state"], "COMPLETED")      # derived
        self.assertEqual(body["terminalOutcome"], "completed")  # derived
        self.assertIn("environment", body)                # captured
        self.assertIn("producedCheckpoints", body)        # actual evidence
        self.assertIn("trainingMetrics", body)            # actual evidence
        self.assertIn("trainingAdmissionReceiptId", body)  # verified binding

    def test_FINAL_TRAIN_14_no_alternate_terminal_run_writer(self):
        """save_training_run refuses; commit_execution_run is the ONLY
        terminal TrainingRun writer — no second mint surface exists."""
        sig = inspect.signature(save_training_run)
        self.assertEqual(list(sig.parameters), ["run", "out_dir"])
        import pkgutil, importlib
        import training
        writers = []
        for mod in pkgutil.walk_packages(training.__path__,
                                         prefix="training."):
            if "tests" in mod.name or mod.name == "training.training_run":
                continue
            m = importlib.import_module(mod.name)
            src = inspect.getsource(m)
            if "TrainingRun(" in src or "save_training_run(" in src:
                writers.append(mod.name)
        self.assertEqual(writers, [])


if __name__ == "__main__":
    unittest.main()
