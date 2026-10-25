# 剩余存储按调用链分组实施
所有者要求相似改造分组一起做（2026-10-25）。在父change既定异步＋合法批量范围内，将剩余六类合为三组交付；组内先验证一条真实buyer再复用实现，一组固定一次候选、双轴评审与最终相关回归，避免逐类型重复完整流程。每个Owner仍各自确认，不将开发分组变成跨Owner事务或流水线。

| 组 | 范围 | 共同部分 | 独立保持 |
|---|---|---|---|
| A（Slice 4，已完成v2） | Control遍历＋Runtime Plan | 两个相同frame/hash/head文件writer共享Host内部机制；贯通同一Driver的选择/接替等待链；配置＋启动合法双记录批次，实测managed调用10→9 | Control候选确认后交工作；replacement-begin→Control abandon→adopt分别确认；原格式/严格度/来源不变 |
| B（Slice 5，已完成） | 资源账目＋P25咨询＋可靠执行记录 | 外部调用前保存、结果后追加的原生异步等待；实际delivery后submission＋receipt两原帧合法合批，write/flush各3→2 | begin/confirm、reserve/complete、prepare/receipt各归原Owner；三值和未决语义不合并 |
| C（Slice 6，已完成） | 身份归档＋初始化＋统一收尾 | 归档/两来源marker/World Owner异步确认、完成/取消/裁决回流与两Host输出/关闭；两个同步桥/九路径计数统一验收 | 归档一次写入，Trace保留自身等级；不借机改恢复游标、自动续跑或路径优化 |

共用机制只服务已有实际buyer，核心仍依赖各领域接口；不增加每类型专用线程、通用队列或数据库。已完成Evidence/World/Run证据保持原封存；组A完成后再顺序处理B、C，父A1–A4未全部证明前不关闭。

组A结果：新26＋10例通过，最终Kernel1249/1252、Host292/293、DSH24/24，仅同3+1旧失败；真实双窗口Kill/四新reader与I/O计数、v2双轴及verify通过。主要运行期5/9，接下来直接处理组B，之后组C统一收尾；不再逐类型单开完整评审/回归。

组B结果：新35＋13例通过，最终Kernel1284/1287、Host343/344、DSH24/24，同3+1旧失败；同场景原三帧/1492字节/1delivery保持，write/flush/批次3→2。四真实Kill/八新reader、两轴各0及verify通过。八运行期领域已接原生async；身份归档计入总路径为8/9，下一C组归档、全域初始化/关闭/普通产物及剩余completion/cancel回流和最终性能验收；父未关闭。

最终结果：C组新13＋8例、Kernel1297/1300、Host356/357、DSH24/24，仅原3+1失败。九路径接线/初始化/关闭及父A1–A4全部证明，两同步桥、原归档实际Kill/两个新reader和九源性能计数通过。v3仅新增Host用例，Kernel/DSH按相同源码/程序集复用v2；Host相关选择重跑。两轴0、独立verify通过，父change关闭。此前每组结果为阶段历史；当前完成状态以本段及父state.md为准。
