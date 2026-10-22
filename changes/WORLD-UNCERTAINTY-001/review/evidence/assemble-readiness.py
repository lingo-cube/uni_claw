"""Assemble the review manifest; this is not a product acceptance runner."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
REVIEW = ROOT / "changes/WORLD-UNCERTAINTY-001/review"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT).decode().strip()


def main():
    inputs = """AGENTS.md
.agents/skills/uniflow/SKILL.md
.agents/skills/codebase-design/SKILL.md
.agents/skills/capability-component/SKILL.md
docs/agents/work-reporting.md
changes/README.md
schemas/work-item.schema.json
docs/architecture/product-architecture-baseline-l0-l3.md
docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md
docs/architecture/uworld-protocol-baseline-l4.md
docs/architecture/simulation-baseline-v0.1.md
docs/architecture/simulation-baseline-v0.2-c7-amendment.md
docs/architecture/simulation-baseline-v0.2-c7-kind-none-delta.md
docs/architecture/simulation-baseline-v0.2-c7-component-delta.md
docs/adr/0010-freshness-is-consumption-relative-sufficiency-judgment.md
docs/adr/0011-consumer-views-are-owner-derived-immutable-projections.md
docs/adr/0012-runtime-transition-context-is-non-evidentiary-association-prior.md
docs/adr/0014-continuity-demand-is-non-evidentiary-input-via-single-dual-mode-seam.md
docs/adr/0015-ui-element-identity-is-demand-gated-logical-item-continuity.md
docs/adr/0017-non-idempotent-action-safety-is-pre-dispatch.md
docs/adr/0023-reliable-execution-source-is-effect-boundary-owned-with-append-only-file-journal.md
docs/adr/0026-kernel-is-ui-domain-runtime-runtime-generalization-is-buyer-driven.md
docs/adr/0049-expected-effect-payload-for-post-action-verification.md
docs/adr/0050-budget-grant-via-consultation-with-policy-hard-cap.md
docs/adr/0052-goal-directed-budget-review-and-continuation.md
docs/adr/0054-confirmed-budget-recovery-and-unresolved-effect-barrier.md
src/UniClaw.Kernel/World/WorldModel.cs
src/UniClaw.Kernel/World/WorldModel.Semantics.cs
src/UniClaw.Kernel/World/WorldModel.Viewports.cs
src/UniClaw.Kernel/World/WorldBeliefRevision.cs
src/UniClaw.Kernel/World/Slice.cs
src/UniClaw.Kernel/World/PageObservation.cs
src/UniClaw.Kernel/World/UiRealization/ContainerAssociation.cs
src/UniClaw.Kernel/World/UiRealization/ProductAssociationStrategy.cs
src/UniClaw.Kernel/Core/CoreSemanticProjection.cs
src/UniClaw.Kernel/Evidence/Provenance.cs
src/UniClaw.Kernel/Evidence/EvidenceRecord.cs
src/UniClaw.Kernel/Capability/SemanticPerception.cs
src/UniClaw.Kernel/Perception/SemanticObservationProjector.cs
src/UniClaw.Kernel/Perception/UiHierarchy/HierarchyCoverage.cs
src/UniClaw.Kernel/Perception/UiHierarchy/TypedHierarchyProposalProjector.cs
src/UniClaw.Kernel/Control/ObservationRecoveryControl.cs
src/UniClaw.Kernel/Runtime/AgentDecision.cs
src/UniClaw.Kernel/Runtime/ConsultationTypes.cs
src/UniClaw.Kernel/Runtime/KernelRunDriver.cs
src/UniClaw.Kernel/Effects/EffectBoundary.cs
src/UniClaw.Kernel/Effects/TargetBinding.cs
src/UniClaw.Kernel/Effects/EffectDispatch.cs
src/UniClaw.Kernel/Assurance/RuntimeAssurance.PostAction.cs
src/UniClaw.Host/UiAutomatorDump.cs
src/UniClaw.Agent.Dsh/DshAgentAdapter.cs
src/UniClaw.Agent.Dsh/DshOpenedHttpPeer.cs
src/UniClaw.Agent.Dsh/ProtocolSchema.cs
src/UniClaw.Agent.Dsh/ProtocolModels.cs
dsh/uniclaw-decision-channel/src/index.js
scenarios/README.md
testsets/simulation-baseline/manifest.json
tests/UniClaw.Simulation.Tests/ComponentWorldReplayTests.cs
tests/UniClaw.Simulation.Tests/ComponentSemanticReplayTests.cs
tests/UniClaw.Simulation.Tests/ControlledCheckedReplayTests.cs
tests/UniClaw.Simulation.Tests/ComponentScenarioScopeTests.cs
tests/UniClaw.Simulation.Tests/ComponentReplay/ComponentReplayCases.cs
tests/UniClaw.Simulation.Tests/ComponentReplay/RecordedComponentInputs.cs
tests/UniClaw.Simulation.Tests/ComponentReplay/ReplayCompositionGuard.cs
tests/UniClaw.Simulation.Tests/Fixtures/ComponentReplay/composition.json
tests/UniClaw.Simulation.Tests/Fixtures/ComponentReplay/suite.json
tests/UniClaw.Simulation.Tests/Fixtures/Replay/recent-recordings.json
tools/scenario_certify.py
tests/UniClaw.Simulation.Tests/WorldScenarioReplayTests.cs
tests/UniClaw.Simulation.Tests/WorldReconstructionTests.cs
tests/UniClaw.Simulation.Tests/RecentRecordingReplayTests.cs
tests/UniClaw.Kernel.Tests/CoreProjectionSeamTests.cs
tests/UniClaw.Kernel.Tests/WorldModelCanonicalOracleTests.cs
tests/UniClaw.Kernel.Tests/Perception/FrameComputationReuseTests.cs
tests/UniClaw.Kernel.Tests/World/PageObservationRecoveryTests.cs
tests/UniClaw.Kernel.Tests/World/WorldSemanticEvidenceTests.cs
tests/UniClaw.Kernel.Tests/World/WorldObservedNavigationTests.cs
tests/UniClaw.Kernel.Tests/World/WorldViewportTransitionTests.cs
tests/UniClaw.Kernel.Tests/Runtime/ObservationRecoveryControlTests.cs
tests/UniClaw.Agent.Dsh.Tests/DecisionChannelClosureTests.cs
changes/WORLD-UNCERTAINTY-001/evidence/freeze-manifest.json
changes/WORLD-UNCERTAINTY-001/evidence/thread-dispatch.json""".splitlines()
    examined_change_docs = set("state spec implementation-handoff derivation-review-request handoff-coverage-audit model-interface-review multi-visit-completeness trusted-element-reuse-design correction-impact-design agent-information q5-design-assessment node-edge-design page-expression-design version-layer-design replay-input-audit coverage-audit goal-completion-mapping".split())
    freeze_path = ROOT / "changes/WORLD-UNCERTAINTY-001/evidence/freeze-manifest.json"
    freeze = json.loads(freeze_path.read_text())
    freeze_comparison = []
    for name, expected in freeze["documents"].items():
        inputs.append(name)
        actual = digest(ROOT / name)
        freeze_comparison.append({"path": name, "expectedSha256": expected, "actualSha256": actual, "matches": actual == expected})
    inputs.extend(str(p.relative_to(ROOT)) for p in sorted((ROOT / "scenarios").glob("SCN-*.json")))
    input_rows = []
    for name in sorted(set(inputs)):
        role = "source_or_contract_examined_at_relevant_sections"
        if name in freeze["documents"] and Path(name).stem not in examined_change_docs and not name.startswith("docs/adr/0054") and not name.startswith("changes/WORLD-VISUALIZATION-001/"):
            role = "frozen_dependency_hash_verified_not_independently_researched"
        if name.endswith("thread-dispatch.json"):
            role = "coordinator_owned_working_routing_evidence_uncommitted_at_entry"
        if name.startswith("scenarios/SCN-"):
            role = "catalog_metadata_parsed_not_test_result"
        input_rows.append({"path": name, "sha256": digest(ROOT / name), "role": role})
    text = (REVIEW / "scenario-derivation.md").read_text()
    decisions = re.findall(r"^\| (Q\d+) \|", text, re.M)
    themes = re.findall(r"^\| ((?:MV|RU|CI)\d{2}) \|", text, re.M)
    expected_decisions = {f"Q{i}" for i in range(1, 21)}
    expected_themes = {f"{prefix}{i:02}" for prefix, count in (("MV", 18), ("RU", 8), ("CI", 12)) for i in range(1, count + 1)}
    assert set(decisions) == expected_decisions and len(decisions) == 20
    assert set(themes) == expected_themes and len(themes) == 38
    assert git("rev-parse", "HEAD") == "fddc69a6478e9b462c5f4bba9be736eac8f0b635", "HEAD drift: reassess before readiness"
    assert all(row["matches"] for row in freeze_comparison), "frozen input drift requires explicit disposition"
    early = [
        ("E01", "四类生产/消费/保持/解决及共因", ["K07", "K12"]),
        ("E02", "真实独立尝试/结果、未采集不填零", ["K08"]),
        ("E03", "持续相关可见、解决/不相关分开", ["K08", "K12"]),
        ("E04", "安全边界提醒去重、新结果再提醒、资源不重置", ["K08", "K10"]),
        ("E05", "旧输入资格/统计分母/B1诊断非发布门槛", ["K11"]),
        ("E06", "scope-id/局部范围/历史依据稳定", ["K01"]),
        ("E07", "正式Agent最终正文/第二次咨询/非首轮能力目录", ["K12"]),
    ]
    findings_data = [
        ("F01", "high", "World/Core", "CoreSemanticProjection scope-id及页内范围/legacy依据", ["K01"]),
        ("F02", "high", "World", "正式多轮累计与有目标历史归属纠错；current gate不放宽", ["K02", "K03", "K04"]),
        ("F03", "high", "World/Perception scheduling", "历史先可读、字段适用后当前沿用；免重复慢调用", ["K05", "K06"]),
        ("F04", "high", "World/observation producer", "四类及最小上下文/覆盖来源完整生产消费", ["K07", "K12"]),
        ("F05", "high", "Execution producer/Control/Runtime", "真实尝试来源、持续可见与再提醒，不以schedule数代替", ["K08"]),
        ("F06", "high", "World/Control/Assurance/Effect/Run", "实际消费依赖与未知影响边界；按原Owner核查效果", ["K09", "K10"]),
        ("F07", "high", "Kernel/Host/Agent.Dsh", "严格codec/生成物及实际咨询正文双轮证明", ["K11", "K12"]),
        ("F08", "medium", "World/Replay consumer", "复用非权威表达，产品无tests依赖，展示buyer信息保留", ["K03", "K12"]),
    ]
    findings = [{"id": i, "severity": severity, "owner": owner, "scope": "accepted first-period implementation", "finding": description, "disposition": "resolved_in_review_as_required_implementation_change", "reviewBlocking": False, "implementationBlockingIfOmitted": True, "validationCases": cases, "evidence": ["architecture-review.md", "model-interface-disposition.md", "scenario-inventory.md"]} for i, severity, owner, description, cases in findings_data]
    review_names = ["scenario-inventory.md", "scenario-derivation.md", "architecture-review.md", "model-interface-disposition.md", "optimization-disposition.md", "evidence/entry.json", "evidence/scenario-catalog.json", "evidence/finite-derivation.py", "evidence/finite-derivation.json", "evidence/assemble-readiness.py", "evidence/recording-pin-audit.json", "evidence/scenario-certification-check.txt"]
    result = {
        "schemaVersion": "world-uncertainty-independent-review.v1",
        "clientDate": "2026-10-23",
        "status": "ready",
        "statusMeaning": "Independent design review ready for implementation; no product acceptance or CLOSED claim",
        "reviewer": {"role": "independent review Leader", "threadIdFromCoordinatorEvidence": "01a164bf-de01-77d2-beee-ebcc5064b28d", "requestedModel": "gpt-6.1-sol", "requestedReasoningEffort": "ultra", "modelEvidenceBoundary": "coordinator routing evidence records requested model/create success; no independent inference-time model attestation", "worker": {"agentType": "worker", "forkTurns": "none", "explicitModel": "gpt-6-luna", "usableArtifact": False, "disposition": "leader completed audit, no silent substitution"}},
        "reviewedHead": git("rev-parse", "HEAD"),
        "simulationInputHead": "d53e19f44f11038224408597a5fd72001f7b5a67",
        "inputFiles": input_rows,
        "frozenInputVerification": freeze_comparison,
        "headDrift": [],
        "inputDrift": [],
        "reviewFiles": [{"path": str((REVIEW / name).relative_to(ROOT)), "sha256": digest(REVIEW / name)} for name in review_names],
        "selfHashPolicy": "readiness.json excludes itself; later validation/publication records are supplemental and never self-hash a commit",
        "caseCoverage": {"mapping": "scenario-derivation.md", "currentAssertions": "scenario-inventory.md", "decisions": [{"id": i, "disposition": "mapped_with_current_assertions_gap_and_validation_or_defer", "productStatus": "not_run"} for i in decisions], "themes": [{"id": i, "disposition": "mapped_to_detailed_family_not_product_pass", "productStatus": "not_run"} for i in themes], "earlyRequirements": [{"id": i, "requirement": description, "validationCases": cases, "disposition": "required_implementation", "productStatus": "not_run"} for i, description, cases in early], "detailedFamilies": [f"K{i:02}" for i in range(1, 13)], "catalog": json.loads((REVIEW / "evidence/scenario-catalog.json").read_text())["summary"]},
        "axiomAssumptions": [
            {"id": "A1", "assumption": "既有Owner authority与accepted evidence边界", "source": "baseline sections 11-16; P2/P3/P22/P23"},
            {"id": "A2", "assumption": "固定记录不可变，发生/处理/接纳分轴，无canonical clock", "source": "ADR-0010/0011/0023; Q17/Q20"},
            {"id": "A3", "assumption": "访问发生须合法来源，身份不等访问", "source": "Q17; MV01/03/17/18"},
            {"id": "A4", "assumption": "固定scope语义可规范编码；hash碰撞不作严格数学无碰撞证明", "source": "P4; Q17"},
            {"id": "A5", "assumption": "累计须整组时间/空间/对象兼容，不能假设两两相容可传递", "source": "P4/UWM coverage; Q14"},
            {"id": "A6", "assumption": "对应/适用/反证充分由World判断，动态字段独立", "source": "Q19; ADR-0010/0011/0015"},
            {"id": "A7", "assumption": "实际执行关联由producer给出且声明完整性", "source": "Q6/Q8/Q10"},
            {"id": "A8", "assumption": "有限固定实际依赖图；完备边才有完整影响闭包，缺边仅下界；允许环和独立支撑", "source": "Q15/Q20"},
            {"id": "A9", "assumption": "可靠提交边界/当前状态/授权及原Owner效果判定，不假设现实可逆", "source": "baseline 24.3/24.4; ADR-0017/0023/0049/0054"},
            {"id": "A10", "assumption": "局部活性需来源最终可得/公平处理/足够预算授权，不证明全局停机或成功", "source": "ADR-0052; Q7/Q10/Q19"},
            {"id": "A11", "assumption": "Unknown/Absent/Conflict/历史/未采集不可折叠；Oracle非真值", "source": "UWM; Q1/Q5/Q9/Q11/Q18"},
        ],
        "findings": findings,
        "blockingFindings": [],
        "requiredImplementationChanges": [{"findingRef": f["id"], "owner": f["owner"], "change": f["finding"], "validationCases": f["validationCases"], "status": "required_not_implemented", "contractDetail": "model-interface-disposition.md"} for f in findings],
        "architectureVerdict": {"verdict": "ready_for_implementation_with_required_changes", "unresolvedOwnerOrAdrConflicts": [], "fivePrinciples": "architecture-review.md", "newCanonicalOwners": False, "newRegistryCapabilityRequired": False, "worldVisualizationBuyer": "core lineage/scope/version/impact retained; visualization only after core CLOSED and pushed"},
        "deferred": ["Q2 Runtime confirmed budget recovery implementation, ADR-0054 retained", "formal Goal integration Q13", "full Settings traversal/H7/navigation purpose", "generic identity merge/full relations/lifecycle/cross-Container continuity", "generic compensation/full runtime recovery", "calibrated B1 publication threshold and B2/C divergent consequences"],
        "verification": {"reviewLevel": "CONTRACT + bounded abstract calculation", "method": "source/contract cross-check + read-only catalog and recording-pin parsing + scenario_certify.py --check + finite-derivation.py + manifest coverage/hash validation", "expected": "20 decisions/38 themes/early requirements mapped, sources and reports hashable, no unresolved review semantics/Owner/ADR blockers", "actual": "review obligations mapped; eight implementation requirements retained; certification check 41 files/0 violations; recording/composition 94 references match and exist in reviewed commit; finite assertions satisfied only in declared abstract domains", "evidence": ["scenario-inventory.md", "scenario-derivation.md", "architecture-review.md", "model-interface-disposition.md", "optimization-disposition.md", "evidence/finite-derivation.json", "evidence/recording-pin-audit.json", "evidence/scenario-certification-check.txt"], "productTests": "not_run", "productSimulation": "not_run", "device": "not_run", "realProductModel": "not_run", "actualFinalProviderInput": "not_collected_in_review", "performance": "not_measured", "productPass": False, "productClosed": False},
    }
    (REVIEW / "readiness.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps({"status": result["status"], "inputFiles": len(input_rows), "reviewFiles": len(review_names), "decisions": len(decisions), "themes": len(themes), "earlyRequirements": len(early), "reviewBlocking": 0, "requiredImplementationChanges": len(findings), "frozenInputsMatch": len(freeze_comparison)}))


if __name__ == "__main__":
    main()
