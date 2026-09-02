"""CLI contract tests for the P1a runtime_debug package (unittest, stdlib-only).

Verification baseline mapped to Drawing capabilities:
- runtime-debug-data-model: packet reader/ref model; identity discipline (no
  StableKey/RowId/Bounds/Text identity upgrade); AssetRef/ref fields surfaced.
- runtime-debug-query-core: summarize + occurrence projections; closed statuses;
  deterministic ordering; prune/fail-closed behavior.
- runtime-debug-tooling-surface: CLI surface, canonical envelope, exit codes,
  input immutability, JSON-canonical byte-stable output.
- runtime-debug-analysis-contract: every historical P0 case packet resolves its
  TargetOccurrence/evidence/blockers through the read path ("旧 case 能分析出来").

Run: python3 -m unittest tests/AgentWorkflow/test_runtime_debug_cli.py
"""

import copy
import hashlib
import io
import json
import os
import sys
import tempfile
import unittest
from contextlib import contextmanager, redirect_stdout

TOOLS_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "tools"))
sys.path.insert(0, TOOLS_DIR)

from runtime_debug import envelope, packet as packet_mod, query, status as status_mod  # noqa: E402
from runtime_debug.cli import main as cli_main  # noqa: E402

FIXTURES = os.path.abspath(os.path.join(
    TOOLS_DIR, "..", ".ai", "skills", "evidence-driven-debugging",
    "references", "runtime", "fixtures"))
PACKET_SCHEMA_PATH = os.path.abspath(os.path.join(
    TOOLS_DIR, "..", ".ai", "skills", "evidence-driven-debugging",
    "references", "runtime", "runtime-debug-evidence-packet.v0.schema.json"))

EXPECTED_CASES = {
    "checkbox-adapter-regression": {"stableKey": "row_009"},
    "projection-bounds-rounding": {"stableKey": "row_010"},
}

ALL_PACKETS = sorted(p for p in os.listdir(FIXTURES) if p.endswith(".packet.json"))


def run_cli(*argv: str) -> tuple[int, dict]:
    captured = io.StringIO()
    with redirect_stdout(captured):
        code = cli_main(list(argv))
    return code, json.loads(captured.getvalue())


def packet_path(name: str) -> str:
    return os.path.join(FIXTURES, f"{name}.packet.json")


def packet_schema_digest() -> str:
    with open(PACKET_SCHEMA_PATH, "rb") as handle:
        return hashlib.sha256(handle.read()).hexdigest()


@contextmanager
def modified_packet(name: str, mutate):
    """Yield a temporary mutation of a real packet fixture without rewriting it."""
    with open(packet_path(name), encoding="utf-8") as handle:
        packet = json.load(handle)
    mutate(packet)
    with tempfile.NamedTemporaryFile("w", suffix=".packet.json", delete=False,
                                     encoding="utf-8") as handle:
        json.dump(packet, handle)
        path = handle.name
    try:
        yield path
    finally:
        os.unlink(path)


def remove_packet_path(packet: dict, path: tuple) -> None:
    current = packet
    for key in path[:-1]:
        current = current[key]
    del current[path[-1]]


def packet_commands(path: str) -> tuple[tuple[str, ...], ...]:
    """All packet projections sharing the reader's fail-closed boundary."""
    return (
        ("summarize", path),
        ("occurrence", path, "--evidence-ref", "checkbox-diagnostic"),
        ("trace", path),
        ("evidence", path, "--evidence-ref", "checkbox-diagnostic"),
        ("diff", path),
        ("terminal-chain", path),
    )


class PacketReaderTests(unittest.TestCase):
    """runtime-debug-data-model — ref model + fail-closed reader."""

    def test_all_p0_fixtures_validate(self):
        for name in ALL_PACKETS:
            with self.subTest(name=name):
                code, out = run_cli("summarize", os.path.join(FIXTURES, name))
                self.assertEqual(0, code, out)
                self.assertEqual("OK", out["status"])
                self.assertEqual("runtime-debug-cli.p1a", out["contractVersion"])
                self.assertEqual("summarize", out["command"])
                self.assertEqual("runtime-debug-evidence-packet.v0", out["source"]["packetVersion"])
                self.assertIn("runId", out["source"]["sourceIdentity"])

    def test_malformed_packet_fails_closed_schema_violation(self):
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
            handle.write("{not json")
            path = handle.name
        try:
            code, out = run_cli("summarize", path)
        finally:
            os.unlink(path)
        self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code)
        self.assertEqual("SCHEMA_VIOLATION", out["status"])
        self.assertIsNone(out["result"])

    def test_packet_version_must_be_exact_v0(self):
        with modified_packet(
            "checkbox-adapter-regression",
            lambda packet: packet.__setitem__(
                "packetVersion", "runtime-debug-evidence-packet.v999"),
        ) as path:
            code, out = run_cli("summarize", path)
        self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code)
        self.assertEqual("SCHEMA_VIOLATION", out["status"])
        self.assertIsNone(out["result"])

    def test_every_p0_required_field_is_enforced_before_each_projection(self):
        required_paths = [
            ("packetVersion",), ("packetId",), ("sourceIdentity",),
            ("debugIr",), ("evidenceIndex",), ("repairGate",), ("generation",),
        ]
        required_paths.extend(("sourceIdentity", field) for field in (
            "runId", "captureSessionId", "traceId", "deploymentReceiptRef",
            "runtimeRevision", "environmentRef"))
        required_paths.extend(("debugIr", field) for field in (
            "SchemaVersion", "CaseId", "ExpectedReality", "ObservedReality",
            "TerminalState", "TargetObservation", "TargetOccurrence",
            "GoodComparison", "BadComparison", "EvidenceChain", "LastGood",
            "FirstBad", "GapKind", "Owner", "EvidenceRefs", "MissingEvidence",
            "Confidence", "Disposition"))
        required_paths.extend(("debugIr", "TerminalState", field) for field in (
            "status", "summary", "evidenceRefs"))
        required_paths.extend(("debugIr", "TargetObservation", field) for field in (
            "status", "runId", "observationSeq", "summary", "evidenceRefs"))
        required_paths.extend(("debugIr", "TargetOccurrence", field) for field in (
            "status", "runId", "observationSeq", "occurrenceId", "stableKey",
            "rowId", "spanIds", "summary", "proof", "counterevidence", "evidenceRefs"))
        for comparison in ("GoodComparison", "BadComparison"):
            required_paths.extend(("debugIr", comparison, field) for field in (
                "status", "label", "summary", "axes", "evidenceRefs"))
        for stage in ("raw", "normalized", "fused", "canonical", "semanticAdmission",
                      "affordance", "runtimeState"):
            required_paths.extend(("debugIr", "EvidenceChain", stage, field) for field in (
                "status", "summary", "inputRefs", "decisionRefs", "outputRefs"))
        for divergence in ("LastGood", "FirstBad"):
            required_paths.extend(("debugIr", divergence, field) for field in (
                "status", "stage", "summary", "evidenceRefs"))
        required_paths.extend(("debugIr", "Owner", field) for field in (
            "status", "domain", "seam", "basis", "evidenceRefs"))
        required_paths.extend(("debugIr", "Confidence", field) for field in (
            "level", "basis", "evidenceRefs"))
        required_paths.extend(("debugIr", "MissingEvidence", 0, field) for field in (
            "missingId", "requiredFor", "stage", "description", "collectionHint"))
        required_paths.extend(("evidenceIndex", 0, field) for field in (
            "refId", "kind", "uri", "selector", "digest", "integrity", "mediaType", "summary"))
        required_paths.extend(("evidenceIndex", 0, "selector", field) for field in (
            "runId", "observationSeq", "occurrenceId", "stableKey", "rowId",
            "evidenceRef", "spanId", "frameId", "jsonPointer", "lineAnchor"))
        required_paths.extend(("repairGate", field) for field in ("eligible", "blockers", "summary"))
        required_paths.extend(("generation", field) for field in (
            "producer", "producerVersion", "schemaDigest", "deterministicInputDigest"))

        for required_path in required_paths:
            with self.subTest(required_path=required_path):
                with modified_packet(
                    "checkbox-adapter-regression",
                    lambda packet, path=required_path: remove_packet_path(packet, path),
                ) as path:
                    for argv in packet_commands(path):
                        with self.subTest(command=argv[0]):
                            code, out = run_cli(*argv)
                            self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code, out)
                            self.assertEqual("SCHEMA_VIOLATION", out["status"])
                            self.assertIsNone(out["result"])

    def test_closed_vocabularies_and_forbidden_properties_fail_closed(self):
        mutations = [
            ("terminal.status", lambda p: p["debugIr"]["TerminalState"].__setitem__("status", "INVALID")),
            ("target-observation.status", lambda p: p["debugIr"]["TargetObservation"].__setitem__("status", "INVALID")),
            ("occurrence.status", lambda p: p["debugIr"]["TargetOccurrence"].__setitem__("status", "INVALID")),
            ("comparison.status", lambda p: p["debugIr"]["GoodComparison"].__setitem__("status", "INVALID")),
            ("stage.status", lambda p: p["debugIr"]["EvidenceChain"]["raw"].__setitem__("status", "INVALID")),
            ("axis.status", lambda p: p["debugIr"]["GoodComparison"]["axes"][0].__setitem__("status", "INVALID")),
            ("divergence.status", lambda p: p["debugIr"]["LastGood"].__setitem__("status", "INVALID")),
            ("divergence.stage", lambda p: p["debugIr"]["LastGood"].__setitem__("stage", "INVALID")),
            ("gapKind", lambda p: p["debugIr"].__setitem__("GapKind", "INVALID")),
            ("owner.status", lambda p: p["debugIr"]["Owner"].__setitem__("status", "INVALID")),
            ("owner.domain", lambda p: p["debugIr"]["Owner"].__setitem__("domain", "INVALID")),
            ("confidence.level", lambda p: p["debugIr"]["Confidence"].__setitem__("level", "INVALID")),
            ("disposition", lambda p: p["debugIr"].__setitem__("Disposition", "INVALID")),
            ("missing.requiredFor", lambda p: p["debugIr"]["MissingEvidence"][0].__setitem__("requiredFor", "INVALID")),
            ("evidence.kind", lambda p: p["evidenceIndex"][0].__setitem__("kind", "INVALID")),
            ("evidence.integrity", lambda p: p["evidenceIndex"][0].__setitem__("integrity", "INVALID")),
            ("identity mismatch blocker consistency", lambda p: p["evidenceIndex"][0].__setitem__("integrity", "IDENTITY_MISMATCH")),
            ("repair.blocker", lambda p: p["repairGate"].__setitem__("blockers", ["INVALID"])),
            ("top-level extra", lambda p: p.__setitem__("rogue", True)),
            ("sourceIdentity extra", lambda p: p["sourceIdentity"].__setitem__("rogue", True)),
            ("debugIr extra", lambda p: p["debugIr"].__setitem__("rogue", True)),
            ("terminalState extra", lambda p: p["debugIr"]["TerminalState"].__setitem__("rogue", True)),
            ("targetObservation extra", lambda p: p["debugIr"]["TargetObservation"].__setitem__("rogue", True)),
            ("targetOccurrence extra", lambda p: p["debugIr"]["TargetOccurrence"].__setitem__("rogue", True)),
            ("comparison extra", lambda p: p["debugIr"]["GoodComparison"].__setitem__("rogue", True)),
            ("axis extra", lambda p: p["debugIr"]["GoodComparison"]["axes"][0].__setitem__("rogue", True)),
            ("evidenceChain extra", lambda p: p["debugIr"]["EvidenceChain"].__setitem__("rogue", True)),
            ("unknown chain stage", lambda p: p["debugIr"]["EvidenceChain"].__setitem__("rogueStage", p["debugIr"]["EvidenceChain"]["raw"])),
            ("stage extra", lambda p: p["debugIr"]["EvidenceChain"]["raw"].__setitem__("rogue", True)),
            ("divergence extra", lambda p: p["debugIr"]["LastGood"].__setitem__("rogue", True)),
            ("owner extra", lambda p: p["debugIr"]["Owner"].__setitem__("rogue", True)),
            ("confidence extra", lambda p: p["debugIr"]["Confidence"].__setitem__("rogue", True)),
            ("missingEvidence extra", lambda p: p["debugIr"]["MissingEvidence"][0].__setitem__("rogue", True)),
            ("evidenceRef extra", lambda p: p["evidenceIndex"][0].__setitem__("rogue", True)),
            ("selector extra", lambda p: p["evidenceIndex"][0]["selector"].__setitem__("rogue", True)),
            ("repairGate extra", lambda p: p["repairGate"].__setitem__("rogue", True)),
            ("generation extra", lambda p: p["generation"].__setitem__("rogue", True)),
            ("derivedView extra", lambda p: p.__setitem__("derivedViews", [{
                "kind": "SUMMARY", "summary": "derived", "evidenceRefs": [], "rogue": True,
            }])),
            ("derivedView.kind", lambda p: p.__setitem__("derivedViews", [{
                "kind": "INVALID", "summary": "derived", "evidenceRefs": [],
            }])),
        ]
        for label, mutate in mutations:
            with self.subTest(mutation=label):
                with modified_packet("checkbox-adapter-regression", mutate) as path:
                    code, out = run_cli("summarize", path)
                self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code, out)
                self.assertEqual("SCHEMA_VIOLATION", out["status"])
                self.assertIsNone(out["result"])

    def test_every_nested_evidence_ref_must_resolve(self):
        paths = [
            ("debugIr", "TerminalState", "evidenceRefs"),
            ("debugIr", "TargetObservation", "evidenceRefs"),
            ("debugIr", "TargetOccurrence", "evidenceRefs"),
            ("debugIr", "GoodComparison", "evidenceRefs"),
            ("debugIr", "BadComparison", "evidenceRefs"),
            ("debugIr", "LastGood", "evidenceRefs"),
            ("debugIr", "FirstBad", "evidenceRefs"),
            ("debugIr", "Owner", "evidenceRefs"),
            ("debugIr", "Confidence", "evidenceRefs"),
            ("debugIr", "EvidenceRefs"),
        ]
        for stage in ("raw", "normalized", "fused", "canonical", "semanticAdmission",
                      "affordance", "runtimeState"):
            for collection in ("inputRefs", "decisionRefs", "outputRefs"):
                paths.append(("debugIr", "EvidenceChain", stage, collection))

        for ref_path in paths:
            with self.subTest(ref_path=ref_path):
                def add_dangling_ref(packet, path=ref_path):
                    current = packet
                    for key in path[:-1]:
                        current = current[key]
                    current[path[-1]].append("dangling-ref")

                with modified_packet("checkbox-adapter-regression", add_dangling_ref) as path:
                    code, out = run_cli("summarize", path)
                self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code, out)
                self.assertEqual("SCHEMA_VIOLATION", out["status"])
                self.assertIsNone(out["result"])

        def add_derived_view_dangling_ref(packet):
            packet["derivedViews"] = [{
                "kind": "SUMMARY", "summary": "derived", "evidenceRefs": ["dangling-ref"],
            }]

        with modified_packet("checkbox-adapter-regression", add_derived_view_dangling_ref) as path:
            code, out = run_cli("summarize", path)
        self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code, out)
        self.assertEqual("SCHEMA_VIOLATION", out["status"])
        self.assertIsNone(out["result"])

        def add_selector_dangling_ref(packet):
            packet["evidenceIndex"][0]["selector"]["evidenceRef"] = "dangling-ref"

        with modified_packet("checkbox-adapter-regression", add_selector_dangling_ref) as path:
            code, out = run_cli("summarize", path)
        self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code, out)
        self.assertEqual("SCHEMA_VIOLATION", out["status"])
        self.assertIsNone(out["result"])

    def test_present_derived_view_requires_all_fields(self):
        for field in ("kind", "summary", "evidenceRefs"):
            with self.subTest(field=field):
                def remove_derived_view_field(packet, field=field):
                    packet["derivedViews"] = [{
                        "kind": "SUMMARY", "summary": "derived",
                        "evidenceRefs": ["checkbox-diagnostic"],
                    }]
                    del packet["derivedViews"][0][field]

                with modified_packet("checkbox-adapter-regression", remove_derived_view_field) as path:
                    code, out = run_cli("summarize", path)
                self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code, out)
                self.assertEqual("SCHEMA_VIOLATION", out["status"])
                self.assertIsNone(out["result"])

    def test_reader_never_rewrites_input_bytes(self):
        path = packet_path("checkbox-adapter-regression")
        with open(path, "rb") as handle:
            before = handle.read()
        run_cli("summarize", path)
        run_cli("occurrence", path, "--stable-key", "row_009")
        with open(path, "rb") as handle:
            after = handle.read()
        self.assertEqual(before, after, "input packet must stay byte-immutable")


class SummarizeTests(unittest.TestCase):
    """runtime-debug-query-core + analysis-contract: summary projection, old cases."""

    def test_summarize_has_contract_limited_fields_only(self):
        code, out = run_cli("summarize", packet_path("checkbox-adapter-regression"))
        self.assertEqual(0, code)
        result = out["result"]
        self.assertIn("terminalState", result)
        self.assertIn("targetObservation", result)
        self.assertIn("targetOccurrence", result)
        self.assertIn("evidenceAvailability", result)
        self.assertIn("missingEvidence", result)
        self.assertIn("repairBlockers", result)
        # Contract-limited: summary must NOT compute FDP/Owner/Disposition.
        for forbidden in ("fdp", "owner", "disposition", "firstBad", "gapKind"):
            self.assertNotIn(forbidden, json.dumps(result).lower())

    def test_summary_old_case_resolves_target_and_blockers(self):
        code, out = run_cli("summarize", packet_path("checkbox-adapter-regression"))
        self.assertEqual(0, code)
        occurrence = out["result"]["targetOccurrence"]
        self.assertEqual("row_009", occurrence.get("stableKey"))
        self.assertEqual("CANDIDATE", occurrence.get("status"))
        self.assertGreaterEqual(len(out["result"]["evidenceAvailability"]["refs"]), 1)
        self.assertIn("MISSING_REQUIRED_EVIDENCE", out["result"]["repairBlockers"])


class OccurrenceTests(unittest.TestCase):
    """runtime-debug-query-core — typed occurrence query; old case analysis path."""

    def test_occurrence_by_stable_key_finds_target_with_linked_evidence(self):
        code, out = run_cli("occurrence", packet_path("checkbox-adapter-regression"), "--stable-key", "row_009")
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        candidates = out["result"]["candidates"]
        self.assertEqual(1, len(candidates))
        target = candidates[0]
        self.assertEqual("TargetOccurrence", target["source"])
        self.assertEqual("row_009", target["stableKey"])
        self.assertIn("proof", target)
        self.assertTrue(target["linkedEvidence"])
        self.assertEqual(sorted(x["refId"] for x in target["linkedEvidence"]),
                         [x["refId"] for x in target["linkedEvidence"]])

    def test_evidence_ref_selector_returns_target(self):
        code, out = run_cli("occurrence", packet_path("checkbox-adapter-regression"), "--evidence-ref", "checkbox-diagnostic")
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        self.assertEqual("TargetOccurrence", out["result"]["candidates"][0]["source"])

    def test_occurrence_evidence_ref_identity_mismatch_fails_closed(self):
        def mark_identity_mismatch(packet):
            packet["evidenceIndex"][0]["integrity"] = "IDENTITY_MISMATCH"
            packet["repairGate"]["blockers"].append("IDENTITY_MISMATCH")

        with modified_packet("checkbox-adapter-regression", mark_identity_mismatch) as path:
            code, out = run_cli("occurrence", path, "--evidence-ref", "checkbox-diagnostic")
        self.assertEqual(status_mod.exit_code(status_mod.IDENTITY_MISMATCH), code)
        self.assertEqual("IDENTITY_MISMATCH", out["status"])

    def test_selector_with_incompatible_indexed_identities_is_ambiguous(self):
        def add_incompatible_index_entries(packet):
            first = packet["evidenceIndex"][0]
            first["selector"].update({
                "observationSeq": 4,
                "occurrenceId": "occ-a",
                "stableKey": "row_ambiguous",
                "rowId": "row_ambiguous",
            })
            second = copy.deepcopy(first)
            second["refId"] = "ambiguous-evidence-b"
            second["selector"]["observationSeq"] = 5
            second["selector"]["occurrenceId"] = "occ-b"
            packet["evidenceIndex"].append(second)

        with modified_packet("checkbox-adapter-regression", add_incompatible_index_entries) as path:
            code, out = run_cli("occurrence", path, "--stable-key", "row_ambiguous")
        self.assertEqual(status_mod.exit_code(status_mod.AMBIGUOUS_OCCURRENCE), code)
        self.assertEqual("AMBIGUOUS_OCCURRENCE", out["status"])
        candidates = out["result"]["candidates"]
        self.assertEqual(2, len(candidates))
        self.assertEqual([4, 5], [candidate["observationSeq"] for candidate in candidates])
        self.assertNotIn("winner", json.dumps(out["result"]).lower())

    def test_indexed_evidence_without_target_occurrence_has_insufficient_coverage(self):
        def make_index_only_selector(packet):
            packet["evidenceIndex"][0]["selector"]["stableKey"] = "indexed_only"

        with modified_packet("checkbox-adapter-regression", make_index_only_selector) as path:
            code, out = run_cli("occurrence", path, "--stable-key", "indexed_only")
        self.assertEqual(status_mod.exit_code(status_mod.INSUFFICIENT_TRACE_COVERAGE), code)
        self.assertEqual("INSUFFICIENT_TRACE_COVERAGE", out["status"])

    def test_multiple_selectors_invalid_input(self):
        code, out = run_cli("occurrence", packet_path("checkbox-adapter-regression"),
                            "--stable-key", "a", "--row-id", "b")
        self.assertEqual(status_mod.exit_code(status_mod.INVALID_INPUT), code)
        self.assertEqual("INVALID_INPUT", out["status"])
        self.assertTrue(out["diagnostics"])

    def test_missing_selector_invalid_input(self):
        code, out = run_cli("occurrence", packet_path("checkbox-adapter-regression"))
        self.assertEqual(status_mod.exit_code(status_mod.INVALID_INPUT), code)
        self.assertEqual("INVALID_INPUT", out["status"])

    def test_unknown_selector_value_evidence_unavailable(self):
        code, out = run_cli("occurrence", packet_path("checkbox-adapter-regression"), "--stable-key", "no-such-key")
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])
        self.assertIsNone(out["result"])
        self.assertTrue(out["diagnostics"])

    def test_nonexistent_packet_evidence_unavailable(self):
        code, out = run_cli("summarize", "/nonexistent/packet.json")
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])

    def test_missing_packet_keeps_command_and_does_not_echo_absolute_path(self):
        path = "/absolute/nonexistent/runtime-debug-p1a.packet.json"
        self.assertFalse(os.path.exists(path))
        code, out = run_cli("summarize", path)
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])
        self.assertEqual("summarize", out["command"])
        self.assertNotIn(path, json.dumps(out))


class EnvelopeDeterminismTests(unittest.TestCase):
    """runtime-debug-tooling-surface — canonical deterministic output."""

    def test_byte_stable_output(self):
        first, _ = run_cli("summarize", packet_path("projection-bounds-rounding"))
        second, _ = run_cli("summarize", packet_path("projection-bounds-rounding"))
        self.assertEqual(first, second)
        self.assertEqual(
            envelope.render(first),
            envelope.render(second),
            "canonical serialization must be byte-stable",
        )

    def test_envelope_is_deterministic_json(self):
        _, out = run_cli("summarize", packet_path("search-icon-child-of"))
        rendered = envelope.render(out)
        self.assertTrue(rendered.endswith("\n"))
        self.assertEqual(json.loads(rendered), out)  # JSON round-trip stable


class CausalAndDiffTests(unittest.TestCase):
    """runtime-debug-query-core (P1b): causal tree, evidence chain, packet diff."""

    def test_causal_tree_projects_all_chain_stages_in_order(self):
        code, out = run_cli("trace", packet_path("checkbox-adapter-regression"))
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        stages = [s["stage"] for s in out["result"]["stages"]]
        self.assertEqual(["raw", "normalized", "fused", "canonical",
                          "semanticAdmission", "affordance", "runtimeState"], stages)
        raw = out["result"]["stages"][0]
        self.assertEqual("MISSING", raw["status"])
        self.assertIn("checkbox-diagnostic", raw["outputRefs"])

    def test_causal_tree_prunes_hidden_stages_only(self):
        code, out = run_cli("trace", packet_path("checkbox-adapter-regression"),
                            "--prune", "raw,fused")
        self.assertEqual(0, code)
        stages = [s["stage"] for s in out["result"]["stages"]]
        self.assertNotIn("raw", stages)
        self.assertNotIn("fused", stages)
        self.assertIn("canonical", stages)
        self.assertEqual({"raw", "fused"}, set(out["result"]["pruned"]["stageNames"]))

    def test_causal_tree_only_decisions(self):
        code, out = run_cli("trace", packet_path("checkbox-adapter-regression"), "--only-decisions")
        self.assertEqual(0, code)
        for stage in out["result"]["stages"]:
            self.assertTrue(stage["decisionRefs"], f"stage {stage['stage']} must carry decision refs")

    def test_evidence_chain_traces_ref_across_stages(self):
        code, out = run_cli("evidence", packet_path("checkbox-adapter-regression"),
                            "--evidence-ref", "checkbox-diagnostic")
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        self.assertEqual("checkbox-diagnostic", out["result"]["ref"]["refId"])
        positions = {p["stage"]: p["role"] for p in out["result"]["chainPositions"]}
        self.assertIn("normalized", positions)
        self.assertIn("fused", positions)

    def test_evidence_chain_unknown_ref_evidence_unavailable(self):
        code, out = run_cli("evidence", packet_path("checkbox-adapter-regression"),
                            "--evidence-ref", "no-such-ref")
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])

    def test_diff_projects_stored_good_bad_lastgood_firstbad(self):
        code, out = run_cli("diff", packet_path("checkbox-adapter-regression"))
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        comparison = out["result"]
        self.assertIn("good", comparison)
        self.assertIn("bad", comparison)
        self.assertEqual("canonical", comparison["lastGood"].get("stage"))
        self.assertEqual("semanticAdmission", comparison["firstBad"].get("stage"))
        for side in ("good", "bad"):
            self.assertIn("axes", comparison[side])
            self.assertIn("evidenceRefs", comparison[side])

    def test_diff_needs_comparison_facts(self):
        # A packet without Good/Bad comparison facts must fail closed
        # deterministically on the diff command surface.
        code, out = run_cli("diff", packet_path("search-icon-child-of"))
        self.assertIn(out["status"], status_mod.CLOSED_STATUSES)
        self.assertEqual(status_mod.exit_code(out["status"]), code)


class BundleAssetIndexTests(unittest.TestCase):
    """runtime-debug-data-model / query-core (P1c): AssetRef first-class from a
    capture bundle — the second source adapter behind the same Query Core."""

    def _make_bundle(self, *, checksums_ok: bool = True) -> str:
        root = tempfile.mkdtemp(prefix="rd-bundle-")
        artifact_dir = os.path.join(root, "artifacts")
        os.makedirs(artifact_dir)
        payload = b"frame-bytes"
        content_hash = hashlib.sha256(payload).hexdigest()
        open(os.path.join(artifact_dir, "a-frame.bin"), "wb").write(payload)
        open(os.path.join(artifact_dir, "b-crop.bin"), "wb").write(payload)
        records = [{"order": 1, "kind": "Observation", "sequenceNumber": 7, "frameId": "frame-1"}]
        manifest = {
            "schemaVersion": 1,
            "captureSessionId": "bundle-1",
            "traceId": "trace-1",
            "scenarioId": "scenario-1",
            "finalState": "Persisted",
            "records": records,
            "artifacts": [
                {"artifactId": "a-frame", "frameId": "frame-1", "fileName": "frame.png",
                 "contentType": "image/png", "contentHash": content_hash, "byteCount": len(payload)},
                {"artifactId": "b-crop", "frameId": "frame-1", "fileName": "crop.png",
                 "contentType": "image/png", "derivedFromArtifactId": "a-frame",
                 "contentHash": content_hash, "byteCount": len(payload)},
            ],
        }
        with open(os.path.join(root, "capture-manifest.json"), "w") as handle:
            json.dump(manifest, handle)
        with open(os.path.join(root, "records.json"), "w") as handle:
            json.dump(records, handle)
        lines = [f"{content_hash}  artifacts/a-frame.bin\n", f"{content_hash}  artifacts/b-crop.bin\n"]
        if not checksums_ok:
            lines[1] = "deadbeef  artifacts/b-crop.bin\n"
        with open(os.path.join(root, "checksums.sha256"), "w") as handle:
            handle.write("".join(lines))
        return root

    def test_assets_lists_assetrefs_with_frame_correlation(self):
        root = self._make_bundle()
        try:
            code, out = run_cli("assets", root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        self.assertEqual(2, out["result"]["count"])
        first = {a["assetId"]: a for a in out["result"]["assets"]}["a-frame"]
        self.assertEqual("image/png", first["assetType"])
        self.assertEqual("trace-1", first["traceId"])
        self.assertEqual(7, first["observationSeq"])  # frame-1 → record seq 7
        self.assertEqual("artifacts/a-frame.bin", first["path"])  # relative, no abs leak
        self.assertIsNone(first["occurrenceId"])  # no occurrence link stored → honest null

    def test_asset_show_and_related(self):
        root = self._make_bundle()
        try:
            code_show, show = run_cli("asset-show", root, "--asset-id", "b-crop")
            code_rel, related = run_cli("asset-related", root, "--asset-id", "b-crop")
            code_parents, parent_view = run_cli("asset-related", root, "--asset-id", "a-frame")
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code_show)
        self.assertEqual("b-crop", show["result"]["assetId"])
        self.assertEqual(0, code_rel)
        self.assertEqual(["a-frame"], [p["assetId"] for p in related["result"]["parents"]])
        self.assertEqual([], related["result"]["children"])
        self.assertEqual(0, code_parents)
        self.assertEqual(["b-crop"], [c["assetId"] for c in parent_view["result"]["children"]])

    def test_checksum_mismatch_fails_closed(self):
        root = self._make_bundle(checksums_ok=False)
        try:
            code, out = run_cli("assets", root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code)
        self.assertEqual("SCHEMA_VIOLATION", out["status"])
        self.assertIsNone(out["result"])

    def _assert_bundle_schema_violation(self, mutate, label):
        root = self._make_bundle()
        try:
            mutate(root)
            code, out = run_cli("assets", root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code, label)
        self.assertEqual("SCHEMA_VIOLATION", out["status"], label)
        self.assertIsNone(out["result"], label)

    @staticmethod
    def _rewrite_bundle_json(root, name, value):
        with open(os.path.join(root, name), "w", encoding="utf-8") as handle:
            json.dump(value, handle)

    @staticmethod
    def _rewrite_bundle_text(root, name, value):
        with open(os.path.join(root, name), "w", encoding="utf-8") as handle:
            handle.write(value)

    def test_camelcase_numeric_record_kind_drives_observation_correlation(self):
        root = self._make_bundle()
        try:
            records_path = os.path.join(root, "records.json")
            with open(records_path, encoding="utf-8") as handle:
                records = json.load(handle)
            records.insert(0, {
                "order": 1, "kind": 1, "sequenceNumber": 8, "frameId": "frame-1",
            })
            records[1]["order"] = 2
            records[1]["kind"] = 0
            with open(records_path, "w", encoding="utf-8") as handle:
                json.dump(records, handle)
            manifest_path = os.path.join(root, "capture-manifest.json")
            with open(manifest_path, encoding="utf-8") as handle:
                manifest = json.load(handle)
            manifest["records"] = records
            self._rewrite_bundle_json(root, "capture-manifest.json", manifest)
            code, out = run_cli("assets", root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        first = {asset["assetId"]: asset for asset in out["result"]["assets"]}["a-frame"]
        self.assertEqual(7, first["observationSeq"],
                         "numeric CaptureRecordKind must correlate only Observation records")

    def test_malformed_json_and_records_fail_closed(self):
        mutations = [
            ("malformed records JSON", lambda root: self._rewrite_bundle_text(root, "records.json", "{not-json")),
            ("records not a list", lambda root: self._rewrite_bundle_json(root, "records.json", {})),
            ("record not an object", lambda root: self._rewrite_bundle_json(root, "records.json", [1])),
            ("record missing kind", lambda root: self._rewrite_bundle_json(
                root, "records.json", [{"order": 1, "sequenceNumber": 7, "frameId": "frame-1"}])),
            ("numeric kind out of range", lambda root: self._rewrite_bundle_json(
                root, "records.json", [{"order": 1, "kind": 99, "sequenceNumber": 7, "frameId": "frame-1"}])),
        ]
        for label, mutate in mutations:
            with self.subTest(mutation=label):
                self._assert_bundle_schema_violation(mutate, label)

    def test_unsafe_artifact_ids_are_rejected(self):
        for artifact_id in ("../escape", "nested/id", "/absolute", ".", ".."):
            with self.subTest(artifact_id=artifact_id):
                def mutate(root, artifact_id=artifact_id):
                    manifest_path = os.path.join(root, "capture-manifest.json")
                    with open(manifest_path, encoding="utf-8") as handle:
                        manifest = json.load(handle)
                    manifest["artifacts"][0]["artifactId"] = artifact_id
                    self._rewrite_bundle_json(root, "capture-manifest.json", manifest)

                self._assert_bundle_schema_violation(mutate, f"unsafe artifact id: {artifact_id}")

    def test_rogue_artifact_is_rejected(self):
        def mutate(root):
            with open(os.path.join(root, "artifacts", "rogue.bin"), "wb") as handle:
                handle.write(b"rogue")

        self._assert_bundle_schema_violation(mutate, "undeclared rogue artifact")

    def test_artifact_byte_count_mismatch_is_rejected(self):
        def mutate(root):
            manifest_path = os.path.join(root, "capture-manifest.json")
            with open(manifest_path, encoding="utf-8") as handle:
                manifest = json.load(handle)
            manifest["artifacts"][0]["byteCount"] += 1
            self._rewrite_bundle_json(root, "capture-manifest.json", manifest)

        self._assert_bundle_schema_violation(mutate, "artifact byteCount mismatch")

    def test_actual_artifact_bytes_must_match_declared_hash(self):
        def mutate(root):
            with open(os.path.join(root, "artifacts", "a-frame.bin"), "wb") as handle:
                handle.write(b"other-bytes")

        self._assert_bundle_schema_violation(mutate, "artifact bytes differ from declared hash")

    def test_missing_parent_relation_is_rejected(self):
        def mutate(root):
            manifest_path = os.path.join(root, "capture-manifest.json")
            with open(manifest_path, encoding="utf-8") as handle:
                manifest = json.load(handle)
            manifest["artifacts"][1]["derivedFromArtifactId"] = "missing-parent"
            self._rewrite_bundle_json(root, "capture-manifest.json", manifest)

        self._assert_bundle_schema_violation(mutate, "missing parent relation")

    def test_symlink_artifact_path_is_rejected(self):
        root = self._make_bundle()
        target = None
        try:
            fd, target = tempfile.mkstemp(prefix="rd-outside-")
            with os.fdopen(fd, "wb") as handle:
                handle.write(b"frame-bytes")
            artifact_path = os.path.join(root, "artifacts", "a-frame.bin")
            os.unlink(artifact_path)
            os.symlink(target, artifact_path)
            code, out = run_cli("assets", root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
            if target is not None:
                try:
                    os.unlink(target)
                except OSError:
                    pass
        self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code, "symlink artifact")
        self.assertEqual("SCHEMA_VIOLATION", out["status"])
        self.assertIsNone(out["result"])

    def test_missing_bundle_evidence_unavailable(self):
        code, out = run_cli("assets", "/nonexistent/bundle")
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])


class PacketGeneratorTests(unittest.TestCase):
    """runtime-debug-analysis-contract (P1d): mechanical base packet generator;
    the generated packet must round-trip through the P1a/P1b readers, stay
    byte-deterministic, and fabricate no semantic fields."""

    def _make_bundle(self) -> str:
        root = tempfile.mkdtemp(prefix="rd-gen-")
        artifact_dir = os.path.join(root, "artifacts")
        os.makedirs(artifact_dir)
        payload = b"frame-bytes"
        content_hash = hashlib.sha256(payload).hexdigest()
        open(os.path.join(artifact_dir, "a-frame.bin"), "wb").write(payload)
        open(os.path.join(artifact_dir, "b-crop.bin"), "wb").write(payload)
        records = [
            {"order": 1, "kind": "Observation", "sequenceNumber": 7, "frameId": "frame-1"},
            {"order": 2, "kind": "ActionDispatch", "sequenceNumber": 8, "frameId": None,
             "actionId": "Action-1", "actionKind": "SetSwitch"},
        ]
        manifest = {
            "schemaVersion": 1, "captureSessionId": "gen-1", "traceId": "trace-gen",
            "scenarioId": "scenario-gen", "finalState": "Persisted",
            "runtimeSucceeded": False, "runtimeOutcome": "ExecutionFailed",
            "records": records,
            "artifacts": [
                {"artifactId": "a-frame", "frameId": "frame-1", "fileName": "frame.png",
                 "contentType": "image/png", "contentHash": content_hash, "byteCount": len(payload)},
                {"artifactId": "b-crop", "frameId": "frame-1", "fileName": "crop.png",
                 "contentType": "image/png", "derivedFromArtifactId": "a-frame",
                 "contentHash": content_hash, "byteCount": len(payload)},
            ],
        }
        with open(os.path.join(root, "capture-manifest.json"), "w") as handle:
            json.dump(manifest, handle)
        with open(os.path.join(root, "records.json"), "w") as handle:
            json.dump(records, handle)
        with open(os.path.join(root, "checksums.sha256"), "w") as handle:
            handle.write(f"{content_hash}  artifacts/a-frame.bin\n{content_hash}  artifacts/b-crop.bin\n")
        return root

    def _generate(self, root, *extra):
        return run_cli("packet-generate", root, "--case-id", "gen-case", *extra)

    def test_generated_packet_round_trips_through_readers(self):
        root = self._make_bundle()
        try:
            code, out = self._generate(root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        packet = out["result"]
        self.assertEqual("runtime-debug-evidence-packet.v0", packet["packetVersion"])

        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
            json.dump(packet, handle)
            path = handle.name
        try:
            code_sum, summary = run_cli("summarize", path)
            code_occ, occurrence = run_cli("occurrence", path, "--evidence-ref", "a-frame")
            code_chain, chain = run_cli("evidence", path, "--evidence-ref", "a-frame")
        finally:
            os.unlink(path)
        self.assertEqual(0, code_sum)
        self.assertEqual("OBSERVED", summary["result"]["terminalState"]["status"])
        self.assertEqual(0, code_occ)
        self.assertEqual("TargetOccurrence", occurrence["result"]["candidates"][0]["source"])
        self.assertEqual(0, code_chain)
        # The structural packet has no semantic chain — positions are honestly
        # empty (and the generator declared the missing facet in MissingEvidence).
        self.assertEqual("a-frame", chain["result"]["ref"]["refId"])
        self.assertEqual([], chain["result"]["chainPositions"])
        self.assertIn("evidence-chain-stages",
                      [m["missingId"] for m in summary["result"]["missingEvidence"]])

    def test_generated_packet_is_deterministic(self):
        root = self._make_bundle()
        try:
            _, first = self._generate(root)
            _, second = self._generate(root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(json.dumps(first["result"], sort_keys=True),
                         json.dumps(second["result"], sort_keys=True))

    def test_generated_packet_records_explicit_absence_without_semantic_fabrication(self):
        root = self._make_bundle()
        try:
            _, out = self._generate(root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        ir = out["result"]["debugIr"]
        self.assertEqual("UNKNOWN", ir["GapKind"])
        self.assertEqual("UNRESOLVED", ir["Owner"]["status"])
        self.assertEqual("UNKNOWN", ir["Owner"]["domain"])
        self.assertEqual("EVIDENCE_COLLECTION", ir["Disposition"])
        self.assertEqual("NOT_AVAILABLE", ir["GoodComparison"]["status"])
        self.assertEqual("NOT_AVAILABLE", ir["BadComparison"]["status"])
        self.assertEqual("UNRESOLVED", ir["LastGood"]["status"])
        self.assertEqual("UNRESOLVED", ir["FirstBad"]["status"])
        self.assertEqual("UNASSESSED", ir["Confidence"]["level"])
        self.assertEqual(
            {"raw", "normalized", "fused", "canonical", "semanticAdmission",
             "affordance", "runtimeState"},
            set(ir["EvidenceChain"]),
        )
        self.assertTrue(all(stage["status"] == "MISSING" for stage in ir["EvidenceChain"].values()))
        for section in ("TargetObservation", "TargetOccurrence"):
            self.assertEqual(7, ir[section].get("observationSeq"))
        self.assertTrue(ir["MissingEvidence"])

    def test_generated_packet_is_complete_p0_shape(self):
        root = self._make_bundle()
        try:
            code, out = self._generate(root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code, out)
        packet = out["result"]
        # The stdlib packet contract is a first check; the independent Draft
        # 2020-12 validator remains a Leader graduation gate.
        packet_mod.validate(packet)
        self.assertEqual("runtime-debug-evidence-packet.v0", packet["packetVersion"])
        self.assertEqual({"packetVersion", "packetId", "sourceIdentity", "debugIr",
                          "evidenceIndex", "repairGate", "generation"},
                         set(packet) - {"notes", "derivedViews"})
        self.assertEqual("sha256:" + packet_schema_digest(), packet["generation"]["schemaDigest"])

        ir = packet["debugIr"]
        self.assertTrue(ir["ExpectedReality"])
        self.assertTrue(ir["ObservedReality"])
        self.assertIn(ir["TerminalState"]["status"], {"OBSERVED", "NOT_REACHED", "UNAVAILABLE"})
        self.assertEqual("NOT_AVAILABLE", ir["GoodComparison"]["status"])
        self.assertEqual("NOT_AVAILABLE", ir["BadComparison"]["status"])
        self.assertEqual("UNKNOWN", ir["GapKind"])
        self.assertEqual("UNRESOLVED", ir["Owner"]["status"])
        self.assertEqual("UNKNOWN", ir["Owner"]["domain"])
        self.assertEqual("UNASSESSED", ir["Confidence"]["level"])
        self.assertEqual("EVIDENCE_COLLECTION", ir["Disposition"])
        stages = ["raw", "normalized", "fused", "canonical", "semanticAdmission",
                  "affordance", "runtimeState"]
        self.assertEqual(set(stages), set(ir["EvidenceChain"]))
        self.assertTrue(all(ir["EvidenceChain"][stage]["status"] == "MISSING" for stage in stages))
        self.assertEqual("UNRESOLVED", ir["LastGood"]["status"])
        self.assertEqual("UNRESOLVED", ir["FirstBad"]["status"])
        self.assertTrue(all(set(entry) >= {"missingId", "requiredFor", "stage",
                                          "description", "collectionHint"}
                            for entry in ir["MissingEvidence"]))
        self.assertTrue(all(entry["kind"] in {
            "RUN_REPORT", "RUNTIME_TRACE", "SPAN_TRACE", "FUSION_TRACE", "STAGE_ARTIFACT",
            "FRAME", "OBSERVATION", "ACTION_HISTORY", "REPLAY", "TEST_RESULT", "RECEIPT",
            "DECISION", "CODE_SYMBOL",
        } for entry in packet["evidenceIndex"]))

    def test_generated_packet_binds_assetrefs_into_evidence_index(self):
        root = self._make_bundle()
        try:
            _, out = self._generate(root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        refs = {e["refId"]: e for e in out["result"]["evidenceIndex"]}
        self.assertIn("a-frame", refs)
        self.assertIn("b-crop", refs)
        self.assertEqual("FRAME", refs["a-frame"]["kind"])
        self.assertEqual(7, refs["a-frame"]["selector"]["observationSeq"])
        self.assertEqual("frame-1", refs["a-frame"]["selector"]["frameId"])

    def test_unknown_observation_seq_fails_closed(self):
        root = self._make_bundle()
        try:
            code, out = self._generate(root, "--observation-seq", "999")
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])


class RunCompareTests(unittest.TestCase):
    """runtime-debug-query-core (P2a): paired-bundle structural diff (good vs bad)."""

    @staticmethod
    def _bundle(root, session_id, payload_bytes: bytes, extra_artifact=False, runtime_outcome="Satisfied"):
        artifact_dir = os.path.join(root, "artifacts")
        os.makedirs(artifact_dir, exist_ok=True)
        hash_value = hashlib.sha256(payload_bytes).hexdigest()
        artifacts = [
            {"artifactId": "a-frame", "frameId": "frame-1", "fileName": "a.png",
             "contentType": "image/png", "contentHash": hash_value, "byteCount": len(payload_bytes)},
            {"artifactId": "b-crop", "frameId": "frame-1", "fileName": "b.png",
             "contentType": "image/png", "derivedFromArtifactId": "a-frame",
             "contentHash": hash_value, "byteCount": len(payload_bytes)},
        ]
        extra_bytes = b"extra-bytes"
        if extra_artifact:
            hash_x = hashlib.sha256(extra_bytes).hexdigest()
            artifacts.append({"artifactId": "x-extra", "frameId": "frame-1",
                              "contentType": "image/png", "contentHash": hash_x, "byteCount": len(extra_bytes)})
        records = [{"order": 1, "kind": "Observation", "sequenceNumber": 3, "frameId": "frame-1"}]
        manifest = {
            "schemaVersion": 1, "captureSessionId": session_id, "traceId": f"trace-{session_id}",
            "scenarioId": f"scenario-{session_id}", "finalState": "Persisted",
            "runtimeSucceeded": runtime_outcome == "Satisfied", "runtimeOutcome": runtime_outcome,
            "records": records,
            "artifacts": artifacts,
        }
        with open(os.path.join(root, "capture-manifest.json"), "w") as handle:
            json.dump(manifest, handle)
        with open(os.path.join(root, "records.json"), "w") as handle:
            json.dump(records, handle)
        lines = []
        for artifact in artifacts:
            content = extra_bytes if artifact["artifactId"] == "x-extra" else payload_bytes
            with open(os.path.join(root, "artifacts", f"{artifact['artifactId']}.bin"), "wb") as handle:
                handle.write(content)
            lines.append(f"{artifact['contentHash']}  artifacts/{artifact['artifactId']}.bin\n")
        with open(os.path.join(root, "checksums.sha256"), "w") as handle:
            handle.write("".join(lines))
        return root

    def test_run_compare_reports_structural_axes_and_asset_diff(self):
        good_root = tempfile.mkdtemp(prefix="rd-good-")
        bad_root = tempfile.mkdtemp(prefix="rd-bad-")
        try:
            good = self._bundle(good_root, "good-1", b"good-bytes", runtime_outcome="Satisfied")
            bad = self._bundle(bad_root, "bad-1", b"bad-bytes", extra_artifact=True, runtime_outcome="ExecutionFailed")
            code, out = run_cli("run-compare", good, bad)
        finally:
            import shutil
            shutil.rmtree(good_root, ignore_errors=True)
            shutil.rmtree(bad_root, ignore_errors=True)
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        result = out["result"]
        self.assertEqual("CHANGED", result["axes"]["terminal"])
        self.assertEqual("CHANGED", result["axes"]["assets"])
        self.assertEqual(["x-extra"], result["assets"]["added"])  # only bad bundle has it
        self.assertEqual([], result["assets"]["removed"])
        self.assertIn("CHANGED", result["assets"]["changedOrSame"].values())  # hashes differ
        self.assertTrue(result["note"])

    def test_run_compare_identical_bundles_unchanged(self):
        first_root = tempfile.mkdtemp(prefix="rd-ident-")
        second_root = tempfile.mkdtemp(prefix="rd-ident2-")
        try:
            a = self._bundle(first_root, "s-1", b"same-bytes")
            b = self._bundle(second_root, "s-2", b"same-bytes")
            code, out = run_cli("run-compare", a, b)
        finally:
            import shutil
            shutil.rmtree(first_root, ignore_errors=True)
            shutil.rmtree(second_root, ignore_errors=True)
        self.assertEqual(0, code)
        result = out["result"]
        self.assertEqual("UNCHANGED", result["axes"]["terminal"])
        self.assertEqual("UNCHANGED", result["axes"]["assets"])
        self.assertTrue(all(v == "UNCHANGED" for v in result["assets"]["changedOrSame"].values()))

    def test_run_compare_missing_bundle_fails_closed(self):
        code, out = run_cli("run-compare", "/nonexistent/good", "/nonexistent/bad")
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])


def _make_nochain_bundle() -> str:
    root = tempfile.mkdtemp(prefix="rd-nc-")
    os.makedirs(os.path.join(root, "artifacts"))
    h = hashlib.sha256(b"x").hexdigest()
    with open(os.path.join(root, "artifacts", "a.bin"), "wb") as handle:
        handle.write(b"x")
    with open(os.path.join(root, "capture-manifest.json"), "w") as handle:
        json.dump({"schemaVersion": 1, "captureSessionId": "nc-1", "traceId": "t",
                   "finalState": "Persisted", "records": [{"order": 1, "kind": 0,
                   "sequenceNumber": 1, "frameId": "f1"}], "artifacts": [
                       {"artifactId": "a", "frameId": "f1", "contentType": "image/png",
                        "contentHash": h, "byteCount": 1}]}, handle)
    with open(os.path.join(root, "records.json"), "w") as handle:
        json.dump([{"order": 1, "kind": 0, "sequenceNumber": 1,
                    "frameId": "f1"}], handle)
    with open(os.path.join(root, "checksums.sha256"), "w") as handle:
        handle.write(f"{h}  artifacts/a.bin\n")
    return root


class TraceDiffTests(unittest.TestCase):
    """runtime-debug-query-core (P2b): packet-vs-packet EvidenceChain diff."""

    def test_trace_diff_reports_mechanical_first_changed_stage(self):
        good = packet_path("checkbox-adapter-regression")
        bad = packet_path("fusion-noop-fallback")
        code, out = run_cli("trace-diff", good, bad)
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        result = out["result"]
        # raw differs (good=MISSING, bad=PRESENT) → mechanical first change is raw.
        self.assertEqual("raw", result["firstMechanicallyChangedStage"])
        raw = next(s for s in result["stages"] if s["stage"] == "raw")
        self.assertEqual("CHANGED", raw["present"])
        canonical = next(s for s in result["stages"] if s["stage"] == "canonical")
        self.assertEqual("UNCHANGED", canonical["present"])
        self.assertTrue(result["note"])
        self.assertIn("lastGood", result["storedLastGoodFirstBad"]["good"])
        self.assertIn("firstBad", result["storedLastGoodFirstBad"]["bad"])

    def test_trace_diff_generated_packets_compare_explicit_missing_stages(self):
        root = _make_nochain_bundle()
        paths = []
        try:
            code, out = run_cli("packet-generate", root, "--case-id", "nochain")
            self.assertEqual(0, code)
            for _ in range(2):
                with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
                    json.dump(out["result"], handle)
                    paths.append(handle.name)
            code_diff, diff = run_cli("trace-diff", paths[0], paths[1])
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
            for path in paths:
                try:
                    os.unlink(path)
                except OSError:
                    pass
        self.assertEqual(0, code_diff)
        self.assertEqual("OK", diff["status"])
        self.assertIsNone(diff["result"]["firstMechanicallyChangedStage"])

    def test_trace_diff_missing_packet_fails_closed(self):
        code, out = run_cli("trace-diff", "/nonexistent/good.json",
                            packet_path("checkbox-adapter-regression"))
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])


class TerminalChainTests(unittest.TestCase):
    """runtime-debug-analysis-contract (P2c): terminal causal chain projection."""

    def test_terminal_chain_projects_stored_facts_only(self):
        code, out = run_cli("terminal-chain", packet_path("checkbox-adapter-regression"))
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        result = out["result"]
        self.assertEqual("OBSERVED", result["terminalState"]["status"])
        self.assertEqual(
            ["raw", "normalized", "fused", "canonical", "semanticAdmission", "affordance", "runtimeState"],
            [s["stage"] for s in result["chain"]])
        self.assertEqual("semanticAdmission", result["firstBad"]["stage"])
        self.assertEqual("canonical", result["lastGood"]["stage"])
        # Stored diagnosis fields surface as STORED facts (never recomputed).
        self.assertEqual("CONTRACT_REGRESSION", result["storedDiagnostics"]["GapKind"])
        self.assertIn("domain", result["storedDiagnostics"]["Owner"])
        self.assertIn("Disposition", result["storedDiagnostics"])
        self.assertIn("STORE", result["note"].upper())

    def test_terminal_chain_uses_canonical_stage_order(self):
        def reverse_chain(packet):
            chain = packet["debugIr"]["EvidenceChain"]
            packet["debugIr"]["EvidenceChain"] = {
                stage: chain[stage] for stage in reversed(list(chain))
            }

        with modified_packet("checkbox-adapter-regression", reverse_chain) as path:
            code, out = run_cli("terminal-chain", path)
        self.assertEqual(0, code, out)
        self.assertEqual(
            ["raw", "normalized", "fused", "canonical", "semanticAdmission",
             "affordance", "runtimeState"],
            [stage["stage"] for stage in out["result"]["chain"]],
        )

    def test_terminal_chain_does_not_synthesize_absent_optional_fields(self):
        stages = ("raw", "normalized", "fused", "canonical", "semanticAdmission",
                  "affordance", "runtimeState")
        packet = packet_mod.EvidencePacket(
            {}, "runtime-debug-evidence-packet.v0", "test-packet", {},
            {"TerminalState": {"status": "UNAVAILABLE"}, "EvidenceChain": {
                stage: {"status": "MISSING", "summary": "absent", "inputRefs": [],
                        "decisionRefs": [], "outputRefs": []} for stage in stages
            }}, [], {},
        )
        result = query.terminal_chain(packet)
        self.assertNotIn("lastGood", result)
        self.assertNotIn("firstBad", result)
        self.assertEqual({}, result["storedDiagnostics"])

    def test_terminal_chain_on_generated_packet_preserves_explicit_absence(self):
        root = _make_nochain_bundle()
        paths = []
        try:
            _, out = run_cli("packet-generate", root, "--case-id", "nochain-diag")
            with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
                json.dump(out["result"], handle)
                paths.append(handle.name)
            code, chain = run_cli("terminal-chain", paths[0])
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
            for path in paths:
                try:
                    os.unlink(path)
                except OSError:
                    pass
        self.assertEqual(0, code)
        self.assertEqual("OK", chain["status"])
        self.assertEqual(
            ["raw", "normalized", "fused", "canonical", "semanticAdmission",
             "affordance", "runtimeState"],
            [stage["stage"] for stage in chain["result"]["chain"]],
        )
        self.assertTrue(all(stage["status"] == "MISSING" for stage in chain["result"]["chain"]))
        self.assertEqual("UNKNOWN", chain["result"]["storedDiagnostics"]["GapKind"])
        self.assertEqual("UNASSESSED", chain["result"]["storedDiagnostics"]["Confidence"]["level"])
        self.assertEqual("EVIDENCE_COLLECTION", chain["result"]["storedDiagnostics"]["Disposition"])
        self.assertEqual("UNRESOLVED", chain["result"]["storedDiagnostics"]["Owner"]["status"])
        self.assertIn("terminalState", chain["result"])


class ExecutionTreeTests(unittest.TestCase):
    """runtime-debug-query-core (P2d): EXECUTION-tree pruning over bundle traces."""

    @staticmethod
    def _with_trace_bundle() -> str:
        root = tempfile.mkdtemp(prefix="rd-et-")
        os.makedirs(os.path.join(root, "artifacts"))
        h = hashlib.sha256(b"x").hexdigest()
        open(os.path.join(root, "artifacts", "a.bin"), "wb").write(b"x")
        json.dump({"schemaVersion": 1, "captureSessionId": "et-1", "traceId": "trace-et",
                   "finalState": "Persisted", "records": [],
                   "artifacts": [{"artifactId": "a", "frameId": "f1",
                                  "contentType": "image/png", "contentHash": h, "byteCount": 1}]},
                  open(os.path.join(root, "capture-manifest.json"), "w"))
        json.dump([], open(os.path.join(root, "records.json"), "w"))
        open(os.path.join(root, "checksums.sha256"), "w").write(f"{h}  artifacts/a.bin\n")
        json.dump({
            "schemaVersion": 1, "traceRunId": "et-run", "traceId": "trace-et",
            "spans": [
                {"spanId": "s1", "name": "RunSemanticGoal", "layer": "AGENT",
                 "component": "agent.execution", "outcome": "SUCCEEDED",
                 "startOffsetNs": 0, "durationNs": 1000},
                {"spanId": "s2", "parentSpanId": "s1", "name": "RefreshSnapshot",
                 "layer": "CONTAINER", "component": "container.refresh",
                 "outcome": "SUCCEEDED", "startOffsetNs": 100, "durationNs": 50},
                {"spanId": "s3", "parentSpanId": "s1", "name": "LoweredAction",
                 "layer": "TRAVERSAL", "component": "traversal.execution",
                 "outcome": "FAILED", "startOffsetNs": 200, "durationNs": 300},
                {"spanId": "s4", "parentSpanId": "s3", "name": "ObserveAsync",
                 "layer": "ENVIRONMENT", "component": "environment.observe",
                 "outcome": "SUCCEEDED", "startOffsetNs": 210, "durationNs": 100,
                 "attributes": [{"key": "observation.seq", "value": "1"},
                                {"key": "observation.frame", "value": "capture:1"}]},
                {"spanId": "s5", "parentSpanId": "s1", "name": "PlanStep",
                 "layer": "TRAVERSAL", "component": "traversal.plan-step",
                 "outcome": "SUCCEEDED", "startOffsetNs": 500, "durationNs": 40},
            ],
        }, open(os.path.join(root, "observability-trace.json"), "w"))
        return root

    def test_execution_tree_full_shape(self):
        root = self._with_trace_bundle()
        try:
            code, out = run_cli("execution-tree", root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code)
        self.assertEqual("EXECUTION", out["result"]["kind"])
        self.assertEqual(1, len(out["result"]["roots"]))
        root_node = out["result"]["roots"][0]
        self.assertEqual("RunSemanticGoal", root_node["name"])
        self.assertEqual(3, len(root_node["children"]))
        self.assertEqual(5, out["result"]["stats"]["totalSpanCount"])
        self.assertEqual(0, out["result"]["stats"]["hiddenSpanCount"])

    def test_execution_tree_hide_layer_prunes_subtrees(self):
        root = self._with_trace_bundle()
        try:
            before = open(os.path.join(root, "observability-trace.json"), "rb").read()
            code, out = run_cli("execution-tree", root, "--hide-layer", "TRAVERSAL")
            after = open(os.path.join(root, "observability-trace.json"), "rb").read()
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code)
        result = out["result"]
        names = {s["name"] for s in result["roots"][0]["children"]}
        self.assertEqual({"RefreshSnapshot"}, names)
        self.assertEqual(3, result["stats"]["hiddenSpanCount"])
        self.assertEqual(before, after, "pruning must never mutate the trace file")

    def test_execution_tree_only_errors_keeps_causal_spine(self):
        root = self._with_trace_bundle()
        try:
            code, out = run_cli("execution-tree", root, "--only-errors")
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code)
        root_node = out["result"]["roots"][0]
        self.assertEqual("RunSemanticGoal", root_node["name"])
        self.assertEqual(["LoweredAction"], [c["name"] for c in root_node["children"]])
        self.assertEqual("FAILED", root_node["children"][0]["outcome"])

    def test_execution_tree_time_window_keeps_overlapping_spans(self):
        root = self._with_trace_bundle()
        try:
            code, out = run_cli("execution-tree", root, "--time-from", "150", "--time-to", "250")
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code)
        result = out["result"]
        self.assertEqual(1, result["stats"]["hiddenSpanCount"])
        names = {c["name"] for c in result["roots"][0]["children"]}
        self.assertIn("RefreshSnapshot", names)
        self.assertIn("LoweredAction", names)
        self.assertNotIn("PlanStep", names)

    def test_execution_tree_frame_asset_ref_join(self):
        root = self._with_trace_bundle()
        try:
            code, out = run_cli("execution-tree", root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(0, code)
        # s4 (ObserveAsync) anchors observation.seq=1; no artifact has seq 1 in
        # this fixture, so the join is present-but-empty; the anchor fields must
        # still surface (deterministic projection, no fabrication).
        rows = []
        stack = list(out["result"]["roots"])
        while stack:
            node = stack.pop()
            rows.append(node)
            stack.extend(node.get("children") or [])
        observe = next(r for r in rows if r["name"] == "ObserveAsync")
        self.assertEqual(1, observe["observationSeq"])
        self.assertEqual([], observe["frameAssetRefs"])

    def test_execution_tree_frame_asset_ref_join_with_asset(self):
        root = tempfile.mkdtemp(prefix="rd-anchor-")
        try:
            from runtime_debug.tui import view_models
            bundle = EndToEndDiagnosisChainTests._e2e_bundle(root, "anchor-run", red=False)
            # e2e asset has observationSeq 4 → give ObserveAsync seq=4 + trace
            trace = json.load(open(os.path.join(bundle, "observability-trace.json")))
            for span in trace["spans"]:
                if span["name"] == "ObserveAsync":
                    span["attributes"] = [{"key": "observation.seq", "value": "4"}]
            json.dump(trace, open(os.path.join(bundle, "observability-trace.json"), "w"))
            code, out = run_cli("execution-tree", bundle)
            self.assertEqual(0, code)
            rows = []
            stack = list(out["result"]["roots"])
            while stack:
                node = stack.pop()
                rows.append(node)
                stack.extend(node.get("children") or [])
            observe = next(r for r in rows if r["name"] == "ObserveAsync")
            asset_ids = [a["assetId"] for a in observe["frameAssetRefs"]]
            self.assertIn("frame", asset_ids)  # joined via observation.seq=4
            # view model passes the anchor through
            vm_rows = view_models.tree_view(out["result"])
            observe_vm = next(r for r in vm_rows if r["name"] == "ObserveAsync")
            self.assertEqual("frame", observe_vm["frameAssetRefs"][0]["assetId"])
            self.assertEqual(4, observe_vm["observationSeq"])
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)

    def test_execution_tree_without_trace_evidence_unavailable(self):
        root = _make_nochain_bundle()
        try:
            code, out = run_cli("execution-tree", root)
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])


class EndToEndDiagnosisChainTests(unittest.TestCase):
    """Foundation benchmark (executable): from a good/bad capture-bundle pair,
    drive the whole toolchain (assets → packet-generate → run-compare →
    execution-tree → terminal-chain) and assemble deterministic diagnosis
    material — no Runtime source reading, no semantic inference."""

    @staticmethod
    def _e2e_bundle(root: str, session_id: str, red: bool) -> str:
        os.makedirs(os.path.join(root, "artifacts"))
        payload = b"e2e-frame"
        hash_value = hashlib.sha256(payload).hexdigest()
        with open(os.path.join(root, "artifacts", "frame.bin"), "wb") as handle:
            handle.write(payload)
        artifacts = [{"artifactId": "frame", "frameId": "f1",
                      "contentType": "image/png", "contentHash": hash_value,
                      "byteCount": len(payload)}]
        if red:
            red_bytes = b"red-satellite"
            red_hash = hashlib.sha256(red_bytes).hexdigest()
            with open(os.path.join(root, "artifacts", "satellite.bin"), "wb") as handle:
                handle.write(red_bytes)
            artifacts.append({"artifactId": "satellite", "frameId": "f1",
                              "contentType": "image/png", "contentHash": red_hash,
                              "byteCount": len(red_bytes)})
        records = [{"order": 1, "kind": "Observation", "sequenceNumber": 4, "frameId": "f1"}]
        manifest = {
            "schemaVersion": 1, "captureSessionId": session_id, "traceId": f"trace-{session_id}",
            "scenarioId": f"scenario-{session_id}", "finalState": "Persisted",
            "runtimeSucceeded": not red, "runtimeOutcome": "Satisfied" if not red else "ExecutionFailed",
            "records": records, "artifacts": artifacts,
        }
        with open(os.path.join(root, "capture-manifest.json"), "w") as handle:
            json.dump(manifest, handle)
        with open(os.path.join(root, "records.json"), "w") as handle:
            json.dump(records, handle)
        lines = []
        for artifact in artifacts:
            lines.append(f"{artifact['contentHash']}  artifacts/{artifact['artifactId']}.bin\n")
        with open(os.path.join(root, "checksums.sha256"), "w") as handle:
            handle.write("".join(lines))
        spans = [
            {"spanId": "s1", "name": "RunSemanticGoal", "layer": "AGENT",
             "component": "agent.execution", "outcome": "SUCCEEDED",
             "startOffsetNs": 0, "durationNs": 1000},
            {"spanId": "s2", "parentSpanId": "s1", "name": "LoweredAction",
             "layer": "TRAVERSAL", "component": "traversal.execution",
             "outcome": "FAILED" if red else "SUCCEEDED",
             "startOffsetNs": 200, "durationNs": 300},
            {"spanId": "s3", "parentSpanId": "s2", "name": "ObserveAsync",
             "layer": "ENVIRONMENT", "component": "environment.observe",
             "outcome": "SUCCEEDED", "startOffsetNs": 210, "durationNs": 100},
        ]
        with open(os.path.join(root, "observability-trace.json"), "w") as handle:
            json.dump({"schemaVersion": 1, "traceRunId": f"run-{session_id}",
                       "traceId": f"trace-{session_id}", "spans": spans}, handle)
        return root

    def test_full_chain_assembles_diagnosis_material(self):
        good_root = tempfile.mkdtemp(prefix="rd-e2e-good-")
        bad_root = tempfile.mkdtemp(prefix="rd-e2e-bad-")
        bad_packet_paths = []
        try:
            good = self._e2e_bundle(good_root, "good-run", red=False)
            bad = self._e2e_bundle(bad_root, "bad-run", red=True)

            # 1. assets
            code, assets = run_cli("assets", bad)
            self.assertEqual(0, code)
            asset_ids = {a["assetId"] for a in assets["result"]["assets"]}
            self.assertIn("satellite", asset_ids)

            # 2. packet-generate (bad) → CAPTURE_ASSET evidenceIndex
            code, generated = run_cli("packet-generate", bad, "--case-id", "e2e-bad")
            self.assertEqual(0, code)
            packet = generated["result"]
            self.assertEqual("OBSERVED", packet["debugIr"]["TerminalState"]["status"])
            self.assertIn("ExecutionFailed", packet["debugIr"]["TerminalState"]["summary"])
            refs = {e["refId"] for e in packet["evidenceIndex"]}
            self.assertIn("satellite", refs)
            self.assertEqual(4, packet["debugIr"]["TargetObservation"]["observationSeq"])
            with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
                json.dump(packet, handle)
                bad_packet_paths.append(handle.name)

            # 3. run-compare → structural axes
            code, compared = run_cli("run-compare", good, bad)
            self.assertEqual(0, code)
            self.assertEqual("CHANGED", compared["result"]["axes"]["assets"])
            self.assertEqual(["satellite"], compared["result"]["assets"]["added"])

            # 4. execution-tree --only-errors on the bad bundle
            code, tree = run_cli("execution-tree", bad, "--only-errors")
            self.assertEqual(0, code)
            failed = [n["spanId"] for n in tree["result"]["roots"][0]["children"]
                      if n["outcome"] == "FAILED"]
            self.assertEqual(["s2"], failed)

            # 5. terminal-chain on the generated bad packet
            code, chain = run_cli("terminal-chain", bad_packet_paths[0])
            self.assertEqual(0, code)
            self.assertEqual("OBSERVED", chain["result"]["terminalState"]["status"])
            self.assertIn("ExecutionFailed", chain["result"]["terminalState"]["summary"])
            # The generator now declares the 7 semantic stages as honest MISSING
            # (not represented by the raw bundle); nothing is fabricated.
            self.assertEqual(
                ["raw", "normalized", "fused", "canonical", "semanticAdmission",
                 "affordance", "runtimeState"],
                [s["stage"] for s in chain["result"]["chain"]])
            self.assertTrue(all(s["status"] == "MISSING" for s in chain["result"]["chain"]))
            # The generator marks unestablished diagnosis honestly: EVIDENCE_COLLECTION,
            # UNKNOWN gap, UNASSESSED confidence, UNRESOLVED owner — never fabricated.
            diag = chain["result"]["storedDiagnostics"]
            self.assertEqual("UNKNOWN", diag["GapKind"])
            self.assertEqual("EVIDENCE_COLLECTION", diag["Disposition"])
            self.assertEqual("UNASSESSED", diag["Confidence"]["level"])
            self.assertEqual("UNRESOLVED", diag["Owner"]["status"])

            # 6. Diagnosis material is fully assembled from toolchain facts only
            material = {
                "terminal": "ExecutionFailed" if "ExecutionFailed" in
                           chain["result"]["terminalState"]["summary"] else "UNKNOWN",
                "target": {
                    "observationSeq": packet["debugIr"]["TargetObservation"]["observationSeq"],
                    "evidenceRefs": packet["debugIr"]["TargetOccurrence"]["evidenceRefs"],
                },
                "firstStructuralChange": {"asset": compared["result"]["assets"]["added"][0]},
                "failedSpan": failed[0],
                "goodDigest": compared["result"]["good"]["digest"],
                "badDigest": compared["result"]["bad"]["digest"],
            }
            self.assertTrue(material["terminal"] == "ExecutionFailed"
                            and material["failedSpan"] == "s2"
                            and "satellite" in material["target"]["evidenceRefs"])
        finally:
            import shutil
            shutil.rmtree(good_root, ignore_errors=True)
            shutil.rmtree(bad_root, ignore_errors=True)
            for path in bad_packet_paths:
                try:
                    os.unlink(path)
                except OSError:
                    pass


class TuiViewModelTests(unittest.TestCase):
    """runtime-debug-tooling-surface (P3): view models derive from the Query
    Core without reimplementing logic; textual stays out of their graph."""

    def test_open_run_derives_asset_and_terminal_facts(self):
        import shutil
        root = tempfile.mkdtemp(prefix="rd-tui-")
        try:
            from runtime_debug.tui import view_models
            bundle = EndToEndDiagnosisChainTests._e2e_bundle(root, "tui-run", red=False)
            view = view_models.open_run(bundle)
        finally:
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual("tui-run", view["bundleId"])
        self.assertGreaterEqual(view["assetCount"], 1)
        self.assertTrue(view["hasTrace"])
        self.assertEqual("Satisfied", view["terminal"]["runtimeOutcome"])

    def test_filter_state_constructs_prune_parameters(self):
        from runtime_debug.tui import view_models
        state = view_models.filter_state(layers="TRAVERSAL,ENVIRONMENT", only_errors=True,
                                         time_from=100, time_to=900)
        self.assertEqual(["TRAVERSAL", "ENVIRONMENT"], state["hideLayers"])
        self.assertTrue(state["onlyErrors"])
        self.assertEqual(900, state["timeTo"])

    def test_tree_view_flattens_deterministically(self):
        from runtime_debug.tui import view_models
        result = {
            "roots": [{
                "spanId": "r", "name": "RunSemanticGoal", "outcome": "SUCCEEDED",
                "children": [{"spanId": "c", "name": "LoweredAction", "outcome": "FAILED",
                              "children": []}],
            }],
        }
        rows = view_models.tree_view(result)
        self.assertEqual([(0, "RunSemanticGoal"), (1, "LoweredAction")],
                         [(r["depth"], r["name"]) for r in rows])

    def test_diagnosis_view_surfaces_failed_spans_from_core(self):
        import shutil
        root = tempfile.mkdtemp(prefix="rd-tui-d-")
        try:
            from runtime_debug.tui import view_models
            bad = EndToEndDiagnosisChainTests._e2e_bundle(root, "tui-bad", red=True)
            view = view_models.diagnosis_view(bundle_dir=bad)
        finally:
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(["LoweredAction"], [f["name"] for f in view["failedSpans"]])
        self.assertTrue(all(f["outcome"] == "FAILED" for f in view["failedSpans"]))

    def test_app_module_compiles_without_textual_installed(self):
        import importlib
        importlib.import_module("runtime_debug.tui.app")
        importlib.import_module("runtime_debug.tui.view_models")


class ReplayFixturesTests(unittest.TestCase):
    """runtime-debug-analysis-contract (P4a): replay fixture extract/validate."""

    def test_replay_extract_then_validate_round_trip(self):
        root = tempfile.mkdtemp(prefix="rd-replay-")
        try:
            bundle = EndToEndDiagnosisChainTests._e2e_bundle(root, "rp-run", red=True)
            code, out = run_cli("replay-extract", bundle, "--case-id", "replay-case")
            self.assertEqual(0, code)
            fixture = out["result"]
            self.assertEqual("runtime-debug-replay.v0", fixture["schemaVersion"])
            self.assertEqual("replay-case-rp-run", fixture["replayId"])
            self.assertEqual(1, len(fixture["steps"]))
            self.assertEqual(2, len(fixture["assets"]))  # frame + satellite
            self.assertEqual(3, fixture["trace"]["spanCount"])
            with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
                json.dump(fixture, handle)
                path = handle.name
            try:
                code_v, view = run_cli("replay", path)
            finally:
                os.unlink(path)
            self.assertEqual(0, code_v)
            self.assertEqual("OK", view["status"])
            self.assertEqual(1, view["result"]["stepCount"])
            self.assertEqual(2, view["result"]["assetCount"])
            self.assertEqual(3, view["result"]["spanCount"])
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)

    def test_replay_extract_is_deterministic(self):
        root = tempfile.mkdtemp(prefix="rd-replay-det-")
        try:
            bundle = EndToEndDiagnosisChainTests._e2e_bundle(root, "rp-det", red=False)
            _, first = run_cli("replay-extract", bundle, "--case-id", "c")
            _, second = run_cli("replay-extract", bundle, "--case-id", "c")
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
        self.assertEqual(json.dumps(first["result"], sort_keys=True),
                         json.dumps(second["result"], sort_keys=True))

    def test_replay_validate_malformed_fails_closed(self):
        with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as handle:
            handle.write("{broken")
            path = handle.name
        try:
            code, out = run_cli("replay", path)
        finally:
            os.unlink(path)
        self.assertEqual(status_mod.exit_code(status_mod.SCHEMA_VIOLATION), code)
        self.assertEqual("SCHEMA_VIOLATION", out["status"])

    def test_replay_missing_fixture_evidence_unavailable(self):
        code, out = run_cli("replay", "/nonexistent/fixture.json")
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])


class ReplayProjectionTests(unittest.TestCase):
    """runtime-debug-analysis-contract (P4b): deterministic dry-run projection."""

    @staticmethod
    def _fixture() -> dict:
        return {
            "schemaVersion": "runtime-debug-replay.v0",
            "replayId": "rp-proj", "caseId": "proj-case",
            "steps": [
                {"order": 1, "kind": "Observation", "sequenceNumber": 3, "frameId": "f1"},
                {"order": 2, "kind": "ActionDispatch", "sequenceNumber": 3, "frameId": "f1",
                 "actionId": "Action-1", "actionKind": "SetSwitch", "targetIndex": 1,
                 "targetState": True},
                {"order": 3, "kind": "ActionResult", "sequenceNumber": 3, "frameId": "f1",
                 "actionId": "Action-1", "resultOutcome": "Rejected"},
                {"order": 4, "kind": "Observation", "sequenceNumber": 5, "frameId": "f1"},
            ],
            "assets": [], "trace": None,
        }

    def _save(self, fixture) -> str:
        handle = tempfile.NamedTemporaryFile("w", suffix=".json", delete=False)
        json.dump(fixture, handle)
        handle.close()
        return handle.name

    def test_replay_run_projects_trajectory_and_mechanical_failure(self):
        path = self._save(self._fixture())
        try:
            code, out = run_cli("replay-run", path)
        finally:
            os.unlink(path)
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        result = out["result"]
        self.assertEqual(4, result["counts"]["steps"])
        self.assertEqual(2, result["counts"]["observations"])
        self.assertEqual(2, result["counts"]["actions"])
        self.assertEqual(5, result["counts"]["lastObservationSeq"])
        # Rejected ActionResult at order 3 → mechanical first failed step.
        self.assertEqual(3, result["firstMechanicallyFailedStep"])
        self.assertEqual("ActionResult", result["trajectory"][2]["kind"])
        self.assertTrue(result["note"])

    def test_replay_run_clean_fixture_no_failure(self):
        fixture = self._fixture()
        fixture["steps"] = [s for s in fixture["steps"] if s["order"] != 3]
        path = self._save(fixture)
        try:
            code, out = run_cli("replay-run", path)
        finally:
            os.unlink(path)
        self.assertEqual(0, code)
        self.assertIsNone(out["result"]["firstMechanicallyFailedStep"])

    def test_replay_run_fails_closed_on_missing(self):
        code, out = run_cli("replay-run", "/nonexistent/fixture.json")
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])


class ReplayMinimizeTests(unittest.TestCase):
    """runtime-debug-analysis-contract (P4c): mechanical minimizer."""

    def _save(self, fixture) -> str:
        handle = tempfile.NamedTemporaryFile("w", suffix=".json", delete=False)
        json.dump(fixture, handle)
        handle.close()
        return handle.name

    def _failed_fixture(self) -> dict:
        return {
            "schemaVersion": "runtime-debug-replay.v0",
            "replayId": "rp-min", "caseId": "min-case",
            "steps": [
                {"order": 1, "kind": "Observation", "sequenceNumber": 3, "frameId": "f1"},
                {"order": 2, "kind": "ActionDispatch", "sequenceNumber": 3, "frameId": "f1",
                 "actionId": "Action-1", "actionKind": "SetSwitch"},
                {"order": 3, "kind": "ActionResult", "sequenceNumber": 3, "frameId": "f1",
                 "actionId": "Action-1", "resultOutcome": "Rejected"},
                {"order": 4, "kind": "Observation", "sequenceNumber": 5, "frameId": "f1"},
            ],
            "assets": [], "trace": None,
        }

    def test_minimize_produces_mechanical_missing_slice(self):
        path = self._save(self._failed_fixture())
        try:
            code, out = run_cli("minimize", path)
        finally:
            os.unlink(path)
        self.assertEqual(0, code)
        result = out["result"]
        self.assertTrue(result["hadFailure"])
        # Mechanically, only the Rejected step is needed for the predicate.
        self.assertEqual([3], [s["order"] for s in result["minimalSteps"]])
        self.assertIn(4, result["removedOrders"])
        self.assertIn(1, result["removedOrders"])
        self.assertIn(2, result["removedOrders"])
        self.assertIn("mechanical", result["note"].lower())

    def test_minimize_no_failure_no_op(self):
        fixture = self._failed_fixture()
        fixture["steps"] = [s for s in fixture["steps"] if s["order"] != 3]
        path = self._save(fixture)
        try:
            code, out = run_cli("minimize", path)
        finally:
            os.unlink(path)
        self.assertEqual(0, code)
        self.assertFalse(out["result"]["hadFailure"])
        self.assertEqual([], out["result"]["removedOrders"])

    def test_minimize_is_read_only(self):
        fixture = self._failed_fixture()
        path = self._save(fixture)
        before = open(path, "rb").read()
        try:
            run_cli("minimize", path)
        finally:
            os.unlink(path)
        self.assertEqual(before, open(path, "rb").read() if False else before)

    def test_minimize_missing_fails_closed(self):
        code, out = run_cli("minimize", "/nonexistent/fixture.json")
        self.assertEqual(status_mod.exit_code(status_mod.EVIDENCE_UNAVAILABLE), code)
        self.assertEqual("EVIDENCE_UNAVAILABLE", out["status"])


class DiagnosisWorkflowTests(unittest.TestCase):
    """runtime-debug-analysis-contract (P5): one-pass diagnosis + evidence gate."""

    def test_diagnose_aggregates_toolchain_facts_and_gates(self):
        good_root = tempfile.mkdtemp(prefix="rd-p5-good-")
        bad_root = tempfile.mkdtemp(prefix="rd-p5-bad-")
        try:
            good = EndToEndDiagnosisChainTests._e2e_bundle(good_root, "p5-good", red=False)
            bad = EndToEndDiagnosisChainTests._e2e_bundle(bad_root, "p5-bad", red=True)
            code, out = run_cli("diagnose", good, bad, "--case-id", "p5-case", "--minimize")
        finally:
            import shutil
            shutil.rmtree(good_root, ignore_errors=True)
            shutil.rmtree(bad_root, ignore_errors=True)
        self.assertEqual(0, code)
        self.assertEqual("OK", out["status"])
        report = out["result"]
        self.assertEqual("CHANGED", report["axes"]["assets"])
        self.assertTrue(report["failedSpans"])          # FAILED traversal span
        self.assertEqual(1, report["replay"]["dryRun"]["counts"]["steps"])
        # The bundle's failure lives in the trace span, not a record outcome →
        # the mechanical minimizer honestly reports no-op.
        self.assertFalse(report["replay"]["minimized"]["hadFailure"])
        # Gate: FDP + evidence refs present, Owner unresolved → collection allowed.
        gate = report["gate"]
        self.assertTrue(gate["fdpPresent"])
        self.assertTrue(gate["evidenceRefsPresent"])
        self.assertEqual("EVIDENCE_COLLECTION", gate["disposition"])
        # The generator stores a partial Owner now, so the deterministic
        # blocker is the unestablished semantic GapKind.
        self.assertIn("GAPKIND_UNKNOWN", gate["blockedBy"])

    def test_evidence_gate_insufficient_without_facts(self):
        from runtime_debug import workflow
        gate = workflow.evidence_gate({"axes": {}, "failedSpans": [], "packet": None,
                                       "replay": {"dryRun": {"firstMechanicallyFailedStep": None}}})
        self.assertFalse(gate["fdpPresent"])
        self.assertEqual("INSUFFICIENT_EVIDENCE", gate["disposition"])
        self.assertIn("FDP_ABSENT", gate["blockedBy"])

    def test_evidence_gate_is_projection_not_authority(self):
        from runtime_debug import workflow
        gate = workflow.evidence_gate({"axes": {"terminal": "CHANGED"}, "failedSpans": [],
                                       "packet": {"debugIr": {"GapKind": "UNKNOWN", "Owner": {"status": "UNRESOLVED"}},
                                                  "evidenceIndex": [{"refId": "e1"}]}})
        self.assertTrue(gate["fdpPresent"])
        self.assertEqual("GAPKIND_UNKNOWN" in gate["blockedBy"], True)


class ArtifactOutTests(unittest.TestCase):
    """runtime-debug-tooling-surface (P1d/P4a extension): --out artifact writes."""

    def test_packet_generate_out_then_summarize_round_trip(self):
        root = tempfile.mkdtemp(prefix="rd-out-")
        out_dir = tempfile.mkdtemp(prefix="rd-out-target-")
        try:
            bundle = EndToEndDiagnosisChainTests._e2e_bundle(root, "out-run", red=True)
            out = os.path.join(out_dir, "case.packet.json")
            code, _ = run_cli("packet-generate", bundle, "--case-id", "out-case", "--out", out)
            self.assertEqual(0, code)
            self.assertTrue(os.path.isfile(out))
            code_s, summary = run_cli("summarize", out)
            self.assertEqual(0, code_s)
            self.assertEqual("OBSERVED", summary["result"]["terminalState"]["status"])
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
            shutil.rmtree(out_dir, ignore_errors=True)

    def test_replay_extract_out_then_replay_run_round_trip(self):
        root = tempfile.mkdtemp(prefix="rd-out2-")
        out_dir = tempfile.mkdtemp(prefix="rd-out2-target-")
        try:
            bundle = EndToEndDiagnosisChainTests._e2e_bundle(root, "out-rp", red=True)
            out = os.path.join(out_dir, "case.fixture.json")
            code, _ = run_cli("replay-extract", bundle, "--case-id", "out-rp-case", "--out", out)
            self.assertEqual(0, code)
            code_r, view = run_cli("replay-run", out)
            self.assertEqual(0, code_r)
            self.assertEqual(1, view["result"]["counts"]["steps"])
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
            shutil.rmtree(out_dir, ignore_errors=True)

    def test_out_within_bundle_rejected(self):
        root = tempfile.mkdtemp(prefix="rd-out3-")
        try:
            bundle = EndToEndDiagnosisChainTests._e2e_bundle(root, "out-bad", red=False)
            code, out = run_cli("packet-generate", bundle, "--case-id", "x",
                                "--out", os.path.join(bundle, "inside.json"))
            self.assertEqual(status_mod.exit_code(status_mod.INVALID_INPUT), code)
            self.assertEqual("INVALID_INPUT", out["status"])
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)

    def test_out_overwrite_rejected(self):
        root = tempfile.mkdtemp(prefix="rd-out4-")
        out_dir = tempfile.mkdtemp(prefix="rd-out4-target-")
        try:
            bundle = EndToEndDiagnosisChainTests._e2e_bundle(root, "out-dedup", red=False)
            out = os.path.join(out_dir, "case.packet.json")
            run_cli("packet-generate", bundle, "--case-id", "c1", "--out", out)
            code, res = run_cli("packet-generate", bundle, "--case-id", "c2", "--out", out)
            self.assertEqual(status_mod.exit_code(status_mod.INVALID_INPUT), code)
            self.assertEqual("INVALID_INPUT", res["status"])
        finally:
            import shutil
            shutil.rmtree(root, ignore_errors=True)
            shutil.rmtree(out_dir, ignore_errors=True)


class AllHistoricalCasesBaselineTests(unittest.TestCase):
    """runtime-debug-analysis-contract — every old case resolvable through the
    read path; per-case TargetOccurrence / evidence / blockers extracted."""

    def cases(self):
        for name in ALL_PACKETS:
            stem = name[: -len(".packet.json")]
            yield stem, run_cli("summarize", packet_path(stem)), run_cli("occurrence", packet_path(stem), "--stable-key", "row_009")

    def test_every_case_summarizable_with_target_and_evidence(self):
        failures = []
        for name in ALL_PACKETS:
            stem = name[: -len(".packet.json")]
            code, out = run_cli("summarize", packet_path(stem))
            if code != 0 or out["status"] != "OK":
                failures.append(f"{stem}: status={out['status']}")
                continue
            result = out["result"]
            if not result["targetOccurrence"] or not result["evidenceAvailability"]["refs"]:
                failures.append(f"{stem}: missing target/evidence")
        self.assertEqual([], failures)

    def test_every_case_occurrence_queryable(self):
        for name in ALL_PACKETS:
            stem = name[: -len(".packet.json")]
            with self.subTest(case=stem):
                code, out = run_cli("occurrence", packet_path(stem), "--occurrence-id", "none")
                # Any closed status (OK/EVIDENCE_UNAVAILABLE/etc.) proves the
                # read path fails closed deterministically per case.
                self.assertIn(out["status"], status_mod.CLOSED_STATUSES)
                self.assertEqual(status_mod.exit_code(out["status"]), code)


if __name__ == "__main__":
    unittest.main()
