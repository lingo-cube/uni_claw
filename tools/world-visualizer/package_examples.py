#!/usr/bin/env python3
"""Package recorded World Owner outputs into repeatable visualizer examples."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


CHANGE_EVIDENCE = Path("changes/WORLD-UNCERTAINTY-001/evidence/implementation-entry")
EXAMPLES = Path("tools/world-visualizer/examples")
FAMILY_TITLES = {
    "reuse": "已核实页面语义沿用",
    "context": "页面上下文知识缺口",
    "recovery": "有界观察恢复审查",
    "terminal": "终局后的影响复核",
    "correction": "历史身份纠正及消费影响",
    "correction-conflict": "纠正证据被拒绝时保留原归属",
    "late-result": "迟到解释仍绑定原观察",
    "reuse-negative": "当前变化不沿用历史定位与状态",
    "scroll": "滚动页面累计内容",
    "recorded": "录制布局的真实 World 与咨询",
}
OPTIONAL_FAMILIES = {"correction-conflict", "late-result", "reuse-negative"}


def load(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as stream:
        return json.load(stream)


def save(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def camel_key(key: str) -> str:
    if not key or not key[0].isupper():
        return key
    return key[0].lower() + key[1:]


def normalize(value: Any) -> Any:
    if isinstance(value, dict):
        # `worldState` is a map keyed by product subjects, not a record whose
        # keys should be normalized. Preserve those subject identifiers.
        result = {}
        for key, item in value.items():
            normalized_key = camel_key(key)
            if key in {"WorldState", "worldState"} and isinstance(item, dict):
                result[normalized_key] = {subject: normalize(claim) for subject, claim in item.items()}
            else:
                result[normalized_key] = normalize(item)
        return result
    if isinstance(value, list):
        return [normalize(item) for item in value]
    return value


def field(value: dict[str, Any], name: str, default: Any = None) -> Any:
    return value.get(name, value.get(name[0].upper() + name[1:], default))


def make_frame(key: str, label: str, revision: Any, *, knowledge: Any = None,
               pages: list[Any] | None = None, slices: list[Any] | None = None,
               historical_slices: list[Any] | None = None, **extra: Any) -> dict[str, Any]:
    return {
        "key": key,
        "label": label,
        "revision": normalize(revision),
        "knowledge": normalize(knowledge),
        "pages": normalize(pages or []),
        "slices": normalize(slices or []),
        "historicalSlices": normalize(historical_slices or []),
        **{camel_key(k): normalize(v) for k, v in extra.items()},
    }


def wrapper(family: str, source_test: str, frames: list[dict[str, Any]],
            limitations: list[str], **artifacts: Any) -> dict[str, Any]:
    return {
        "schemaVersion": "world-visualization.v1",
        "id": "world-visualization:" + family,
        "title": FAMILY_TITLES[family],
        "provenance": {
            "coreCommit": "56a75fd7",
            "sourceTest": source_test,
            "inputClass": "受控来源/真实核心",
            "limitations": limitations,
        },
        "frames": frames,
        "artifacts": normalize(artifacts),
    }


def package_correction(root: Path, source_path: Path | None = None) -> None:
    if source_path is not None and source_path.is_file():
        source = load(source_path)
        if source.get("schemaVersion") != "world-visualization.v1" or source.get("id") != "world-visualization:correction":
            raise ValueError("correction source must be a world-visualization.v1 correction wrapper")
        if not isinstance(source.get("frames"), list) or not source["frames"]:
            raise ValueError("correction source wrapper must contain actual frames")
        save(root / EXAMPLES / "correction.json", source)
        return
    source = load(root / CHANGE_EVIDENCE / "correction-world-example.json")
    revisions = {r["RevisionId"]: r for r in source["revisions"]}
    current_id = source["currentPages"][0]["SourceRevisionId"]
    if any(page["SourceRevisionId"] != current_id for page in source["currentPages"]):
        raise ValueError("correction currentPages contain mixed source revisions")
    if current_id not in revisions:
        raise ValueError("correction page source revision is absent from exported revisions")
    knowledge = source["actualConsultation"].get("WorldKnowledge")
    if not knowledge or knowledge.get("SourceRevisionId") != current_id:
        raise ValueError("correction consultation knowledge does not match page revision")
    frames = []
    for revision_id, revision in revisions.items():
        exact_pages = [page for page in source["currentPages"] if page["SourceRevisionId"] == revision_id]
        exact_knowledge = knowledge if knowledge.get("SourceRevisionId") == revision_id else None
        exact_impacts = [impact for impact in source["effectImpacts"]
                         if impact["SourceRevisionId"] == revision_id]
        exact_corrections = [item for item in source["corrections"]
                             if item["SourceRevisionId"] == revision_id]
        exact_consultation = source["actualConsultation"] if exact_knowledge is not None else None
        frames.append(make_frame(
            revision_id,
            ("纠正后的当前 W" if revision_id == current_id else "历史 W；未导出该 W 的 PageView/Slice，保持未知"),
            revision, knowledge=exact_knowledge, pages=exact_pages, slices=[], historical_slices=[],
            corrections=exact_corrections, effectImpacts=exact_impacts,
            terminal=False if revision_id == current_id else None,
            outcome=None, actualConsultation=exact_consultation,
            capture=(revision.get("CurrentPageObservation") or {}).get("Capture")))
    save(root / EXAMPLES / "correction.json", wrapper(
        "correction", "WorldCorrectionImpactTests.CorrectedPlanBasis_ReassessesVerifiedReceipt_AndBlocksNextActAtStepBoundary",
        frames, ["来源只提供 rev-8 当前页视图；其他 W 没有可配对的 PageView 或 Slice，因此留空并保持未知。",
                  "受控布局、native context、provider 与 delivery double；未证明 live device/model。"],
        decisionUses=source["decisionUses"], receipts=source["receipts"],
        verifications=source["verifications"], effectImpacts=source["effectImpacts"],
        correctionLog=source["corrections"],
        immutableHistoricalRevisionId=source["immutableHistoricalRevisionId"],
        deliveredEffects=source["deliveredEffects"]))


def package_scroll(root: Path, source_path: Path | None = None) -> None:
    if source_path is not None and source_path.is_file():
        source = load(source_path)
        if source.get("schemaVersion") != "world-visualization.v1" or source.get("id") != "world-visualization:scroll":
            raise ValueError("scroll source must be a world-visualization.v1 scroll wrapper")
        if not isinstance(source.get("frames"), list) or not source["frames"]:
            raise ValueError("scroll source wrapper must contain actual frames")
        save(root / EXAMPLES / "scroll.json", source)
        return
    source = load(root / CHANGE_EVIDENCE / "actual-scroll-views.json")
    revisions = source["Revisions"]
    page_views = source["PageViews"]
    frames = []
    for revision in revisions:
        revision_id = revision["RevisionId"]
        exact_views = [page for page in page_views if page["SourceRevisionId"] == revision_id]
        if not exact_views:
            raise ValueError("scroll source has no exact-W OwnerPageView for " + revision_id)
        frames.append(make_frame(
            revision_id,
            "Owner snapshot " + revision_id + " with only exact-W PageViews",
            revision,
            knowledge=None,
            pages=exact_views,
            slices=[],
            historical_slices=[],
            capture=(revision.get("CurrentPageObservation") or {}).get("Capture"),
            content=[item for item in source["Content"] if item["RevisionId"] == revision_id],
        ))
    save(root / EXAMPLES / "scroll.json", wrapper(
        "scroll", "recorded scroll Owner PageViews from actual-scroll-views.json",
        frames, ["源材料导出了指定 revisions 与精确 W PageViews，但没有 Slice/Core projection；slices 留空。",
                 "revisions 是受控录制回放，不是真机或实时 provider 证据。"],
        graph=source["Graph"], receipts=source["Receipts"],
        content=source["Content"], status=source["Status"]))


def package_recorded(root: Path, source_path: Path) -> None:
    source = normalize(load(source_path))
    current = source.get("current")
    revisions = source.get("revisions")
    graph = source.get("graph")
    if not isinstance(current, dict) or not isinstance(revisions, list) or not isinstance(graph, dict):
        raise ValueError("recorded source must contain actual current, revisions, and graph exports")
    revision_id = current.get("revisionId")
    if not revision_id or graph.get("sourceRevisionId") != revision_id:
        raise ValueError("recorded graph and current revision must be the same actual W")
    pages = [source["worldPage"]] if source.get("worldPage", {}).get("sourceRevisionId") == revision_id else []
    actual_consultations = []
    for body in source.get("httpConsultBodies", []):
        try:
            request = json.loads(body) if isinstance(body, str) else body
        except (TypeError, json.JSONDecodeError):
            continue
        context = request.get("context") if isinstance(request, dict) else None
        world_knowledge = context.get("worldKnowledge") if isinstance(context, dict) else None
        if isinstance(world_knowledge, dict) and world_knowledge.get("sourceRevisionId") == revision_id:
            actual_consultations.append(request)
    slices = []
    if source.get("slice", {}).get("sourceRevisionId") == revision_id:
        slices.append({"sliceId": source.get("sliceId"), "slice": source["slice"],
                       "coreSlice": source.get("coreSlice"),
                       "presentation": source.get("slicePresentation")})
    frame = make_frame("recorded-current", "真实 Kernel/World current 与同 W 的录制来源页面",
        current, knowledge=source.get("knowledge"), pages=pages, slices=slices,
        actualConsultation=actual_consultations[-1] if actual_consultations else None,
        historical_slices=[], capture=(current.get("currentPageObservation") or {}).get("capture"), graph=graph)
    save(root / EXAMPLES / "recorded.json", wrapper(
        "recorded", "WorldKnowledgeChannelTests.RecordedSettingsLayout_FlowsThroughWorldDriverAndDshConsults_AndExportsFormalViewsWhenRequested",
        [frame], ["输入节点与 bounds 来自录制 XML；session/time/device 标签由测试控制。",
                  "DSH HTTP provider 是 double；最终 provider envelope 与 live model 未证。"],
        source=source.get("source"), consultationGapCounts=source.get("consultationGapCounts"),
        httpConsultBodies=source.get("httpConsultBodies", []), graph=graph,
        revisionHistory=revisions))


def build_index(root: Path) -> None:
    rows = []
    for family, title in FAMILY_TITLES.items():
        path = root / EXAMPLES / (family + ".json")
        if not path.is_file():
            if family in OPTIONAL_FAMILIES:
                continue
            raise FileNotFoundError("missing family example: " + str(path))
        payload = load(path)
        if payload.get("schemaVersion") != "world-visualization.v1":
            raise ValueError("incompatible example schema: " + str(path))
        rows.append({"id": payload["id"], "family": family, "title": title,
                     "file": path.name,
                     "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    save(root / EXAMPLES / "index.json", {"schemaVersion": "world-visualization.index.v1", "examples": rows})


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--recorded-source", type=Path,
        default=Path("changes/WORLD-VISUALIZATION-001/evidence/recorded-world-visualization-source.json"))
    parser.add_argument("--correction-source", type=Path,
        default=Path("changes/WORLD-VISUALIZATION-001/evidence/correction-world-visualization-source.json"))
    parser.add_argument("--scroll-source", type=Path,
        default=Path("changes/WORLD-VISUALIZATION-001/evidence/scroll-world-visualization-source.json"))
    args = parser.parse_args()
    root = args.repo_root.resolve()
    source_path = args.recorded_source if args.recorded_source.is_absolute() else root / args.recorded_source
    correction_source = args.correction_source if args.correction_source.is_absolute() else root / args.correction_source
    scroll_source = args.scroll_source if args.scroll_source.is_absolute() else root / args.scroll_source
    if not source_path.is_file():
        raise FileNotFoundError(
            "recorded source export is required; run the DSH recorded-layout test with "
            "UNICLAW_WORLD_VIS_RECORDED_EXPORT=" + str(source_path)
        )
    package_correction(root, correction_source if correction_source.is_file() else None)
    package_scroll(root, scroll_source if scroll_source.is_file() else None)
    package_recorded(root, source_path)
    build_index(root)


if __name__ == "__main__":
    main()
