# UniClaw Test Lab

本地测试管理项目的规范名称是 **UniClaw Test Lab**，canonical projectRef 为
`project/uni-claw-test-lab`。

这里暂时维护 Host 尚未提供持久化 Project/Test Set Catalog 时所需的声明式测试资产。每个子目录对应一个测试集，包含 manifest、任务引用、fixture 和验收引用；当前版本统一使用 `default`。目录是本地 storage adapter 的输入，不是 Worktree、Workspace 存储，也不替代 Host 或 Runtime 的权威数据。

UniClaw Workspace 是产品界面名称，负责读取和展示这些项目资产、任务实例、运行记录与证据。Test Lab 是被管理的测试项目，两者保持独立，后续可以由 Host Catalog adapter 替换本地目录而不改前端契约。
