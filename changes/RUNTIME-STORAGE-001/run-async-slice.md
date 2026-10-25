# Run原生异步实施片（Slice 3）
状态：COMPLETE（C1–C5）；父change保持implementing。沿已授权的Run journal/Owner、Kernel/Driver、正式文件adapter及历史reader公开缝实施，不需要追加用户决策。

## Scope / Decisions
IAsyncRunStateJournal扩展原领域接口。Run admission/cycle/action/terminal及遍历记录共用候选→真实确认→发布，原同步入口为显式兼容桥；旧同步journal兼容。单Owner一条在途操作，重入立即拒绝，收集/准入窗口0；接受前取消不写，接受后等待确认，失败含ack丢失锁存、不重试。

正式Kernel/Driver/Host贯通await，不使用Task.Run包装存储。FileRunStateJournal保持原v2/v1读取、frame/hash/head/EOF及刷盘请求；采用Asynchronous＋WriteThrough写入frame和临时head，FlushAsync后原子替换head，再确认。默认16MiB可配编码frame上限；不引入通用队列、跨Owner事务或跨外部动作凑批。单逻辑记录一次提交不报额外batch收益。构造/历史读取、Control/Plan/资源/P25/执行/归档仍同步，父A1–A4仍未完成。

WriteThrough选择依据：[官方FileOptions文档](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileoptions?view=net-10.0)与[.NET 10 Unix实现](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/Microsoft/Win32/SafeHandles/SafeFileHandle.Unix.cs)映射O_SYNC。它保留刷盘请求，非普通FlushAsync替代Flush(true)；不据此新增掉电/目录元数据耐久性声明。

## Acceptance
C1 Run公开异步Owner：admission未ack时View/RunId/State不可见，之后所有状态/遍历事实与HistoryPosition确认后发布；同Owner同步/异步重入拒绝；失败前/提交后ack丢失锁存，取消接受前零写、接受后结算。
C2 Kernel/Driver：cycle未ack零投递，action已投递后等记录确认才继续观察/咨询，terminal未ack不发Outcome/不提前报完成；遍历operation/Running记录未ack不调用handler。正式Host使用async等待/关闭，同步兼容语义保留。
C3 文件真实异步frame/flush与head确认、正常异步关闭、字节上限/失败锁存及旧v1/v2读取一致；不降低原刷盘请求、不声称额外合批收益。
C4 真实非terminal Driver在Run文件提交后Owner获ack前Kill，两个新reader读取原记录/原来源SHA，不授权自动续跑。取消/故障等待期间不绕过已接受保存。
C5 固定dirty入口653构建输入、17候选及前片120封存项；新增行为用例/相关回归、同入口旧失败对照、独立双轴review和四元组通过。旧失败可复用与当前653源码完全一致的前片已封存最终TRX，不能把不同源码结果当基线。

## Plan
公开admission未ack RED→共享Run异步Owner GREEN→Kernel/Driver运行期及遍历await→文件原生async/关闭→故障取消和实际Kill/新reader→固定候选→独立双轴review→验证/封存。本片父change不关闭。

## Verification / Result
level: DETERMINISTIC + SCENARIO
method: 公开Run Owner/Kernel/Driver延迟确认、原生文件adapter、真实Driver提交后Owner ack前Kill与两个新reader；固定17候选、入口653构建输入的同源基线、独立双轴review与verify。
expected: C1–C5；父A1–A4仍未全域完成。
actual: 新Kernel18/18、Host5/5；最终Kernel1223/1226、Host200/201、DSH24/24，同3+1旧失败与653源码完全一致的入口基线相同；旧README登记9→7且仅减少本片两个Run文件。真实Driver Owner位5/源6条/未调用handler，两个新进程读回原6条及Running历史并保持来源SHA。取消等待窗口补存Cancelled，有RED→GREEN。642非本片构建输入、前片120封存项不变；两轴0，verify通过。单记录无额外合批收益；其他域及初始化保持后续。
evidence: `evidence/slice3/README.md`、`test-summary.json`、`candidate-verification-binding.json`、`review-summary.json`、`verification.json`、`final-artifacts.json`（均在slice3）。下一工程动作：Control遍历journal/Owner及Driver原生async，选中工作确认后才交出，不重做恢复游标/接收协议。
