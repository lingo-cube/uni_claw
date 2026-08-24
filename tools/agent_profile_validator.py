#!/usr/bin/env python3
"""Deterministic validation and resolution for UniClaw coding-agent profiles."""

import argparse
import copy
import hashlib
import json
import re
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PROFILE_DIR = REPO_ROOT / ".ai" / "profiles"
SCHEMA_DIR = REPO_ROOT / ".ai" / "schemas"
CUSTOM_AGENT_DIR = REPO_ROOT / ".codex" / "agents"


class ProfileError(ValueError):
    pass


def load_json(path):
    with Path(path).open("r", encoding="utf-8") as handle:
        return json.load(handle)


def _registry(name):
    data = load_json(PROFILE_DIR / name)
    if data.get("authority") != "NONE":
        raise ProfileError("Profile registry must declare authority NONE: %s" % name)
    profiles = data.get("profiles")
    if not isinstance(profiles, list) or not profiles:
        raise ProfileError("Profile registry must contain profiles: %s" % name)
    ids = [item.get("id") for item in profiles]
    if any(not item for item in ids) or len(ids) != len(set(ids)):
        raise ProfileError("Profile ids must be present and unique: %s" % name)
    return data


def load_registries():
    return {
        "roles": _registry("roles.json"),
        "execution": _registry("execution.json"),
        "modules": _registry("modules.json"),
    }


def index_profiles(registry):
    return {item["id"]: item for item in registry["profiles"]}


def find_profile(registry_name, profile_id, registries=None):
    registries = registries or load_registries()
    profiles = index_profiles(registries[registry_name])
    if profile_id not in profiles:
        raise ProfileError("Unknown %s profile: %s" % (registry_name, profile_id))
    return copy.deepcopy(profiles[profile_id])


def merge_mapping_strict(left, right, path=""):
    merged = copy.deepcopy(left)
    for key, value in right.items():
        current_path = "%s.%s" % (path, key) if path else key
        if key not in merged:
            merged[key] = copy.deepcopy(value)
        elif isinstance(merged[key], dict) and isinstance(value, dict):
            merged[key] = merge_mapping_strict(merged[key], value, current_path)
        elif merged[key] != value:
            raise ProfileError("Profile conflict at %s" % current_path)
    return merged


def compose_profile(role_id, execution_id, module_id, registries=None):
    registries = registries or load_registries()
    role = find_profile("roles", role_id, registries)
    execution = find_profile("execution", execution_id, registries)
    module = find_profile("modules", module_id, registries)
    if role_id == "module-worker" and execution["permissions"].get("spawn_agent") is not False:
        raise ProfileError("Worker execution profile must forbid spawn_agent")
    return {
        "role_profile": role,
        "execution_profile": execution,
        "module_profile": module,
    }


def _path_in_root(path, root):
    normalized_path = path.strip("/")
    normalized_root = root.strip("/")
    return normalized_path == normalized_root or normalized_path.startswith(normalized_root + "/")


def _path_allowed(path, roots):
    return any(_path_in_root(path, root) for root in roots)


def resolve_module_for_path(path, registries=None):
    registries = registries or load_registries()
    matches = []
    for module in registries["modules"]["profiles"]:
        if _path_allowed(path, module["owned_paths"]):
            matches.append(module["id"])
    if len(matches) == 1:
        return matches[0]
    return "coding-leader"


def resolve_agents_for_path(path):
    target = (REPO_ROOT / path).resolve()
    try:
        target.relative_to(REPO_ROOT)
    except ValueError:
        raise ProfileError("Path is outside repository: %s" % path)
    current = target if target.is_dir() else target.parent
    candidates = []
    while True:
        for name in ("AGENTS.override.md", "AGENTS.md"):
            candidate = current / name
            if candidate.is_file():
                candidates.append(candidate.relative_to(REPO_ROOT).as_posix())
                break
        if current == REPO_ROOT:
            break
        current = current.parent
    return list(reversed(candidates))


def build_context_manifest(module_id, execution_id, source_revision, model_binding_version=None, registries=None):
    registries = registries or load_registries()
    composed = compose_profile("module-worker", execution_id, module_id, registries)
    module = composed["module_profile"]
    local_agents = []
    for configured_agent in module["context_sources"].get("agents", []):
        if "<" not in configured_agent and (REPO_ROOT / configured_agent).is_file():
            local_agents.append(configured_agent)
    for path in module["owned_paths"] + module["test_paths"]:
        for agent_file in resolve_agents_for_path(path):
            if agent_file not in local_agents:
                local_agents.append(agent_file)
    context_sources = copy.deepcopy(module["context_sources"])
    context_sources["effective_agents"] = local_agents
    concrete_rule_paths = list(local_agents) + list(module["public_contracts"])
    for values in module["context_sources"].values():
        for path in values:
            if "<" not in path and (REPO_ROOT / path.split("#", 1)[0]).is_file():
                concrete_rule_paths.append(path.split("#", 1)[0])
    model_binding_version = model_binding_version or current_model_binding_version()
    key = profile_context_key(
        composed["role_profile"]["version"],
        composed["execution_profile"]["version"],
        module["version"],
        model_binding_version,
        rule_digest(registries, concrete_rule_paths),
        source_revision,
    )
    return {
        "profile_context_key": key,
        "role_profile": "module-worker",
        "execution_profile": execution_id,
        "module_profile": module_id,
        "context_sources": context_sources,
        "entrypoints": module["entrypoints"],
        "public_contracts": module["public_contracts"],
        "test_gates": module["test_gates"],
    }


def rule_digest(registries=None, rule_paths=None):
    registries = registries or load_registries()
    payload = json.dumps(registries, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    digest = hashlib.sha256(payload.encode("utf-8"))
    for path in sorted(set(rule_paths or [])):
        file_path = REPO_ROOT / path
        if file_path.is_file():
            digest.update(path.encode("utf-8"))
            digest.update(file_path.read_bytes())
    return digest.hexdigest()


def current_model_binding_version():
    text = (REPO_ROOT / ".ai" / "model-routing.yaml").read_text(encoding="utf-8")
    match = re.search(r"(?m)^# version:\s*(\d+)\s*$", text)
    if not match:
        raise ProfileError("model-routing.yaml must declare a version")
    return "model-routing-v%s" % match.group(1)


def profile_context_key(role_version, execution_version, module_version, model_binding_version, rules_digest, source_revision):
    values = [role_version, execution_version, module_version, model_binding_version, rules_digest, source_revision]
    return hashlib.sha256("\n".join(values).encode("utf-8")).hexdigest()


def _require_string(data, name, errors):
    if not isinstance(data.get(name), str) or not data.get(name).strip():
        errors.append("%s must be a non-empty string" % name)


def _require_string_list(data, name, errors, non_empty=False):
    value = data.get(name)
    if not isinstance(value, list) or any(not isinstance(item, str) for item in value):
        errors.append("%s must be a string array" % name)
    elif non_empty and not value:
        errors.append("%s must not be empty" % name)
    elif len(value) != len(set(value)):
        errors.append("%s must not contain duplicates" % name)


def _contract_ref_contains(point, contract_refs):
    for reference in contract_refs if isinstance(contract_refs, list) else []:
        if not isinstance(reference, str) or "<" in reference:
            continue
        path = REPO_ROOT / reference.split("#", 1)[0]
        if path.is_file() and point in path.read_text(encoding="utf-8", errors="ignore"):
            return True
    return False


def _validate_semantic_brief(item, errors):
    brief = item.get("semantic_brief")
    if not isinstance(brief, dict):
        errors.append("semantic_brief 必须是对象")
        return
    unknown = sorted(set(brief) - {"summary", "core_points"})
    for field in unknown:
        errors.append("semantic_brief 包含未知字段: %s" % field)
    summary = brief.get("summary")
    if not isinstance(summary, str) or not summary.strip():
        errors.append("semantic_brief.summary 必须是非空字符串")
    elif len(summary) > 240:
        errors.append("semantic_brief.summary 不能超过240字符")
    core_points = brief.get("core_points")
    if not isinstance(core_points, list):
        errors.append("semantic_brief.core_points 必须是数组")
        return
    if not core_points:
        errors.append("semantic_brief.core_points 不能为空")
    if len(core_points) > 5:
        errors.append("semantic_brief.core_points 不能超过5项")
    if len(core_points) != len({point for point in core_points if isinstance(point, str)}):
        errors.append("semantic_brief.core_points 不能重复")
    formal_constraints = []
    for field in ("change_principles", "forbidden", "acceptance"):
        values = item.get(field)
        if isinstance(values, list):
            formal_constraints.extend(value for value in values if isinstance(value, str))
    constraint_terms = ("必须", "不得", "只能", "禁止", "需要保持", "不能改变")
    for index, point in enumerate(core_points):
        if not isinstance(point, str) or not point.strip():
            errors.append("semantic_brief.core_points[%d] 必须是非空字符串" % index)
            continue
        if len(point) > 100:
            errors.append("semantic_brief.core_points[%d] 不能超过100字符" % index)
        if any(term in point for term in constraint_terms):
            anchored = any(point == value or point in value or value in point for value in formal_constraints if value)
            if not anchored and not _contract_ref_contains(point, item.get("contract_refs")):
                errors.append("semantic_brief.core_points[%d] 的约束语义缺少正式约束锚点" % index)


def validate_work_item(item, registries=None):
    registries = registries or load_registries()
    errors = []
    if not isinstance(item, dict):
        errors.append("WorkItem must be a JSON object (dict), got %s" % type(item).__name__)
        return errors
    allowed_fields = {
        "id", "change_set_id", "base_revision", "role_profile", "execution_profile",
        "module_profile", "worker_owner", "objective", "semantic_brief", "scope", "anchors",
        "change_principles", "contract_refs", "acceptance", "forbidden", "escalation",
        "leader_decisions_frozen", "unresolved_architecture",
    }
    for field in sorted(set(item) - allowed_fields):
        errors.append("unknown WorkItem field: %s" % field)
    for field in ("id", "change_set_id", "base_revision", "role_profile", "execution_profile", "module_profile", "worker_owner", "objective"):
        _require_string(item, field, errors)
    _validate_semantic_brief(item, errors)
    for field in ("change_principles", "contract_refs", "acceptance", "forbidden", "escalation"):
        _require_string_list(item, field, errors, non_empty=field in ("change_principles", "acceptance"))
    if item.get("role_profile") != "module-worker":
        errors.append("dispatched WorkItem role_profile must be module-worker")
    if item.get("leader_decisions_frozen") is not True:
        errors.append("leader_decisions_frozen must be true")
    if item.get("unresolved_architecture") not in (None, []):
        errors.append("WorkItem cannot contain unresolved architecture decisions")
    if isinstance(item.get("worker_owner"), (list, tuple, dict)) or "worker_owners" in item:
        errors.append("WorkItem must have exactly one scalar worker_owner")
    scope = item.get("scope")
    if not isinstance(scope, dict):
        errors.append("scope must be an object")
        write_paths = []
    else:
        write_paths = scope.get("write")
        read_hints = scope.get("read_hints")
        if not isinstance(write_paths, list) or any(not isinstance(path, str) for path in write_paths):
            errors.append("scope.write must be a string array")
            write_paths = []
        if not isinstance(read_hints, list) or any(not isinstance(path, str) for path in read_hints):
            errors.append("scope.read_hints must be a string array")
    anchors = item.get("anchors")
    if not isinstance(anchors, list) or any(not isinstance(anchor, dict) or not anchor.get("path") for anchor in anchors):
        errors.append("anchors must be objects with path")
    execution_profiles = index_profiles(registries["execution"])
    module_profiles = index_profiles(registries["modules"])
    execution = execution_profiles.get(item.get("execution_profile"))
    module = module_profiles.get(item.get("module_profile"))
    if execution is None:
        errors.append("unknown execution_profile")
    if module is None:
        errors.append("unknown module_profile")
    if execution is not None and execution["permissions"].get("spawn_agent") is not False:
        errors.append("worker execution profile must forbid spawn_agent")
    if execution is not None and module is not None:
        execution_id = execution["id"]
        if execution_id in ("verification", "semantic-analysis", "tool-only") and write_paths:
            errors.append("%s forbids source write scope" % execution_id)
        elif execution_id == "test-authoring":
            for path in write_paths:
                if not _path_allowed(path, module["test_paths"]):
                    errors.append("test-authoring write is outside module test paths: %s" % path)
        elif execution_id == "development":
            allowed = module["owned_paths"] + module["test_paths"]
            for path in write_paths:
                if not _path_allowed(path, allowed):
                    errors.append("development write is outside module scope: %s" % path)
    return errors


def build_work_item(payload, summary, core_points, registries=None):
    item = copy.deepcopy(payload)
    item["semantic_brief"] = {
        "summary": summary,
        "core_points": list(core_points),
    }
    errors = validate_work_item(item, registries)
    if errors:
        raise ProfileError("Invalid WorkItem: %s" % "; ".join(errors))
    return item


def serialize_work_item(item, registries=None):
    errors = validate_work_item(item, registries)
    if errors:
        raise ProfileError("Invalid WorkItem: %s" % "; ".join(errors))
    return json.dumps(item, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def validate_work_result(result):
    errors = []
    if not isinstance(result, dict):
        errors.append("WorkResult must be a JSON object (dict), got %s" % type(result).__name__)
        return errors
    allowed_fields = {"id", "status", "base_revision", "changed", "verification", "module_context_delta", "deviations", "unresolved"}
    for field in sorted(set(result) - allowed_fields):
        errors.append("unknown WorkResult field: %s" % field)
    for field in ("id", "status", "base_revision"):
        _require_string(result, field, errors)
    allowed_statuses = {
        "DONE", "BLOCKED_FOR_SPEC", "BLOCKED_FOR_SEMANTIC_REVIEW",
        "BLOCKED_FOR_ARCHITECTURE_REVIEW", "BLOCKED_FOR_HUMAN", "ROUTING_UNAVAILABLE",
    }
    if result.get("status") not in allowed_statuses:
        errors.append("unsupported WorkResult status")
    for field in ("changed", "verification", "deviations", "unresolved"):
        if not isinstance(result.get(field), list):
            errors.append("%s must be an array" % field)
    delta = result.get("module_context_delta")
    if not isinstance(delta, dict):
        errors.append("module_context_delta must be an object")
    else:
        for field in ("affected_symbols", "new_test_refs", "contract_changes", "obsolete_refs"):
            _require_string_list(delta, field, errors)
    return errors


def accept_module_context_delta(result, accepted_by_leader=False):
    errors = validate_work_result(result)
    if errors:
        raise ProfileError("Invalid WorkResult: %s" % "; ".join(errors))
    if not accepted_by_leader:
        return None
    return copy.deepcopy(result["module_context_delta"])


def validate_change_set(work_items):
    errors = []
    owners = {}
    writers = {}
    for item in work_items:
        item_id = item.get("id", "<missing>")
        owner = item.get("worker_owner")
        if item_id in owners and owners[item_id] != owner:
            errors.append("WorkItem %s has multiple owners" % item_id)
        owners[item_id] = owner
        for path in item.get("scope", {}).get("write", []):
            if path in writers and writers[path] != owner:
                errors.append("Path %s has concurrent writers %s and %s" % (path, writers[path], owner))
            writers[path] = owner
    return errors


def route_task(shape):
    if shape.get("deterministic"):
        return {"route": "tool-only", "role_profile": None, "execution_profile": "tool-only", "agent": None}
    if shape.get("cross_module") and not shape.get("contract_frozen"):
        return {"route": "coding-leader", "role_profile": "coding-leader", "execution_profile": None, "agent": None}
    if shape.get("semantic_analysis"):
        return {"route": "semantic-analyzer", "role_profile": "module-worker", "execution_profile": "semantic-analysis", "agent": "semantic-analyzer"}
    if shape.get("verification_only"):
        return {"route": "verifier", "role_profile": "module-worker", "execution_profile": "verification", "agent": "verifier"}
    if shape.get("test_authoring_only"):
        return {"route": "test-author", "role_profile": "module-worker", "execution_profile": "test-authoring", "agent": "test-author"}
    if shape.get("atomic") and shape.get("module_id") and shape.get("change_principles_frozen"):
        return {"route": "module-worker", "role_profile": "module-worker", "execution_profile": "development", "agent": "module-worker"}
    return {"route": "coding-leader", "role_profile": "coding-leader", "execution_profile": None, "agent": None}


def codex_model_bindings():
    text = (REPO_ROOT / ".ai" / "model-routing.yaml").read_text(encoding="utf-8")
    leader = re.search(r'codex:\s*\n\s*PROJECT_LEADER_MODEL:\s*"([^"]+)"\s*\n\s*EXECUTION_WORKER_MODEL:\s*"([^"]+)"', text)
    if not leader:
        raise ProfileError("Cannot resolve Codex model bindings")
    return {"coding-leader": leader.group(1), "module-worker": leader.group(2)}


def validate_custom_agent_file(path):
    text = Path(path).read_text(encoding="utf-8")
    errors = []
    for field in ("name", "description", "developer_instructions"):
        if not re.search(r"(?m)^%s\s*=" % re.escape(field), text):
            errors.append("%s missing %s" % (Path(path).name, field))
    model = re.search(r'(?m)^model\s*=\s*"([^"]+)"', text)
    if not model or model.group(1) != "gpt-5.6-luna":
        errors.append("%s must bind gpt-5.6-luna" % Path(path).name)
    if re.search(r"gpt-5\.5", text, flags=re.IGNORECASE):
        errors.append("%s must not use GPT-5.5" % Path(path).name)
    return errors


def validate_repository():
    errors = []
    registries = load_registries()
    for module in registries["modules"]["profiles"]:
        required = ("responsibility", "owned_paths", "entrypoints", "key_symbols", "public_contracts", "dependencies", "context_sources", "indexes", "test_gates")
        for field in required:
            if field not in module:
                errors.append("module %s missing %s" % (module["id"], field))
        for path in module["owned_paths"]:
            if not (REPO_ROOT / path).exists():
                errors.append("module %s owned path missing: %s" % (module["id"], path))
    bindings = codex_model_bindings()
    if bindings != {"coding-leader": "gpt-5.6-sol", "module-worker": "gpt-5.6-luna"}:
        errors.append("Codex model bindings must be Sol leader and Luna worker")
    for name in ("module-worker.toml", "test-author.toml", "verifier.toml", "semantic-analyzer.toml"):
        path = CUSTOM_AGENT_DIR / name
        if not path.is_file():
            errors.append("missing custom agent: %s" % name)
        else:
            errors.extend(validate_custom_agent_file(path))
    for schema_name in ("work-item.schema.json", "work-result.schema.json"):
        try:
            load_json(SCHEMA_DIR / schema_name)
        except (OSError, ValueError) as error:
            errors.append("invalid schema %s: %s" % (schema_name, error))
    return errors


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    subparsers.add_parser("validate")
    work_item_parser = subparsers.add_parser("work-item")
    work_item_parser.add_argument("path")
    context_parser = subparsers.add_parser("context")
    context_parser.add_argument("--module", required=True)
    context_parser.add_argument("--execution", required=True)
    context_parser.add_argument("--revision", required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "validate":
            errors = validate_repository()
            if errors:
                for error in errors:
                    print("FAIL: %s" % error)
                return 1
            print("AGENT_WORKFLOW_VALIDATION_PASS")
            return 0
        if args.command == "work-item":
            errors = validate_work_item(load_json(args.path))
            if errors:
                for error in errors:
                    print("FAIL: %s" % error)
                return 1
            print("WORK_ITEM_VALIDATION_PASS")
            return 0
        manifest = build_context_manifest(args.module, args.execution, args.revision)
        print(json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True))
        return 0
    except (OSError, ValueError, ProfileError) as error:
        print("FAIL: %s" % error)
        return 1


if __name__ == "__main__":
    sys.exit(main())
