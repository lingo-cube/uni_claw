# 资源账目：最新状态与正反版本读取

资源首家族已完成；父 RUNTIME-STORAGE-002 保持开放。代码入口：`FileRunResourceJournal.cs`、`FileRunResourceJournal.Versioned.cs`、`ResourceSnapshotDelta.cs`。Runtime 原记录、计费、来源与恢复资格不变；新文件使用v4增量、周期基线与原子head，新增data-only最新/双向版本读取。旧v2/v3只读与原5参构造器保留，无恢复追加、自动删除或业务重执行。

## 验证
| method | expected | actual | evidence |
|---|---|---|---|
| 精确隔离源构建、公开journal/Driver/恢复与前片消费者 | 原值/顺序/null/删除可正反读取，提交失败不伪最新，旧消费者保持 | Host181/181、DSH50/50 | pure-host-v4.trx、pure-dsh-v4.trx |
| 真实进程终止后独立reader | settled可认证、pending仍未确认，全部原文件SHA保持 | 2/2；递归导出含baseline | pure-kill-v4.trx、pure-kill-v4/ |
| 来源、末位及非法输入回归 | 初始化双空、结果与head实际末位一致、Inspect非法格式返回unreadable | 三组实际RED→GREEN，Integrity最终15/15 | init-source-red/、integrity-tests/ |
| 独立双轴评审 | 无剩余可行动缺陷 | v4两轴通过；v2/v3为完整评审与增量复核链 | review-spec-v4.md、review-standards-v4.md及前版 |
| 同输入成本对照 | 保留同一logical内容，分别报告收益与代价 | 16/64/256输入hash和latest值相同 | cost/comparison-v4.json |
| 封存核验 | 前两片及本片源/证据摘要不变 | verify.py只读复核 | verification-v4.json、final-artifacts-v4.json |

256次合成确认：总保存量40,323,859→3,608,605字节；latest查询扫描40,323,675→757,083字节。writer编码耗时/分配增加，snapshot增加写/flush；不宣称全部更快。时间样例没有隔离并发负载，不能当严格延迟benchmark。源prefix hashing两边关闭，未测生产外源hash；读取计数是query本身，构造另校验head，query耗时/分配包含构造。合成fixture未运行真实预算决策、模型或设备。snapshot数量与空间单列；retained是writer存活时full-GC后值，不是峰值。

默认策略唯一源为ResourceVersionOptions：最多64条尾部记录，或累计增量达到max(64KiB,4×上一基线大小)触发新基线。该策略在三档样例中验证边界与成本，不宣称全局最优。原严格恢复仍全历史核验，快查询不认证未展开历史、不授予执行许可。

## 复现与范围
`candidate-v4/manifest.json`冻结15个所属文件；`build-binding-v4.json`记录1015输入、48个程序集指纹及精确叠加顺序。原archive在slice1，slice1 v6 overlay、slice2 v2与readonly输入、最后本片v4依次覆盖；每个输入都有SHA。`verify.py`不运行模型/设备或修改源码：

```sh
python3 changes/RUNTIME-STORAGE-002/evidence/slice3/verify.py
```

最初RED只证明新查询未实现，不把原v3 writer称为业务bug；原测试、scaffold和adapter副本已保存。编译竞争和旧物理fixture假设导致的失败保留，不能计作有效RED。旧物理fault测试显式构造v3夹具且断言不改；v4另有完整性/提交边界回归。初评对CreateNew及逐帧当前外源hash的过度推断已单独复判；真实遗漏为初始化source双空读校验，已修。

这是定向通过，不是当前共享工作区全量通过。共享tools README并发变更单列parallel patch并保留。前片全量既有失败不在本片修复范围。未提交/推送、未调用产品模型/设备，不承诺掉电/OS崩溃/跨节点。下一步核对其他适用核心族，再做World/辅助基线、增量和保留维护；不可变事实不强制逆向，默认不清理旧log或baseline。
