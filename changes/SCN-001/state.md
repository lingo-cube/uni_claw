# SCN-001 — 场景库：可执行的场景定义 + 能力覆盖率

lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: 1f01b52

## Intent

仿真要知道来源、目的，要能模拟/回放过程，要能建立基线和场景库，从而
反馈产品整体能力。场景库是**独立的仿真基础设施**（按职能定位），不是
任何文档的附属品。150 场景推理文档（ARCH-DOC-017）是场景的**一种来源**
——由此派生的场景可选标注 srRef，但库的身份不依附于文档。

## Vision（用户 2026-09-22）

- 快速模拟实际问题、验证逻辑链路、有效利用测试/生产资产
- 回放时能注入修正、打断、进行某一环节的修正（Phase 3 交互调试）
- 建立基线 → 场景库 → 反馈产品整体可达能力
- 集成不同实现，为不同组件提供测试（SIM-001 已就位）

## Scope（Phase 1：本 change）

- `scenarios/` 目录 + JSON schema（8 元数据字段 + 期望值）
- 迁移全部 ~20 个既有场景（GoldenScenarioBundles + 测试断言中的期望）
- `tools/scenario-coverage.py`：聚合 srRef × status → 能力覆盖率报告
- 与 ARCH-DOC-017 的 SR-xxx 建立交叉引用

## Out of Scope（Phase 2/3，买家驱动）

- Phase 2（F9 落地时）：参数化场景生成（B 形态）
- Phase 3（有真实调试需求时）：交互式调试（暂停/注入/续跑，C 形态）
- 150 场景全量入库（只迁已有测试的 ~20 个，其余等有测试再入）

## Decisions

- D1 三种形态分阶段：A 回放（现在）→ B 生成（F9）→ C 交互（等买家）
- D2 srRef 可选：150 场景文档是来源之一（derived-from-doc 时标注），
  其他来源（recorded / generated / bug-repro / component-test）不强制
- D3 8 元数据字段：id/name/source/purpose/capability/components/status/version
- D4 目录：`scenarios/`（与 changes/、plans/、evidence/ 平级）
- D5 覆盖率工具：确定性 python 脚本，扫 JSON → 聚合报告

## Acceptance

1. `scenarios/` 目录存在，含 schema 定义文件
2. 全部既有 ~20 场景以 JSON 形式在库，每条带 srRef
3. `python3 tools/scenario-coverage.py` 输出能力覆盖率报告
   （按 capability × status × source 聚合，不限于文档来源）
4. 既有测试全部通过（迁移不改变行为，只抽取元数据）
5. 报告中能按 components 过滤（"kernel 相关场景通过率"）

## Status log

- 2026-09-22 · created·persisted · grill 四问落定（A+B 先行 / srRef 强制 /
  8 字段 / 全量迁移 + 覆盖率工具）；用户愿景已录于 Vision 节
