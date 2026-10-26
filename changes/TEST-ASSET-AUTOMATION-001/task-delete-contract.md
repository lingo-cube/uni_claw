# 显式删除开发任务资产

这是Change资产的显式退役，不删除Codex聊天、Change状态、产品数据或正式基线。state、assets索引和追加的taskDeletions回执为最小审计壳；原Run/归档条目与其历史hash不重写。未登记材料列为保留/未知，不靠目录名取得删除许可。

公开缝：plan_task_delete(repo, change)及delete_task_assets(repo, change, plan)，由原test-assets CLI暴露plan-delete-task和delete-task-assets。计划是只读盘点，包含确定的planId、候选文件身份/大小/SHA、kept/pending/absent与引用理由。执行重算盘点核对同一计划；新增引用、文件/绑定/元数据变化拒绝旧计划。CLI --out只写明确计划文件，不自动删数据。

所有外部test-assets逻辑引用、物理路径/别名/覆盖目录、baseline/原来源都保护。目标任务自己的历史文件不是外部消费者；同Task Run间依赖建图，保留消费者的依赖递归保留。未决/活动/不完整或未知材料保留；循环或不确定依赖不强删。归档按各Run旧layout定位、验证成员/摘要与原消费者，不按当前profile猜根。

执行仅unlink清单中仍一致的独占普通文件；拒绝链接/硬链接、未知文件和漂移。先写pending，逐次记实际deleted/alreadyAbsent/逻辑字节，不把计划计为删除。只移除空目录。按消费者先于依赖处理；失败保留其尚需依赖，明确pending，下一份当前计划可继续。回执存原引用图及record摘要供部分删除后的重试，不冒充原始数据。

任务外层锁协调新Run登记与archive/restore/release；原索引锁/Run锁仍沿既有顺序。开始执行即关闭该资产集合的新建，完整或部分退役Run的读取/归档/恢复明确拒绝，保留Run仍可读，既有活动Run可完成收尾。工具核对已登记来源与仓库当前引用，不宣称全机/跨Owner原子事务；调用者须先结束目标任务自有读写，未遵守协议的并行漂移明确未决。

本轮只在新建独占受控任务上执行实际删除。自动GC、存量迁移和静默删除历史任务不启用；无设备、模型或外部投递。
