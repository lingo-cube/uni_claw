# product/ — 产品声明面（PRF-002 / ADR-0041）

本目录只放**版本化、评审准入的产品运行时装配工件**。产品代码在 `src/`，
宿主绑定在 `.dsh/`，两者都不得进入本目录。

## 允许放置

| 子目录 | 内容 | owner |
|---|---|---|
| `profiles/` | UniAgent Profile（host-neutral 装配描述：身份/能力词汇/模型角色/工件引用/修订号） | 产品（change 评审） |
| `tasks/` | 任务级 profile 声明：能力要求/参数/预算/终止/策略引用（ADR-0040；PRF-008 引入） | 产品（change 评审） |
| `skills/` | Product Agent Skill：领域级指导 manifest、guidance 与 reference 工件 | 产品（change 评审） |
| `prompt/` | 静态产品 prompt manifest（PRF-002 后续片引入） | 产品 |
| `policy/` | 危险动作等可机械校验的产品 policy 工件（PRF-004 引入） | Product/Kernel policy |
| `knowledge/` | 规范知识目录（Memory 侧 buyer 出现后引入） | Memory System |

## 禁止放置

- 任何 provider 名、model 名、服务端点——那是宿主 realization 绑定
  （DSH 侧 `.dsh/product/`），UniAgent Profile 里出现即违规（loader
  fail-closed 交叉核对）。
- 代码（→ `src/`）、测试（→ `tests/`）、Harness 语义（→ `.agents/` 等）、
  运行产物（→ 各自产物目录）。
- 运行态：session/run state、message history、usage——profile 永远是
  声明式配置。
- Skill 只描述观察、规划、受控执行、验证和证据流程；不得声明 provider/model/endpoint，
  不得授予 Capability、Effect 或 Memory 权限。工件经 Profile `allowedSkillRefs`
  按 name/revision/path/sha256 fail-closed 校验后才可装配。

## 变更纪律

- 内容变更须经 change 并递增 `profileRevision`（钉扎执法见 PRF-005）。
- 引用工件（prompt/policy）只以 hash 引用，正文归各自 owner。
- Skill 工件内容变更须更新 revision 与 artifact hash；manifest、guidance 和
  references 的顺序及路径保持可复现，禁止页面、车型或 OEM 专用 Skill 进入领域级包。
