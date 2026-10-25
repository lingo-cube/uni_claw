# World运行期原生异步：Slice 2证据

## 修改与原行为
原`WorldModel→IWorldJournal→FileWorldJournal`同步写入，使观察处理即使走`DriveAsync`也会阻塞在World保存。现沿`IAsyncWorldJournal→WorldModel.RecordOperationAsync→UniKernel→KernelRunDriver→两个正式Host`贯通真实等待；八类操作共用候选/确认/发布逻辑，等待期间只读旧状态，单Owner重入立即拒绝，失败锁存。文件使用原生异步frame/flush和临时head写/flush后原子替换，异步关闭等待已接受操作。

本片保留v2增量格式、v1只读、原来源/顺序/head/EOF校验；16MiB默认可配编码frame上限，收集与准入排队窗口0。构造初始化、Run/Control/Plan/资源/P25/执行/归档和部分Host产物仍同步，父change保持implementing。单逻辑操作一条frame没有额外合批收益；无掉电耐久性、自动续跑或实时环境验收声明。

## 独立四元组
| Acceptance | method | expected | actual | evidence |
|---|---|---|---|---|
| B1 | 公开World Owner＋延迟/失败journal，成功、失败、ack丢失、取消与需求序列 | 未ack不发布，同步/异步重入均拒绝，失败不重试，接受后取消等待真实结果 | 新Kernel 10例全通过，涵盖B1/B2；八类共享同一记录实现 | `kernel-final.trx`、`candidate-source-v1/src/UniClaw.Kernel/World/` |
| B2 | 公开Kernel/Driver延迟World提交；调用探针只观察异步方法是否及时返回未完成Task | 等待时零咨询/投递/重拉、第二观察/Drive拒绝，contract demand与正式Host等待 | Kernel延迟提交RED超时→GREEN；Driver普通/取消/失败均通过，观察先Evidence后World | `kernel-wait-red.trx`、`kernel-final.trx`、`candidate-v1.patch` |
| B3 | 原生文件公开adapter、旧读取/增量场景、写前限制 | async frame/flush/head确认后返回，原值/顺序不变，过大写入前失败锁存 | 新Host 4例全通过，含B4；128观测/257操作/128修订仍7,951,934→1,107,547字节，减少86.07198% | `host-final.trx`、`host-final/size/size.json` |
| B4 | 非terminal真实Driver子进程，文件提交后Owner获ack前Kill；两个新reader | 等待时Owner仍旧位，重读原记录，源SHA不变，不声称继续执行 | DriverPending=true；Owner seq2、文件3条、已发布revision=0；Kill非0，两个reader各读原3条及1个历史修订，来源SHA相同 | 最终选择`host-final/kill/`，含ready/kill-observed/expected/reader-0/reader-1/source-hashes |
| B5 | 固定16候选、649入口构建输入、旧失败对照、双轴review、独立verify | 不改非本片输入/前片封存；新用例通过，旧失败可归属 | 640非本片输入与前片382项不变；Kernel1205/1208、Host147/148、DSH24/24；同4失败在精确入口649源码复现；两轴0 | `entry.json`、`candidate-verification-binding.json`、`baseline-archive.json`、`test-summary.json`、`review-summary.json`、`verification.json` |

## 最终选择与失败分类
最终回归仅选择`kernel-final.trx`、`host-final.trx`、`dsh-final.trx`，分别绑定候选16个SHA和26个相关程序集SHA。Kernel三个原失败为演进顺序、origin断言、8处旧源码README登记；新增`WorldModel.Async.cs`已登记。Host旧失败是随机OwnerId参与整份JSON比较；`baseline-kernel-three.trx`与`baseline-ownerid.trx`使用全部649输入匹配的入口源码重现，不算本片新增失败。

TDD的`world-owner-red.log`缺async接口、`kernel-wait-red.trx`超时、`file-async-red.log`缺IAsyncDisposable均保留；后续GREEN可追溯。`world-contracts.trx`首次continuity测试漏已有revision前提，修正为公开创建revision后`world-contracts-v2.trx`10/10，不放宽领域前提。入口载体首次准备因系统Python不支持`extractall(filter=...)`失败，未构建；日志保留，随后手工校验普通文件提取成功。`file-first/`是中间Kill导出，最终只引用`host-final/kill/`。

## 复核与复现
`verify.py`核对当前源码/封存/真实Kill/最终TRX及SHA，输出`verification.json`。`reproduce.py <全新空目录>`从归档入口880文件与16个候选快照还原独立构建载体，顺序运行三组同选择回归并导出新的Kill/size；不依赖后续可变工作区，不覆盖原证据。该复现工具已做语法检查；本片原始测试已实跑，未为验证工具本身再跑一轮。入口临时构建载体649输入全部验SHA后归档，已删除该单一临时目录。后续源码变化应使用新候选/新证据，不能覆盖本片封存。

## 后续
Run journal/Owner→Kernel/Driver确认链迁移原生async，保留Effect之前的持久化屏障。全域A1–A4及初始化异步、其余合法批次性能仍待完成。
