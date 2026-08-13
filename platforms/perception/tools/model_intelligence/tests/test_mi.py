"""perception-model-intelligence falsifiers (MI-01..MI-18)."""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from tools.model_intelligence import mi
from tools.model_intelligence.mi import (
    KEEP_CANONICAL, KEEP_DIAGNOSTIC, DERIVED_DISPOSABLE, UNKNOWN_REVIEW,
    SourceSnapshot, classify_artifact, derive_snapshot, explain_chart,
    explain_metric, is_stale, render_compare_md, render_current_md,
    write_reports,
)


class ClassificationTests(unittest.TestCase):
    def test_MI01_best_pt_cannot_imply_active(self):
        """weights/best.pt is framework output (DIAGNOSTIC) — the renderers
        contain no path from a filename to ACTIVE."""
        self.assertEqual(classify_artifact("runs/ultralytics/x/weights/best.pt"),
                         KEEP_DIAGNOSTIC)
        src = open(mi.__file__, encoding="utf-8").read()
        self.assertNotIn("ACTIVE\" if", src)
        self.assertNotIn("best.pt\" -> ACTIVE", src)

    def test_MI05_canonical_values_override_filenames(self):
        """Report identity comes from manifests, never file names."""
        snap = SourceSnapshot("deploy:x", "cand:y", "trun:z", "run:w")
        md = render_current_md(snap)
        self.assertIn("deploy:x", md)
        self.assertIn("cand:y", md)
        self.assertNotIn("best.pt", md.split("## 当前生产部署")[1]
                         .split("## 最新 Candidate")[0])

    def test_MI06_unknown_file_review_before_delete(self):
        self.assertEqual(classify_artifact("runs/x/some/mystery.dat"),
                         UNKNOWN_REVIEW)
        self.assertEqual(classify_artifact("totally-unknown.bin"), UNKNOWN_REVIEW)

    def test_MI07_canonical_artifact_never_disposable(self):
        self.assertEqual(
            classify_artifact("model-store/0f72dd1c.pt"), KEEP_CANONICAL)
        self.assertEqual(
            classify_artifact("manifests/runs/abc.json"), KEEP_CANONICAL)

    def test_MI08_materialization_is_derived(self):
        self.assertEqual(classify_artifact("mini-data/images/train/a.png"),
                         DERIVED_DISPOSABLE)

    def test_MI09_reports_do_not_mutate_canonical_files(self):
        """write_reports only writes into the reports dir."""
        src = open(mi.__file__, encoding="utf-8").read()
        self.assertIn("REPORTS_DIR", src)
        self.assertNotIn("TRAINING_MANIFESTS.write", src)
        self.assertNotIn("manifests.write", src)

    def test_MI18_regeneration_overwrites_only_derived_markdown(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp)
            snap = SourceSnapshot("d", "c", "t", "r")
            written = write_reports(reports_dir=out, snapshot=snap)
            for p in written.values():
                self.assertEqual(p.suffix, ".md")
                self.assertIn(str(out), str(p))
            # second write overwrites fine (derived view)
            written2 = write_reports(reports_dir=out, snapshot=snap)
            self.assertEqual(set(written), set(written2))


class ReportLanguageTests(unittest.TestCase):
    def _snap(self) -> SourceSnapshot:
        return SourceSnapshot("deploy:d", "cand:c", "trun:t", "run:r")

    def test_MI03_no_release_decision_not_established(self):
        md = render_current_md(self._snap())
        self.assertIn("AUTHORITATIVE RELEASE DECISION: NOT ESTABLISHED", md)
        for banned in ("RELEASED", "APPROVED", "REJECTED —"):
            self.assertNotIn(banned, md)

    def test_MI04_legacy_provenance_stays_partial(self):
        md = render_current_md(self._snap())
        # ACTIVE provenance is read from canonical truth; when absent it is
        # rendered as legacy-partial — never upgraded to complete.
        self.assertIn("provenance stance", md)
        self.assertNotIn("TRAINING_LINEAGE_LINKED", md.split("## 当前生产部署")[1]
                         .split("## 最新 Candidate")[0])

    def test_MI12_compare_prints_not_release_comparison(self):
        a = {"trainingRunId": "trun:a", "state": "COMPLETED",
             "datasetVersionId": "dataset:x", "trainingConfigId": "tcfg:x"}
        b = {"trainingRunId": "trun:b", "state": "COMPLETED",
             "datasetVersionId": "dataset:y", "trainingConfigId": "tcfg:y"}
        md = render_compare_md(a, b, self._snap())
        self.assertIn("TRAINING-RUN COMPARISON IS NOT RELEASE COMPARISON", md)
        self.assertNotIn("should replace ACTIVE", md)

    def test_MI02_higher_map_not_superiority(self):
        """The helper never PARSES metric values to rank runs —
        superiority statements are structurally impossible."""
        src = open(mi.__file__, encoding="utf-8").read()
        self.assertNotIn("csv.reader", src)        # no CSV parsing
        self.assertNotIn("import csv", src)
        self.assertNotIn("read_csv", src)
        self.assertNotIn("trainingMetrics", src)   # no metric reading
        self.assertNotIn("superior", src)
        self.assertNotIn("better than ACTIVE", src)
        self.assertNotIn("should replace ACTIVE", src)

    def test_MI13_chart_explanation_diagnostic_not_release(self):
        for name in ("results.png", "PR_curve.png", "confusion_matrix.png"):
            text = explain_chart(name)
            self.assertIn("训练诊断", text)
            self.assertIn("不是发布证据", text)
        for name in ("train_batch0.jpg", "val_batch0_labels.jpg"):
            self.assertIn("不是发布证据", explain_chart(name))

    def test_metric_language_has_disclaimer(self):
        for m in ("Precision", "Recall", "mAP"):
            self.assertIn("不直接决定是否上线", explain_metric(m))

    def test_MI14_candidate_without_evaluation_not_validated(self):
        md = render_current_md(self._snap())
        # candidate section must never CLAIM validated status
        cand_section = md.split("## 最新 Candidate")[1].split("## 最近一次训练")[0]
        self.assertNotIn("status: `VALIDATED`", cand_section)
        self.assertNotIn("已验证", cand_section)
        self.assertNotIn("VALIDATED（", cand_section)
        # and it must state the evaluation state explicitly
        self.assertIn("Evaluation state", cand_section)

    def test_MI15_evaluation_without_release_policy_no_conclusion(self):
        md = render_current_md(self._snap())
        self.assertIn("NOT ESTABLISHED", md.split("## Release 状态")[1])

    def test_MI16_full_ids_traceable(self):
        md = render_current_md(self._snap())
        self.assertIn("deploy:d", md)
        self.assertIn("cand:c", md)
        self.assertIn("trun:t", md)

    def test_MI17_display_version_never_in_identity(self):
        """SourceSnapshot contains canonical IDs only — no display strings."""
        snap = self._snap()
        line = snap.to_line()
        self.assertNotIn("V1", line)
        self.assertNotIn("best", line)
        self.assertNotIn("latest", line)


class StaleDetectionTests(unittest.TestCase):
    def test_MI10_report_stale_when_ids_change(self):
        snap_a = SourceSnapshot("deploy:a", "cand:a", "trun:a", "run:a")
        snap_b = SourceSnapshot("deploy:b", "cand:a", "trun:a", "run:a")
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp)
            write_reports(reports_dir=out, snapshot=snap_a)
            report = out / "CURRENT.md"
            self.assertFalse(is_stale(report, snap_a))
            self.assertTrue(is_stale(report, snap_b))

    def test_MI11_same_snapshot_deterministic_content(self):
        snap = SourceSnapshot("d", "c", "t", "r")
        self.assertEqual(render_current_md(snap), render_current_md(snap))
        self.assertEqual(render_compare_md({}, {}, snap),
                         render_compare_md({}, {}, snap))


if __name__ == "__main__":
    unittest.main()
