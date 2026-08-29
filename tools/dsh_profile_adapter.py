#!/usr/bin/env python3
"""DSH Profile Adapter — consumer/runtime adapter for the UniClaw Profile Core.

DSH is a CONSUMER of the generic profile, never a second authority. Every
profile semantic decision (compose, merge priority, WorkItem/WorkResult
validation, module resolution, context key) is delegated to
tools/agent_profile_validator.py. This module adds only DSH runtime concerns:

  - ProfileSource: pinned source config + version/validation gate (read-only)
  - ProfileLoader / ProfileAdapter: load registries, compose AgentProfile
  - ModelBinding: decoupled bindings with a single leader-authority token,
    allow-listed fallback reasons, checkpoint-based takeover
  - WorkerRouter: routing policy mirroring uniflow-coding-workflow.md §7
  - Scheduler: single owner per WorkItem, no fanout, no concurrent same-file
    writers, dependency ordering, write-heavy serial default
  - ModuleContextLoader: auto context manifest via upstream `context`
  - ProfileCache: keyed by upstream profile_context_key; controlled reuse
  - WorkEnvelope: outer envelope, never pollutes the generic WorkItem
  - WorkResultGate: ordered acceptance checks; delta only applied on Accept
  - LeaderCheckpoint: minimal reference/summary state
  - EventLog: the 12 minimal workflow events only

Verification entry: python3 tools/dsh_profile_adapter.py validate
"""

import copy
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
CONFIG_PATH = REPO_ROOT / ".dsh" / "profile-adapter" / "profile-source.yaml"

# 区分“未显式传入（回退读 Host 配置）”与“显式无 Host 默认”。
_UNSET = object()

SPEC = importlib.util.spec_from_file_location(
    "agent_profile_validator", REPO_ROOT / "tools" / "agent_profile_validator.py"
)
validator = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(validator)


DSH_PROTOCOL_VERSION = 1
ENVELOPE_FIELDS = ("protocol_version", "session_id", "run_id", "correlation_id",
                   "profile_version", "work_item")
# dispatch_work_item 派发的 Envelope 在此 6 字段基础上追加 "model_binding"（五.1）。
ENVELOPE_DISPATCH_FIELDS = ENVELOPE_FIELDS + ("model_binding",)
WORKFLOW_EVENTS = (
    "profile.source.validated", "profile.loaded", "profile.conflict",
    "workflow.route.selected", "work_item.dispatched", "worker.context.loaded",
    "worker.completed", "worker.blocked", "work_result.accepted",
    "work_result.rejected", "leader.fallback.started", "checkpoint.updated",
)
FALLBACK_ALLOWED_REASONS = {
    "provider_unavailable", "connection_failure", "timeout",
    "platform_tool_failure", "structured_output_repeated_failure",
}

# Profile source "revision" is the content fingerprint of the rule set that
# defines profiling semantics (lockfile-style content pinning — NOT the repo
# HEAD, which would trip the drift gate on every commit).  The pin file itself
# (.dsh/profile-adapter/profile-source.yaml) is intentionally excluded to avoid
# a self-referential "update pin -> content changed -> drift again" loop.
PROFILE_PIN_FILES = (
    ".ai/profiles/execution.json",
    ".ai/profiles/modules.json",
    ".ai/profiles/roles.json",
    ".ai/schemas/work-item.schema.json",
    ".ai/schemas/work-result.schema.json",
    "tools/agent_profile_validator.py",
)


def profile_source_fingerprint(repo_root):
    """sha256 over sorted pin paths + bytes; missing/unreadable file fails."""
    digest = hashlib.sha256()
    for rel in PROFILE_PIN_FILES:
        path = Path(repo_root) / rel
        try:
            payload = path.read_bytes()
        except OSError as error:
            raise DshAdapterError(
                "cannot resolve source revision: %s" % error) from error
        digest.update(rel.encode("utf-8"))
        digest.update(payload)
    return digest.hexdigest()
FALLBACK_FORBIDDEN_REASONS = {
    "worker_test_failed", "leader_decision_error", "rule_conflict",
    "user_goal_changed", "work_item_split_invalid",
}

# ExecutionProfile → DSH binding role (单一路由真相; 不得硬编码 provider/model 到 Profile)。
# development / test-authoring / verification 共享 implementation_efficient;
# semantic-analysis 使用 semantic_read; tool-only 无模型。
EXECUTION_BINDING_ROLES = {
    "development": "implementation_efficient",
    "test-authoring": "implementation_efficient",
    "verification": "implementation_efficient",
    "semantic-analysis": "semantic_read",
    "tool-only": "tool_only",
}

# Host 回执必含字段（六.1）——由 DSH Host 生成，模型正文自述不算。
HOST_RECEIPT_FIELDS = (
    "session_id", "run_id", "work_item_id", "worker_owner",
    "actual_provider", "actual_model", "actual_reasoning",
    "binding_revision", "started_at",
)

# Host 能力不足 / 回执缺失 / 绑定不符 时 fail-closed 返回的代码。
ROUTING_CAPABILITY_LIMIT = "ROUTING_CAPABILITY_LIMIT"
REQUIRED_SKILL_UNAVAILABLE = "REQUIRED_SKILL_UNAVAILABLE"


class DshAdapterError(ValueError):
    """Fail-closed adapter error."""


class WorkItemRequired(DshAdapterError):
    """dispatch 必须传入合法 JSON WorkItem 对象；Markdown/自然语言描述一律拒绝。"""


class RequiredSkillUnavailable(DshAdapterError):
    """Required Skill payload 缺失或与已校验 ModuleContext 不一致。"""

    def __init__(self, message):
        super().__init__(message)
        self.code = REQUIRED_SKILL_UNAVAILABLE


class RoutingCapabilityRequired(DshAdapterError):
    """Host 不支持 Envelope 指定的 provider/model/reasoning（写入前必须 fail-closed）。"""

    def __init__(self, message, binding=None):
        super().__init__(message)
        self.binding = binding or {}
        self.code = ROUTING_CAPABILITY_LIMIT


class LeaderDecisionRequired(DshAdapterError):
    """Rule conflict or ambiguity that only the leader may resolve."""

    def __init__(self, message, conflict_rules=None):
        super().__init__(message)
        self.conflict_rules = list(conflict_rules or [])


# ── Event log ─────────────────────────────────────────────────────────────────


def _validate_path_component(value, label):
    """Validate an identity before it can become a state path component."""
    if not isinstance(value, str) or not value or value in (".", ".."):
        raise DshAdapterError("invalid %s path component" % label)
    if "\x00" in value or "/" in value or "\\" in value:
        raise DshAdapterError("unsafe %s path component" % label)
    if Path(value).is_absolute():
        raise DshAdapterError("absolute %s path component" % label)
    return value


@dataclass(frozen=True)
class RunEventContext:
    """Immutable UniFlow identity carried by every persisted Run event."""

    session_id: str
    run_id: str
    correlation_id: str

    def __post_init__(self):
        for field in ("session_id", "run_id", "correlation_id"):
            _validate_path_component(getattr(self, field), field)

    def as_dict(self):
        return {
            "session_id": self.session_id,
            "run_id": self.run_id,
            "correlation_id": self.correlation_id,
        }


SYSTEM_EVENTS = {
    "profile.source.validated", "profile.loaded", "profile.conflict",
    "leader.fallback.started", "checkpoint.updated",
}


class EventLog:
    def __init__(self, state_dir=None, max_events=512):
        self.max_events = max_events
        self.events = []
        self._state_root = Path(state_dir) if state_dir else None

    def emit(self, name, context=None, **fields):
        if name not in WORKFLOW_EVENTS:
            raise DshAdapterError("unknown workflow event: %s" % name)
        if context is not None and not isinstance(context, RunEventContext):
            if isinstance(context, dict):
                context = RunEventContext(**context)
            else:
                raise DshAdapterError("invalid Run event context")
        event = {"event": name, "ts": time.time(), **fields}
        if name in SYSTEM_EVENTS:
            event["scope"] = "system"
        else:
            if context is None and self._state_root is not None:
                raise DshAdapterError(
                    "Run event requires explicit session/run/correlation context")
            if context is not None:
                event = {**event, "scope": "run", **context.as_dict()}
        self.events.append(event)
        if len(self.events) > self.max_events:
            self.events = self.events[-self.max_events:]
        if self._state_root is not None:
            if name in SYSTEM_EVENTS:
                path = self._state_root / "system" / "events.jsonl"
            else:
                path = (self._state_root / "sessions" / context.session_id /
                        "runs" / context.run_id / "events.jsonl")
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("a", encoding="utf-8") as handle:
                handle.write(json.dumps(self.events[-1], ensure_ascii=False,
                                        sort_keys=True) + "\n")

    def names(self):
        return [event["event"] for event in self.events]


# ── Profile Source / Loader ───────────────────────────────────────────────────


def load_config(path=CONFIG_PATH):
    text = Path(path).read_text(encoding="utf-8")
    match = re.search(r"#BEGIN JSON\n(.*?)\n#END JSON", text, re.DOTALL)
    if not match:
        raise DshAdapterError("profile-source config missing JSON block")
    return json.loads(match.group(1))


class ProfileSource:
    """Read-only pinned source with version + validation gates."""

    def __init__(self, config=None, repo_root=REPO_ROOT):
        self.config = config or load_config()
        source = self.config["profile_source"]
        self.root = Path(source["root"])
        self.repo_root = Path(repo_root)
        if not self.root.is_absolute():
            self.root = self.repo_root / self.root
        self.schema_version = source["schema_version"]
        self.source_revision = source["source_revision"]
        self.validation_command = source["validation_command"]
        self.mode = source["mode"]
        # Direct ProfileSource use is validation-only; DshWorkflowRuntime wires
        # its persistent EventLog after construction.  This prevents standalone
        # validate/source checks from mutating repository operational state.
        self.events = EventLog()

    def _state_dir(self):
        return self.config.get("state_dir") or ".dsh/profile-adapter/state"

    def _current_revision(self):
        """Content fingerprint of the profile rule set (lockfile-style pin).

        Only changes to the pin file set alter the fingerprint, so unrelated
        docs/code commits never trip the drift gate.  Fail-closed on any
        missing/unreadable pin file.
        """
        return profile_source_fingerprint(self.repo_root)

    def _current_schema_version(self, registries):
        versions = {registry.get("schema_version")
                    for registry in registries.values()}
        if len(versions) != 1 or versions.pop() is None:
            raise DshAdapterError("inconsistent upstream schema_version")
        return list({registry.get("schema_version")
                     for registry in registries.values()})[0]

    def validate_source(self):
        proc = subprocess.run(self.validation_command, shell=True,
                              cwd=str(self.root), capture_output=True,
                              text=True)
        if proc.returncode != 0:
            raise DshAdapterError(
                "upstream profile validation failed: %s" % proc.stdout.strip())
        self.events.emit("profile.source.validated",
                         command=self.validation_command)
        return True

    def load(self):
        self.validate_source()
        if self.mode != "local":
            raise DshAdapterError("unsupported profile source mode: %s"
                                  % self.mode)
        current_revision = self._current_revision()
        if current_revision != self.source_revision:
            raise DshAdapterError(
                "source revision drift: pinned %s != current %s"
                % (self.source_revision, current_revision))
        registries = validator.load_registries()
        upstream_schema = self._current_schema_version(registries)
        if upstream_schema != self.schema_version:
            raise DshAdapterError(
                "profile schema version mismatch: config %s != upstream %s"
                % (self.schema_version, upstream_schema))
        self.events.emit("profile.loaded", schema_version=upstream_schema,
                         source_revision=current_revision)
        return registries

    def fingerprint(self):
        """Digest over upstream profile files — detects drift, proves read-only."""
        digest = hashlib.sha256()
        for name in ("roles.json", "execution.json", "modules.json"):
            digest.update(name.encode("utf-8"))
            digest.update((self.root / ".ai" / "profiles" / name).read_bytes())
        return digest.hexdigest()


# ── Profile Adapter (compose + conflict) ──────────────────────────────────────


class ProfileAdapter:
    """Composes AgentProfile with upstream-identical merge semantics."""

    def __init__(self, events=None):
        self.events = events or EventLog()

    def compose(self, role_id, execution_id, module_id=None, registries=None):
        try:
            if module_id is None:
                registries = registries or validator.load_registries()
                role = validator.find_profile("roles", role_id, registries)
                execution = validator.find_profile("execution", execution_id,
                                                   registries)
                return {"role_profile": role, "execution_profile": execution}
            return validator.compose_profile(role_id, execution_id, module_id,
                                             registries)
        except validator.ProfileError as error:
            self.events.emit("profile.conflict", message=str(error))
            raise LeaderDecisionRequired(str(error)) from error

    def merge_strict(self, left, right):
        try:
            return validator.merge_mapping_strict(left, right)
        except validator.ProfileError as error:
            self.events.emit("profile.conflict", message=str(error))
            raise LeaderDecisionRequired(str(error)) from error


# ── Model Binding ─────────────────────────────────────────────────────────────


class ModelBinding:
    """Decoupled bindings; leader authority is a single runtime token."""

    def __init__(self, config, events=None):
        self.bindings = copy.deepcopy(config["model_bindings"])
        self.events = events or EventLog()
        frontier = self.bindings["decision_frontier"]
        self._leader_endpoint = "primary"
        frontier["primary"]["leader_authority"] = True
        frontier["fallback"]["leader_authority"] = False
        self.model_call_count = {"tool_only": 0}
        self.leader_receipt = None

    def binding_for(self, role):
        if role not in self.bindings:
            raise DshAdapterError("unknown binding role: %s" % role)
        return copy.deepcopy(self.bindings[role])

    def binding_for_execution(self, execution_profile):
        """ExecutionProfile → resolved binding role (四). Resolution is
        delegated to the DSH binding config, never to hard-coded model ids."""
        role = EXECUTION_BINDING_ROLES.get(execution_profile)
        if role is None:
            raise DshAdapterError(
                "no binding role for execution_profile: %s" % execution_profile)
        binding = self.binding_for(role)
        if role == "tool_only" and binding["primary"].get("model") not in (None, "none"):
            raise DshAdapterError("tool_only binding must be model none")
        return role, binding

    def binding_digest(self, revision):
        """Deterministic digest over the resolved binding config block —
        the `binding config revision` carried by envelope and receipts."""
        payload = json.dumps(self.bindings, ensure_ascii=False, sort_keys=True,
                             separators=(",", ":"))
        return hashlib.sha256(("%s|%s" % (revision, payload)).encode("utf-8")).hexdigest()

    def leader_binding(self):
        endpoint = self.bindings["decision_frontier"][self._leader_endpoint]
        return copy.deepcopy(endpoint)

    def record_leader_receipt(self, receipt):
        """UniFlow 启动时记录 Host 提供的 Leader 实际模型回执（七.1）。
        receipt 必须由 Host 生成并包含 actual 字段；模型正文自述不算。"""
        missing = [field for field in HOST_RECEIPT_FIELDS
                   if field not in receipt]
        if missing:
            raise DshAdapterError("leader receipt missing fields: %s"
                                  % ", ".join(missing))
        self.leader_receipt = {"role": "decision_frontier[%s]" % self._leader_endpoint,
                               **copy.deepcopy(receipt)}
        return self.leader_receipt

    def assert_leader_primary(self):
        """当前主 Leader 必须为 zai/glm-5.2/high（七.2）。
        返回失败原因列表；调用方必须 fail-closed，不得静默降级。"""
        primary = self.bindings["decision_frontier"]["primary"]
        failures = []
        if primary.get("provider") != "zai":
            failures.append("leader primary provider must be zai")
        if primary.get("model") != "glm-5.2":
            failures.append("leader primary model must be glm-5.2")
        if primary.get("reasoning") != "high":
            failures.append("leader primary reasoning must be high")
        return failures

    def request_fallback(self, reason):
        if reason in FALLBACK_FORBIDDEN_REASONS:
            raise DshAdapterError(
                "fallback forbidden for business failure reason: %s" % reason)
        if reason not in FALLBACK_ALLOWED_REASONS:
            raise DshAdapterError("unknown fallback reason: %s" % reason)
        if self._leader_endpoint == "fallback":
            return self.leader_binding()
        frontier = self.bindings["decision_frontier"]
        frontier["primary"]["leader_authority"] = False
        frontier["fallback"]["leader_authority"] = True
        self._leader_endpoint = "fallback"
        self.events.emit("leader.fallback.started", reason=reason)
        return self.leader_binding()

    def worker_binding(self):
        return self.binding_for("implementation_efficient")

    def semantic_binding(self):
        return self.binding_for("semantic_read")

    def tool_only(self):
        binding = self.binding_for("tool_only")
        if binding["primary"].get("model") != "none":
            raise DshAdapterError("tool_only binding must be model none")
        self.model_call_count["tool_only"] += 1  # invocation, never a model call
        return {"tool": "deterministic", "model": "none"}

    def model_calls_for_tool_only(self):
        return 0  # tool-only never invokes a model, by construction


# ── Worker Router ─────────────────────────────────────────────────────────────


class WorkerRouter:
    """Routing policy mirroring uniflow-coding-workflow.md §7 via upstream
    route_task, extended with the DSH execution-agent mapping."""

    AGENTS = {
        "module-worker": "development",
        "test-author": "test-authoring",
        "verifier": "verification",
        "semantic-analyzer": "semantic-analysis",
    }

    def __init__(self, events=None):
        self.events = events or EventLog()

    def route(self, shape, binding=None, event_context=None):
        decision = validator.route_task(shape)
        self.events.emit("workflow.route.selected", context=event_context,
                         route=decision["route"],
                         shape=dict(sorted(shape.items())))
        if decision["route"] == "tool-only":
            if binding is not None:
                binding.tool_only()
        return decision


# ── Scheduler ─────────────────────────────────────────────────────────────────


class Scheduler:
    """Single-owner unicast, no fanout, no concurrent same-file writers."""

    def __init__(self, events=None):
        self.events = events or EventLog()
        self.dispatched = {}
        self.file_writers = {}

    def plan(self, work_items, serial_write_heavy=True):
        errors = validator.validate_change_set(work_items)
        if errors:
            raise DshAdapterError("; ".join(errors))
        ordered = list(work_items)
        if serial_write_heavy:
            ordered.sort(key=lambda item: (len(item.get("scope", {}).get("write", [])) > 0,
                                           item.get("id", "")))
        for item in ordered:
            self.dispatch(item)
        return [item["id"] for item in ordered]

    def dispatch(self, item, event_context=None, emit_event=True):
        item_id = item.get("id")
        owner = item.get("worker_owner")
        if item_id in self.dispatched:
            if self.dispatched[item_id] != owner:
                raise DshAdapterError(
                    "WorkItem %s fanout rejected: owners %s and %s"
                    % (item_id, self.dispatched[item_id], owner))
            raise DshAdapterError(
                "WorkItem %s already dispatched to %s (fanout rejected)"
                % (item_id, owner))
        errors = validator.validate_work_item(item)
        if errors:
            raise DshAdapterError("invalid WorkItem: %s" % "; ".join(errors))
        execution = validator.index_profiles(
            validator.load_registries()["execution"])[item["execution_profile"]]
        if execution["permissions"].get("spawn_agent") is not False:
            raise DshAdapterError("execution profile must forbid spawn_agent")
        for path in item["scope"]["write"]:
            if path in self.file_writers and self.file_writers[path] != owner:
                raise DshAdapterError(
                    "Path %s has concurrent writers %s and %s"
                    % (path, self.file_writers[path], owner))
            self.file_writers[path] = owner
        self.dispatched[item_id] = owner
        if emit_event:
            self.events.emit("work_item.dispatched", context=event_context,
                             work_item_id=item_id, worker_owner=owner)

    def request_spawn(self, owner):
        raise DshAdapterError(
            "worker %s cannot create sub-agents (worker boundary)" % owner)


# ── ModuleContext Loader + Cache ──────────────────────────────────────────────


def validate_required_skill_payload(item, manifest):
    """验证 DSH Worker payload 可直接消费完整、有序的 canonical Skill。"""
    names = item.get("required_skills", [])
    context_sources = manifest.get("context_sources", {})
    expected_paths = context_sources.get("required_skills")
    skill_context = manifest.get("required_skill_context")
    if not isinstance(skill_context, dict):
        return ["required_skill_context missing"]
    documents = skill_context.get("documents")
    if not isinstance(documents, list):
        return ["required_skill_context.documents missing"]
    errors = []
    document_names = [document.get("name") for document in documents
                      if isinstance(document, dict)]
    document_paths = [document.get("path") for document in documents
                      if isinstance(document, dict)]
    if len(document_names) != len(documents):
        errors.append("required Skill document must be an object")
    if document_names != names:
        errors.append("required Skill name/order mismatch")
    if not isinstance(expected_paths, list) or document_paths != expected_paths:
        errors.append("required Skill path/order mismatch")
    if not skill_context.get("directive"):
        errors.append("required Skill loading directive missing")
    if skill_context.get("failure_status") != "BLOCKED_FOR_SPEC" or \
            skill_context.get("failure_reason") != REQUIRED_SKILL_UNAVAILABLE:
        errors.append("required Skill fail-closed policy mismatch")
    for document in documents:
        if not isinstance(document, dict):
            continue
        content = document.get("content")
        if not isinstance(content, str) or not content.strip():
            errors.append("required Skill content missing: %s" %
                          document.get("name"))
            continue
        digest = hashlib.sha256(content.encode("utf-8")).hexdigest()
        if document.get("content_sha256") != digest:
            errors.append("required Skill content digest mismatch: %s" %
                          document.get("name"))
    return errors


def build_worker_task_payload(item, manifest, model_binding):
    errors = validate_required_skill_payload(item, manifest)
    if errors:
        raise RequiredSkillUnavailable("; ".join(errors))
    return {
        "work_item": copy.deepcopy(item),
        "manifest": copy.deepcopy(manifest),
        "model_binding": copy.deepcopy(model_binding),
    }


class ModuleContextStore:
    """DSH holds the authoritative ModuleContext; the model session is cache."""

    DELTA_FIELDS = ("affected_symbols", "new_test_refs", "contract_changes",
                    "obsolete_refs")

    def __init__(self, state_dir=None, events=None):
        self.events = events or EventLog()
        self.path = Path(state_dir) / "module-context.json" if state_dir else None
        self.contexts = {}
        self.accepted_deltas = {}
        if self.path is not None and self.path.is_file():
            data = json.loads(self.path.read_text(encoding="utf-8"))
            self.contexts = data.get("contexts", {})
            self.accepted_deltas = data.get("accepted_deltas", {})

    def _persist(self):
        if self.path is None:
            return
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(json.dumps(
            {"contexts": self.contexts, "accepted_deltas": self.accepted_deltas},
            ensure_ascii=False, sort_keys=True, indent=2), encoding="utf-8")

    def load_for_work_item(self, item, binding=None, source_revision=None,
                           event_context=None):
        try:
            manifest = validator.build_context_manifest(
                item["module_profile"], item["execution_profile"],
                source_revision or item["base_revision"],
                required_skills=item.get("required_skills", []))
        except (OSError, validator.ProfileError) as error:
            raise RequiredSkillUnavailable(str(error)) from error
        errors = validate_required_skill_payload(item, manifest)
        if errors:
            raise RequiredSkillUnavailable("; ".join(errors))
        self.events.emit("worker.context.loaded", context=event_context,
                         module_profile=item["module_profile"],
                         execution_profile=item["execution_profile"],
                         profile_context_key=manifest["profile_context_key"])
        return manifest

    def resolve_module(self, path):
        """Unique resolution; ambiguous/unowned → coding-leader."""
        registries = validator.load_registries()
        matches = [module["id"] for module in registries["modules"]["profiles"]
                   if validator._path_allowed(path, module["owned_paths"])]
        if len(matches) == 1:
            return matches[0]
        return "coding-leader"

    def get(self, key):
        return self.contexts.get(key)

    def put(self, key, manifest, reuse_appendix=None):
        entry = {"manifest": manifest, "reuse_appendix": list(reuse_appendix or [])}
        self.contexts[key] = entry
        self._persist()
        return entry

    def invalidate_if_stale(self, key, current_manifest):
        entry = self.contexts.get(key)
        if entry is None:
            return True
        stale = (entry["manifest"]["profile_context_key"]
                 != current_manifest["profile_context_key"])
        if stale:
            del self.contexts[key]
            self._persist()
        return stale

    def apply_delta(self, work_item_id, result, accepted_by_leader):
        if not accepted_by_leader:
            return None
        errors = validator.validate_work_result(result)
        if errors:
            raise DshAdapterError("invalid WorkResult: %s" % "; ".join(errors))
        delta = copy.deepcopy(result["module_context_delta"])
        applied = {field: list(delta.get(field, []))
                   for field in self.DELTA_FIELDS}
        self.accepted_deltas[work_item_id] = applied
        self._persist()
        return applied

    def accepted_delta(self, work_item_id):
        return self.accepted_deltas.get(work_item_id)


class WorkerSessionCache:
    """Session reuse keyed by upstream ProfileContextKey; append-only."""

    ALLOWED_APPENDIX = ("work_item", "changeset_contract", "accepted_delta")

    def __init__(self):
        self.sessions = {}

    def key_for(self, manifest):
        return manifest["profile_context_key"]

    def reuse(self, manifest, new_appendix):
        key = self.key_for(manifest)
        session = self.sessions.setdefault(
            key, {"key": key, "appendix": []})
        for item in new_appendix:
            if item.get("kind") not in self.ALLOWED_APPENDIX:
                raise DshAdapterError(
                    "cache appendix allows only %s" %
                    ", ".join(self.ALLOWED_APPENDIX))
        session["appendix"].extend(new_appendix)
        return session

    def invalidate(self, key=None):
        if key is None:
            self.sessions.clear()
        else:
            self.sessions.pop(key, None)

    def invalidate_on(self, reason):
        """reason: profile_version | rule_digest | source_revision |
        module_profile | model_binding | worker_blocked | protocol_violation"""
        allowed = {"profile_version", "rule_digest", "source_revision",
                   "module_profile", "model_binding", "worker_blocked",
                   "protocol_violation"}
        if reason not in allowed:
            raise DshAdapterError("unknown invalidation reason: %s" % reason)
        self.invalidate()


# ── Model Binding Resolution (四) ────────────────────────────────────────────


def resolve_worker_binding(binding, execution_profile, profile_version,
                           binding_revision):
    """解析 dispatch 所需的 requested model binding 元数据（五.1）。

    只读取 DSH binding 配置，绝不把 provider/model 硬编码进 Profile / WorkItem。
    """
    role, resolved = binding.binding_for_execution(execution_profile)
    primary = resolved["primary"]
    return {
        "binding_role": role,
        "provider": primary.get("provider"),
        "model": primary.get("model"),
        "reasoning": primary.get("reasoning"),
        "profile_version": profile_version,
        "binding_revision": binding_revision,
    }


# ── Dispatch Gate (二) ───────────────────────────────────────────────────────


class DispatchGate:
    """强制 WorkItem 派发门：唯一合法派发入口必须是合法 JSON WorkItem。

    Markdown 标题、自然语言任务说明、缺失必填字段或非对象 semantic_brief
    一律拒绝（fail-closed）。tool-only 不创建 Subagent、model=none，且
    出现源码/测试写入范围或语义判断请求时派发失败。
    """

    TOOL_ONLY_EXECUTIONS = ("tool-only",)

    def __init__(self, events=None):
        self.events = events or EventLog()

    def check(self, item, shape=None):
        """返回错误列表；空列表 = 通过。"""
        errors = []
        if not isinstance(item, dict):
            errors.append("dispatch requires a JSON WorkItem object "
                          "(markdown/natural-language task descriptions are forbidden)")
            return errors
        upstream = validator.validate_work_item(item)
        if upstream:
            errors.extend("WorkItem: %s" % error for error in upstream)
        execution_profile = item.get("execution_profile")
        if execution_profile == "tool-only":
            write = item.get("scope", {}).get("write", []) if isinstance(item.get("scope"), dict) else []
            if write:
                errors.append("tool-only WorkItem must not declare source or test write scope")
            shape = shape or {}
            if shape.get("semantic_judgment") or shape.get("semantic_analysis"):
                errors.append("tool-only WorkItem must not request semantic judgment")
        else:
            # ExecutionProfile 与任务形态一致性（二.4.v）：shape 派生的路由必须
            # 与 WorkItem 声明的 execution_profile 一致。
            shape = shape or {}
            decision = validator.route_task(shape)
            if decision.get("execution_profile") is not None and \
                    decision["execution_profile"] != execution_profile:
                errors.append(
                    "execution_profile mismatch: WorkItem=%s but task shape routes=%s"
                    % (execution_profile, decision["execution_profile"]))
        return errors


# ── Host 派发 seam（五）──────────────────────────────────────────────────────


class DshHostClient:
    """DSH Host seam：从已校验 Envelope 读取 provider/model/reasoning 并显式
    传入 Subagent 创建；支持能力检查在写入前 fail-closed；返回 Host 生成的回执。

    非 UniFlow 的 Host 原生工具不属本 seam；UniFlow 路径禁止绕过本 seam。
    """

    def supports(self, provider, model, reasoning):
        raise NotImplementedError

    def spawn_worker(self, envelope, task_payload):
        """按 envelope.model_binding 显式创建 Subagent，返回 Host 回执 dict
        （字段见 HOST_RECEIPT_FIELDS）。Host 能力不足时必须在任何文件修改前
        抛出 RoutingCapabilityRequired。"""
        raise NotImplementedError


class CapabilityLimitedHostClient(DshHostClient):
    """默认 Host seam：不支持任何 provider/model/reasoning —— 写入前 fail-closed。

    这是仓库侧对“Host 无法保证指定模型”的诚实表述；不模拟成功回执。
    """

    def __init__(self, reason="host does not support requested provider/model/reasoning"):
        self.reason = reason

    def supports(self, provider, model, reasoning):
        return False

    def spawn_worker(self, envelope, task_payload):
        binding = envelope.get("model_binding") or {}
        raise RoutingCapabilityRequired(
            "%s (requested %s/%s/%s)" % (
                self.reason, binding.get("provider"), binding.get("model"),
                binding.get("reasoning")),
            binding=binding)


class DeferredSessionSpawnHostClient(DshHostClient):
    """CLI 派发专用 Host seam：spawn 延迟到 DSH 会话侧执行（L3/M0）。

    语义：CLI 负责把“已 gate 校验 + 已解析绑定 + 已记录 dispatch record”
    的派发意图落盘；实际 Subagent 创建由 DSH 会话按 envelope.model_binding
    执行，随后以 `receipt` 子命令从持久 session 日志核对 requested-vs-actual。

    这不是绕过 fail-closed：
    - supports() 如实声明"能力待会话验证"（CLI 派发时点无法证明 Host 能力，
      真相只能在 session 日志产生后核对）；
    - spawn_worker 返回 PENDING 回执（actual_* 为 None）——WorkResultGate
      的回执核对会如实拒绝 PENDING 回执，因此 CLI 派发在 receipt 验证
      通过前不可能被接受任何结果；
    - capability 判定没有缺失，只是移动到唯一能产真实回执的位置。
    """

    def supports(self, provider, model, reasoning):
        # 能力验证显式延迟到 session 侧 receipt 核对；dispatch record 的
        # host_note 如实声明该边界。
        return True

    def spawn_worker(self, envelope, task_payload):
        inner = envelope.get("dsh_work_envelope", {})
        binding = inner.get("model_binding", {})
        return {
            "session_id": inner.get("session_id"),
            "run_id": inner.get("run_id"),
            "correlation_id": inner.get("correlation_id"),
            "work_item_id": binding.get("work_item_id"),
            "worker_owner": binding.get("worker_owner"),
            "actual_provider": None,
            "actual_model": None,
            "actual_reasoning": None,
            "binding_revision": binding.get("binding_revision"),
            "started_at": None,
            "receipt_status": "PENDING_SESSION_SPAWN",
        }


def check_host_receipt(receipt, requested_binding, work_item_id, worker_owner,
                       expected_session_id=None, expected_run_id=None,
                       expected_correlation_id=None):
    """核对 Host 回执（六.5）：存在性 + requested vs actual 一致。

    返回拒绝原因列表；[] = 通过。缺回执或任一字段不一致必须拒绝，
    不允许静默 fallback。"""
    reasons = []
    if not isinstance(receipt, dict):
        return ["model_receipt_missing"]
    if receipt.get("work_item_id") != work_item_id:
        reasons.append("model_receipt_work_item_mismatch")
    if receipt.get("worker_owner") != worker_owner:
        reasons.append("model_receipt_worker_owner_mismatch")
    expected_session_id = (expected_session_id if expected_session_id is not None
                           else (requested_binding or {}).get("session_id"))
    expected_run_id = (expected_run_id if expected_run_id is not None
                       else (requested_binding or {}).get("run_id"))
    expected_correlation_id = (
        expected_correlation_id if expected_correlation_id is not None else
        (requested_binding or {}).get("correlation_id"))
    if expected_session_id is not None and \
            receipt.get("session_id") != expected_session_id:
        reasons.append("model_receipt_session_mismatch")
    if expected_run_id is not None and receipt.get("run_id") != expected_run_id:
        reasons.append("model_receipt_run_mismatch")
    if expected_correlation_id is not None and \
            receipt.get("correlation_id") is not None and \
            receipt.get("correlation_id") != expected_correlation_id:
        reasons.append("model_receipt_correlation_mismatch")
    if requested_binding is not None:
        for field, actual_key in (("provider", "actual_provider"),
                                  ("model", "actual_model"),
                                  ("reasoning", "actual_reasoning")):
            requested = requested_binding.get(field)
            actual = receipt.get(actual_key)
            if requested is None:
                continue
            if actual is None:
                reasons.append("model_receipt_missing:%s" % actual_key)
                continue
            if requested != actual:
                reasons.append("model_binding_mismatch")
                break
        if requested_binding.get("binding_revision") and \
                receipt.get("binding_revision") != requested_binding["binding_revision"]:
            reasons.append("model_receipt_binding_revision_mismatch")
    missing = [field for field in HOST_RECEIPT_FIELDS if field not in receipt]
    if missing:
        reasons.append("model_receipt_incomplete:%s" % ",".join(missing))
    if not reasons and not receipt.get("started_at"):
        reasons.append("model_receipt_missing_started_at")
    return reasons


def read_host_receipt_from_session_log(session_dir, work_item_id, worker_owner,
                                       binding_revision, expected_session_id=None,
                                       expected_run_id=None):
    """从 DSH Host 会话日志读取实际模型回执（六.3/六.4）。

    日志是 Host 生成（`request/header` 事件持久化实际 LlmCallConfig），
    模型正文自述不能替代。返回 HOST_RECEIPT_FIELDS 字典；字段缺失时
    由 check_host_receipt 判为 incomplete，绝不伪造。

    需要 zstd 命令可解压 `session.jsonl.zstd`；否则抛 DshAdapterError。
    """
    import subprocess as _subprocess
    session_dir = Path(session_dir)
    log = session_dir / "session.jsonl.zstd"
    if not log.is_file():
        raise DshAdapterError("Host session log not found: %s" % log)
    proc = _subprocess.run(["zstd", "-d", "-c", str(log)],
                           capture_output=True, text=True)
    if proc.returncode != 0:
        raise DshAdapterError("cannot decompress Host session log: %s"
                              % proc.stderr.strip())
    config = None
    started_at = None
    session_id = None
    for line in proc.stdout.splitlines():
        try:
            event = json.loads(line)
        except ValueError:
            continue
        event_type = event.get("type")
        if event_type == "session" and session_id is None:
            session_id = event.get("id")
        if event_type == "request/header":
            data = event.get("data") or {}
            header = data.get("header") or {}
            config = dict(header.get("config") or {})
            started_at = event.get("time")
    if config is None:
        raise DshAdapterError("no request/header event in Host session log")
    actual = {
        # Host session identity is evidence only.  Never infer UniFlow Run
        # identity from its directory name; callers supply dispatch identity.
        "session_id": expected_session_id,
        "run_id": expected_run_id,
        "host_session_id": session_id,
        "work_item_id": work_item_id,
        "worker_owner": worker_owner,
        "actual_provider": config.get("provider"),
        "actual_model": config.get("model"),
        "actual_reasoning": config.get("reasoningEffort"),
        "binding_revision": binding_revision,
        "started_at": started_at,
    }
    return actual


# ── Work Envelope ─────────────────────────────────────────────────────────────


def wrap_work_envelope(work_item, session_id, run_id, correlation_id,
                       profile_version, model_binding=None,
                       protocol_version=DSH_PROTOCOL_VERSION):
    errors = validator.validate_work_item(work_item)
    if errors:
        raise DshAdapterError("invalid WorkItem: %s" % "; ".join(errors))
    envelope = {
        "protocol_version": protocol_version,
        "session_id": session_id,
        "run_id": run_id,
        "correlation_id": correlation_id,
        "profile_version": profile_version,
        "work_item": copy.deepcopy(work_item),
    }
    if model_binding is not None:
        envelope["model_binding"] = copy.deepcopy(model_binding)
    return {"dsh_work_envelope": envelope}


def unwrap_work_envelope(envelope):
    inner = envelope["dsh_work_envelope"]["work_item"]
    return copy.deepcopy(inner)


# ── WorkResult Gate ───────────────────────────────────────────────────────────


class WorkResultGate:
    """Ordered acceptance checks per spec §WorkResult 接收门 + 模型回执核对（六）。"""

    ORDER = ("schema", "profile_version", "base_revision", "worker_owner",
             "changed_paths", "write_scope", "local_rules", "invariant",
             "forbidden", "evidence", "scenario_gate", "model_receipt",
             "model_binding")

    def __init__(self, events=None):
        self.events = events or EventLog()

    def check(self, work_item, result, profile_version=None,
              source_revision=None, scenario_gate=True, receipt=None,
              requested_binding=None, require_receipt=False,
              event_context=None):
        rejections = []

        if validator.validate_work_result(result):
            return ["schema"], None
        if profile_version is not None and result.get("profile_version") not in (None, profile_version):
            rejections.append("profile_version")
        if result.get("base_revision") != work_item["base_revision"]:
            rejections.append("base_revision")
        if work_item["worker_owner"] != result.get("worker_owner",
                                                  work_item["worker_owner"]):
            rejections.append("worker_owner")
        changed = [entry.get("path") for entry in result.get("changed", [])
                   if isinstance(entry, dict) and entry.get("path")]
        outside = [path for path in changed
                   if not validator._path_allowed(path, work_item["scope"]["write"])]
        if outside:
            rejections.append("write_scope_violation")
        if result.get("status") == "DONE" and not result.get("verification"):
            rejections.append("missing_evidence")
        if not scenario_gate:
            rejections.append("scenario_gate_failed")
        # 模型回执核对（六.5）：缺回执 / id / owner / revision / requested-vs-actual
        # 任一不一致 → 拒绝结果。模型正文自述不能替代 Host 回执；无静默 fallback。
        # require_receipt=True 或派发路径提供 requested_binding 时强校验；
        # 纯 gate 层旧调用（未走派发）保持兼容。
        if require_receipt or requested_binding is not None:
            receipt_reasons = check_host_receipt(
                receipt, requested_binding,
                work_item_id=work_item["id"],
                worker_owner=work_item["worker_owner"])
            rejections.extend(receipt_reasons)

        if rejections:
            self.events.emit("work_result.rejected", context=event_context,
                             work_item_id=work_item["id"], reasons=rejections)
            return rejections, None
        self.events.emit("work_result.accepted", context=event_context,
                         work_item_id=work_item["id"])
        return [], result

    def check_blocked(self, result, event_context=None):
        status = result.get("status", "")
        blocked = status.startswith("BLOCKED") or status == "ROUTING_UNAVAILABLE"
        if blocked:
            self.events.emit("worker.blocked", context=event_context,
                             status=status)
        return blocked


# ── LeaderCheckpoint ──────────────────────────────────────────────────────────


class LeaderCheckpoint:
    """Minimal reference/summary state; no reasoning, no worker transcripts."""

    def __init__(self, session_id, profile_version, goal_ref, events=None,
                 state_dir=None):
        self.events = events or EventLog()
        self.path = (Path(state_dir) / "leader-checkpoint.json"
                     if state_dir else None)
        self.data = {
            "session_id": session_id,
            "revision": 0,
            "profile_version": profile_version,
            "goal_ref": goal_ref,
            "frozen_decisions": [],
            "active_invariants": [],
            "active_contracts": [],
            "completed_work": [],
            "pending_work": [],
            "blocked_work": [],
            "module_context_refs": [],
            "evidence_refs": [],
            "active_leader_provider": None,
        }
        if self.path is not None and self.path.is_file():
            self.data = json.loads(self.path.read_text(encoding="utf-8"))

    def _persist(self):
        if self.path is None:
            return
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(
            json.dumps(self.data, ensure_ascii=False, sort_keys=True, indent=2),
            encoding="utf-8")

    def set_provider(self, provider):
        self.data["active_leader_provider"] = provider

    def add_pending(self, work_item_id):
        if work_item_id not in self.data["pending_work"]:
            self.data["pending_work"].append(work_item_id)
        self._commit()

    def complete(self, work_item_id, evidence_refs=()):
        if work_item_id in self.data["pending_work"]:
            self.data["pending_work"].remove(work_item_id)
        if work_item_id not in self.data["completed_work"]:
            self.data["completed_work"].append(work_item_id)
        for ref in evidence_refs:
            if ref not in self.data["evidence_refs"]:
                self.data["evidence_refs"].append(ref)
        self._commit()

    def block(self, work_item_id):
        if work_item_id in self.data["pending_work"]:
            self.data["pending_work"].remove(work_item_id)
        if work_item_id not in self.data["blocked_work"]:
            self.data["blocked_work"].append(work_item_id)
        self._commit()

    def _commit(self):
        self.data["revision"] += 1
        self._persist()
        self.events.emit("checkpoint.updated", revision=self.data["revision"])

    @classmethod
    def restore_latest(cls, state_dir, events=None):
        path = Path(state_dir) / "leader-checkpoint.json"
        if not path.is_file():
            raise DshAdapterError("no leader checkpoint to restore")
        data = json.loads(path.read_text(encoding="utf-8"))
        checkpoint = cls(data["session_id"], data["profile_version"],
                         data["goal_ref"], events=events, state_dir=state_dir)
        checkpoint.data = data
        return checkpoint


# ── Host 默认 reasoning（六.5 运行时补齐）────────────────────────────


def read_host_default_reasoning(path=None):
    """读取 DSH Host 侧 agent-default-model.reasoningEffort 作为 host 默认
    reasoning。值来自 Host 配置（~/.dsh/settings.yaml），非模型正文自述。"""
    path = Path(path) if path else Path.home() / ".dsh" / "settings.yaml"
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return None
    match = re.search(r"(?m)^agent-default-model:\s*$", text)
    if not match:
        return None
    for line in text[match.end():].splitlines()[:8]:
        if re.match(r"^\S", line):
            break
        m = re.match(r"^\s*reasoningEffort:\s*(\S+)\s*$", line)
        if m:
            return m.group(1)
    return None


# ── Fallback takeover ─────────────────────────────────────────────────────────


def leader_fallback_takeover(binding, reason, state_dir, events=None,
                             receipt=None):
    endpoint = binding.request_fallback(reason)
    checkpoint = LeaderCheckpoint.restore_latest(state_dir, events=events)
    checkpoint.set_provider(endpoint["provider"])
    result = {"binding": endpoint, "checkpoint": checkpoint,
              "pending_work": list(checkpoint.data["pending_work"]),
              "module_context_refs": list(
                  checkpoint.data["module_context_refs"])}
    if receipt is not None:
        result["receipt"] = binding.record_leader_receipt(receipt)
    return result


# ── Spawn seam audit (八) ─────────────────────────────────────────────────────


def audit_subagent_spawn_seams():
    """枚举 DSH 侧 Subagent 创建入口并确认 UniFlow 唯一合法入口。

    DSH Host 原生 seam（ctx.subagents providers / subagent 工具 / workflow
    agent()）是宿主能力；账号内 UniFlow 路径必须以
    DshWorkflowRuntime.dispatch_work_item() 为唯一入口并带上已核对回执。
    Worker 永远不能自行再 spawn（Scheduler.request_spawn 硬拒绝）。
    """
    return {
        "host_native_seams": [
            "ctx.subagents.start(provider, ...)",
            "subagent/subagent_fork tool (host-provided)",
            "workflow agent() hook (host-provided, explicit provider/model override)",
        ],
        "uniflow_legal_entry": "DshWorkflowRuntime.dispatch_work_item()",
        "worker_spawn": "forbidden (Scheduler.request_spawn raises)",
        "bypass_guard": "markdown/natural-language task input rejected by DispatchGate; "
                        "missing receipt rejected by WorkResultGate",
    }


# ── Workflow runtime facade ───────────────────────────────────────────────────


class DshWorkflowRuntime:
    """Wires source → adapter → binding → router → scheduler → gate → host.

    dispatch_work_item() 是 UniFlow 唯一合法派发入口（二.3）：生成一个合法 JSON
    WorkItem，经 DispatchGate 机械校验 → ExecutionProfile 解析 ModelBinding →
    带 requested binding 的 Envelope → Host seam 显式传入 provider/model/reasoning
    创建 Subagent（tool-only 不创建）→ 预执行回执核对 → WorkResultGate 接收时按
    回执再次核对，全部通过后才接受结果与 ModuleContext Delta。
    """

    def __init__(self, config=None, state_dir=None, host_client=None,
                 host_default_reasoning=_UNSET):
        self.config = config or load_config()
        configured_state = self.config.get("state_dir", ".dsh/profile-adapter/state")
        resolved_state_path = Path(state_dir) if state_dir else Path(configured_state)
        if not resolved_state_path.is_absolute():
            resolved_state_path = REPO_ROOT / resolved_state_path
        resolved_state = str(resolved_state_path)
        self.state_dir = resolved_state_path
        self.events = EventLog(resolved_state)
        self.source = ProfileSource(self.config)
        if host_default_reasoning is _UNSET:
            self.host_default_reasoning = read_host_default_reasoning()
        else:
            self.host_default_reasoning = host_default_reasoning
        self.source.events = self.events
        self.registries = self.source.load()
        self.adapter = ProfileAdapter(self.events)
        self.binding = ModelBinding(self.config, self.events)
        self.router = WorkerRouter(self.events)
        self.scheduler = Scheduler(self.events)
        self.store = ModuleContextStore(resolved_state, self.events)
        self.cache = WorkerSessionCache()
        self.gate = WorkResultGate(self.events)
        self.dispatch_gate = DispatchGate(self.events)
        self.host = host_client or CapabilityLimitedHostClient()
        self.profile_version = "%s@%s" % (
            self.source.schema_version, self.source.source_revision[:12])
        self.binding_revision = "dsb@%s" % self.source.source_revision[:12]
        self.requests = {}   # work_item id → requested binding（revision+digest）
        self.receipts = {}   # work_item id → actual Host receipt
        self.event_contexts = {}  # work_item id → immutable Session/Run identity

    def record_leader_receipt(self, receipt):
        """记录 Host 提供的 Leader 实际模型回执并校验主 Leader（七.1/七.2）。"""
        failures = self.binding.assert_leader_primary()
        if failures:
            raise RoutingCapabilityRequired(
                "leader binding not satisfiable: %s" % "; ".join(failures))
        recorded = self.binding.record_leader_receipt(receipt)
        if recorded.get("actual_provider") != "zai" or \
                recorded.get("actual_model") != "glm-5.2":
            raise RoutingCapabilityRequired(
                "leader actual receipt mismatches primary binding "
                "(actual %s/%s, requested zai/glm-5.2)" % (
                    recorded.get("actual_provider"), recorded.get("actual_model")))
        return recorded

    def dispatch_work_item(self, item, session_id, run_id, correlation_id,
                           task_shape=None):
        # Validate all path-bearing identities before any dispatch event/state
        # can be emitted.  Host session identity is not involved here.
        context = RunEventContext(session_id, run_id, correlation_id)
        if isinstance(item, dict) and "id" in item:
            _validate_path_component(item["id"], "work_item_id")
        gate_errors = self.dispatch_gate.check(item, shape=task_shape)
        if gate_errors:
            if any("required skill" in error.lower() or
                   "required_skills" in error.lower()
                   for error in gate_errors):
                raise RequiredSkillUnavailable("; ".join(gate_errors))
            raise WorkItemRequired("; ".join(gate_errors))

        execution_profile = item["execution_profile"]
        binding_role, resolved = self.binding.binding_for_execution(
            execution_profile)
        requested = resolve_worker_binding(
            self.binding, execution_profile, self.profile_version,
            self.binding_revision)
        requested["binding_role"] = binding_role
        requested["binding_digest"] = self.binding.binding_digest(
            self.binding_revision)
        requested["work_item_id"] = item["id"]
        requested["worker_owner"] = item["worker_owner"]
        requested.update(context.as_dict())
        self.event_contexts[item["id"]] = context

        if execution_profile == "tool-only":
            # tool-only 不创建 Subagent、model=none、零模型调用（二.5 / 九.5）——
            # 直接调度（无 Host seam），不产生任何模型调用。
            self.scheduler.dispatch(item, event_context=context,
                                    emit_event=False)
            manifest = self.store.load_for_work_item(item,
                                                     event_context=context)
            self.binding.tool_only()
            assert requested["model"] == "none"
            envelope = wrap_work_envelope(
                item, session_id, run_id, correlation_id, self.profile_version,
                model_binding=requested)
            self.events.emit("work_item.dispatched", context=context,
                             work_item_id=item["id"],
                             worker_owner=item["worker_owner"],
                             provider=requested.get("provider"),
                             model=requested.get("model"),
                             reasoning=requested.get("reasoning"))
            return {"envelope": envelope, "manifest": manifest,
                    "worker_payload": None, "spawn": None, "receipt": None}

        # Host seam：能力不足必须在任何文件/调度记录产生前 fail-closed（五.5 / 九.10）。
        if not self.host.supports(requested["provider"], requested["model"],
                                  requested["reasoning"]):
            raise RoutingCapabilityRequired(
                "host cannot honor requested binding %s/%s/%s" % (
                    requested["provider"], requested["model"],
                    requested["reasoning"]),
                binding=requested)
        manifest = self.store.load_for_work_item(item, event_context=context)
        payload = build_worker_task_payload(item, manifest, requested)
        self.scheduler.dispatch(item, event_context=context, emit_event=False)
        envelope = wrap_work_envelope(
            item, session_id, run_id, correlation_id, self.profile_version,
            model_binding=requested)
        self.requests[item["id"]] = copy.deepcopy(requested)

        receipt = self.host.spawn_worker(envelope, payload)
        receipt = self._enrich_receipt_reasoning(receipt)
        # 预执行回执核对（六.4）：不一致时在 Worker 写任何文件前拒绝。
        # 例外：PENDING_SESSION_SPAWN（DeferredSessionSpawnHostClient）——
        # spawn 被显式延迟到 DSH 会话侧，actual_* 为空是如实的，不是缺失；
        # 真正的核对发生在 receipt 子命令（session 日志）与 WorkResultGate。
        pending = isinstance(receipt, dict) and \
            receipt.get("receipt_status") == "PENDING_SESSION_SPAWN"
        mismatch = [] if pending else check_host_receipt(
            receipt, requested,
            work_item_id=item["id"],
            worker_owner=item["worker_owner"],
            expected_session_id=session_id, expected_run_id=run_id,
            expected_correlation_id=correlation_id)
        if mismatch:
            self.receipts[item["id"]] = copy.deepcopy(receipt)
            raise RoutingCapabilityRequired(
                "pre-execution receipt mismatch: %s" % ", ".join(mismatch),
                binding=requested)
        self.receipts[item["id"]] = copy.deepcopy(receipt)
        self.events.emit("work_item.dispatched", context=context,
                         work_item_id=item["id"],
                         worker_owner=item["worker_owner"],
                         provider=requested["provider"], model=requested["model"],
                         reasoning=requested["reasoning"])
        return {"envelope": envelope, "manifest": manifest,
                "worker_payload": payload,
                "spawn": {"provider": requested["provider"],
                          "model": requested["model"],
                          "reasoning": requested["reasoning"]},
                "receipt": copy.deepcopy(receipt)}

    def _enrich_receipt_reasoning(self, receipt):
        """回执缺 actual_reasoning 时，用 Host 默认 reasoning（来自 Host 配置，
        非模型自述）补齐参与 requested-vs-actual 核对；Host 无默认则不补，
        Gate 仍会以 model_receipt_missing 拒绝。"""
        if not isinstance(receipt, dict):
            return receipt
        if receipt.get("actual_reasoning") is None and self.host_default_reasoning:
            receipt = dict(receipt)
            receipt["actual_reasoning"] = self.host_default_reasoning
            receipt["reasoning_source"] = "host_default_reasoning"
        return receipt

    def accept_result(self, item, result, scenario_gate=True,
                      receipt=None, requested_binding=None):
        """接收 WorkResult：先按 Envelope 的 requested binding 核对回执，
        全部 Gate 通过后才接受结果并应用 ModuleContext Delta（六.5 / 九.16）。"""
        requested_binding = requested_binding or self.requests.get(item["id"])
        receipt = receipt if receipt is not None else self.receipts.get(item["id"])
        receipt = self._enrich_receipt_reasoning(receipt)
        rejections, accepted = self.gate.check(
            item, result, profile_version=self.profile_version,
            scenario_gate=scenario_gate, receipt=receipt,
            requested_binding=requested_binding,
            event_context=self.event_contexts.get(item["id"]))
        if rejections:
            reason_codes = [r for r in rejections
                            if r in ("model_receipt_missing", "model_binding_mismatch")]
            outcome = {"accepted": False, "rejections": rejections}
            if reason_codes:
                outcome["code"] = ROUTING_CAPABILITY_LIMIT
                outcome["binding_reasons"] = reason_codes
            return outcome
        if self.gate.check_blocked(
                result, event_context=self.event_contexts.get(item["id"])):
            return {"accepted": False, "rejections": ["blocked"]}
        delta = None
        if result.get("status") == "DONE":
            delta = self.store.apply_delta(item["id"], result,
                                           accepted_by_leader=True)
        return {"accepted": True, "applied_delta": delta}


# ── CLI ───────────────────────────────────────────────────────────────────────


def check_install_integrity(profile_root=None):
    """安装完整性校验（生命周期 L4 / M6）。

    检查 DSH profile root 下 package.json 声明的本地 `file:` 插件依赖：
    - `file:` 目标必须存在（ding-chime 式悬空链接属安装损坏）；
    - node_modules 内对应符号链接必须有效（不悬空）。

    返回错误列表；空列表 = 通过。npm 版本包依赖不检查（registry 托管）。
    """
    import os
    root = Path(profile_root) if profile_root else (
        Path.home() / ".dsh" / "profiles" / "web")
    errors = []
    manifest = root / "package.json"
    if not manifest.is_file():
        return []  # 无 profile root（如 CI）不视为损坏
    try:
        deps = json.loads(manifest.read_text(encoding="utf-8")).get(
            "dependencies", {})
    except (OSError, ValueError) as error:
        return ["profile manifest unreadable: %s" % error]
    for name, spec in sorted(deps.items()):
        if not isinstance(spec, str) or not spec.startswith("file:"):
            continue
        target = Path(spec[len("file:"):])
        if not target.exists():
            errors.append(
                "dangling file: dependency %s -> %s (target deleted; "
                "remove the dependency or restore the target)" % (name, target))
        link = root / "node_modules" / name
        if link.is_symlink() and not os.path.exists(os.path.abspath(link)):
            errors.append(
                "dangling symlink node_modules/%s -> %s" % (name, os.readlink(link)))
    return errors


def _cmd_validate():
    # Validation is not a Run and must not append to the caller's operational
    # state.  Use an isolated temporary sink for the same source checks.
    try:
        with tempfile.TemporaryDirectory(prefix="dsh-profile-validate-") as isolated:
            runtime = DshWorkflowRuntime(state_dir=isolated)
    except (DshAdapterError, OSError, ValueError) as error:
        print("FAIL: %s" % error)
        return 1
    integrity_errors = check_install_integrity()
    if integrity_errors:
        for error in integrity_errors:
            print("FAIL INSTALL_INTEGRITY %s" % error)
        return 1
    print("DSH_PROFILE_ADAPTER_VALIDATION_PASS %s" % runtime.profile_version)
    return 0


def _cmd_dispatch(argv):
    """`dispatch <work-item.json> [--session-id S] [--run-id R] [--record-dir D]`

    单命令派发收口（生命周期 L3 / M0）：WorkItem 文件 → DispatchGate →
    ModelBinding → Envelope →（CLI 无 Host seam，spawn 由 DSH 会话侧执行）→
    原子产出 dispatch record（含 requested binding 与 profile/binding 版本）。
    dispatch record 是命令副作用而非记忆义务 —— 无记录即未派发。

    退出码：0 = 派发成功；1 = fail-closed（gate/binding/写入错误）；
    2 = 用法错误。stdout 末行 `DISPATCH_OK <record-path>`；失败输出
    `DISPATCH_REJECTED <code>` 供脚本消费。
    """
    import argparse
    parser = argparse.ArgumentParser(
        prog="dsh_profile_adapter.py dispatch",
        description="UniFlow single-command dispatch (gate → binding → "
                    "envelope → dispatch record, atomic)")
    parser.add_argument("work_item", help="path to validated WorkItem JSON")
    parser.add_argument("--session-id", default=os.environ.get("DSH_SESSION_ID") or "cli")
    parser.add_argument("--run-id", default=None,
                        help="Run identity; defaults to work-item id")
    parser.add_argument("--record-dir", default=None,
                        help="dispatch-record directory; defaults to "
                             ".dsh/profile-adapter/state/dispatches/")
    parser.add_argument("--task-shape", default=None,
                        help="optional JSON file of the task shape for "
                             "execution-profile consistency routing")
    args = parser.parse_args(argv)

    from datetime import datetime, timezone
    try:
        item = json.loads(Path(args.work_item).read_text(encoding="utf-8"))
    except OSError as error:
        print("DISPATCH_REJECTED WORK_ITEM_UNREADABLE %s" % error, file=sys.stderr)
        return 1
    except ValueError as error:
        print("DISPATCH_REJECTED WORK_ITEM_INVALID_JSON %s" % error, file=sys.stderr)
        return 1

    task_shape = None
    if args.task_shape:
        try:
            task_shape = json.loads(
                Path(args.task_shape).read_text(encoding="utf-8"))
        except (OSError, ValueError) as error:
            print("DISPATCH_REJECTED TASK_SHAPE_UNREADABLE %s" % error,
                  file=sys.stderr)
            return 1

    session_id = args.session_id
    default_run_id = item.get("id", "run") if isinstance(item, dict) else "run"
    run_id = args.run_id or default_run_id
    try:
        _validate_path_component(session_id, "session_id")
        _validate_path_component(run_id, "run_id")
        if isinstance(item, dict) and "id" in item:
            _validate_path_component(item["id"], "work_item_id")
    except DshAdapterError as error:
        print("DISPATCH_REJECTED ADAPTER_FAIL_CLOSED %s" % error,
              file=sys.stderr)
        return 1
    try:
        runtime = DshWorkflowRuntime(
            host_client=DeferredSessionSpawnHostClient())
        outcome = runtime.dispatch_work_item(
            item, session_id=session_id,
            run_id=run_id,
            correlation_id="%s-%d" % (item.get("id", "wi"), int(time.time())),
            task_shape=task_shape)
    except WorkItemRequired as error:
        print("DISPATCH_REJECTED WORK_ITEM_GATE %s" % error, file=sys.stderr)
        return 1
    except RequiredSkillUnavailable as error:
        print("DISPATCH_REJECTED %s %s" %
              (REQUIRED_SKILL_UNAVAILABLE, error), file=sys.stderr)
        return 1
    except RoutingCapabilityRequired as error:
        print("DISPATCH_REJECTED %s %s" % (ROUTING_CAPABILITY_LIMIT, error),
              file=sys.stderr)
        return 1
    except (DshAdapterError, OSError, ValueError) as error:
        print("DISPATCH_REJECTED ADAPTER_FAIL_CLOSED %s" % error, file=sys.stderr)
        return 1

    # 原子写 dispatch record：同目录临时文件 + os.replace（崩溃不留半写状态）。
    record_dir = (Path(args.record_dir) if args.record_dir else
                  runtime.state_dir / "sessions" / session_id / "runs" /
                  run_id / "dispatches")
    record_dir.mkdir(parents=True, exist_ok=True)
    record_path = record_dir / ("%s.json" % item["id"])
    record = {
        "record_kind": "uniflow-dispatch-record",
        "protocol_version": DSH_PROTOCOL_VERSION,
        "recorded_at": datetime.now(timezone.utc).isoformat(),
        "work_item_id": item["id"],
        "change_set_id": item.get("change_set_id"),
        "worker_owner": item["worker_owner"],
        "execution_profile": item["execution_profile"],
        "module_profile": item.get("module_profile"),
        "session_id": session_id,
        "run_id": run_id,
        "profile_version": runtime.profile_version,
        "binding_revision": runtime.binding_revision,
        "requested_binding": runtime.requests.get(item["id"]),
        "envelope": outcome["envelope"],
        "worker_payload": outcome["worker_payload"],
        "spawn": outcome["spawn"],
        "receipt_status": (outcome["receipt"] or {}).get("receipt_status"),
        "host_note": ("CLI dispatch: spawn is executed by the DSH session "
                      "side using worker_payload unchanged; verify with `receipt` "
                      "before accepting results."),
    }
    tmp_path = record_path.with_suffix(".json.tmp")
    tmp_path.write_text(
        json.dumps(record, ensure_ascii=False, sort_keys=True, indent=2),
        encoding="utf-8")
    os.replace(tmp_path, record_path)

    print("DISPATCH_OK %s" % record_path)
    binding = runtime.requests.get(item["id"]) or {}
    print("requested_binding %s/%s/%s role=%s" % (
        binding.get("provider"), binding.get("model"),
        binding.get("reasoning"), binding.get("binding_role")))
    return 0


def _cmd_receipt(argv):
    """`receipt <session-dir> --work-item-id ID --worker-owner OWNER`

    事后回执核验（生命周期 L2/L3 / M0）：从 DSH Host 会话持久日志重建
    实际模型回执并与派发记录中的 requested binding 核对。用于：
    - DSH 重启后恢复回执（session 日志是持久真相）；
    - 验收前核对 requested-vs-actual（缺/不一致 → RECEIPT_LOST 拒绝）。

    退出码：0 = 回执一致；1 = 缺失/不一致（fail-closed，不猜）；
    2 = 用法错误。
    """
    import argparse
    parser = argparse.ArgumentParser(
        prog="dsh_profile_adapter.py receipt",
        description="Rebuild + verify the actual model receipt from the "
                    "persistent Host session log against the dispatch record")
    parser.add_argument("session_dir", help="DSH session directory "
                        "(contains session.jsonl.zstd)")
    parser.add_argument("--work-item-id", required=True)
    parser.add_argument("--worker-owner", required=True)
    parser.add_argument("--record-dir", default=None)
    parser.add_argument("--session-id", default=None,
                        help="UniFlow session identity for v2 lookup")
    parser.add_argument("--run-id", default=None,
                        help="UniFlow run identity for v2 lookup")
    args = parser.parse_args(argv)

    try:
        _validate_path_component(args.work_item_id, "work_item_id")
        if (args.session_id is None) != (args.run_id is None):
            raise DshAdapterError("session-id and run-id must be provided together")
        if args.session_id is not None:
            _validate_path_component(args.session_id, "session_id")
            _validate_path_component(args.run_id, "run_id")
    except DshAdapterError as error:
        print("RECEIPT_LOST %s" % error, file=sys.stderr)
        return 1

    try:
        runtime = DshWorkflowRuntime()
    except (OSError, ValueError, DshAdapterError) as error:
        print("RECEIPT_LOST %s" % error, file=sys.stderr)
        return 1
    try:
        if args.record_dir:
            candidates = [Path(args.record_dir) / ("%s.json" % args.work_item_id)]
        elif args.session_id is not None:
            v2 = (runtime.state_dir / "sessions" / args.session_id / "runs" /
                  args.run_id / "dispatches" / ("%s.json" % args.work_item_id))
            v1 = runtime.state_dir / "dispatches" / ("%s.json" % args.work_item_id)
            candidates = [v2, v1]
        else:
            candidates = [runtime.state_dir / "dispatches" /
                          ("%s.json" % args.work_item_id)]
        record_path = next((path for path in candidates if path.is_file()), None)
        if record_path is None:
            print("RECEIPT_LOST dispatch record not found: %s" % candidates[0],
                  file=sys.stderr)
            return 1
        record = json.loads(record_path.read_text(encoding="utf-8"))
    except (OSError, ValueError, DshAdapterError) as error:
        print("RECEIPT_LOST %s" % error, file=sys.stderr)
        return 1
    expected_session_id = args.session_id or record.get("session_id")
    expected_run_id = args.run_id or record.get("run_id")
    envelope = ((record.get("envelope") or {}).get("dsh_work_envelope") or {})
    requested = (record.get("requested_binding") or {})
    identity_mismatches = []
    for field, expected in (("session_id", expected_session_id),
                            ("run_id", expected_run_id),
                            ("work_item_id", args.work_item_id),
                            ("worker_owner", args.worker_owner)):
        if expected is not None and record.get(field) != expected:
            identity_mismatches.append("record_%s" % field)
        if expected is not None and envelope.get(field) not in (None, expected):
            identity_mismatches.append("envelope_%s" % field)
        if expected is not None and requested.get(field) not in (None, expected):
            identity_mismatches.append("binding_%s" % field)
    if identity_mismatches:
        print("RECEIPT_MISMATCH dispatch identity mismatch: %s" %
              ", ".join(identity_mismatches), file=sys.stderr)
        return 1
    binding_revision = requested.get("binding_revision") or runtime.binding_revision

    try:
        receipt = read_host_receipt_from_session_log(
            args.session_dir, args.work_item_id, args.worker_owner,
            binding_revision, expected_session_id=expected_session_id,
            expected_run_id=expected_run_id)
    except DshAdapterError as error:
        print("RECEIPT_LOST %s" % error, file=sys.stderr)
        return 1

    mismatch = check_host_receipt(receipt, requested,
                                  work_item_id=args.work_item_id,
                                  worker_owner=args.worker_owner,
                                  expected_session_id=expected_session_id,
                                  expected_run_id=expected_run_id)
    if mismatch:
        print("RECEIPT_MISMATCH %s" % ", ".join(mismatch), file=sys.stderr)
        return 1
    print("RECEIPT_OK %s/%s/%s" % (receipt.get("actual_provider"),
                                   receipt.get("actual_model"),
                                   receipt.get("actual_reasoning")))
    return 0


def main(argv=None):
    argv = list(sys.argv[1:] if argv is None else argv)
    if not argv:
        print("usage: dsh_profile_adapter.py {validate|dispatch|receipt}",
              file=sys.stderr)
        return 2
    command, rest = argv[0], argv[1:]
    if command == "validate":
        return _cmd_validate()
    if command == "dispatch":
        return _cmd_dispatch(rest)
    if command == "receipt":
        return _cmd_receipt(rest)
    print("usage: dsh_profile_adapter.py {validate|dispatch|receipt}",
          file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
