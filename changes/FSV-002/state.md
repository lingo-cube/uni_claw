# FSV-002 — Fast Perception provider variant activation

lifecycle_state: closed · disposition: none · depth: standard · base: c973b3e0

## Intent

让快速视觉 provider 的变体注册可配置、可诊断。默认 CPU 部署不应因为机器上存在但不可用的 MPS 变体而在启动期失败；MPS 仍必须显式启用并保持 fail-closed。

## Scope

- 增加 `UNICLAW_PIPELINE_VARIANTS` 激活集合配置。
- 默认仅激活 CPU 变体；显式声明未知变体时启动失败并报告名称。
- provider 只注册激活集合中的变体。
- 补充配置测试、启动验证和维护文档。

## Out of Scope

- 不改变默认管道输出、Runtime contract、Fast/Slow authority 或模型推理语义。
- 不把 MPS 自动降级到 CPU。
- 不启动、重启或关闭 3080 服务。

## Acceptance

1. 未设置配置时，CPU 变体可注册，MPS 变体不阻塞启动。
2. 显式启用 MPS 时只加载 MPS 变体；不可用硬件仍 fail-closed。
3. 未声明变体被明确拒绝。
4. 文档说明默认值、覆盖方式和硬件前置条件。

## Verification

```yaml
level: ENVIRONMENT
method: provider pytest with the repository perception venv; dedicated provider startup probe; git diff check
expected: configurable activation is deterministic, CPU startup remains available, and MPS is opt-in/fail-closed
actual: "activation tests 3/3; screenparse integration tests 20/20; live A4 non-empty inference 1/1; default active set is fastscreen-replacement, fastscreen-integration, promote-off (torch-mps and screenparser-mps excluded); dedicated UDS provider startup reached startup complete and /version 200; git diff --check passed. The full B2 ablation remains dependent on the host OpenMP shared-memory setup and is recorded as an environment limitation rather than a product regression."
evidence: platforms/perception/tests/test_replacement.py; platforms/perception/tests/test_screenparse_integration.py; platforms/perception/uniclaw_perception/pipeline.py; platforms/perception/uniclaw_perception/server.py; platforms/perception/README.md
```

## Status log

- 2026-09-30 · implemented · Root cause localized to unconditional startup lint of `fastscreen-replacement-mps` on a host without MPS.
- 2026-09-30 · implemented→verified→closed · Added explicit activation configuration, provider registry filtering, tests, documentation, and dedicated startup verification.

## Gate disposition

本 change 已闭合。默认 CPU provider 的启动路径已恢复；MPS 仍由显式配置管理并保持 fail-closed。B2 完整消融的 OpenMP 共享内存限制属于测试主机环境配置，后续可单独维护，不影响本 change 的配置边界。
