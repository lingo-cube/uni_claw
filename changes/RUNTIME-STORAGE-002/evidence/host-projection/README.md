# Host查询摘要资格与完整处置

## 保存责任与结果
O5的authority为Runtime Host。RuntimeRunStore的准入身份/幂等键、attach后HostSessionRef、admission/attach失败原因以及Host终态并非都可从Kernel重建，因此按核心用途保存。字段来源表见host-projection-contract.md。本片完成这项原资格缺口，不再遗留“Host字段待核定”。

原代码坏snapshot被Get/Find视作不存在，同一launch/幂等请求会铸造第二Run；事件PayloadAvailable=false也不能还原完整snapshot。现有原JSON DTO/API和物理单参数构造保持，新增内部v2确认摘要+不可变revision文件，首基线和原event/可逆JSON变化绑定来源、long版本和hash。每Run最新小摘要仍完整保存，latest只核对应确认版，不展开旧数据；正反数据历史不取得执行/恢复权限。原事件分页从已确认版本读取；旧v1源可读，活动旧Run更新时以旧latest建立显式保留起点，之前未保存的数据版本返回不可用，原events前缀不重写。

同一准入链的ActivationRecordStore也补齐先append后发布三个索引、关闭/故障拒写和共锁关闭，原CanonicalKey、冲突、终局与JSON行保持。产品根仍由构造root组合snapshot/revisions；新字节限额overload默认16MiB，参数显式注入，不读Harness资产JSON。

## 四元组
- method：原公开Get/Create损坏来源对照与Activation关闭后Reserve/Finalize/Fail先真实5/5 RED；新版本/legacy/坏链/未确认suffix/限额/拒绝callback/非空JSON上下文/long.MaxValue边界，复用原Identity/Activation/RunStore用例；独立双轴及意见复核。
- expected：坏核心来源显式不可用且不能重铸同幂等Run；版本还原所有原字段，终局Outcome不因artifact更新改变；旧源、事件分页和保留起点可判定；越界写前拒绝；无Agent/Effect调用。
- actual：第一次行为RED构建被未完成的worker草稿阻断，单独保留、不冒充运行RED；保存草稿后用精确入口实现执行5/5 RED，观察到重复第二Run及仅内存activation。中间编译/编码失败与修复均保留，最后同一共享源码30/30；独立Spec发现修订溢出与四个JSON上下文覆盖，checked计数+边界/非空上下文补齐后，独立真实源组件32/32。两轴最终增量各0。原Host全项目复验曾被并行Coverage.Paths编译阻断，记录host-review-final.json；不是Host行为失败，也不算最终联合构建通过。
- evidence：host-baseline-red.json；host-final-v2.json；host-pure-final.json及外置source-binding.json；candidate-v2.json/candidate-v2.patch；review-spec.md（原2项P1）+review-spec-delta.md（0）；review-standards.md+review-standards-delta.md（0）。所有候选均按captured entry审阅，非整个dirty HEAD。

真实源组件直接编译同一7个生产模块及6个公开测试文件，未使用替身实现，不依赖施工中的Coverage。32个结果的源码绑定和TRX均保留；它证明Host组件，不替代最后真实Host/DSH集成验收。原candidate.json/patch、原审查及入口不改写，最终delta另存。

## 编码与限制
投影JSON先经紧凑节点规范化，再生成/应用hash及delta，避免DateTimeOffset的+号词法差异；revision文件紧凑落盘，避免JsonElement数组被pretty后原始文本hash不等。最新小摘要照旧写完整值，新增历史与确认信息有额外存储/IO成本，本家族不宣称压缩收益或峰值/吞吐提升。历史读取覆盖声明保留链，latest不是旧前缀完整认证；不支持旧源不存在的历史、自动续跑、跨Owner事务或自动删历史。OS/掉电及跨节点保证未由本片证明。

## 资产与收尾
每轮独立AssetRun保存原始日志/计数/必要TRX，源码与配置绑定；新测试独占目录均清理。纯组件编译产物在外置Run中作为最终候选绑定证据保留供联合发布复核，不进入Git。最早未完成worker草稿已保存最小两个文件并恢复；不覆盖他人材料或旧封存源。工作区仍由私有index隔离准备提交，真实index与其他任务未暂存项保持。用户已于本轮授权验收后联合提交推送，最后由Leader执行，不改写过去“当时未提交”的事实。
