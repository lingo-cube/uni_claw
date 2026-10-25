# World独立基线与有界重建契约

## 范围与公开读取缝
按原Leader正式交接与WI-STORAGE002-WORLD-CONTINUATION，现有`FileWorldJournal`写入/完整只读构造器、`WorldHistoryRead.ReadOriginal`以及RunRecoveryProbe的World历史读取是已同意公开验收缝。本片在Host文件adapter内增加`WorldHistoryVersionReader`只读数据查询；不改Kernel领域事实、Evidence入证、Control/Plan/资源版本引擎或执行许可。产品构造Options与已解析产品文件路径贯通读写，不读取Harness JSON。

## 格式、基线和提交
- 新建文件使用World history v3；v1/v2只读兼容，原文件不迁移、不重哈希。原frame的RecordingId、Sequence、Previous/hash与head完整末位验证保留。
- 每隔配置数量的修订写入一份独立完整修订基线，其余仍用现有forward delta；初始修订必须为基线。基线随对应原World operation同一frame与head确认，不新增旁路提交、不能把仅prepare的基线当作已发生版本。
- 基线包含原完整修订字段/顺序/值，包括空值和未知扩展字段。数据版本、覆盖修订号、operation sequence和来源RecordingId明确关联。重建最多需要当前基线与后续`BaselineInterval-1`个修订增量；缺失/损坏/错序/错来源拒绝，不猜测或回退旧值冒充最新。
- `WorldHistoryVersionOptions.BaselineInterval`为显式产品构造参数，默认32，候选32会通过同内容成本验证，不以目录/日期定义保留。新基线不授权删除任何前缀或旧证据。

## 完整恢复与数据查询分别承诺
原完整reader继续解码全部逻辑记录，`WorldHistoryRead.ReadOriginal`保留原领域引用/修订parent/入证等完整认证；它实际消费v3基线并与原逻辑记录逐项等价。新版本reader仍校验完整物理frame链/head并索引修订，但只从目标最近基线解码数据。它不是完整World语义认证，不重放领域操作、不生成Live World、不执行Agent/Effect、不授予续跑。v3按上述策略限制解码链，旧v2可读但明确没有独立基线界限，v1全量修订可直接读取。

## 验收与成本
1. 实际WorldModel→FileWorldJournal→原WorldHistoryRead同值/同顺序，独立基线由原reader消费，原归属和提交确认不改。
2. 最新与指定历史查询的来源/operation/revision位置一致，独立基线后解码量有界；前缀物理损坏即使目标有好基线也拒绝。原完整reader仍发现语义坏链，数据查询不冒称完整认证。
3. v1/v2兼容及原文件摘要保持；基线缺失/损坏/错误间隔/错来源、重复或缺失增量、未知修订、未确认head等显式拒绝。完整无修订历史查询为空。
4. 使用同一原始World逻辑记录比较新格式、原v2和v1的保存字节/基线额外成本、writer managed写/flush、完整读取和版本查询的扫描/解码/分配/耗时；分开披露完整校验仍需扫描原链，不能宣称IO/完整恢复为O(1)或单次时间为稳定速度。原128MiB公开完整读取分配预算保持。
5. RED→GREEN、原定向回归、独立Spec/Standards、实际资产收尾完成后才标记World WorkItem done；Coverage随后按原授权开始，父Change不关闭。
