# Content Pinning for Profile Source Revision

DocumentType: `CHANGE_PROPOSAL`
Human Direction: `APPROVED`（2026-08-29 会话直接授权执行，业界 lockfile 规范对照）
Classification: `Medium`（既有契约内修改，不引入新 abstraction / boundary / lifecycle）
Status: `PROPOSED`

## Why

`ProfileSource._current_revision()` 当前用 `git rev-parse HEAD` 作为 profile 源
"版本身份"，`profile-source.yaml` 的 `source_revision` 钉扎一个 commit hash。
该选择是反模式（业界 lockfile / content-addressing 均锁定**内容指纹**而非仓库
进度）：**任何提交（含文档、测试、无关代码）都会让 pin 过期**，导致：

- 完整 AgentWorkflow 成片误报 `source revision drift`（20 用例挂起数日）；
- 生产 `validate` 从首个协议 commit 起长期拒绝运行；
- 每次协议变更都要人工"推一次锁"，且因 pin 更新本身就是 commit（hash 鸡生蛋），
  commit 后必然再次 drift——结构上无解。

业界规范做法（npm `package-lock` / `go.sum` / OCI digest）：锁 **规则内容指纹**，
锁文件由工具自动维护，重验放在**变更入口**（commit gate / CI），运行时只保留
轻量检查。本 change 对齐该做法且保留 fail-closed 保护。

## What Changes

- `_current_revision()` 从 `git rev-parse HEAD` 改为**规则文件集内容指纹**
  （sha256 over sorted paths + bytes）：
  `.ai/profiles/{execution,modules,roles}.json`、
  `.ai/schemas/{work-item,work-result}.schema.json`、
  `tools/agent_profile_validator.py`。
- `profile-source.yaml` 的 `source_revision` 语义从 commit hash 改为该指纹；
  **yaml 自身不参与指纹**（避免"更新锁→内容变→再漂移"自指）。
- 运行时 drift 检查（`load()` fail-closed）与兼容门不变——仅度量对象改变。
- 提交门：`verify-before-commit.sh` 检测"规则文件变更但 pin 未同步"并提示；
  新增 `scripts/sync-profile-pin.py`（幂等，原子写）维护锁。
- `binding_revision` / `profile_version` 继续取 `source_revision[:12]`，
  语义随之为内容指纹前缀，与绑定内容直接挂钩（更正确）。

## Capabilities

### New Capabilities

- `profile-source-content-pinning`:规则文件集内容指纹作为唯一 source revision 身份；
  非规则改动零干扰，规则改动精确失效；锁由工具维护。

### Modified Capabilities

无（现有 cap 的 pin 实现修正）。

## Impact

- `tools/dsh_profile_adapter.py`：`_current_revision()` 实现与 pin 语义。
- `.dsh/profile-adapter/profile-source.yaml`：一次性迁移 pin 为指纹。
- `tests/AgentWorkflow/`：`_pin_to_head` 与 setUp 改取指纹（workitem `base_revision`
  保持 commit hash 语义）；新增"非规则改动零漂移 / 规则改动精确漂移"用例。
- `scripts/verify-before-commit.sh`、`scripts/sync-profile-pin.py`：锁维护与门检测。
- `.dsh/profile-adapter/README.md`：pin 语义与维护说明。
- 不修改 `.ai/profiles/`、`.ai/schemas/` 内容、Runtime/Perception 生产代码、
  历史 `.dsh/profile-adapter/state/` 与 OpenSpec archive。

## Design Docs

- `openspec/changes/profile-source-content-pinning/design.md`
- 实现由 AgentWorkflow 与一致性门提供独立复核证据。