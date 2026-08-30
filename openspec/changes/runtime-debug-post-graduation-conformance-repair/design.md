## Context

P0 已冻结完整 Debug IR/Evidence Packet Schema；P1d 曾为了“基础骨架”绕过 required fields，P1a reader 只做浅层校验，P1c 又把 manifest/checksum coverage 表述成 verified digest。P2b/P2c 因此会在 malformed 或悬空引用输入上产生看似成功的 projection。此纠偏以 P0 Schema 为唯一 packet shape，不新增第二种同名版本。

## Goals / Non-Goals

**Goals:** 一个输入边界、一个完整 P0 shape、全引用闭包、真实字节完整性、固定阶段顺序、可重复的 graduation gate。

**Non-Goals:** 不新增 Trace 字段/服务，不修改 Runtime/Harness wire，不推断 FDP/Owner/Disposition，不实现 repair/replay/minimization，不引入第三方运行时依赖。

## Decisions

### D1 — P0 Schema 优先于已归档 P1d skeleton 设计

生成器填充 explicit absence，而不是删除 required fields；这是 Schema 已定义的语义，不是诊断 fabrication。备选“另建 base packet v0”会制造同名双合同；“另建 v1”会扩大 gate，均拒绝。

### D2 — stdlib fail-closed validator 是命令输入边界

reader 实现与冻结 v0 等价的结构、closed enum、additionalProperties、digest/refId 和引用闭包校验；测试中再用 Draft 2020-12 validator 对 Schema 做独立交叉验证。工具运行时不新增依赖，也不打开 EvidenceRef URI。

### D3 — Capture bundle integrity 读取 bytes，但不产生新 authority

bundle adapter 按实际 camelCase wire shape 逐项校验，并用固定大小 chunk 流式计算 SHA-256 与 byteCount。它只验证既有 artifact，不复制、不解码、不写入；`VERIFIED` 仅在 byte digest 一致后输出。

### D4 — canonical projection 由合同顺序驱动

EvidenceChain 只按冻结七阶段常量输出，不依赖 JSON property insertion order。可选字段用 conditional insertion；required explicit-absence 字段按原值投影。

### D5 — 历史毕业记录不回写

旧 graduation decisions 保留为历史事实；新 change 产出 post-graduation correction/revalidation receipt。只有新门禁全绿后，状态投影才写 `PASS_AFTER_CONFORMANCE_REPAIR`。

## Risks / Trade-offs

- [读取大型 artifact 增加耗时] → 流式、常量内存；这是声明 digest VERIFIED 的必要成本。
- [手写 validator 与 Schema 漂移] → v0 冻结、schemaDigest 固定、fixture corpus + 独立 Draft 2020-12 validator 双重门禁。
- [共享 dirty worktree 并发修改] → 限定文件 ownership、每次 patch 前后复查 diff 与测试，不覆盖无关改动。

## Migration Plan

1. 先新增 RED falsifier，证明当前浅校验、生成 shape 与 bundle integrity 缺口。
2. 修 reader/bundle/generator/projection，保持 CLI 名称与只读边界。
3. 运行 focused/full Python、P0 Schema corpus、OpenSpec strict、一致性与只读/确定性检查。
4. 全绿后追加 correction receipt、更新当前投影并归档本 change；任一门禁失败则保持 active。
