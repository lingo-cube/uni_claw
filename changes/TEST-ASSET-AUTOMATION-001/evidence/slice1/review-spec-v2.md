# TEST-ASSET-AUTOMATION-001 slice1 — Spec review v2

本轮仅复核上一轮 P1 的 profile 漂移修复及其相邻的首片资产契约，不扩展到
归档、诊断、重放或清理。首片的配置选择/路径隔离属 LAYOUT，登记、批次、
消费者完成后收尾属 AUTOMATION；两者仍由同一调用链验证。

上一轮问题已解除：布局绑定现固定发布到本机 state registry
（`tools/test_assets.py:83-86,129-144`），与 CLI、环境变量、profile 和
`XDG_CONFIG_HOME` 解耦；`locate_run()` 按清单记录的 namespace/digest 从该
不可变 registry 重建原 Run 根并校验 digest（`tools/test_assets.py:186-197`）。
`layout.json` 仍作为 Run 内记录保留（`tools/test_assets.py:239-243`），没有以
当前配置猜测旧路径。临时文件写完并 fsync 后才用硬链接发布最终 binding，竞争
者不会读取半写 JSON（`tools/test_assets.py:129-144`）。这符合保留规范对旧 Run
固定绑定、配置改变不得漂移及丢失时明确失败的要求
（`docs/agents/test-asset-retention.md:18-20,53-55,95-99`），也满足 LAYOUT A4。

复跑定向契约：`test_test_assets.py` 10/10、`test_verify_assets.py` 5/5、
`test_verify_entry.py` 9/9。前者真实创建 Run 后切换 `XDG_CONFIG_HOME` 仍可定位，
并覆盖跨 Change 首次绑定发布竞争；后两者继续证明 TRX/coverage/信号先消费、
取消保留 pending、失败不改写验证结论、retry 不覆盖失败 Run。未发现本次固定
范围内仍可行动的 Spec 问题。

该结论只覆盖 A1–A3 的 verify-change 首片；A4/A5 自动接入、可验证归档和任务
清理尚未实施，父 AUTOMATION Change 不应据此关闭。
