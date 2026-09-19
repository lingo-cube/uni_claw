# Evidence — CORE-003 Core 对象与最小语义关系

- Date: 2026-09-19
- Scope: `UniClaw.Core` 候选对象基线（Clause 四类条款 + Event 组合表达）与纯 Core 关系链验证
- Level: DETERMINISTIC
- Status: VERIFIED_WITH_SCOPE

## 实施范围

- `src/UniClaw.Core/CoreRecords.cs`：
  - `Clause` 增加 `ClauseKind`（Requirement / Permission / Constraint / Criterion，候选词汇）；
  - Event 发生语义**不新增类型**：发生/来源/时间/参与者/次数/同一性/因果判断由
    Evidence + Claim 组合表达（CORE-001 §4.4 验证缺口的正向证据；独立类型仅在
    表达/演化反例出现时引入）；
  - 其余记录形状零变更（未新增 freshness/trust/confidence/correlation/version 类字段）。
- `tests/UniClaw.Core.Tests/CoreObjectRelationTests.cs`（5 tests）：
  1. Clause 四类 + 「观察永不产生授权」三重结构证明（Core 无产出 Clause 的
     导出 API；World/Effect 记录无条款字段；CoreInvariants 判定面不消费
     Claim/Evidence）；
  2. 主垂直 tracer（手机滚动—点击域，spec story 15）：Specification 条款 →
     Segment ⊃ 多 Slice（重叠覆盖）→ Evidence → Claim（Revise 保留依据）→
     Effect ⊃ Attempt uses TargetBinding（basis 固定、尝试时间独立）→
     点击后 Evidence → 状态 Claim（依据=新证据，非 attempt）；
  3. Event 组合表达：七面逐项可取回；发生主张依据世界证据而非 receipt；
     因果是判断（Candidate）且引用 attempt 依据；事件主张无权限副作用通路；
  4. 删除/演化检查：update-then-use 与 use-then-update 两条路径最终状态全等
     （字段级）、中途快照不被后续追加改写、必要区分（basis 固定 vs 最新、
     Unknown 不升级）双路径保持；
  5. Core 纯度：程序集零 UniClaw.* 引用；八类记录属性类型白名单
     （string/CoreId/DateTimeOffset/枚举/CoreId、string 列表）。
- 未触碰 Kernel、CoreAdapter、Agent 及其测试；无新项目、无 slnx 变更。

## 验证命令

```text
dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --logger 'console;verbosity=minimal'
dotnet test UniClaw.Kernel.slnx --logger 'console;verbosity=minimal'
```

## 预期

- Core.Tests 8/8（既有 3 + 新增 5）。
- solution 全量 525/525（520 基线 + 5 新增）通过；Core 依赖闭包保持域中立。

## 实际结果

```text
Core.Tests:               8/8 通过
UniClaw.Kernel.slnx:      525/525（Agent 17 + Core 8 + Simulation 112 + Kernel 388）
```

环境说明：`NU1900`（NuGet 漏洞缓存路径无权限）既有警告，未影响执行。

## 发现的候选摩擦（留痕，不在本 Change 裁决）

- **列表字段引用相等**：候选记录的 `IReadOnlyList` 字段参与 record 相等性时按
  引用比较——两条构造路径产出同内容记录，`Assert.Equal(recordA, recordB)` 为
  false（删除/演化检查实际撞上）。本 Change 以字段级比较实现状态全等断言；
  「候选记录是否应具有列表内容相等性」是候选协议决策，留待后续裁决，不单方面改。

## 剩余缺口

1. Event 独立类型未裁决：组合表达通过正向表达测试，但「表达失败/演化失败」
   反例尚未出现（CORE-001 §4.4 缺口保持开放）。
2. 严格最小性未证明（spec 明示）；ClauseKind 四词为候选词汇，不锁最终枚举。
3. 授权结构证明限于 Core 边界内（无导出 API / 记录无授权字段 / 判定面不消费
   观察内容）；Core 之外的系统仍可能错误实现授权流，Core 只保证自身边界。
4. 旧模型对齐（Kernel 侧投影语义与本对象基线的进一步收敛）明确属后续 Change
   （spec Further Notes），由具体责任与反例驱动。
