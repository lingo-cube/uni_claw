# Profile Source Content Pinning — Spec

## Requirement: source revision 身份 = 规则文件集内容指纹

`ProfileSource` 的 source revision MUST 是规则文件集的内容指纹（sha256 over
sorted 路径 + 字节），而不是仓库 commit hash。指纹文件集 MUST 包含：
`.ai/profiles/{execution,modules,roles}.json`、
`.ai/schemas/{work-item,work-result}.schema.json`、
`tools/agent_profile_validator.py`。

`profile-source.yaml` 自身 MUST NOT 参与指纹（自指规避）。

### Scenario: 非规则变更零干扰

- **WHEN** 仅文档、测试、Runtime/Perception 等非 pin 文件变更（仓库 HEAD 前进）
- **THEN** 指纹不变，`load()` MUST 通过，且不要求人工更新 pin

### Scenario: 规则变更精确失效

- **WHEN** pin 文件集中任一文件内容变化
- **THEN** 指纹变化，`load()` MUST fail-closed（现有 drift 拒绝语义不变）

### Scenario: pin 文件缺失

- **WHEN** 任一 pin 文件缺失或不可读
- **THEN** 解析 revision MUST 报错 fail-closed（不得使用部分文件集）

## Requirement: fail-closed 兼容门保持

schema version 门与 drift 拒绝 MUST 保持现有行为与错误消息形态
（`source revision drift: pinned … != current …`），仅 pinned/current 的值
从 commit hash 变为指纹。

### Scenario: 回溯兼容

- **WHEN** 既有调用方（DshWorkflowRuntime、CLI、测试）构造相同语义的配置
- **THEN** pin 值必须为指纹格式；workitem `base_revision`（commit hash 语义）
  MUST 与 pin 解耦，不得混用

## Requirement: 提交门维护锁

提交前验证 MUST 检测"pin 文件集变更但 `source_revision` 未同步"情形并提示；
锁的同步 MUST 由工具完成（幂等、原子写、不触碰其它字段）。

### Scenario: 锁同步工具

- **WHEN** `sync-profile-pin.py` 运行
- **THEN** `profile-source.yaml` 的 `source_revision`（宽松字段与 JSON 块）
  MUST 原子更新为当前指纹，重复运行结果不变

### Scenario: 门提示而非阻断

- **WHEN** verify 检测到 pin 漂移
- **THEN** 输出规则变更识别与 sync 命令提示，且 MUST NOT 阻断提交
  （运行时 fail-closed 由 `load()` 守护）

## Requirement: 迁移一次性完成

现有 commit hash 形式的 pin 值 MUST 迁移为指纹，作为本 change 落地的同一提交
的一部分；迁移后无需再次手工维护（除非真实规则变更）。

### Scenario: 迁移后幂等

- **WHEN** 落盘后任意非 pin 变更提交
- **THEN** pin 不变且 `validate` 通过