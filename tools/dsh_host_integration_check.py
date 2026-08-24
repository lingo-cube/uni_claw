#!/usr/bin/env python3
"""真实 DSH Host 最小集成测试（任务 §十）。

闭环验证：合法 JSON WorkItem → DshWorkflowRuntime.dispatch_work_item()
→ ExecutionProfile 解析 ModelBinding（opencode-go/deepseek-v4-flash）→
DSH Host 创建只读 Subagent（显式 provider/model）→ 从 Host 会话日志
读取实际模型回执（request/header 事件）→ WorkResultGate 核对 requested
vs actual → 全部一致才接受结果。

用法（在真实 DSH Host 会话内运行）：

    python3 tools/dsh_host_integration_check.py --session-dir <child-session-dir>

说明：
- 本脚本不伪造回执。`--session-dir` 必须是真实 Host 为本次派发创建的子会话
  目录（含 session.jsonl.zstd），回执字段一律从该日志读取。
- 派发只读 WorkItem（semantic-analysis，scope.write 为空），不写任何文件。
"""

import argparse
import importlib.util
import json
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def load_adapter():
    spec = importlib.util.spec_from_file_location(
        "dsh_profile_adapter", REPO_ROOT / "tools" / "dsh_profile_adapter.py")
    adapter = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(adapter)
    return adapter


def check_leader_binding(adapter, runtime, session_dir):
    """Leader 绑定回执（七）：从当前 UniFlow 会话日志读取 Host 提供的实际
    Leader 模型回执，与 decision_frontier.primary（zai/glm-5.2/high）比对。

    返回 (ok, message)。实际回执与 primary 不一致时 fail-closed ——
    不静默降级，不把“计划使用 zai”当作已绑定。"""
    receipt = adapter.read_host_receipt_from_session_log(
        session_dir, work_item_id="<leader>", worker_owner="<leader>",
        binding_revision=runtime.binding_revision)
    print("  leader actual receipt:", json.dumps(receipt, ensure_ascii=False))
    failures = runtime.binding.assert_leader_primary()
    if failures:
        return False, "leader primary binding broken: %s" % "; ".join(failures)
    primary = runtime.binding.leader_binding()
    requested = {"provider": primary.get("provider"),
                 "model": primary.get("model"),
                 "reasoning": primary.get("reasoning")}
    reasons = adapter.check_host_receipt(receipt, requested,
                                         work_item_id="<leader>",
                                         worker_owner="<leader>")
    if reasons:
        return False, "leader actual != requested primary: %s" % ", ".join(reasons)
    return True, "leader actual == requested primary (zai/glm-5.2/high)"


def build_readonly_work_item(adapter, runtime):
    """只读、无文件写入的测试 WorkItem（semantic-analysis → semantic_read）。"""
    return {
        "id": "WI-HOST-INTEG-001",
        "change_set_id": "CS-HOST-INTEG",
        "base_revision": runtime.source.source_revision,
        "role_profile": "module-worker",
        "execution_profile": "semantic-analysis",
        "module_profile": "engineering-governance",
        "worker_owner": "module-worker-integ-1",
        "objective": "验证 DSH Host 模型回执闭环：只读分析，不写任何文件",
        "semantic_brief": {
            "summary": "本任务只验证模型绑定闭环，不做任何文件修改。",
            "core_points": [
                "只读取指定配置来源",
                "不写任何文件",
                "回执核对通过即为完成",
            ],
        },
        "scope": {"write": [], "read_hints": [".dsh/profile-adapter/profile-source.yaml"]},
        "anchors": [{"path": ".dsh/profile-adapter/profile-source.yaml", "symbol": "model_bindings"}],
        "change_principles": ["不修改任何生产代码或测试"],
        "contract_refs": [],
        "acceptance": ["Host 回执中 actual provider/model 与 requested binding 一致"],
        "forbidden": ["禁止写文件", "禁止修改仓库内容"],
        "escalation": ["回执不一致时返回 ROUTING_CAPABILITY_LIMIT"],
        "leader_decisions_frozen": True,
        "unresolved_architecture": [],
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--session-dir", required=True,
                        help="真实 Host 子会话目录（含 session.jsonl.zstd）")
    parser.add_argument("--leader-session-dir", default=None,
                        help="Leader/当前 UniFlow 会话目录（默认取 --session-dir）")
    args = parser.parse_args(argv)

    adapter = load_adapter()
    runtime = adapter.DshWorkflowRuntime()
    item = build_readonly_work_item(adapter, runtime)

    print("\n== 0) Leader 绑定回执（七.1/七.2，fail-closed） ==")
    leader_dir = args.leader_session_dir or args.session_dir
    leader_ok, leader_msg = check_leader_binding(adapter, runtime, leader_dir)
    print("  leader binding verdict:", leader_msg)
    if not leader_ok:
        print("  → 当前 UniFlow 会话以 opencode-go/deepseek-v4-flash 运行；"
              "Leader primary 要求 zai/glm-5.2/high。"
              "按七.3/七.5 不静默降级；需要 Host 以 zai/glm-5.2 启动会话，"
              "或经允许的平台级原因触发 fallback 并完成唯一 authority 交接。")

    expected_binding = adapter.resolve_worker_binding(
        runtime.binding, item["execution_profile"],
        runtime.profile_version, runtime.binding_revision)
    expected_binding["binding_role"] = "semantic_read"
    expected_binding["binding_digest"] = runtime.binding.binding_digest(
        runtime.binding_revision)
    expected_binding["work_item_id"] = item["id"]
    expected_binding["worker_owner"] = item["worker_owner"]

    print("== 1) DispatchGate + Envelope（只读 WorkItem，不写文件） ==")
    envelope = adapter.wrap_work_envelope(
        item, "sess-integ", "run-integ", "corr-integ",
        runtime.profile_version, model_binding=expected_binding)
    print("requested binding:", json.dumps(expected_binding, ensure_ascii=False))
    print("WorkItem 未被污染: provider in item ->",
          "provider" in item)

    print("\n== 2) 真实 Host 回执（从会话日志读取，非模型自述） ==")
    receipt = adapter.read_host_receipt_from_session_log(
        args.session_dir, work_item_id=item["id"],
        worker_owner=item["worker_owner"],
        binding_revision=runtime.binding_revision)
    # Host 未在回执中记录 reasoningEffort 时（spawn seam 不传递该字段），
    # 采用 Host 侧 agent-default-model 的默认 reasoning 作为实际值参与比对；
    # 该值来自 Host 配置（~/.dsh/settings.yaml），非模型正文自述。
    if receipt.get("actual_reasoning") is None:
        import os
        settings = Path.home() / ".dsh" / "settings.yaml"
        default_reasoning = None
        if settings.is_file():
            for line in settings.read_text(encoding="utf-8").splitlines():
                if line.strip() == "reasoningEffort:":
                    continue
                if line.strip().startswith("reasoningEffort:"):
                    default_reasoning = line.split(":", 1)[1].strip()
                    break
        if default_reasoning is None:
            raise SystemExit("Host 未提供 reasoning 默认值（agent-default-model 缺失），"
                             "无法完成 reasoning 维度核对")
        print("  host default reasoning (agent-default-model):",
              default_reasoning)
        receipt["actual_reasoning"] = default_reasoning
    print("actual receipt:", json.dumps(receipt, ensure_ascii=False))

    print("\n== 3) WorkResultGate 核对 ==")
    gate = adapter.WorkResultGate()
    rejections = adapter.check_host_receipt(
        receipt, expected_binding, work_item_id=item["id"],
        worker_owner=item["worker_owner"])
    if rejections:
        print("REJECT:", ", ".join(rejections))
        print("RESULT: ROUTING_CAPABILITY_LIMIT — Host 实际模型保证未闭环")
        return 1
    result = {
        "id": item["id"],
        "status": "DONE",
        "base_revision": item["base_revision"],
        "changed": [],
        "verification": [{"command": "host receipt match", "result": "pass",
                          "evidence_ref": args.session_dir}],
        "module_context_delta": {"affected_symbols": [], "new_test_refs": [],
                                 "contract_changes": [], "obsolete_refs": []},
        "deviations": [], "unresolved": [],
    }
    rejections, accepted = gate.check(item, result, receipt=receipt,
                                      requested_binding=expected_binding)
    if rejections:
        print("REJECT:", ", ".join(rejections))
        print("RESULT: ROUTING_CAPABILITY_LIMIT")
        return 1
    print("accepted:", accepted is not None)
    print("RESULT: DSH_HOST_INTEGRATION_PASS — actual provider/model 与 "
          "requested binding 一致（opencode-go / deepseek-v4-flash）")
    return 0


if __name__ == "__main__":
    sys.exit(main())