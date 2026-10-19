# 真实Settings对照预注册（运行结果前保存）

同一正式产品模型绑定、专用3081、注册p26_pixel克隆；每组重新打开Settings根页。共用SettingsCoverageRunner的当前事实guard、完整Kernel动作链、观察/验证及Owner账本。Slow关闭且逐组一致；任何实际Slow调用必须另计，不以关闭必需观察制造优势。预热与正式运行分别记录。

两类代表段：known-path为Network & internet进入/观察/返回；candidates为当前根页两个允许入口Network & internet、Connected devices分别进入/观察/返回。后者检验运行时选择多个当前候选，现场不声称出现了仿真C式新增入口，也不声称全树遍历。三组分别Step、ShortPlan、RulePlan，所有决策由相同正式模型实际生成，差别仅是要求采用的规划形式。模型仍可申报未知/失败，不由测试替它补Plan。

先运行Step基线并保存baseline-metrics.json，再写入基于该基线的thresholds.json；保存门槛之前不运行/读取ShortPlan或RulePlan结果。基线未完成时保留失败消耗，先解决同一范围运行前提再建立有效基线，不将截断结果作为低成本参考。

候选代表段每组计划重复2次，已知路径每组至少1次；记录样本量和差异，只作探索性工程验证。对照目标：完成相同范围且不减少逐动作验证，完整决策调用少于有效Step基线；token/时延/可得费用按实际基线标定并独立判断，未改善如实保留。无货币账单则费用写不可得，不从过时价目表推算。所有失败/纠正/重规划运行都纳入全试验账本。

每次trial.json记录完整咨询数、总时延、完成/未知/受阻、验证动作数与会话ID；使用collect_metrics.py只读取该会话的provider usage、assistant/message及无消息attempt，统计全部模型尝试（含未成功者）、输入/输出/cache/total token。缺失usage单独计数并标记统计不完整，不能写成零消耗；不复制会话正文/凭据。现场首次比较只证明本范围，不证明性能普遍或统计显著，也不替代SETTINGS-COVERAGE-005全遍历验收。

有效样本同时要求Runtime Completed、Owner范围CoverageComplete、所有预定动作独立验证/归属，以及实际提案形态符合组别：Step不能包含Plan或Policy；ShortPlan包含具体Plan且不含ForEach/Policy；RulePlan实际包含ForEach且不含Policy。形态不符、超范围或失败样本保留消耗，但不进入有效基线和收益通过数。

基线准备期补充：known-path/Step/1因当前页投影与retry关联错误失败；Step/2动作2/2且Owner覆盖完成，但模型以intent ID冒充dispatch receipt锚，被Runtime拒绝完成。两次均保留且不计有效样本。统一咨询提示明确已满足mandatory义务时省略completion、intent不是receipt；后续三种模式共享此语义提示与同一编译产物，仍未运行Plan。

Step/3与4连续两次原始/既有retry均在provider报告工具参数JSON未闭合。正式Prompt/Profile提升至15，hash8829fab7be32596578ce36801f6012e09f7734e95df0e1cefb41433000b528bd，新增完整外层封装示例并已独立静态复核。接下来有效基线及所有Plan组固定此版本和同一DLL/source哈希；旧准备样本仅计失败消耗，不混合epoch建立收益参考。

首轮Plan结果：已知短Plan和规则Plan均功能完成，但均未达调用/token/时延门槛。候选组未继续消耗旧膨胀版本：先修已由现场证实的输入膨胀（完整审计不删，咨询改为实际attempt引用），并明确Source每次body内完成返回。保存comparison-result-uncompressed及原门槛；后续以新二进制条件建立compact独立基线/门槛，仍保留全部旧失败/未改善成本，不将两个epoch混作收益证明。

compact epoch固定Prompt/Profile16（3cdf975686600bb1f12da8f6fdd9b4f6b2a287e0aab96131aec5918821cb3134）、紧凑咨询投影及同一DLL。基线预定known Step/6、candidates Step/3和4；冻结文件为baseline-compact-metrics.json与thresholds-compact.json，冻结脚本按完整二进制/Prompt条件筛选，并拒绝该epoch已有Plan experiment。后续Plan预定known Short/2、Rule/2，candidates Short/1和2、Rule/1和2；所有尝试入账。原thresholds.json和baseline-metrics.json哈希不变。

该compact批次已在候选Short/1无效输出后中止，已知两种Plan调用未改善；不将未运行的剩余用例写成完成。final epoch采用停滞Owner事实修复及Prompt/Profile17，基线为known Step/7、候选Step/5和6。三份功能均有效；候选5有一次失败模型attempt无usage，tokens只能记已报告下界，不能当零消耗。新thresholds-final在本epoch任何Plan前冻结：Plan所有尝试usage完整且累计token/有效完成数不高于基线已报告下界，才可判token不回退；其数学依据为完整Plan总量≤基线下界≤基线真实总量。不能报告基线缺失部分的精确节省比例。已知范围门槛≤2咨询/5模型调用、81458token/20491.8572ms；候选范围≤4咨询/9模型调用、189152.5token下界/35624.8688ms。最终预定known Short/3、Rule/3，候选Short/2和3、Rule/1和2。全部失败、纠正与重规划继续累计，不覆盖两个旧epoch的门槛或样本。

上述final epoch仍未全通过，额外known Short/4成功、候选Short/4形态不符，全部计入原组；不因重跑删除形态不符成本。真实缺失usage已定位并修复到DSH Provider的完整响应校验失败出口，详见protocol/dsh-provider-usage-diagnosis.md。新accounted epoch固定相同Prompt17/模型/目标/权限/必要观察验证，并记录实际provider bundle哈希3c05833a…；harness对两种Plan首次响应明确给出所需kind/items形态，仍由真实模型生成，不能由测试替补。已知Step8失败、Step9成功，候选Step7/8成功，四次usage均完整。thresholds-accounted在该产物任何Plan前保存，基线失败保留于成本分子，按有效完成数摊计token/时延；调用门槛仍严于最小有效Step样本，已知≤2咨询/5模型调用，候选≤4/9。token/时延门槛分别为126488/36252.03ms及188331.5/35257.3563ms；同时报告有效Step本身时延，避免用失败摊销隐藏纯路径结果。预定known Short5/Rule4、候选Short5/6和Rule3/4，六份均运行并保留实际结果；旧三epoch、门槛及未知usage不回填、不覆盖。

accounted六份Plan已完成且全部功能有效、usage完整。known-path ShortPlan因首版把导航入口误写开关状态，Runtime零Effect拒绝后修订，3咨询/7模型，调用门槛失败；其余known Rule及候选Short/Rule三组通过。Owner随后明确授权“更换模型并重跑对照”，不是静默替换原正式模型。绑定更换为同一官方provider已公布catalog的deepseek-v4-pro；仅.dsh/product/uniagent-prod-bindings.yaml新增choice及selected，保留原Flash choice、完整before副本/hash，服务/Prompt17/程序集/权限/观察验证/provider产物/thinking disabled均不变，代理与凭据未改。新pro epoch预定Step known10、候选9/10；有无效尝试则保留并补足1/2有效基线。baseline-pro-metrics与thresholds-pro必须在任何Pro Plan之前写入且不可覆盖。之后预定known Short6/Rule5、候选Short7/8、Rule5/6；全部尝试入账，不引用Flash门槛证明Pro收益，不删除旧失败。记录见model-change-pro/authorization-and-binding.json。

Pro数值门槛冻结发生真实顺序偏差：baseline-pro-metrics在Plan前保存，但脚本按全RunDir字典序误选Flash9而拒绝；编排漏拦exit1，两份known Plan已启动。批次停后未读两份结果，独立评审建议按预先固定公式和不变baseline执行结果盲法late lock，不人为剔除分母。thresholds-pro通过显式provider/model/providerSHA及deviation参数锁定，准确列出两份已启动未读路径、baseline/experiment hashes和lock时间；数值与事先盲算完全一致。结果在锁定后解盲，时间字段与threshold hash记录于registration-deviation.json；该轮不得声称严格满足“任何Plan之前冻结”，只能作为透明的探索性工程对照。两份原始功能/计量资格保持，所有成本纳入；其余四份候选Plan在lock之后启动。

Pro初始known短Plan为2咨询/4调用/48715token/28942.2851ms，唯独时延高于固定Step27745.0817ms；规则2/4/48577/26901.0869ms。因known仅一份样本，结果触发有限复核：在追加任何运行前固定known短7/8与规则6/7共四份，两模式平衡、全部跑完、不按PASS提前停止，原门槛/基线不变，首次慢样本及所有新失败继续全额计入。预定记录fixed-known-path-replication.json；这是探索性扩展，不把首次失败改写为通过，不声称统计显著/稳定普遍收益，追加后不无限重跑追PASS。
