# Profile Source Content Pinning — Design

## 现状

`ProfileSource._current_revision()`（`tools/dsh_profile_adapter.py:243`）：

```python
def _current_revision(self):
    proc = subprocess.run(["git", "rev-parse", "HEAD"], ...)
    return proc.stdout.strip()
```

`profile-source.yaml` 的 `source_revision` 目前 = commit hash（如 `3986d3d…`）。

`load()` 的 drift 门：`current_revision != source_revision` → fail-closed
（保留）。`_current_schema_version()` 是独立的 schema version 快门（保留）。
已有 `fingerprint()`（profiles 3 文件的 digest，read-only 证明用，保留不动）。

## 目标设计

### 1. 指纹文件集（恒定、排序、全量）

```python
PROFILE_PIN_FILES = (
    ".ai/profiles/execution.json",
    ".ai/profiles/modules.json",
    ".ai/profiles/roles.json",
    ".ai/schemas/work-item.schema.json",
    ".ai/schemas/work-result.schema.json",
    "tools/agent_profile_validator.py",
)
```

顺序固定（元组）；`_current_revision()` 对每个文件
`digest.update(path.encode()) + digest.update(read_bytes())`，
输出 `sha256.hexdigest()`（64 位）。文件缺失/IO 错误 → `DshAdapterError`
（fail-closed，见 spec "pin 文件缺失"）。

### 2. 自指规避

`profile-source.yaml`（含 pin 值本身）不参与指纹。锁是"记账本"，指纹只记
"规则内容"。

### 3. 调用点适配

| 位置 | 现状 | 改后 |
|---|---|---|
| `_current_revision()` | `git rev-parse HEAD` | 文件集指纹 |
| `load()` drift 门 | 不变 | 仅度量对象变，消息形态不变 |
| `profile_version` / `binding_revision`（`[:12]`） | commit hash 前缀 | 指纹前缀（自动，无需改代码） |
| `load_for_work_item(source_revision or item["base_revision"])` | commit hash | **不动**（workitem 基线语义） |

### 4. 迁移

一次性把 `profile-source.yaml` 两处 `source_revision`（宽松字段 + JSON 块）
更新为当前指纹——由 `scripts/sync-profile-pin.py` 完成（原子写：临时文件 +
`os.replace`，两处同步，幂等）。迁移与本 change 同 commit。

### 5. 测试适配

- `_pin_to_head` helper（两个文件）与三个 CLI setUp：`source_revision` 改取
  `ProfileSource 指纹`（通过新暴露的模块级函数或构造临时 ProfileSource 计算），
  与生产实现同源；workitem `base_revision` 继续用 `git rev-parse HEAD`
  （commit 语义）。
- 新增用例：
  - `non-pin change leaves fingerprint stable`：在临时 root 构造 pin 文件集，
    计算指纹后写入一个非 pin 文件 → 指纹不变；
  - `pin change alters fingerprint`：修改任一 pin 文件 → 指纹变；
  - drift 消息形态回归（已有用例）。

### 6. 提交门

`verify-before-commit.sh` 新增 step：读 pin、算当前指纹；不等则识别
"pin 文件集是否在工作树变更"（`git diff --name-only` 现状 vs HEAD），
输出 sync 命令提示（非阻断）。`scripts/sync-profile-pin.py` 独立可调用。

## 验证

- `tests/AgentWorkflow` 全绿；
- 生产 `python3 tools/dsh_profile_adapter.py validate` 通过（新指纹 pin）；
- 手工实验：非 pin 文件变更 → validate 仍过；pin 文件内容改一个字节 →
  drift 拒绝；`sync-profile-pin.py` 幂等；
- `check-consistency` / `git diff --check`。

## 不做

- 不改变 `.ai/profiles` / `.ai/schemas` 内容与格式；
- 不引入新抽象、不碰 Runtime/Perception、不改变 fail-closed 契约。