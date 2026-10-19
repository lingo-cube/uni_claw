# 最终插件全量43/44的夹具隔离问题

- Expected：每个mock host测试结束经公开detach退出，真实在途retry屏障在测试内部保持，跨测试不泄漏。
- Observed：plugin-tests-final44.txt中PRF-003 section-mount因promptCalls[0]为空失败（43/44）；之前PRF-009 per-turn Skill测试直接plugin.apply，未通过applyHost注册activeHarness。它发turn/end但无决策会触发既有retry，afterEach无host可detach，activeTurn保留到后续咨询。
- First divergence：该直接挂载测试未注册清理对象，非生产one-in-flight屏障错误。
- 修改：仅在该直接挂载测试设置activeHarness=harness，让既有公开detach afterEach执行。产品src、重试上限、deadline与turn/end屏障不变。
- 验证：原完整43/44作为RED；等待当前串行真实模型组结束后复跑直接mount→后续section-mount序列与完整44项，未运行前不标GREEN。

最终GREEN：integration/plugin-tests-final44-green.txt，TAP tests44/pass44/fail0，进程exit0；保留原43/44日志。真实turn/end fence反例未放宽。
