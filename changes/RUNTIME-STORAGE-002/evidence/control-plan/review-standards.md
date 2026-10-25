发现 1 条（非硬违例）：`ControlPlanGrowthProbe.Run` 在创建 journals、构造 Kernel/Driver 前取 `allocatedBefore`，DriveAsync 后才取差值（candidate.patch:91,149-150），却把包含初始化/fixture成本的总分配命名并汇报为 `Driver.ManagedAllocatedBytes`（:218-220）。这使公开预算回归无法精确归因于 Driver 热路径。建议把计时边界移到 DriveAsync 前，或诚实命名为 scenario allocation。依据 AGENTS.md §1.5(1,4)：已接受基线优先、验收须可判定；亦属 Fowler Mysterious Name 的判断项。

未见其他 Standards 发现。缓存仅在 AppendRuntimePlanAsync 成功后更新latest，pending替换序号在 begin 成功后缓存、adopt确认后清除（candidate.patch:37-39,52-58），与 ADR-0055 的真实确认/未决语义一致。未运行测试（按委派范围）。
