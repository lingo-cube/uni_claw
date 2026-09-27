# PER-016 Legacy State Surface Removal

日期：2026-09-27  
范围：PER-015 closed 后的 Product Runtime legacy surface removal

## 做了什么

- 删除 Product Runtime 的 `LegacyStateProjection` production type、Host legacy egress flag、XML `MapTargetStateClaim` writer/routing 与 `SharedSubjects.State`。
- Host 仅保留 typed hierarchy projection；post-action system settings 读数只作为独立现实探针，不再进入 legacy state subject。
- 将 projection mapping 保留为 Kernel test fixture，保留历史/回放 `switch.state` 输入；生产源码 tripwire 明确禁止回流。
- 将 legacy route tests 改为 production surface tripwire；PER-014 inventory 改为零生产 state surface。

## 原来的问题

PER-015 已证明 typed chain 可完成，但 legacy surface 仍可由回滚旗、writer 和兼容 projection 重新接入。继续保留这些生产买方会让删除结论不可执行，也会让未知/partial 发生隐式回退的风险持续存在。

## 修改后的调用链

真实设备：截图 → typed UiAutomator hierarchy projector → EvidenceLedger → WorldModel/SemanticCheckedResolver → RuntimeAssurance → ADB effect。post-action 的独立 settings 读数只用于现实复核与 frame 载荷；没有 `*.state` writer、projection 或 rollback route。

## 验证结果

| 检查 | 结果 |
|---|---|
| production legacy surface tripwire | PASS |
| Kernel | PASS 643/643 |
| Host deterministic + tripwire | PASS 48/48 |
| Simulation | PASS 184/184 |
| Agent DSH | PASS 121/121 |
| full solution | PASS 1051/1051 |
| real TypedLiveChain + LiveCoordinateGate | PASS 3/3 |
| real HostLiveFull | PASS 1/1；`Completed`, `delivered=1` |
| certification | PASS 29/29 |
| scenario coverage | PASS |
| diff/shell checks | PASS |

## 新发现

API 35 Settings hierarchy 对 boolean false 仍按 collapsed capability 保持 Unknown；真机 Host gate 通过固定 off 基线验证 typed checked=true，不把缺席猜成 unchecked。历史 replay fixture 仍保留，未进入 Product Runtime。

## 结论

PER-016 removal gate PASS。PER-011 未启动；历史、fixture、回放输入保留。
