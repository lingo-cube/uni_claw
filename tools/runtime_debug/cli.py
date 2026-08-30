"""Thin CLI adapter: argv -> Query Core -> canonical envelope -> exit code.

No correlation or projection logic lives here (see query.py); this module only
maps user input, catches fail-closed errors, and renders the envelope.
"""

from __future__ import annotations

import argparse
import os
import sys

from . import envelope, packet as packet_source, query, replay as replay_core, status as status_mod, workflow
from .sources import bundle as bundle_source

_SELECTOR_FLAGS = ("occurrence-id", "stable-key", "row-id", "evidence-ref")


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="runtime-debug",
        description="Read-only, deterministic Runtime Debug P1a projections (no authority).",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    summarize = sub.add_parser("summarize", help="contract-limited packet summary projection")
    summarize.add_argument("packet", metavar="<packet>", help="evidence packet v0 file")

    occurrence = sub.add_parser("occurrence", help="typed occurrence query")
    occurrence.add_argument("packet", metavar="<packet>", help="evidence packet v0 file")
    for flag in _SELECTOR_FLAGS:
        occurrence.add_argument(f"--{flag}", metavar="<value>", help=f"typed selector: {flag}")

    causal = sub.add_parser("trace", help="causal/evidence tree projection")
    causal.add_argument("packet", metavar="<packet>", help="evidence packet v0 file")
    causal.add_argument("--prune", metavar="stage,...", default="",
                        help="comma-separated stage names to hide from the projection")
    causal.add_argument("--only-decisions", action="store_true",
                        help="keep only stages carrying decision refs")
    causal.add_argument("--only-evidence", action="store_true",
                        help="keep only evidence-bearing stages")

    chain = sub.add_parser("evidence", help="evidence evidence-chain query")
    chain.add_argument("packet", metavar="<packet>", help="evidence packet v0 file")
    chain.add_argument("--evidence-ref", metavar="<refId>", required=True,
                       help="evidence ref id to trace across the chain")

    diff = sub.add_parser("diff", help="packet-scoped good/bad differential projection")
    diff.add_argument("packet", metavar="<packet>", help="evidence packet v0 file")

    assets_cmd = sub.add_parser("assets", help="AssetRef index of a capture bundle")
    assets_cmd.add_argument("bundle", metavar="<bundle-dir>", help="capture bundle directory")

    asset_show = sub.add_parser("asset-show", help="one AssetRef (metadata only)")
    asset_show.add_argument("bundle", metavar="<bundle-dir>", help="capture bundle directory")
    asset_show.add_argument("--asset-id", metavar="<assetId>", required=True, help="asset id")

    asset_related = sub.add_parser("asset-related", help="parent/child AssetRef relations")
    asset_related.add_argument("bundle", metavar="<bundle-dir>", help="capture bundle directory")
    asset_related.add_argument("--asset-id", metavar="<assetId>", required=True, help="asset id")

    generate = sub.add_parser("packet-generate", help="mechanical base Evidence Packet from a bundle")
    generate.add_argument("bundle", metavar="<bundle-dir>", help="capture bundle directory")
    generate.add_argument("--case-id", metavar="<name>", required=True, help="case identifier for the packet")
    generate.add_argument("--observation-seq", metavar="<N>", type=int, default=None,
                          help="explicit target observation sequence (default: final recorded observation)")
    generate.add_argument("--out", metavar="<path>", default=None,
                          help="write the packet JSON to a new file (never inside the bundle, never overwrite)")

    run_compare = sub.add_parser("run-compare", help="paired-bundle structural diff (good vs bad)")
    run_compare.add_argument("good", metavar="<good-bundle>", help="good capture bundle directory")
    run_compare.add_argument("bad", metavar="<bad-bundle>", help="bad capture bundle directory")

    trace_diff = sub.add_parser("trace-diff", help="packet-vs-packet EvidenceChain diff (good vs bad)")
    trace_diff.add_argument("good", metavar="<good-packet>", help="good evidence packet v0 file")
    trace_diff.add_argument("bad", metavar="<bad-packet>", help="bad evidence packet v0 file")

    terminal = sub.add_parser("terminal-chain", help="mechanical terminal causal chain projection")
    terminal.add_argument("packet", metavar="<packet>", help="evidence packet v0 file")

    replay_extract = sub.add_parser("replay-extract", help="mechanical replay fixture from a bundle")
    replay_extract.add_argument("bundle", metavar="<bundle-dir>", help="capture bundle directory")
    replay_extract.add_argument("--case-id", metavar="<name>", required=True, help="case identifier")
    replay_extract.add_argument("--out", metavar="<path>", default=None,
                                help="write the fixture JSON to a new file (never inside the bundle, never overwrite)")

    replay_validate = sub.add_parser("replay", help="validate + summarize one replay fixture")
    replay_validate.add_argument("fixture", metavar="<fixture.json>", help="replay fixture v0 file")

    replay_run = sub.add_parser("replay-run", help="deterministic dry-run projection over a fixture")
    replay_run.add_argument("fixture", metavar="<fixture.json>", help="replay fixture v0 file")

    minimize = sub.add_parser("minimize", help="mechanical minimal failure-preserving slice")
    minimize.add_argument("fixture", metavar="<fixture.json>", help="replay fixture v0 file")

    diagnose = sub.add_parser("diagnose", help="one-pass diagnosis material + gate for a good/bad pair")
    diagnose.add_argument("good", metavar="<good-bundle>", help="good capture bundle directory")
    diagnose.add_argument("bad", metavar="<bad-bundle>", help="bad capture bundle directory")
    diagnose.add_argument("--case-id", metavar="<name>", required=True, help="case identifier")
    diagnose.add_argument("--minimize", action="store_true", help="include the mechanical minimal slice")

    execution = sub.add_parser("execution-tree", help="EXECUTION tree of a bundle trace with pruning")
    execution.add_argument("bundle", metavar="<bundle-dir>", help="capture bundle directory")
    execution.add_argument("--hide-layer", metavar="L1,L2", default="", help="hide these span layers")
    execution.add_argument("--hide-component", metavar="C1,C2", default="", help="hide these span components")
    execution.add_argument("--hide-name", metavar="N1,N2", default="", help="hide these span names")
    execution.add_argument("--only-errors", action="store_true", help="keep only FAILED/CANCELLED spans + ancestors")
    execution.add_argument("--time-from", metavar="<ns>", type=int, default=None, help="keep spans overlapping from this offset")
    execution.add_argument("--time-to", metavar="<ns>", type=int, default=None, help="keep spans overlapping up to this offset")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _build_parser().parse_args(argv)
    command = args.command

    if command == "diagnose":
        return _diagnose_command(args)

    # Packet-vs-packet diff — needs two packets, no `packet`-only routing.
    if command == "trace-diff":
        return _trace_diff_command(args)

    if command in ("replay", "replay-run", "minimize"):
        return _replay_validate_command(args,
                                        dry_run=command == "replay-run",
                                        minimize=command == "minimize")

    # Bundle-scoped commands (AssetRef first-class) — same Query Core.
    if command in ("assets", "asset-show", "asset-related", "packet-generate", "run-compare",
                   "execution-tree", "replay-extract"):
        return _bundle_command(command, args)

    try:
        packet = _load_packet(args.packet)
    except packet_source.PacketError as exc:
        return _emit(command, exc.status, None,
                     [envelope.diagnostic("READER", exc.message, [])])

    source = envelope.source_ref(packet.packet_version, packet.packet_id, packet.source_identity)

    try:
        if command == "summarize":
            result = query.summarize(packet)
            return _emit_command(command, packet, source, "OK", result, [])
        if command == "occurrence":
            selector_kind, selector_value = _selected_selector(args)
            if selector_kind == "unset":
                return _emit_command(command, packet, source, "INVALID_INPUT", None,
                                     [envelope.diagnostic("SELECTOR", "exactly one typed selector is required", [])])
            result = query.occurrence(packet, selector_value, selector_kind)
            diagnostics = [] if result["status"] == status_mod.OK else [
                envelope.diagnostic("OCCURRENCE", result["diagnostic"], [])
            ]
            return _emit_command(command, packet, source, result["status"],
                                 None if result["candidates"] is None else result, diagnostics)
        if command == "trace":
            prune = tuple(s.strip() for s in args.prune.split(",") if s.strip())
            result = query.causal_tree(packet, prune, args.only_decisions, args.only_evidence)
            diagnostics = [] if result.get("tree") else \
                [envelope.diagnostic("CAUSAL", result["diagnostic"], [])]
            return _emit_command(command, packet, source, result["status"],
                                 result.get("tree"), diagnostics)
        if command == "evidence":
            result = query.evidence_chain(packet, args.evidence_ref)
            diagnostics = [] if result.get("ref") else \
                [envelope.diagnostic("EVIDENCE-CHAIN", result["diagnostic"], [])]
            return _emit_command(command, packet, source, result["status"],
                                 result if result.get("ref") else None, diagnostics)
        if command == "diff":
            result = query.compare(packet)
            diagnostics = [] if result.get("comparison") else \
                [envelope.diagnostic("DIFF", result["diagnostic"], [])]
            return _emit_command(command, packet, source, result["status"],
                                 result.get("comparison"), diagnostics)
        if command == "terminal-chain":
            result = query.terminal_chain(packet)
            return _emit_command(command, packet, source, result["status"], result, [])
        return _emit_command(command, packet, source, "INVALID_INPUT", None,
                             [envelope.diagnostic("COMMAND", f"unsupported command: {command}", [])])
    except Exception:  # pragma: no cover - fail-closed last-resort guard
        return _emit_command(command, packet, source, "SCHEMA_VIOLATION", None,
                             [envelope.diagnostic("INTERNAL", "unexpected projection failure", [])])


def _diagnose_command(args) -> int:
    try:
        report = workflow.diagnose_workflow(args.good, args.bad, args.case_id,
                                            minimize=args.minimize)
    except bundle_source.SourceError as exc:
        return _emit("diagnose", exc.status, None,
                     [envelope.diagnostic("SOURCE", exc.message, [])])
    return _emit("diagnose", report["status"], report, [],
                 source={"good": {"bundleId": (report.get("good") or {}).get("bundleId")},
                         "bad": {"bundleId": (report.get("bad") or {}).get("bundleId")},
                         "caseId": args.case_id})


def _trace_diff_command(args) -> int:
    try:
        good = _load_packet(args.good)
        bad = _load_packet(args.bad)
    except packet_source.PacketError as exc:
        return _emit("trace-diff", exc.status, None,
                     [envelope.diagnostic("READER", exc.message, [])])
    result = query.diff_packets(good, bad)
    diagnostics = [] if result.get("stages") is not None else \
        [envelope.diagnostic("TRACE-DIFF", result.get("diagnostic", ""), [])]
    return _emit("trace-diff", result["status"], None if result.get("stages") is None else result,
                 diagnostics,
                 source={"good": {"packetId": good.packet_id},
                         "bad": {"packetId": bad.packet_id}})


def _replay_validate_command(args, dry_run: bool = False, minimize: bool = False) -> int:
    label = "minimize" if minimize else ("replay-run" if dry_run else "replay")
    try:
        fixture = replay_core.read_fixture_file(args.fixture)
        summary = replay_core.validate_replay_fixture(fixture)
        if minimize:
            result = replay_core.minimize_fixture(fixture)
        elif dry_run:
            result = replay_core.project_replay_run(fixture)
        else:
            result = summary
    except replay_core.FixtureError as exc:
        return _emit(label, exc.status, None, [envelope.diagnostic("REPLAY", exc.message, [])])
    return _emit(label, "OK", result, [],
                 source={"schemaVersion": fixture.get("schemaVersion"),
                         "replayId": fixture.get("replayId"),
                         "caseId": fixture.get("caseId")})


def _bundle_command(command: str, args) -> int:
    """Bundle-scoped AssetRef commands — thin adapter over the same Query Core."""
    if command == "run-compare":
        try:
            good_bundle = bundle_source.read_bundle(args.good)
            bad_bundle = bundle_source.read_bundle(args.bad)
        except bundle_source.SourceError as exc:
            return _emit(command, exc.status, None,
                         [envelope.diagnostic("SOURCE", exc.message, [])])
        result = query.compare_bundles(good_bundle, bad_bundle)
        return _emit(command, result["status"], result, [],
                     source={"good": {"bundleId": good_bundle.capture_session_id,
                                      "traceId": good_bundle.trace_id},
                             "bad": {"bundleId": bad_bundle.capture_session_id,
                                     "traceId": bad_bundle.trace_id}})
    try:
        bundle = bundle_source.read_bundle(args.bundle)
    except bundle_source.SourceError as exc:
        return _emit(command, exc.status, None,
                     [envelope.diagnostic("SOURCE", exc.message, [])])
    bundle_source_ref = {
        "bundleId": bundle.capture_session_id,
        "traceId": bundle.trace_id,
        "scenarioId": bundle.scenario_id,
    }
    if command == "replay-extract":
        result = replay_core.build_replay_fixture(bundle, args.case_id)
        out_issue = _write_artifact(result, args.out, bundle.bundle_dir)
        if out_issue is not None:
            return _emit(command, out_issue[0], None,
                         [envelope.diagnostic("OUTPUT", out_issue[1], [])], source=bundle_source_ref)
        return _emit(command, "OK", result, [], source=bundle_source_ref)
    if command == "execution-tree":
        result = query.execution_tree(
            bundle,
            hide_layers=frozenset(s.strip() for s in args.hide_layer.split(",") if s.strip()),
            hide_components=frozenset(s.strip() for s in args.hide_component.split(",") if s.strip()),
            hide_names=frozenset(s.strip() for s in args.hide_name.split(",") if s.strip()),
            only_errors=args.only_errors,
            time_from=args.time_from,
            time_to=args.time_to)
        diagnostics = [] if result.get("roots") is not None else \
            [envelope.diagnostic("EXECUTION-TREE", result.get("diagnostic", ""), [])]
        return _emit(command, result["status"], None if result.get("roots") is None else result,
                     diagnostics, source=bundle_source_ref)
    if command == "assets":
        payload = query.assets(bundle)
        return _emit(command, "OK", payload, [], source=bundle_source_ref)
    if command == "asset-show":
        result = query.asset_show(bundle, args.asset_id)
        diagnostics = [] if result.get("asset") else \
            [envelope.diagnostic("ASSET", result["diagnostic"], [])]
        return _emit(command, result["status"], result.get("asset"), diagnostics,
                     source=bundle_source_ref)
    if command == "asset-related":
        result = query.asset_related(bundle, args.asset_id)
        diagnostics = [] if result.get("asset") else \
            [envelope.diagnostic("ASSET-RELATED", result["diagnostic"], [])]
        return _emit(command, result["status"],
                     None if result.get("asset") is None else result, diagnostics,
                     source=bundle_source_ref)
    if command == "packet-generate":
        result = query.generate_packet(bundle, args.case_id, args.observation_seq)
        packet = result["packet"]
        requested = args.observation_seq
        target_seq = packet["debugIr"]["TargetObservation"].get("observationSeq")
        if requested is not None and target_seq is None:
            return _emit(command, status_mod.EVIDENCE_UNAVAILABLE, None,
                         [envelope.diagnostic(
                             "GENERATOR",
                             f"observation sequence {requested} is not recorded in this bundle", [])],
                         source=bundle_source_ref)
        out_issue = _write_artifact(packet, args.out, bundle.bundle_dir)
        if out_issue is not None:
            return _emit(command, out_issue[0], None,
                         [envelope.diagnostic("OUTPUT", out_issue[1], [])], source=bundle_source_ref)
        return _emit(command, result["status"], packet, [], source=bundle_source_ref)
    return _emit(command, "INVALID_INPUT", None,
                 [envelope.diagnostic("COMMAND", f"unsupported bundle command: {command}", [])])


def _write_artifact(payload, out_path: str | None, bundle_dir: str) -> tuple[str, str] | None:
    """'--out' artifact write: never inside the bundle, never overwrite, atomic."""
    if out_path is None:
        return None
    try:
        resolved = os.path.realpath(out_path)
        bundle_real = os.path.realpath(bundle_dir)
        if resolved == bundle_real or os.path.commonpath([resolved, bundle_real]) == bundle_real:
            return (status_mod.INVALID_INPUT, "output path must not be inside the bundle directory")
        if os.path.exists(resolved):
            return (status_mod.INVALID_INPUT, "output path already exists (append-only output)")
        parent = os.path.dirname(resolved)
        if parent:
            os.makedirs(parent, exist_ok=True)
        tmp = resolved + f".tmp-{os.getpid()}"
        with open(tmp, "w", encoding="utf-8") as handle:
            handle.write(envelope.render(payload))
        os.replace(tmp, resolved)
        return None
    except OSError as exc:
        return (status_mod.SCHEMA_VIOLATION, f"artifact output write failed: {exc}")


def _load_packet(path: str) -> packet_source.EvidencePacket:
    if not os.path.isfile(path):
        raise packet_source.PacketError(status_mod.EVIDENCE_UNAVAILABLE, "packet file not found")
    with open(path, "rb") as handle:
        data = handle.read()
    return packet_source.read_bytes(data)  # never writes; input bytes stay immutable


def _selected_selector(args) -> tuple[str, str | None]:
    provided = [flag for flag in _SELECTOR_FLAGS
                if getattr(args, flag.replace("-", "_"), None) is not None]
    if len(provided) != 1:
        return "unset", None  # validated in main() -> INVALID_INPUT envelope
    flag = provided[0]
    return flag.replace("-", "_"), getattr(args, flag.replace("-", "_"))


def _emit_command(command: str, packet, source: dict, status_text: str,
                  result, diagnostics: list[dict]) -> int:
    return _emit(command, status_text, result, diagnostics, source=source)


def _emit(command: str, status_text: str, result, diagnostics: list[dict], source: dict | None = None) -> int:
    if source is None:
        source = {"packetVersion": None, "packetId": None, "sourceIdentity": {}}
    sys.stdout.write(envelope.render(envelope.build(command, status_text, source, result, diagnostics)))
    return status_mod.exit_code(status_text)


if __name__ == "__main__":
    sys.exit(main())
