# Host projection Spec review

范围：只审阅 `entry.json` 捕获的两份旧源与 `candidate.patch` 的五个候选文件；未审阅共享 dirty HEAD，未运行构建或测试。

候选摘要已核对：`candidate.json` 所列五个 SHA-256 均与当前文件一致。既有证据记录的 RED（坏摘要不返回 null、同幂等不新铸 Run、activation 关闭后不发布）及最终 30/30 仅作为已提供的验证记录，本次未重跑。

## Findings

1. **[P1] 防止 `Revision` 溢出后把未可读的投影作为成功结果返回。** `RuntimeRunStore.Transition` 与 `UpdateArtifacts` 直接以 `current.Revision + 1` 生成下一修订；当当前值为 `long.MaxValue` 时，该值回绕为负数，而 `CommitProjectionUnsafe` 的间隙检查同样在 `long` 算术中回绕，因此会写出并返回该 projection。随后重新读取会被 envelope 的 `Revision < 1` 拒绝，违反 A3「部分/损坏不发布伪最新」以及本片的 long 修订要求。应在递增前拒绝 `long.MaxValue`（并保持旧 latest 可读），并以边界用例覆盖。路径：[RuntimeRunStore.cs](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/Runtime/RuntimeRunStore.cs:162)、[RuntimeRunStore.cs](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/Runtime/RuntimeRunStore.cs:203)、[RuntimeRunStore.History.cs](/Users/fran/Documents/Code/spacex/uni_claw/src/UniClaw.Host/Runtime/RuntimeRunStore.History.cs:123)；规则：[state.md A3](/Users/fran/Documents/Code/spacex/uni_claw/changes/RUNTIME-STORAGE-002/state.md:44)。

2. **[P1] 补齐非空 Host 上下文字段的双向版本断言。** 合同将 `ProjectRef`、`TestSetRef`、`TaskRef`、`Environment` 列为随创建完整保存的查询/报告上下文；但版本往返用例的 `Request()` 将四者都设为 `null`，仅字节限额用例给 `ProjectRef` 一个非空值，且不会读取任何版本。因此目前 30/30 没有证明这些字段在 forward/reverse delta 中可还原。应以不同形状的非空四字段创建 Run，再对每个 revision 的两个方向断言完整 JSON 相等。路径：[RuntimeRunProjectionVersionTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Host.Tests/RuntimeRunProjectionVersionTests.cs:14)、[RuntimeRunProjectionVersionTests.cs](/Users/fran/Documents/Code/spacex/uni_claw/tests/UniClaw.Host.Tests/RuntimeRunProjectionVersionTests.cs:44)；规则：[host-projection-contract.md](/Users/fran/Documents/Code/spacex/uni_claw/changes/RUNTIME-STORAGE-002/host-projection-contract.md:8)。

其余重点与合同相符：latest 绑定 head frame 与完整 projection hash；完整读取验证 frame 链和旧前缀；legacy 起点、原事件分页、公开 DTO/构造器、拒绝回调、未确认 suffix、字节限额及 activation 关闭前发布均有对应实现或候选测试。数据查询 API 仅声明读取投影，不取得执行或恢复授权。
