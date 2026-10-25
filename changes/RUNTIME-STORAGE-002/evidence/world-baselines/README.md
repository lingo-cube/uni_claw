# World独立基线与有界数据重建

## 做了什么 / 原来的问题
原v2修订从第一份delta顺序解码，没有可独立消费的周期基线。现在新写v3在第1、33、65…修订写完整基线，随原operation/frame/head同次确认；原完整reader消费这些基线，新的`WorldHistoryVersionReader`从目标最近基线重建数据。默认32由本片实际成本比较约束，不是保留期限。

## 这次修改
`FileWorldJournal`保持原完整frame/hash/head和逻辑恢复链，仍读v1/v2，不重写旧源。`WorldRevisionDeltaCodec`按产品Options周期写全字段基线，其余forward delta保留原字段/顺序/值；基线缺失/依赖旧值、错间隔/位置、超额与head未确认拒绝。`FileWorldJournal.Versions`复用原物理验证路径；新version reader的SourcePosition是打开时核验的固定head，重新打开才观察后续提交。查询不是完整World语义认证，`FullWorldHistoryValidated=false`且`ContinuationSupported=false`，不运行World/Agent/Effect。

公开消费者仍包含原`WorldHistoryRead.ReadOriginal`；RunRecoveryProbe新增`read-world-version`，真实子进程Kill后的两种历史场景同时验证原完整恢复与新版查询、源摘要不变。没有Kernel改动、产品/Harness配置混用或前缀删除。用户另明确批准`WorldAsyncStorageTests.cs`一行v2→v3的新写格式期望，其余断言逐字保持，补丁单独保存。

## 验证结果
首个已有公开缝行为RED为35真实受控World观测没有第1/33基线，官方稳定RED见`red-result.json`；API缺失的编译RED另存，不冒称运行用例失败。初次green的信号消费者参数曾写错，原pending/误写FAILED摘要保留，修正helper后新的Run完成真实消费者；首个入口源漂移也保留，不改旧hash。最后`world-final-v2-result.json`为154/154相关公开用例，sourceStable/consumersCompleted均true。包括基线及全部历史等价、值/顺序/未知字段、v1/v2形状 guards、坏前缀/基线/位置/空历史、原配额与周期基线超额故障锁定、实际异步/流式/恢复/子进程Kill。

成本probe用同一RecordingId及原逻辑行生成v1/v2格式对照；native v3用真实typed World生产链。128修订/257 operation：v3 **1,236,533** bytes，v2 **1,059,133**，v1 **8,184,254**；默认32增加约16.7%相对v2存储，最新/中点只解码32修订，旧v2最新128/中点64。所有查询与原完整reader SHA逐次相等，仍校验258物理帧。interval8文件1,914,507 bytes/最新解码8，interval64文件1,124,164 bytes/最新解码64；说明空间与解码上界取舍。完整读取原128MiB预算保持。三次warm样本的分配和耗时只是观察，不是稳定速度、物理IO或峰值内存保证。主报告经`cost-result.json.reportRef`读取；`cost-probe-source.cs`精确匹配当时原Run provenance，后新增旧源只读模式不改该成本证据。

`legacy-result.json`另用既有封存v1/v2及原Evidence的真实公开完整reader与新版query读取，257 operation/128修订均一致，原六源文件及头SHA保持，没有用重生成格式代替既有载体验收。

## 新发现 / 下一步
数据投影不能替代完整World语义认证；测试明确证明重签的坏早期逻辑delta仍被原完整reader拒绝，独立后期基线的数据查询不声称认证整个历史。默认产品保留不变，无自动回收。规范与需求独立审查各0，见`review-standards-v1.md`/`review-spec-v1.md`。候选与before、读取验证入口为`candidate.json`、`candidate.patch`和`verify.py`。本片通过后按原授权接Coverage独立交付；父A1–A10汇总关闭及最终精确提交/推送仍由原Leader处理。
