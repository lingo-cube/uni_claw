## 1. RED-first 测试

- [x] 1.1 新增"非 pin 文件变更指纹不变 / pin 文件变更指纹变"用例（临时 root 构造 pin 文件集）
- [x] 1.2 将 `_pin_to_head` 与 CLI setUp 的 `source_revision` 改为指纹取值（workitem `base_revision` 保持 commit hash）

## 2. 实现

- [x] 2.1 `_current_revision()` 改为规则文件集内容指纹（sha256 over sorted paths + bytes；缺失文件 fail-closed）
- [x] 2.2 保留 `load()` drift 门与消息形态、`fingerprint()`（read-only 证明）、schema version 快门

## 3. 迁移与锁维护

- [x] 3.1 `scripts/sync-profile-pin.py`：原子、幂等更新 yaml 两处 pin 为当前指纹
- [x] 3.2 迁移 `profile-source.yaml` pin（commit hash → 指纹），与实现同 commit
- [x] 3.3 `verify-before-commit.sh` 集成"规则文件变更 → pin 未同步"检测与 sync 提示（非阻断）

## 4. 文档与验证

- [x] 4.1 更新 `.dsh/profile-adapter/README.md` pin 语义说明
- [x] 4.2 运行完整 `tests/AgentWorkflow`、生产 `validate`、`check-consistency.sh`、`git diff --check`
- [x] 4.3 手工 drift 实验：非 pin 变更通过 / pin 内容变更 fail-closed / sync 幂等