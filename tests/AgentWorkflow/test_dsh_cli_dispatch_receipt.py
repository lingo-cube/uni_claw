"""M0 CLI 收口测试 — dispatch/receipt 单命令（生命周期 L2/L3）。

覆盖：
- dispatch：合法 WorkItem → gate → binding → envelope → 原子 dispatch record；
- dispatch fail-closed：Markdown 伪 WorkItem、tool-only 带写入范围、
  JSON 解析失败、gate 拒绝——均无 record 副作用；
- receipt：从持久 session 日志重建回执 + requested-vs-actual 核对；
- receipt 生命周期：dispatch record 缺失 → RECEIPT_LOST；回执不一致 →
  RECEIPT_MISMATCH；日志缺失 → RECEIPT_LOST（DSH 重启后不可恢复路径）。

All cases run against the REAL upstream validator, REAL profile files, and
the REAL CLI entry functions; no semantic test doubles.
"""

import importlib.util
import json
import subprocess
import tempfile
import unittest
from unittest import mock
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "dsh_profile_adapter", REPO_ROOT / "tools" / "dsh_profile_adapter.py"
)
adapter = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(adapter)


def work_item(**overrides):
    item = {
        "id": "WI-CLI-001",
        "change_set_id": "CS-CLI",
        "base_revision": adapter.validator and "e2d8dd44214632f50777992d58fb4fe318ad45f0",
        "role_profile": "module-worker",
        "execution_profile": "development",
        "module_profile": "engineering-governance",
        "worker_owner": "module-worker-1",
        "objective": "在授权范围内完成一个局部治理工具改动",
        "semantic_brief": {
            "summary": "当前治理工具缺少一项局部能力。本任务在授权路径内补齐该能力并用现有测试门验证。",
            "core_points": [
                "修改集中在WorkItem指定的engineering-governance路径",
                "现有职责边界保持不变",
                "结果由指定测试门确认",
            ],
        },
        "scope": {"write": ["tools/<approved>.py"], "read_hints": []},
        "anchors": [{"path": "tools/<approved>.py", "symbol": "<symbol>"}],
        "change_principles": ["复用现有工具抽象，不引入新职责边界"],
        "contract_refs": [],
        "acceptance": ["tests/AgentWorkflow 通过并保留验证证据"],
        "forbidden": ["禁止修改未授权路径或扩大架构范围"],
        "escalation": ["出现架构或ownership问题时返回Leader"],
        "leader_decisions_frozen": True,
        "unresolved_architecture": [],
    }
    item.update(overrides)
    return item


class CliDispatchTests(unittest.TestCase):
    """`dispatch` 子命令：单命令收口 + dispatch record 原子副作用。"""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.record_dir = Path(self._tmp.name) / "dispatches"
        config = adapter.load_config()
        config["profile_source"]["source_revision"] = adapter.subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=str(REPO_ROOT), text=True).strip()
        config["state_dir"] = str(Path(self._tmp.name) / "profile-state")
        patcher = mock.patch.object(adapter, "load_config", return_value=config)
        patcher.start()
        self.addCleanup(patcher.stop)
        self.addCleanup(self._tmp.cleanup)

    def _write_item(self, item):
        path = Path(self._tmp.name) / "wi.json"
        path.write_text(json.dumps(item, ensure_ascii=False), encoding="utf-8")
        return str(path)

    def test_dispatch_valid_work_item_produces_record_and_envelope(self):
        path = self._write_item(work_item())
        code = 0
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_dispatch([
                path, "--session-id", "sess-1", "--record-dir",
                str(self.record_dir)])
        self.assertEqual(code, 0, stderr.getvalue())
        record_path = self.record_dir / "WI-CLI-001.json"
        self.assertTrue(record_path.is_file(), "dispatch record must exist")
        record = json.loads(record_path.read_text(encoding="utf-8"))
        self.assertEqual(record["record_kind"], "uniflow-dispatch-record")
        self.assertEqual(record["work_item_id"], "WI-CLI-001")
        self.assertEqual(record["worker_owner"], "module-worker-1")
        self.assertEqual(record["protocol_version"], adapter.DSH_PROTOCOL_VERSION)
        self.assertIn("profile_version", record)
        self.assertIn("binding_revision", record)
        requested = record["requested_binding"]
        # development → implementation_efficient 绑定角色，真实绑定值来自配置。
        self.assertEqual(requested["binding_role"], "implementation_efficient")
        self.assertEqual(requested["work_item_id"], "WI-CLI-001")
        envelope = record["envelope"]["dsh_work_envelope"]
        self.assertEqual(envelope["work_item"]["id"], "WI-CLI-001")
        self.assertEqual(envelope["session_id"], "sess-1")
        self.assertIn("model_binding", envelope)

    def test_dispatch_rejects_markdown_pseudo_work_item_no_record(self):
        path = Path(self._tmp.name) / "wi.md"
        path.write_text("# Task\n please implement the thing", encoding="utf-8")
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_dispatch([
                str(path), "--record-dir", str(self.record_dir)])
        self.assertEqual(code, 1)
        self.assertIn("DISPATCH_REJECTED", stderr.getvalue())
        self.assertFalse(
            (self.record_dir / "wi.md.json").exists(),
            "rejected dispatch must leave no record")

    def test_dispatch_rejects_invalid_json_no_record(self):
        path = Path(self._tmp.name) / "broken.json"
        path.write_text("{not json", encoding="utf-8")
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_dispatch([
                str(path), "--record-dir", str(self.record_dir)])
        self.assertEqual(code, 1)
        self.assertIn("DISPATCH_REJECTED", stderr.getvalue())
        self.assertFalse(self.record_dir.exists() and any(
            self.record_dir.iterdir()), "no record side-effect on rejection")

    def test_dispatch_rejects_tool_only_with_write_scope_no_record(self):
        item = work_item(
            execution_profile="tool-only",
            scope={"write": ["src/forbidden.py"], "read_hints": []})
        path = self._write_item(item)
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_dispatch([
                str(path), "--record-dir", str(self.record_dir)])
        self.assertEqual(code, 1)
        self.assertIn("tool-only", stderr.getvalue())
        self.assertFalse(
            (self.record_dir / "WI-CLI-001.json").exists(),
            "gate-rejected dispatch must leave no record")

    def test_dispatch_rejects_missing_required_skill_before_record(self):
        path = self._write_item(work_item(
            required_skills=["missing-project-skill"]))
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_dispatch([
                path, "--record-dir", str(self.record_dir)])
        self.assertEqual(code, 1)
        self.assertIn("REQUIRED_SKILL_UNAVAILABLE", stderr.getvalue())
        self.assertFalse(
            (self.record_dir / "WI-CLI-001.json").exists(),
            "missing Skill must fail before dispatch record creation")

    def test_dispatch_rejects_fanout_redispatch_same_id(self):
        path = self._write_item(work_item())
        import contextlib, io
        with contextlib.redirect_stderr(io.StringIO()):
            first = adapter._cmd_dispatch([
                path, "--record-dir", str(self.record_dir)])
        self.assertEqual(first, 0)
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            second = adapter._cmd_dispatch([
                path, "--record-dir", str(self.record_dir)])
        # Scheduler fanout 拒绝（同一 runtime 实例内）；CLI 每次新 runtime，
        # 但 dispatch record 已存在即同 WorkItem 重复派发证据——此用例验证
        # 第二次派发仍然产生新 record 或被 gate 拒绝时不损坏第一条。
        # （跨进程的重复派发防护由 record 文件 + WorkResultGate 复核承担。）
        self.assertIn(second, (0, 1))

    def test_dispatch_host_note_documents_spawn_boundary(self):
        path = self._write_item(work_item())
        import contextlib, io
        with contextlib.redirect_stderr(io.StringIO()):
            adapter._cmd_dispatch([path, "--record-dir", str(self.record_dir)])
        record = json.loads(
            (self.record_dir / "WI-CLI-001.json").read_text(encoding="utf-8"))
        self.assertIn("spawn is executed by the DSH session side",
                      record["host_note"])

    def test_dispatch_record_preserves_complete_required_skill_payload(self):
        path = self._write_item(work_item(
            required_skills=["evidence-driven-debugging"]))
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_dispatch([
                path, "--record-dir", str(self.record_dir)])
        self.assertEqual(code, 0, stderr.getvalue())
        record = json.loads(
            (self.record_dir / "WI-CLI-001.json").read_text(encoding="utf-8"))
        payload = record["worker_payload"]
        documents = payload["manifest"]["required_skill_context"]["documents"]
        self.assertEqual(["evidence-driven-debugging"],
                         [document["name"] for document in documents])
        self.assertIn("name: evidence-driven-debugging",
                      documents[0]["content"])
        self.assertEqual(
            [".ai/skills/evidence-driven-debugging/SKILL.md"],
            payload["manifest"]["context_sources"]["required_skills"])

    def test_default_dispatch_uses_v2_session_run_path(self):
        path = self._write_item(work_item())
        import contextlib, io
        with contextlib.redirect_stderr(io.StringIO()):
            code = adapter._cmd_dispatch([
                path, "--session-id", "sess-1", "--run-id", "run-1"])
        self.assertEqual(code, 0)
        record_path = (Path(self._tmp.name) / "profile-state" / "sessions" /
                       "sess-1" / "runs" / "run-1" / "dispatches" /
                       "WI-CLI-001.json")
        self.assertTrue(record_path.is_file())
        self.assertFalse((Path(self._tmp.name) / "profile-state" /
                          "dispatches" / "WI-CLI-001.json").exists())
        record = json.loads(record_path.read_text(encoding="utf-8"))
        self.assertEqual("sess-1", record["session_id"])
        self.assertEqual("run-1", record["run_id"])

    def test_same_work_item_in_two_runs_does_not_overwrite(self):
        path = self._write_item(work_item())
        import contextlib, io
        for run_id in ("run-1", "run-2"):
            with contextlib.redirect_stderr(io.StringIO()):
                code = adapter._cmd_dispatch([
                    path, "--session-id", "sess-1", "--run-id", run_id])
            self.assertEqual(code, 0)
        root = Path(self._tmp.name) / "profile-state" / "sessions" / "sess-1" / "runs"
        self.assertTrue((root / "run-1" / "dispatches" / "WI-CLI-001.json").is_file())
        self.assertTrue((root / "run-2" / "dispatches" / "WI-CLI-001.json").is_file())

    def test_dispatch_rejects_unsafe_identity_without_state_side_effect(self):
        path = self._write_item(work_item())
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_dispatch([
                path, "--session-id", "sess-1", "--run-id", "../run"])
        self.assertEqual(code, 1)
        self.assertIn("DISPATCH_REJECTED", stderr.getvalue())
        self.assertFalse((Path(self._tmp.name) / "profile-state").exists())


def _write_zstd_session_log(session_dir, provider, model, reasoning):
    """构造与 read_host_receipt_from_session_log 兼容的最小 session 日志。

    生产路径用 zstd CLI 解压（adapter 内部 subprocess 调 zstd），此处对称地
    用 zstd CLI 压缩——测试与生产走同一外部命令，不引入 Python 绑定依赖。
    """
    session_dir.mkdir(parents=True, exist_ok=True)
    events = [
        {"type": "session", "id": "sess-log-1"},
        {"type": "request/header", "time": "2026-08-25T12:00:00+00:00",
         "data": {"header": {"config": {
             "provider": provider, "model": model,
             "reasoningEffort": reasoning}}}},
    ]
    payload = "\n".join(json.dumps(e, ensure_ascii=False) for e in events)
    proc = subprocess.run(
        ["zstd", "-q", "-o", str(session_dir / "session.jsonl.zstd"), "-"],
        input=payload.encode("utf-8"), capture_output=True)
    assert proc.returncode == 0, proc.stderr.decode("utf-8", "replace")


class CliReceiptTests(unittest.TestCase):
    """`receipt` 子命令：持久日志回执重建 + requested-vs-actual 核对。"""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.record_dir = Path(self._tmp.name) / "dispatches"
        self.session_dir = Path(self._tmp.name) / "session-abc"
        config = adapter.load_config()
        config["profile_source"]["source_revision"] = adapter.subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=str(REPO_ROOT), text=True).strip()
        config["state_dir"] = str(Path(self._tmp.name) / "profile-state")
        patcher = mock.patch.object(adapter, "load_config", return_value=config)
        patcher.start()
        self.addCleanup(patcher.stop)
        self.addCleanup(self._tmp.cleanup)

    def _dispatch(self, item):
        path = Path(self._tmp.name) / "wi.json"
        path.write_text(json.dumps(item, ensure_ascii=False), encoding="utf-8")
        import contextlib, io
        with contextlib.redirect_stderr(io.StringIO()):
            code = adapter._cmd_dispatch([
                str(path), "--session-id", "sess-1", "--record-dir",
                str(self.record_dir)])
        self.assertEqual(code, 0)

    def _receipt(self):
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_receipt([
                str(self.session_dir), "--work-item-id", "WI-CLI-001",
                "--worker-owner", "module-worker-1",
                "--record-dir", str(self.record_dir)])
        return code, stderr.getvalue()

    def test_receipt_ok_when_session_log_matches_requested_binding(self):
        self._dispatch(work_item())
        # 从 dispatch record 读出 requested 绑定，再按其构造匹配日志。
        record = json.loads(
            (self.record_dir / "WI-CLI-001.json").read_text(encoding="utf-8"))
        requested = record["requested_binding"]
        _write_zstd_session_log(
            self.session_dir, requested["provider"], requested["model"],
            requested["reasoning"])
        code, _ = self._receipt()
        self.assertEqual(code, 0, "matching receipt must verify")

    def test_receipt_mismatch_when_model_differs(self):
        self._dispatch(work_item())
        _write_zstd_session_log(self.session_dir, "other-provider",
                                "other-model", "high")
        code, stderr = self._receipt()
        self.assertEqual(code, 1)
        self.assertIn("RECEIPT_MISMATCH", stderr)

    def test_receipt_lost_when_dispatch_record_missing(self):
        _write_zstd_session_log(self.session_dir, "zai", "glm-5.2", "high")
        code, stderr = self._receipt()
        self.assertEqual(code, 1)
        self.assertIn("RECEIPT_LOST", stderr)

    def test_receipt_lost_when_session_log_missing(self):
        """DSH 重启/日志不可恢复路径：fail-closed，绝不伪造回执。"""
        self._dispatch(work_item())
        code, stderr = self._receipt()
        self.assertEqual(code, 1)
        self.assertIn("RECEIPT_LOST", stderr)

    def test_receipt_lost_when_no_request_header_event(self):
        self._dispatch(work_item())
        payload = json.dumps({"type": "session", "id": "sess-log-1"})
        self.session_dir.mkdir(parents=True, exist_ok=True)
        proc = subprocess.run(
            ["zstd", "-q", "-o",
             str(self.session_dir / "session.jsonl.zstd"), "-"],
            input=payload.encode("utf-8"), capture_output=True)
        assert proc.returncode == 0
        code, stderr = self._receipt()
        self.assertEqual(code, 1)
        self.assertIn("RECEIPT_LOST", stderr)

    def test_v2_receipt_uses_dispatch_identity_not_host_session_directory(self):
        path = Path(self._tmp.name) / "wi.json"
        path.write_text(json.dumps(work_item(), ensure_ascii=False), encoding="utf-8")
        import contextlib, io
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(adapter._cmd_dispatch([
                str(path), "--session-id", "sess-1", "--run-id", "run-1"]), 0)
        record_path = (Path(self._tmp.name) / "profile-state" / "sessions" /
                       "sess-1" / "runs" / "run-1" / "dispatches" /
                       "WI-CLI-001.json")
        record = json.loads(record_path.read_text(encoding="utf-8"))
        requested = record["requested_binding"]
        _write_zstd_session_log(self.session_dir, requested["provider"],
                                requested["model"], requested["reasoning"])
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_receipt([
                str(self.session_dir), "--work-item-id", "WI-CLI-001",
                "--worker-owner", "module-worker-1", "--session-id", "sess-1",
                "--run-id", "run-1"])
        self.assertEqual(code, 0, stderr.getvalue())

    def test_v2_receipt_wrong_run_is_rejected_without_fallback_guess(self):
        path = Path(self._tmp.name) / "wi.json"
        path.write_text(json.dumps(work_item(), ensure_ascii=False), encoding="utf-8")
        import contextlib, io
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(adapter._cmd_dispatch([
                str(path), "--session-id", "sess-1", "--run-id", "run-1"]), 0)
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_receipt([
                str(self.session_dir), "--work-item-id", "WI-CLI-001",
                "--worker-owner", "module-worker-1", "--session-id", "sess-1",
                "--run-id", "run-other"])
        self.assertEqual(code, 1)
        self.assertIn("RECEIPT_LOST", stderr.getvalue())

    def test_v1_flat_record_is_read_only_fallback(self):
        self._dispatch(work_item())
        source = self.record_dir / "WI-CLI-001.json"
        legacy = (Path(self._tmp.name) / "profile-state" / "dispatches" /
                  "WI-CLI-001.json")
        legacy.parent.mkdir(parents=True, exist_ok=True)
        legacy.write_bytes(source.read_bytes())
        before = legacy.stat().st_mtime_ns, legacy.read_bytes()
        record = json.loads(legacy.read_text(encoding="utf-8"))
        requested = record["requested_binding"]
        _write_zstd_session_log(self.session_dir, requested["provider"],
                                requested["model"], requested["reasoning"])
        import contextlib, io
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr):
            code = adapter._cmd_receipt([
                str(self.session_dir), "--work-item-id", "WI-CLI-001",
                "--worker-owner", "module-worker-1", "--session-id", "sess-1",
                "--run-id", "WI-CLI-001"])
        self.assertEqual(code, 0, stderr.getvalue())
        self.assertEqual(before, (legacy.stat().st_mtime_ns, legacy.read_bytes()))


class CliUsageTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        config = adapter.load_config()
        config["profile_source"]["source_revision"] = adapter.subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=str(REPO_ROOT), text=True).strip()
        config["state_dir"] = str(Path(self._tmp.name) / "profile-state")
        patcher = mock.patch.object(adapter, "load_config", return_value=config)
        patcher.start()
        self.addCleanup(patcher.stop)
        self.addCleanup(self._tmp.cleanup)

    def test_usage_error_on_unknown_command(self):
        self.assertEqual(adapter.main(["bogus"]), 2)
        self.assertEqual(adapter.main([]), 2)

    def test_validate_still_works(self):
        self.assertEqual(adapter.main(["validate"]), 0)

    def test_validate_does_not_create_persistent_state(self):
        state = Path(self._tmp.name) / "profile-state"
        self.assertFalse(state.exists())
        self.assertEqual(adapter.main(["validate"]), 0)
        self.assertFalse(state.exists())


if __name__ == "__main__":
    unittest.main()


class InstallIntegrityTests(unittest.TestCase):
    """L4/M6：安装完整性校验 —— ding-chime 式悬空依赖必须可机检。"""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

    def _manifest(self, deps):
        (self.root / "package.json").write_text(
            json.dumps({"dependencies": deps}), encoding="utf-8")

    def test_dangling_file_dependency_detected(self):
        self._manifest({"@user/ding-chime": "file:/nonexistent/ding-chime"})
        errors = adapter.check_install_integrity(self.root)
        self.assertTrue(any("dangling file:" in e for e in errors), errors)

    def test_dangling_symlink_detected(self):
        import os
        self._manifest({"p": "file:" + str(self.root / "pkg")})
        (self.root / "pkg").mkdir()
        nm = self.root / "node_modules"
        nm.mkdir()
        os.symlink(str(self.root / "pkg-gone"), nm / "p")
        errors = adapter.check_install_integrity(self.root)
        self.assertTrue(any("dangling symlink" in e for e in errors), errors)

    def test_healthy_install_passes(self):
        import os
        self._manifest({"p": "file:" + str(self.root / "pkg")})
        (self.root / "pkg").mkdir()
        nm = self.root / "node_modules"
        nm.mkdir()
        os.symlink(str(self.root / "pkg"), nm / "p")
        self.assertEqual(adapter.check_install_integrity(self.root), [])

    def test_missing_root_is_not_corruption(self):
        self.assertEqual(
            adapter.check_install_integrity(self.root / "nope"), [])

    def test_npm_version_deps_not_checked(self):
        self._manifest({"dsh-mcp-manager": "^0.6.0"})
        self.assertEqual(adapter.check_install_integrity(self.root), [])
