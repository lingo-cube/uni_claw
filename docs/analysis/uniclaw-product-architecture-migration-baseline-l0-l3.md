# UniClaw Product Architecture L0–L3 — Split Manifest

> DocumentType: `DOCUMENT_SET_MIGRATION_MANIFEST`
>
> Status: `SPLIT / ROUTER`
>
> Authority: `NONE`
>
> Date: `2026-09-05`
>
> Scope: 原混合候选文档的拆分记录与导航
>
> Forbidden Boundary: 本文不定义 Target Product Architecture，不定义迁移步骤，不改变现行 Architecture、Protocol、Runtime Contract、active change、代码、测试、生命周期或 Owner。

---

## 1. 拆分结果

原候选文档同时混合了 Target Product Architecture、Legacy 映射和 Migration Procedure。为消除语义与治理耦合，现拆分为三个独立资产：

1. [Target Product Architecture — L0–L3](../architecture/product-architecture-baseline-l0-l3.md)
   - 只回答未来 Product 应该是什么；
   - 定义 L0–L3、Owner、Authority、Lifecycle、Boundary、Invariant 与 Replaceability。
2. [Legacy → Target Architecture Mapping](legacy-to-target-architecture-mapping.md)
   - 只回答现行/旧概念怎样映射到 Target；
   - 保存现行 Architecture、Protocol 与 Runtime Contract 的兼容解释。
3. [Product Architecture Migration Baseline](product-architecture-migration-baseline.md)
   - 只回答如何安全迁移；
   - 定义 owner census、state disposition、cutover、验证、回滚与归档 Gate。

## 2. Migration Manifest

| Source | Target | Preserved facts | Governance correction |
|---|---|---|---|
| 原 `uniclaw-product-architecture-migration-baseline-l0-l3.md` Part I–IV | `product-architecture-baseline-l0-l3.md` | Product 定位、L0–L3、Evidence/Belief/Plan/Effect/Completion 分离、Owner/Authority/Boundary/Invariants | 恢复 Target canonical naming；把 aggregate boundary 与 L2 canonical ownership 分开；移除 legacy 与 migration 内容 |
| 原 Part V、迁移 Contract Card 与 migration invariants | `product-architecture-migration-baseline.md` | Authority-first、disposition、no dual truth、exact-prior、cutover、G0–G7、rollback、archive | 与 Target 语义彻底分离；迁移过程不再定义 Product Architecture |
| 原当前语义映射、术语映射、现行兼容说明 | `legacy-to-target-architecture-mapping.md` | 现行概念、权威来源、兼容关系与映射约束 | 旧术语仅在 mapping 出现，不再成为 Target canonical term |

## 3. 生命周期说明

- 原混合内容不再作为阅读入口或候选基线正文；本文件只保留拆分 provenance 与导航。
- 三个目标文件均为 `Authority: NONE` 的可编辑候选，不替代任何现行权威来源。
- 本次拆分不删除历史 Decision、Gate、Spec、Result 或运行证据。
- 后续修订必须进入与问题类型对应的目标文件，禁止重新把三类内容合并回本文。
