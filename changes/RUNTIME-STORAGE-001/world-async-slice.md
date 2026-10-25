# World原生异步实施片（Slice 2）

状态：COMPLETE（B1–B5）；父change保持implementing。前片WORLD-HISTORY-002的382项封存保持不变。

## Scope / Decisions
沿现有World存储抽象增加IAsyncWorldJournal；FileWorldJournal使用原生WriteAsync/FlushAsync与异步关闭，保留v2增量格式/v1只读及原frame/head确认。World八类操作共享同一候选计算/确认/发布逻辑；原同步入口是显式兼容桥，旧journal保持同步fallback，不用Task.Run。

贯通Kernel观察处理、权限冲突处理、动作后的导航/视口验证、contract demand登记与两个正式Host等待链；Run/Control/Assurance等其他存储自身仍同步，不能声称全部存储已异步。文件和World构造初始化仍同步，留父change初始化迁移后续。本片不重做增量编码，不制造跨操作事务或队列。

单Owner一条在途操作；收集/准入窗口为0，非法重叠立即拒绝。单个编码frame默认上限16MiB（adapter构造参数可配置），超过上限写前失败并锁存；没有跨请求合批或排队。候选准备后先恢复已发布状态，再await实际提交；成功后发布，失败锁存，既有只读视图保持。取消只在操作接受前拒绝；接受后完成真实确认，Driver随后按原安全位置处理取消。关闭等待已接受操作，不以超时伪装未写。

## Acceptance
B1 公开World async journal/Owner：等待时旧Current/日志/版本可读，新候选不可见；同Owner同步和异步写入均拒绝重入；成功后才发布；失败（含原提交后ack丢失）锁存且不重试。
B2 Kernel/Driver：一次观察的Evidence确认后继续await World relevance/reconcile；等待时零咨询/投递/重拉，第二Drive/Process拒绝重入。八类World写入口、权限冲突、动作后验证、contract demand及两个正式Host有真实await接线。
B3 文件：真实异步frame写/flush及head写/flush后才确认，正常关闭等待；旧v1与新v2读取/字段值顺序保持。不把单逻辑操作一条frame当作额外合批收益，原非掉电故障范围不变。
B4 取消前不入证/不写；已接受World等待期间取消不提前返回或投递。真实非terminal Driver进程在World文件提交后、Owner获ack前Kill，两个新reader读取原增量历史并保持来源SHA。
B5 当前dirty入口固定；前片382封存项、非本片构建输入不变；相关回归、旧失败入口对照、独立双轴review和四元组通过。本片完成后父A1–A4仍未全域完成。

## Tests / Plan
测试沿已授权的公开World journal/Owner、UniKernel/Driver、正式文件adapter与历史reader，不新增私有setter或绕Owner造状态。依次：延迟确认公开RED→共享异步Owner/文件实现→Kernel/Driver/Host接线→失败/取消/旧读取与实际Kill→候选固定→双轴review→独立验证/封存。原同步调用者兼容，全部初始化与其他领域单列后续。

## Verification / Result
level: DETERMINISTIC + SCENARIO
method: 公开Owner/Kernel/Driver延迟确认、原生文件adapter、真实非terminal Driver提交后ack前Kill及两个新reader；16候选SHA、649入口输入、精确入口旧失败对照、独立双轴review和verify。
expected: B1–B5，父A1–A4保持未完成。
actual: 新Kernel10/10、Host4/4；完整Kernel1205/1208、相关Host147/148、DSH24/24，同4失败在入口649输入源码复现。实际Kill时Owner seq2/源3条/发布revision0，两个新reader读回原3操作及1历史修订并保持来源SHA。640非本片输入/前片382封存不变，Standards/Spec各0，verify通过。v2增量场景字节仍7,951,934→1,107,547；不声称本片有额外合批或全部初始化异步。
evidence: `evidence/slice2/README.md`、`test-summary.json`、`review-summary.json`、`verification.json`、`final-artifacts.json`（均在slice2）。下一工程动作：Run journal/Owner→Kernel/Driver原生async。
