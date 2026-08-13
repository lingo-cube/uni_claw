"""perception-model-intelligence helper — READ-ONLY derived reporting.

MACHINE MANIFESTS ARE TRUTH.
HUMAN REPORTS EXPLAIN TRUTH; THEY NEVER CREATE TRUTH.

This helper reads canonical manifests only. Its ONLY writes are the three
derived human report files. It has zero model/release/activation authority.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

PERCEPTION = Path(__file__).resolve().parents[2]   # platforms/perception/
TRAINING_MANIFESTS = PERCEPTION / "training" / "artifacts" / "manifests"
TRAINING_RUNS_DIR = TRAINING_MANIFESTS / "runs"
CANDIDATES_DIR = TRAINING_MANIFESTS / "candidates"
MODEL_ARTIFACTS_DIR = TRAINING_MANIFESTS / "model-artifacts"
LINEAGE_DIR = TRAINING_MANIFESTS / "lineage"
MODEL_STORE = PERCEPTION / "training" / "artifacts" / "model-store"
ULTRALYTICS_RUNS = PERCEPTION / "training" / "artifacts" / "runs" / "ultralytics"
EVAL_REPORTS = PERCEPTION / "evaluation" / "reports"
GOVERNANCE_ARTIFACTS = PERCEPTION / "governance" / "artifacts"
REPORTS_DIR = PERCEPTION / "reports"          # the ONLY writable location
ACTIVE_IDENTITY = GOVERNANCE_ARTIFACTS / "current-active-identity.json"

CANONICAL_DIRS = (
    TRAINING_MANIFESTS, EVAL_REPORTS / "runs", EVAL_REPORTS / "baselines",
    EVAL_REPORTS / "predictions", GOVERNANCE_ARTIFACTS, MODEL_STORE,
)

# ── classification ─────────────────────────────────────────────

KEEP_CANONICAL = "KEEP — CANONICAL"
KEEP_DIAGNOSTIC = "KEEP / ARCHIVE — DIAGNOSTIC"
DERIVED_DISPOSABLE = "DERIVED / DISPOSABLE"
UNKNOWN_REVIEW = "UNKNOWN — REVIEW BEFORE DELETE"

DIAGNOSTIC_NAMES = {
    "results.csv", "results.png", "PR_curve.png", "F1_curve.png",
    "P_curve.png", "R_curve.png", "BoxPR_curve.png", "BoxF1_curve.png",
    "BoxP_curve.png", "BoxR_curve.png", "confusion_matrix.png",
    "confusion_matrix_normalized.png", "labels.jpg",
    "labels_correlogram.jpg", "args.yaml",
}
_BATCH_PREFIXES = ("train_batch", "val_batch")


def classify_artifact(rel_path: str) -> str:
    """Classify one file by repository evidence + name — never by name guess
    alone: canonical directory membership and known diagnostic patterns
    are the only inputs. Anything else → UNKNOWN (fail safe)."""
    name = Path(rel_path).name
    base = Path(rel_path).parts[0] if Path(rel_path).parts else ""
    if base in {"manifests", "governance"} or rel_path.startswith("evaluation"):
        return KEEP_CANONICAL
    if name in DIAGNOSTIC_NAMES:
        return KEEP_DIAGNOSTIC
    if name.startswith(_BATCH_PREFIXES):
        return KEEP_DIAGNOSTIC
    if rel_path.startswith("model-store"):
        return KEEP_CANONICAL          # content-addressed ModelArtifact
    if rel_path.startswith("mini-data"):
        return DERIVED_DISPOSABLE      # materialized training view
    if name.endswith(".pt") and "weights" in rel_path:
        return KEEP_DIAGNOSTIC         # framework output; canonical copy is
                                       # the content-addressed artifact
    return UNKNOWN_REVIEW


# ── canonical readers ──────────────────────────────────────────

def _read(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _sorted_files(d: Path) -> list[Path]:
    return sorted(d.glob("*.json")) if d.exists() else []


def current_active() -> dict[str, Any]:
    if not ACTIVE_IDENTITY.exists():
        return {}
    return _read(ACTIVE_IDENTITY).get("active", {})


def completed_runs() -> list[dict[str, Any]]:
    out = []
    for f in _sorted_files(TRAINING_RUNS_DIR):
        d = _read(f)
        if d.get("state") == "COMPLETED":
            out.append(d)
    return out


def failed_runs() -> list[dict[str, Any]]:
    out = []
    for f in _sorted_files(TRAINING_RUNS_DIR):
        d = _read(f)
        if d.get("state") == "FAILED":
            out.append(d)
    return out


def latest_candidate() -> dict[str, Any]:
    """The candidate referenced by the newest lineage link — content-based,
    not timestamp-based."""
    cands = [_read(f) for f in _sorted_files(CANDIDATES_DIR)]
    if not cands:
        return {}
    # the candidate whose TrainingRun is the latest COMPLETED run
    completed_ids = {r.get("trainingRunId") for r in completed_runs()}
    linked = [c for c in cands if c.get("trainingRunId") in completed_ids]
    return (linked or cands)[-1]


def candidate_evaluation_run(candidate: dict[str, Any]) -> dict[str, Any]:
    """The EvaluationRun whose deployment model_id matches the candidate."""
    mid = candidate.get("modelArtifactId")
    for f in _sorted_files(EVAL_REPORTS / "runs"):
        d = _read(f)
        if d.get("deployment", {}).get("model_id") == mid:
            return d
    return {}


def all_evaluation_runs() -> list[dict[str, Any]]:
    return [_read(f) for f in _sorted_files(EVAL_REPORTS / "runs")]


# ── source snapshot (stale detection) ──────────────────────────

@dataclass(frozen=True)
class SourceSnapshot:
    current_deployment_id: str
    latest_candidate_id: str
    latest_completed_run_id: str
    candidate_eval_run_id: str

    def to_line(self) -> str:
        return "|".join((self.current_deployment_id, self.latest_candidate_id,
                         self.latest_completed_run_id, self.candidate_eval_run_id))


def derive_snapshot() -> SourceSnapshot:
    active = current_active()
    cand = latest_candidate()
    eval_run = candidate_evaluation_run(cand)
    completed = completed_runs()
    return SourceSnapshot(
        current_deployment_id=active.get("deploymentId", "NOT_ESTABLISHED"),
        latest_candidate_id=cand.get("candidateId", "NOT_ESTABLISHED"),
        latest_completed_run_id=(completed[-1].get("trainingRunId", "NOT_ESTABLISHED")
                                 if completed else "NOT_ESTABLISHED"),
        candidate_eval_run_id=eval_run.get("runId", "NOT_ESTABLISHED"),
    )


def report_snapshot(report_path: Path) -> str | None:
    """Extract the DerivedFrom snapshot line from a report, if present."""
    if not report_path.exists():
        return None
    for line in report_path.read_text(encoding="utf-8").splitlines():
        if line.startswith("DerivedFrom:"):
            return line.split(":", 1)[1].strip()
    return None


def is_stale(report_path: Path, snapshot: SourceSnapshot) -> bool:
    return report_snapshot(report_path) != snapshot.to_line()


# ── chart / metric language ────────────────────────────────────

CHART_EXPLANATIONS: dict[str, tuple[str, str]] = {
    "results.png": (
        "训练过程中各项指标随 epoch 变化的曲线。",
        "看是否收敛、是否过拟合（val 与 train 明显分叉）。"),
    "PR_curve.png": (
        "检测任务的 Precision-Recall 曲线。",
        "曲线越靠近右上角越好；某类别曲线明显差说明该类难分。"),
    "F1_curve.png": (
        "不同置信度阈值下 F1 分数的变化。",
        "看最优阈值区间；平台当前使用固定阈值，曲线只作诊断。"),
    "P_curve.png": (
        "不同置信度阈值下的 Precision 变化。",
        "阈值越高 Precision 通常越高，但召回会下降。"),
    "R_curve.png": (
        "不同置信度阈值下的 Recall 变化。",
        "阈值越低 Recall 通常越高，但误检会变多。"),
    "confusion_matrix.png": (
        "各类别之间的混淆矩阵。",
        "对角线以外的高值 = 容易混淆的类别对。"),
    "confusion_matrix_normalized.png": (
        "按行归一化的混淆矩阵（百分比版）。",
        "更适合看每个类别的错误去向。"),
    "labels.jpg": (
        "训练标注可视化（每张图画了多少框）。",
        "检查标注质量：漏标、错标、框偏移。"),
    "labels_correlogram.jpg": (
        "标注框的尺寸/位置分布图。",
        "看训练框的几何分布是否覆盖真实场景。"),
}

_BATCH_MEANING = (
    "训练/验证批次预览图。", "检查输入图与标注是否对齐、是否出现坏样本。")

DISCLAIMER_CHART = (
    "注意：训练图表是训练诊断证据，不是 Runtime 失败证明，也不是发布证据。"
    "如需判断上线影响，必须结合 Evaluation 结果与 Scenario 证据。")

METRIC_LANGUAGE = {
    "Precision": "模型判断为某类时，有多少是真的。",
    "Recall": "真实存在的某类元素里，模型找回了多少。",
    "mAP": "训练/验证层面的综合检测指标之一。",
    "loss": "训练损失函数值，越低表示训练过程拟合越好（不等于模型好）。",
}
METRIC_DISCLAIMER = "以上指标用于训练诊断，不直接决定是否上线。"


def explain_chart(name: str) -> str:
    if name in CHART_EXPLANATIONS:
        meaning, look_for = CHART_EXPLANATIONS[name]
        return (f"这张图（{name}）的 ML 含义：{meaning} 该看什么：{look_for} "
                + DISCLAIMER_CHART)
    if name.startswith(_BATCH_PREFIXES):
        return (f"这张图（{name}）的 ML 含义：{_BATCH_MEANING[0]}"
                f" 该看什么：{_BATCH_MEANING[1]} " + DISCLAIMER_CHART)
    return (f"未识别的图表文件（{name}）。无法凭文件名给出可靠解释——"
            "请查看对应的 TrainingRun 与 canonical manifest。 "
            + DISCLAIMER_CHART)


def explain_metric(metric: str) -> str:
    text = METRIC_LANGUAGE.get(metric, f"训练指标 {metric}（含义需查训练框架文档）。")
    return f"{text} {METRIC_DISCLAIMER}"


# ── report rendering ───────────────────────────────────────────

def render_current_md(snapshot: SourceSnapshot) -> str:
    active = current_active()
    cand = latest_candidate()
    eval_run = candidate_evaluation_run(cand)
    completed = completed_runs()
    last_run = completed[-1] if completed else {}
    failures = failed_runs()

    lines = [
        "# UniClaw Perception 当前状态",
        "",
        "> MACHINE MANIFESTS ARE TRUTH. HUMAN REPORTS EXPLAIN TRUTH; "
        "THEY NEVER CREATE TRUTH.",
        "> TRAINING METRICS HAVE ZERO RELEASE AUTHORITY.",
        "",
        f"DerivedFrom: {snapshot.to_line()}",
        "",
        "## 当前生产部署",
        "",
    ]
    if active:
        lines += [
            f"- modelName: `{active.get('modelName', 'UNKNOWN')}`",
            f"- ModelId: `{active.get('modelId', 'UNKNOWN')}`",
            f"- ConfigId: `{active.get('configId', 'UNKNOWN')}`",
            f"- PipelineRevision: `{active.get('pipelineRevision', 'UNKNOWN')}`",
            f"- DeploymentId: `{active.get('deploymentId', 'UNKNOWN')}`",
            f"- provenance stance: `{active.get('provenanceStance', 'LEGACY_PROVENANCE_PARTIAL')}`",
        ]
    else:
        lines.append("- NOT ESTABLISHED")
    lines += ["", "## 最新 Candidate", ""]
    if cand:
        lines += [
            f"- identity: `{cand.get('candidateId', 'UNKNOWN')}`",
            f"- status: `{cand.get('status', 'UNKNOWN')}`",
            f"- source TrainingRun: `{cand.get('trainingRunId', 'UNKNOWN')}`",
            f"- source DatasetVersion: `{cand.get('datasetVersionId', 'UNKNOWN')}`",
        ]
        if eval_run:
            lines.append(
                f"- Evaluation state: 已存在 EvaluationRun `{eval_run.get('runId')}`"
                "（不等于 VALIDATED——VALIDATED 语义属于未来的比较/发布层）")
        else:
            lines.append("- Evaluation state: 无关联 EvaluationRun（尚未经过评估，不等于无效）")
        lines.append("- Candidate-vs-ACTIVE comparison state: NOT ESTABLISHED"
                     "（EvaluationComparison 尚未实现）")
    else:
        lines.append("- NOT ESTABLISHED")
    lines += ["", "## 最近一次训练", ""]
    if last_run:
        lines += [
            f"- TrainingRun: `{last_run.get('trainingRunId', 'UNKNOWN')}`",
            f"- status: `{last_run.get('state', 'UNKNOWN')}` / "
            f"`{last_run.get('terminalOutcome', 'UNKNOWN')}`",
            f"- dataset: `{last_run.get('datasetVersionId', 'UNKNOWN')}`",
            f"- TrainingConfig: `{last_run.get('trainingConfigId', 'UNKNOWN')}`",
            f"- codeRevision: `{last_run.get('codeRevision', 'UNKNOWN')}`"
            f" (dirty={last_run.get('dirty', 'UNKNOWN')})",
        ]
        for cp in last_run.get("producedCheckpoints", []):
            lines.append(
                f"- checkpoint `{cp.get('name')}`: `{cp.get('checkpointId')}`"
                "（checkpoint 名只是训练角色，不是模型身份）")
    else:
        lines.append("- NOT ESTABLISHED")
    lines += ["", "## Evaluation 状态", ""]
    evals = all_evaluation_runs()
    if evals:
        lines.append(f"- 现有 EvaluationRun 数量: {len(evals)}")
        lines.append("- EvidenceSufficiency（首个基线）: PARTIAL")
        lines.append("- 已知未评估切片: OneUI/switch-state/holdout（见缺口）")
    else:
        lines.append("- NOT ESTABLISHED")
    lines += ["", "## Release 状态", "",
              "AUTHORITATIVE RELEASE DECISION: NOT ESTABLISHED",
              "",
              "（不存在权威发布决策。这不是『模型失败』，只是尚未进入发布治理。）",
              "", "## 当前已知缺口", ""]
    gaps = [
        "- Holdout: NOT_ESTABLISHED",
        "- 数值发布阈值: NOT_FROZEN",
        "- EvaluationProfile: NOT_IMPLEMENTED",
        "- ReleasePolicy: DEFERRED（架构已购买，实现推迟）",
        "- Candidate-vs-ACTIVE 比较: NOT_IMPLEMENTED",
    ]
    if failures:
        gaps.append(f"- 存在 {len(failures)} 个 FAILED TrainingRun（诚实保留，未产生 ModelArtifact）")
    lines += gaps + [""]
    return "\n".join(lines)


def render_training_runs_md(snapshot: SourceSnapshot) -> str:
    lines = [
        "# UniClaw Perception 训练历史（人读视图）",
        "",
        "> TRAINING METRICS HAVE ZERO RELEASE AUTHORITY. "
        "本表不按 mAP 排序，不评选『最佳模型』。",
        "",
        f"DerivedFrom: {snapshot.to_line()}",
        "",
    ]
    for run in _sorted_files(TRAINING_RUNS_DIR):
        d = _read(run)
        state = d.get("state", "UNKNOWN")
        cand = next((c for c in
                     [_read(f) for f in _sorted_files(CANDIDATES_DIR)]
                     if c.get("trainingRunId") == d.get("trainingRunId")), {})
        eval_run = candidate_evaluation_run(cand)
        note = {
            "COMPLETED": "Process closure mini run; not model-quality evidence."
                         if d.get("datasetVersionId", "").startswith("dataset:c7abaf")
                         else "Real candidate training; evaluation pending.",
            "FAILED": "FAILED run preserved; no ModelArtifact.",
            "RUNNING": "Operational attempt; noncanonical.",
        }.get(state, "")
        lines += [
            f"## Run `{d.get('trainingRunId', 'UNKNOWN')}`",
            "",
            f"- Outcome: `{state}` — `{d.get('terminalOutcome', '')}`",
            f"- Model: `{cand.get('modelName', '—')}`"
            f" / `{cand.get('modelArtifactId', '无 ModelArtifact')}`",
            f"- Dataset: `{d.get('datasetVersionId', 'UNKNOWN')}`",
            f"- TrainingConfig: `{d.get('trainingConfigId', 'UNKNOWN')}`",
            f"- Checkpoint / ModelArtifact: "
            + ", ".join(f"`{cp.get('name')}`→`{cp.get('checkpointId')}`"
                        for cp in d.get("producedCheckpoints", []))
            + (" / 无" if not d.get("producedCheckpoints") else ""),
            f"- Candidate: `{cand.get('candidateId', '无')}`",
            f"- Evaluation: `{eval_run.get('runId', '无关联 EvaluationRun')}`",
            f"- Purpose: {note or 'UNKNOWN'}",
            f"- Human note: {note or 'UNKNOWN'}",
            "",
        ]
    return "\n".join(lines)


def render_artifact_guide_md(snapshot: SourceSnapshot) -> str:
    rows = [
        ("best.pt", KEEP_DIAGNOSTIC,
         "训练过程按其验证标准选出的 checkpoint（角色名）。",
         "无任何模型/release 权威。ModelId 才是内容身份。",
         "否（除非重新训练）",
         "看 TrainingRun 的 producedCheckpoints 与 ModelArtifact，而不是文件名。"),
        ("last.pt", KEEP_DIAGNOSTIC,
         "最后一个 epoch 的 checkpoint。",
         "无权威。",
         "否",
         "同上。"),
        ("args.yaml", KEEP_DIAGNOSTIC,
         "训练框架的调用参数记录。",
         "无权威；canonical 是 TrainingConfig manifest。",
         "是（由 TrainingConfig 派生）",
         "与 tcfg 清单交叉核对。"),
        ("results.csv", KEEP_DIAGNOSTIC,
         "训练逐 epoch 指标原始数据。",
         "无权威。",
         "否",
         "看收敛趋势，不用于上线判断。"),
        ("results.png", KEEP_DIAGNOSTIC, "指标曲线图。", "无权威。", "是（由 csv 重绘）",
         "看 train/val 分叉。"),
        ("PR_curve.png", KEEP_DIAGNOSTIC, "PR 曲线。", "无权威。", "是", "看类别差异。"),
        ("F1_curve.png", KEEP_DIAGNOSTIC, "F1-阈值曲线。", "无权威。", "是", "仅诊断。"),
        ("P_curve.png", KEEP_DIAGNOSTIC, "Precision-阈值曲线。", "无权威。", "是", "仅诊断。"),
        ("R_curve.png", KEEP_DIAGNOSTIC, "Recall-阈值曲线。", "无权威。", "是", "仅诊断。"),
        ("confusion_matrix.png", KEEP_DIAGNOSTIC, "混淆矩阵。", "无权威。", "是",
         "看易混淆类别对（如 switch↔icon）。"),
        ("confusion_matrix_normalized.png", KEEP_DIAGNOSTIC, "归一化混淆矩阵。",
         "无权威。", "是", "看错误去向。"),
        ("labels.jpg", KEEP_DIAGNOSTIC, "标注可视化。", "无权威。", "是", "查标注质量。"),
        ("labels_correlogram.jpg", KEEP_DIAGNOSTIC, "标注框分布。", "无权威。", "是",
         "查几何分布。"),
        ("train_batch*.jpg / val_batch*_*.jpg", KEEP_DIAGNOSTIC,
         "批次预览图。", "无权威。", "是", "查输入对齐。"),
        ("manifests/*.json", KEEP_CANONICAL,
         "DatasetVersion / TrainingRun / Candidate / lineage 等机器真理清单。",
         "机器真理（只读）。", "否（内容哈希不可再生成）",
         "直接读 JSON；本 Skill 生成的报告只是人读视图。"),
        ("model-store/<modelId>.pt", KEEP_CANONICAL,
         "内容寻址的 ModelArtifact 本体。",
         "canonical 模型字节（modelId = 字节 SHA-256）。", "否",
         "以 modelId 引用，永远不要以文件名引用。"),
        ("mini-data/", DERIVED_DISPOSABLE,
         "物化的训练目录视图（images/labels/data.yaml）。",
         "无权威；canonical 是 DatasetVersion manifest。", "是（从种子生成代码重生成）",
         "删除不影响 dataset 身份。"),
        ("runs/ultralytics/…/weights/*.pt", KEEP_DIAGNOSTIC,
         "训练框架输出的权重文件。",
         "无权威；canonical 副本在 model-store。", "仅通过重新训练",
         "删除前确认 model-store 中存在相同内容的副本。"),
    ]
    lines = [
        "# UniClaw Perception 训练工件指南（人读视图）",
        "",
        "> 分类四档：KEEP — CANONICAL / KEEP / ARCHIVE — DIAGNOSTIC / "
        "DERIVED / DISPOSABLE / UNKNOWN — REVIEW BEFORE DELETE。",
        "> 不熟悉的文件一律先归 UNKNOWN，绝不建议删除。",
        "",
        f"DerivedFrom: {snapshot.to_line()}",
        "",
        "| Artifact | Category | 是什么 | Authority | 可重新生成 | 怎么读 |",
        "|---|---|---|---|---|---|",
    ]
    for name, cat, what, authority, regenerable, how in rows:
        lines.append(f"| `{name}` | {cat} | {what} | {authority} | {regenerable} | {how} |")
    lines.append("")
    return "\n".join(lines)


def render_compare_md(run_a: dict[str, Any], run_b: dict[str, Any],
                      snapshot: SourceSnapshot) -> str:
    lines = [
        "# 训练运行对比（人读视图）",
        "",
        "> **TRAINING-RUN COMPARISON IS NOT RELEASE COMPARISON.**",
        "> 训练指标差异不推导生产优劣。",
        "",
        f"DerivedFrom: {snapshot.to_line()}",
        "",
        f"## Run A `{run_a.get('trainingRunId', 'UNKNOWN')}`",
        f"- outcome: `{run_a.get('state')}`",
        f"- dataset: `{run_a.get('datasetVersionId')}`",
        f"- TrainingConfig: `{run_a.get('trainingConfigId')}`",
        "",
        f"## Run B `{run_b.get('trainingRunId', 'UNKNOWN')}`",
        f"- outcome: `{run_b.get('state')}`",
        f"- dataset: `{run_b.get('datasetVersionId')}`",
        f"- TrainingConfig: `{run_b.get('trainingConfigId')}`",
        "",
        "## 结论",
        "",
        "只允许陈述训练事实（例如：『Run B 的验证 mAP 高于 Run A』）。",
        "禁止陈述：『Run B 更好，应该替换 ACTIVE』。",
        "",
    ]
    return "\n".join(lines)


# ── the ONLY write capability ──────────────────────────────────

def write_reports(reports_dir: str | Path | None = None,
                  snapshot: SourceSnapshot | None = None) -> dict[str, Path]:
    snap = snapshot or derive_snapshot()
    out = Path(reports_dir) if reports_dir is not None else REPORTS_DIR
    out.mkdir(parents=True, exist_ok=True)
    files = {
        "CURRENT.md": render_current_md(snap),
        "TRAINING_RUNS.md": render_training_runs_md(snap),
        "ARTIFACT_GUIDE.md": render_artifact_guide_md(snap),
    }
    written = {}
    for name, content in files.items():
        p = out / name
        p.write_text(content, encoding="utf-8")
        written[name] = p
    return written


if __name__ == "__main__":
    snap = derive_snapshot()
    for name, path in write_reports(snapshot=snap).items():
        print(f"wrote {path}")
    print(f"snapshot: {snap.to_line()}")
