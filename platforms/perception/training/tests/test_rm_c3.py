"""RM-LEAK-01..10 + RM-ANN-01..12 + RM-TRAIN-01..10
(record-minting closure for GAP-006/007/008)."""
from __future__ import annotations

import inspect
import json
import tempfile
import unittest
from pathlib import Path

from evaluation.identity import canonical_hash
from evaluation.stage import EvaluationTargetStage, LabelSpace

from training.annotation import (
    Annotation, AnnotationSource, ReviewStatus, AcceptanceProvenance,
    AnnotationAcceptanceEvent, accept_annotation, accept_and_persist,
    acceptance_event_for, acceptance_stance, create_annotation,
    save_annotation, validate_acceptance_chain,
)
from training.dataset import (
    DatasetMembership, DatasetVersion, Split, TrainingAdmissionReceipt,
    admit_dataset_for_training, load_training_admission_receipt,
    protected_set_id, save_training_admission_receipt,
)
from training.training_config import (
    TrainingConfig, TrainingExecutionSession, execute_training,
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


def _env() -> TrainingEnvironment:
    return TrainingEnvironment(python_version="3.11", ultralytics_version="8",
                               torch_version="2", seed="42")


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

    def accept(self, ann, by="reviewer"):
        save_annotation(ann, self.ann_dir)     # draft via public writer
        return accept_and_persist(
            ann, by, annotation_dir=self.ann_dir, event_dir=self.ev_dir)

    def ann_loader(self):
        def load(aid):
            f = self.ann_dir / f"{aid.replace('annotation:', '')}.json"
            if not f.exists():
                return None
            r = Annotation.from_json(json.loads(f.read_text(encoding="utf-8")))
            return r if r.annotation_id == aid else None
        return load

    def ev_loader(self):
        def load(eid):
            f = self.ev_dir / f"{eid.replace('review:', '')}.json"
            if not f.exists():
                return None
            e = AnnotationAcceptanceEvent.from_json(
                json.loads(f.read_text(encoding="utf-8")))
            return e if e.review_event_id == eid else None
        return load

    def admit(self, ds, protected=()):
        receipt = admit_dataset_for_training(
            ds, set(protected), annotation_dir=self.ann_dir,
            event_dir=self.ev_dir)
        save_training_admission_receipt(receipt, self.receipt_dir)
        return receipt

class RmLeakTests(unittest.TestCase):
    def _admitted(self, store):
        accepted = store.accept(_draft("sha256:a"))
        ds = DatasetVersion(members=(
            DatasetMembership(asset_id="sha256:a", split=Split.TRAIN,
                              annotation_id=accepted.annotation_id),))
        return ds, store.admit(ds)

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
        class FakeModel:
            def __init__(self, path): pass
            def train(self, **kwargs): return None
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
        class FakeModel:
            def __init__(self, p): pass
            def train(self, **kw): return None
        with tempfile.TemporaryDirectory() as tmp:
            store = Store(Path(tmp))
            accepted = store.accept(_draft("sha256:a"))
            ds = DatasetVersion(members=(
                DatasetMembership(asset_id="sha256:a", split=Split.TRAIN,
                                  annotation_id=accepted.annotation_id),))
            receipt = store.admit(ds)
            session = execute_training(
                config=_cfg(), admission_receipt_id=receipt.receipt_id,
                dataset=ds, declared_protected_set=set(),
                annotation_dir=store.ann_dir, event_dir=store.ev_dir,
                    receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir,
                data_path="/x", project_path="/y", base_model_path="/z",
                model_factory=FakeModel)
            self.assertTrue(session.congruent)
            self.assertEqual(session.admission_receipt_id, receipt.receipt_id)

    def test_RM_LEAK09_training_run_records_verified_receipt_identity(self):
        tmp, store, session = RmTrainTests()._executed()
        self.addCleanup(tmp.cleanup)
        run = training_run_from_execution(
            config=_cfg(), session=session, environment=_env(),
            code_revision="abc", dirty=False, base_model_artifact_id=None,
            state=TrainingRunState.COMPLETED, terminal_outcome="ok",
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
    def _executed(self):
        class FakeModel:
            def __init__(self, path): pass
            def train(self, **kwargs): return None
        tmp = tempfile.TemporaryDirectory()
        store = Store(Path(tmp.name))
        accepted = store.accept(_draft("sha256:a"))
        ds = DatasetVersion(members=(DatasetMembership(
            asset_id="sha256:a", split=Split.TRAIN,
            annotation_id=accepted.annotation_id),))
        receipt = store.admit(ds)
        session = execute_training(
            config=_cfg(), admission_receipt_id=receipt.receipt_id,
            dataset=ds, declared_protected_set=set(),
            annotation_dir=store.ann_dir, event_dir=store.ev_dir,
                    receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir, data_path="/x",
            project_path="/y", base_model_path="/z", model_factory=FakeModel)
        return tmp, store, session

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
        with self.assertRaises(Exception):
            commit_execution_run(
                config=_cfg(epochs=2), session=session, environment=_env(),
                code_revision="abc", dirty=False, base_model_artifact_id=None,
                state=TrainingRunState.COMPLETED, terminal_outcome="ok",
                receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)

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
            config=cfg, session=session, environment=_env(),
            code_revision="abc", dirty=False, base_model_artifact_id=None,
            state=TrainingRunState.COMPLETED, terminal_outcome="ok",
            receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)
        self.assertEqual(run.training_config_id, cfg.training_config_id)
        self.assertEqual(run.invocation_hash,
                         canonical_hash(run.invocation_args))

    def test_RM_TRAIN06_identity_derives_from_session(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        cfg = _cfg()
        run = commit_execution_run(
            config=cfg, session=session, environment=_env(),
            code_revision="abc", dirty=False, base_model_artifact_id=None,
            state=TrainingRunState.COMPLETED, terminal_outcome="ok",
            out_dir=Path(tempfile.mkdtemp()), receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)[0]
        self.assertEqual(run.dataset_version_id, session.dataset_version_id)
        self.assertEqual(run.training_admission_receipt_id, session.admission_receipt_id)

    def test_RM_TRAIN07_receipt_identity_retained(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        run = training_run_from_execution(
            config=_cfg(), session=session, environment=_env(),
            code_revision="abc", dirty=False, base_model_artifact_id=None,
            state=TrainingRunState.COMPLETED, terminal_outcome="ok",
            receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)
        self.assertEqual(run.training_admission_receipt_id, session.admission_receipt_id)

    def test_RM_TRAIN08_failed_real_execution_preservable(self):
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        cfg = _cfg()
        run, path = commit_execution_run(
            config=cfg, session=session, environment=_env(),
            code_revision="abc", dirty=False, base_model_artifact_id=None,
            state=TrainingRunState.FAILED, terminal_outcome="failed: x",
            out_dir=Path(tempfile.mkdtemp()), receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)
        self.assertEqual(run.state, TrainingRunState.FAILED)
        self.assertTrue(path.exists())

    def test_RM_LEAK06_forged_session_cannot_mint_terminal_history(self):
        """Caller-created dataclasses have no authority even if their visible
        fields claim congruence: exact invocation and persisted receipt bind."""
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        forged = TrainingExecutionSession(
            training_config_id=session.training_config_id,
            resolved_kwargs=dict(session.resolved_kwargs),
            captured_kwargs=dict(session.resolved_kwargs), congruent=True,
            admission_receipt_id=session.admission_receipt_id,
            dataset_version_id=session.dataset_version_id,
            execution_location={**session.execution_location, "project": "/forged"})
        with self.assertRaises(Exception) as error:
            commit_execution_run(
                config=_cfg(), session=forged, environment=_env(),
                code_revision="abc", dirty=False, base_model_artifact_id=None,
                state=TrainingRunState.COMPLETED, terminal_outcome="ok",
                receipt_dir=store.receipt_dir, session_evidence_dir=store.session_evidence_dir)
        self.assertIn("INVOCATION_MISMATCH", str(error.exception))

    def test_RM_LEAK06a_session_with_invented_evidence_id_cannot_mint(self):
        """Even an exact-looking session has no terminal-run authority until
        its content-addressed execution evidence has been persisted."""
        tmp, store, session = self._executed()
        self.addCleanup(tmp.cleanup)
        forged = TrainingExecutionSession(
            training_config_id=session.training_config_id,
            resolved_kwargs=dict(session.resolved_kwargs),
            captured_kwargs=dict(session.captured_kwargs), congruent=True,
            admission_receipt_id=session.admission_receipt_id,
            dataset_version_id=session.dataset_version_id,
            execution_location=dict(session.execution_location),
            session_evidence_id="execution:" + "0" * 64)
        with self.assertRaises(Exception) as error:
            commit_execution_run(
                config=_cfg(), session=forged, environment=_env(),
                code_revision="abc", dirty=False, base_model_artifact_id=None,
                state=TrainingRunState.COMPLETED, terminal_outcome="ok",
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


if __name__ == "__main__":
    unittest.main()
