#!/usr/bin/env python3
# coding: utf-8
"""Reparse live inputs and audit traceability. No product tests or PASS migration."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
GROUPS = [
    (1, 8, "G1", "任务操作/阶段", "D11-D14", "操作目标/期望明确；前后handler不替代独立验证", "K3,K7"),
    (9, 24, "G2", "Evidence/World输入", "D2,D3,D13", "观察缺失/冲突/迟到保留原因；补证前不猜测匹配和检查成功", "K4"),
    (25, 36, "G3", "当前目标绑定", "D11,D13,D16", "目标唯一、当前且与观察关联；前置后漂移需重检，不继承旧坐标", "K2,K3,K4"),
    (37, 50, "G4", "原安全与Run边界", "D6,D12,D15", "handler不授权；逐动作有效授权/范围/预算/gate执法", "K6"),
    (51, 62, "G5", "在途对账/执行可靠性", "D6,D13", "投递/验证/handler位置分别对账；未知结果不盲重发，内存暂停不等于恢复编排", "K3,K5"),
    (63, 74, "G6", "Assurance/终局证明", "D7,D8,D15", "handler返回、遍历结束、receipt与Goal满足分别判断", "K8"),
    (75, 88, "G7", "World身份/任务去重", "D1-D3,D11", "沿用World有依据身份，去重仍须区分任务、上下文及剩余深度", "K2"),
    (89, 98, "G8", "Evidence/World演化", "D2,D3,D13", "历史/更正/冲突不复活旧目标；相关变化须使处理结果重新评估", "K2,K4"),
    (99, 108, "G9", "Traversal发现/暂停", "D4-D8,D12", "完整发现、深度、返回、进度保留、预算与结束缺口可核对", "K1,K2,K5,K8"),
    (109, 116, "G10", "Run生命周期", "D4-D6", "只在合法原Run内推进；查询和handler不激活第二Run或复活终局", "K3,K6"),
    (117, 122, "G11", "独立Memory/治理", "D2,D3,D6", "Memory只为prior；跨session和开发治理不能由菜单遍历购买", "K7"),
    (123, 130, "G12", "来源完整性与权限", "D3,D12,D15", "页面文字/Finding不授权；完整性检测仍依赖独立能力", "K4,K6"),
    (131, 150, "G13", "交叉转移验证", "D6,D8,D13-D16", "保留事件先后、共享状态、原Owner义务；单轴通过不能推出组合通过", "K2,K3,K5,K8"),
]

# Explicit per-library derivations. Left side is semantic composition, not new wire types.
LIBRARY = {
    "SCN-BARRIER-001": ("有序交互A/验证A/交互B/验证B；各act可挂检查", "A验证前B零投递，后置handler不能提前；两次效果由原Goal证明", "K3,K5"),
    "SCN-BARRIER-002": ("交互A后缺中间证据，停留等待/受控停止", "不跑A成功后置、不投递B；等待和终局按原Owner处理", "K3,K6"),
    "SCN-BARRIER-003": ("交互A后世界与期望矛盾", "A不被handler返回覆盖为成功，B不投递，缺口/再决策保留", "K3,K8"),
    "SCN-PERC-001": ("观察目标/交互/新观察/原Goal评估", "感知事实进入World后再匹配；文本检查不替代Goal", "K4,K8"),
    "SCN-PERC-002": ("Fast错误→补充证据→World更正→重新绑定", "检查结果与旧观察关联，不把旧通过迁移到新目标", "K2,K4"),
    "SCN-PERC-003": ("缺少纠正证据，规则/检查未知", "不因唯一候选或handler存在而点击；原决策失败出口保留", "K4,K6"),
    "SCN-PERC-004": ("无关/重复观察不制造新工作", "去重由Evidence/World保证，handler记录不能靠新ID无限重查", "K2,K4"),
    "SCN-PERC-005": ("依据冲突→World修订→重评规则和检查", "冲突与Unknown保留；不得选择后项假装已知", "K2,K4"),
    "SCN-PERC-006": ("迟到更正→World演化→按当前依据续行", "旧handler结果保留来源，相关变化使继续条件重评", "K2,K4"),
    "SCN-PERC-007": ("完成/失败/安全停止/证据不足分别处理", "Traversal Result不是第五种统一成功，也不代替原终局证据", "K8"),
    "SCN-PERC-008": ("仍错误或冲突→未知/暂停", "不确定目标零点击；不将Finding当事实纠正器", "K4,K6"),
    "SCN-POLICY-001": ("目标态已满足→只读核对→零交互", "无需为挂handler制造点击；原Goal仍独立证明", "K3,K7,K8"),
    "SCN-POLICY-002": ("复用目标态策略，24→23→22→20，每次动作独立验证", "同按钮可再次操作，visited不禁止必要重复；每个act有自己的检查输入", "K2,K3,K7"),
    "SCN-POLICY-003": ("目标态规则域外19→无匹配→暂停/再咨询", "无规则不自动生成动作；不同于选择fallback完成目标", "K7,K8"),
    "SCN-POLICY-004": ("局部应用预算耗尽→保留消耗和缺口", "1次后不再动作，不因修订重置预算", "K6,K7"),
    "SCN-POLICY-005": ("连续无变化→既有停滞守卫→停止推进", "no-progress是失败/缺口，不是发现穷尽或目标已满足", "K1,K7"),
    "SCN-POLICY-006": ("守卫冲突Unknown→零新投递", "不能当false跳到其他操作；检查Unknown按声明继续条件处置", "K4,K6"),
    "SCN-POLICY-007": ("中途验证失败→暂停原执行/再决策", "成功后置handler不执行；旧Effect不因修订被重放", "K3,K5"),
    "SCN-POLICY-008": ("两次动作间identity/lease漂移→绑定失效", "前置通过不沿用旧身份；继续前重检，零新Effect", "K2,K3"),
    "SCN-POLICY-009": ("采纳前拒绝越权EffectClass", "handler引用不能绕过既有拒绝，不能给越权能力换包装", "K6"),
    "SCN-POLICY-010": ("采纳前拒绝超过Run授权的策略工作额", "新handler工作也需计入相应资源，不从计划参数获额", "K6"),
    "SCN-POLICY-011": ("拒绝未知AST/操作/谓词", "不把任意operation字符串反射成调用；typed接口有限声明", "K4,K6"),
    "SCN-WIFI-001": ("未达目标→操作switch→独立验证→后置检查", "点击授权与元素语言检查分别执法，Goal不由handler宣布", "K3,K4,K8"),
    "SCN-WIFI-002": ("当前已on→只读检查，零Effect", "前/后检查挂在只读处理也可工作，不能多切一次", "K3,K7"),
    "SCN-WIFI-003": ("重复激活→沿原Run基数拒绝", "handler/查询不能启动第二个执行链", "K3,K6"),
    "SCN-WIFI-004": ("投递后缺stimulus→等待新观察", "未验证不推进成功后置handler，也不重发switch点击", "K3,K5,K6"),
    "SCN-WIFI-005": ("等待时取消→原SafeStop/在途对账", "取消不倒转已发生Effect；后续handler不得自动续行", "K3,K5"),
    "SCN-WIFI-006": ("与001模板同义的新输入路径", "生成来源不等于运行证明；必须用新机制真实路径重新验证", "K3,K8"),
}

INTERACTIONS = {
    "A1": ("Traversal规则选择导航/只读/标记；各操作挂元素检查", "CONDITIONAL_MAPPING", "发现末端、共享入口、返回身份、深度和K1-K6具体契约"),
    "A2": ("既有目标态策略＋重复减号操作及其检查", "COMPOSITION_REQUIRED", "visited不能取代目标态循环；温度反馈与滞后判据"),
    "A3": ("有序任务阶段＋地图/输入/出发操作", "DOMAIN_ADAPTER_REQUIRED", "地图、键盘和出发语义非菜单DFS提供"),
    "A4": ("目标态或定次物理输入＋反馈检查", "DOMAIN_ADAPTER_REQUIRED", "物理按键能力和音量条可验证状态"),
    "A5": ("主题操作＋双屏关联的联合后验", "DOMAIN_ADAPTER_REQUIRED", "不能把两份独立handler通过直接当同一时刻双屏一致"),
    "A6": ("禁用项只读处理＋状态检查handler", "CONDITIONAL_MAPPING", "驾驶上下文需依据；负向点击另需明确授权"),
    "B1": ("期望态处理；已满足只读，否则交互后验证", "COMPOSITION_REQUIRED", "不得为挂语言检查点击已on开关"),
    "B2": ("动态通知集合＋明确授权的清除操作/检查", "COMPOSITION_REQUIRED", "清除会改变集合；不可清除项须列缺口"),
    "B3": ("搜索→目标消歧→输入→发送的阶段计划", "DOMAIN_ADAPTER_REQUIRED", "发送授权和目标身份独立，搜索不继承发送许可"),
    "B4": ("按依赖顺序处理字段，各输入绑定前后检查", "COMPOSITION_REQUIRED", "字段依赖/敏感输入/错误反馈不能靠UI兄弟顺序决定"),
    "B5": ("验证码识别/语义决策→明确续行", "DOMAIN_ADAPTER_REQUIRED", "缺能力时暂停不算登录成功；无具体适配不可计覆盖"),
    "B6": ("列表发现＋目标匹配；命中后返回证据", "CONDITIONAL_MAPPING", "无限列表未找到不等于不存在；购买不在查找授权内"),
    "B7": ("升级操作→外部等待→重启后重建/验证", "BEYOND_LIVE_RESUME_SCOPE", "同一存活执行暂停不保证跨进程恢复"),
    "C1": ("目标态重复按物理按钮＋灯色反馈", "CROSS_DOMAIN", "机械域动作/观察适配未证"),
    "C2": ("按物体集合组合抓取/放置及反馈", "CROSS_DOMAIN", "物体身份、抓取结果、物理安全适配未证"),
    "C3": ("有界领域反馈控制调用，异常再决策", "CROSS_DOMAIN", "连续力控需要具体策略，不能塞入菜单遍历证明"),
    "C4": ("抓空→新观察/新绑定→修订后续行", "CROSS_DOMAIN", "旧物体位置失效、在途结果和机械适配未证"),
    "C5": ("有序采样点处理＋每点校验", "CROSS_DOMAIN", "标定/坐标系/可达性及反馈适配未证"),
}

H_CASES = [
    ("H01", "普通规则遮蔽危险节点规则", "普通项先T，危险项也T→只选首项", "安全gate仍独立拒绝不合规动作；规则选中不授权", "K6"),
    ("H02", "前项Unknown、后项导航True", "首项可能匹配→不能直接选后项", "保留未知，按约定暂停/报告缺口，不当false", "K4"),
    ("H03", "单元素语言检查范围不足", "选A但能力只给整capture汇总通过", "只有关联到A及字段覆盖的结果才能声称A检查完成", "K4"),
    ("H04", "前置通过后目标漂移", "检查A→弹窗/列表更新→旧坐标", "重新绑定并走原安全链；旧检查不能授权新对象", "K2,K3"),
    ("H05", "动作未验证便观察提交", "任一新capture提交→误跑后置handler", "先证明该操作完成及验证，不将post-commit等同AfterVerified", "K3"),
    ("H06", "原元素操作后消失", "点击A→新页无A→后置仍引用A", "输入缺失保持未知；显式结果目标不能悄悄替换原目标", "K3,K4"),
    ("H07", "后置不满足后的恢复", "点击已验证→检查失败→暂停→恢复", "从可核对的handler位置继续，已投递act不从头重放", "K5"),
    ("H08", "同一共享页不同剩余深度", "root→A→C先到深度2；root→C再到深度1", "保留浅路径带来的展开机会，否则漏D", "K2"),
    ("H09", "相同页不同入口检查义务", "入口X/Y都到同页，只visited页", "入口文字/导航边义务单独计，不能只检查目标页", "K2,K8"),
    ("H10", "虚拟列表重复末屏", "连续无新项但后续还可加载", "no-progress不能自动作穷尽证据，报告未穷尽", "K1"),
    ("H11", "已查状态后来改变", "switch只读检查→外部用户改态→再进入", "相关版本/状态改变使结果重评，永久visited无效", "K2"),
    ("H12", "可选能力缺失与必需能力失效", "可选无绑定/运行中必需失联", "前者显式跳过并报告；后者暂停，不自动替换", "K4,K6"),
    ("H13", "只读节点挂检查", "读switch→检查文字/记录Checked", "零点击；缺Checked不补false；记录覆盖范围明确", "K3,K4"),
    ("H14", "handler隐藏Effect或卡住", "前置handler调用tap/永久等待", "有Effect走原链；全部工作受预算/超时，不能绕过", "K6"),
    ("H15", "暂停在途并遇取消/撤销", "已投递→结果未知→取消→尝试续行", "先对账，取消不倒转已发生动作，不自动重发/续行", "K3,K5"),
    ("H16", "全部已知项完成但还有禁入分支", "frontier空＋回首页＋handler全过", "只报告限定范围成果与未探索，不单独证明全Goal", "K1,K8"),
]

FIELD_REQUIREMENTS = {
    "OccurrenceRef": "handler目标/观察精确关联；跨capture重定位，不作永久去重键",
    "ParentOccurrenceRef": "本次capture内层级；不能当页面导航或稳定父路径",
    "WindowOccurrenceRef": "处理项与窗口作用域关联；缺能力不假造单窗口",
    "SiblingOrder": "本屏枚举线索；不等于稳定任务顺序/身份",
    "DrawingOrder": "遮挡和次序线索；缺失不猜可点击",
    "Class": "规则类型线索；不单独判定导航/安全",
    "ResourceId": "关联线索；跨revision连续性仍由World提供",
    "Package": "任务/应用/窗口作用域校验；不授权操作",
    "Text": "元素语言handler输入；文字覆盖不等于全像素文字，页面文字不授权",
    "ContentDescription": "可声明文本检查输入，保留来源及敏感处理",
    "Hint": "可选文本输入；Unsupported不宣称已检查",
    "Checkable": "只读/可检查属性；不推出允许切换",
    "Checked": "只读记录/期望态结果；缺失/partial不补false",
    "Enabled": "当前操作前提/只读检查；未启用不自动测试点击",
    "Selected": "选择状态检查；不与Checked互换",
    "Focused": "输入/窗口上下文；变化后需重评操作目标",
    "Scrollable": "发现动作线索；不构成末端/穷尽证明",
    "Clickable": "交互线索；不推导navigate或safe",
    "Focusable": "聚焦线索；不证明输入driver或提交已支持",
    "VisibleToUser": "当前可见性依据；未知不推Absent",
    "Password": "沿已有敏感脱敏规则处理，不为语言检查泄露原值",
    "Bounds": "当前坐标框架定位；检查后漂移须重绑，不当身份",
    "Title": "窗口语义线索；标题不是唯一身份/授权",
    "Visible": "窗口当前可见性；不证明所有控件可操作",
}


def build():
    sr_path = "docs/design/scenario-reasoning-v0.1.md"
    interaction_path = "docs/design/decision-granularity-scenarios-v0.1.md"
    ui_path = "src/UniClaw.Kernel/Perception/UiHierarchy/UiHierarchyObservation.cs"
    sources = {sr_path, interaction_path, ui_path,
        "src/UniClaw.Kernel/Perception/UiHierarchy/ObservedValue.cs", "CONTEXT.md",
        "changes/RUNTIME-PLAN-002/flow-contract.md", "changes/RUNTIME-PLAN-002/protocol-sketch.md",
        "docs/architecture/product-architecture-baseline-l0-l3.md",
        "docs/adr/0053-composable-product-plan-with-bounded-local-execution.md",
        "src/UniClaw.Kernel/Runtime/RunDriverInputs.cs", "src/UniClaw.Kernel/Runtime/KernelRunDriver.cs",
        "src/UniClaw.Kernel/Capability/LanguageInspection.cs",
        "src/UniClaw.Host/Capability/LanguageInspectionPostCommit.cs",
        "src/UniClaw.Host/Capability/LanguageInspectionBinding.cs", "scenarios/README.md"}
    rows = re.findall(r"^\| (SR-\d{3}) \| ([^|]+) \| ([^|]+) \| ([^|]+) \|",
                      (ROOT / sr_path).read_text(), re.M)
    assert [r[0] for r in rows] == [f"SR-{n:03}" for n in range(1, 151)]
    overrides = {
        1: "目标态操作不是按元素只处理一次；先看是否已满足",
        4: "发现完备和末端依据尚需typed契约；无新项不等于穷尽",
        5: "输入能力/敏感数据/输入后验证独立适配，tap不购买输入",
        6: "返回动作与返回位置验证均有真实能力；不存在逆边不能假定可回",
        7: "零交互只读处理也可挂handler，不得额外点击",
        46: "用户抢占检测和已投递结果对账，不假定暂停撤销物理动作",
        53: "恢复journal与handler位置须共同对账；本轮不承诺跨进程恢复",
        60: "同机重新打开进程不等于同一存活Traversal暂停恢复",
        64: "receipt成功不证明世界改变，后置检查通过也不代替Goal",
        73: "无剩余已知工作不证明已知图就是完整世界",
        81: "文件世界需实际观察/动作/验证适配；不计Android UI支持",
        82: "机器人地图/连续反馈不能仅由菜单节点抽象购买",
        87: "源码中的World连续性优先；不能把旧压力描述签名变化即新身份当规范",
        99: "新页由既有规则机械展开；累计事实/独立进度/发现覆盖分别有依据",
        100: "停滞与穷尽分开：no-progress可导致暂停，但非全覆盖证据",
        107: "同实例恢复仍重检操作attempt与handler位置，不从整个act开头重发",
        108: "跨进程续跑是本轮外恢复适配，不由D6推出",
        117: "记忆是prior，不构成当前handler输入事实或权限",
        118: "历史引用不能建立current claim或当前可操作目标",
        119: "跨session目标延续需独立授权/恢复/基数契约",
        120: "开发Harness学习流程，不是UI任务执行适配",
        121: "跨Run经验独立适配，不用永久visited跳过新任务检查",
        122: "ADR治理流程，不是UI任务执行适配",
        136: "弹窗与预算同时触发需组合轨迹；先检查权限/在途而非盲目关闭",
        137: "抢占在途不等于撤回已发生Effect；恢复先对账",
        141: "撤销后禁止新投递，已有在途结果保留不能回滚历史",
        149: "新观察改变处理前提时重评规则/handler结果，不永久visited",
        150: "Goal不可验证时保持未证明；handler完成不能补足缺失判据",
    }
    reasoning = []
    for sid, title, axes, old_anchor in rows:
        n = int(sid[3:])
        _, _, group, position, decisions, obligation, gaps = next(g for g in GROUPS if g[0] <= n <= g[1])
        scope = ("NON_UI_GOVERNANCE" if n in (120, 122) else
                 "CROSS_DOMAIN" if n in (81, 82) else
                 "RECOVERY_DEPENDENCY" if n in (53, 54, 59, 60, 108, 135, 145) else
                 "MEMORY_DEPENDENCY" if n in (117, 118, 119, 121) else "CONDITIONAL_OBLIGATION")
        reasoning.append(dict(id=sid, title=title.strip(), axes=axes.strip(), group=group,
            position=position, decisions=decisions, obligation=overrides.get(n, obligation),
            gaps=gaps, scope=scope, historical_anchor_not_pass=old_anchor.strip(),
            implementation="NOT_APPLICABLE" if scope == "NON_UI_GOVERNANCE" else "UNVERIFIED"))
    library = []
    for p in sorted((ROOT / "scenarios").glob("SCN-*.json")):
        d = json.loads(p.read_text()); sid = d["id"]; assert sid in LIBRARY
        sources.add(str(p.relative_to(ROOT)))
        shape, obligation, gaps = LIBRARY[sid]
        library.append(dict(id=sid, name=d["name"], sr_ref=d.get("srRef"),
            composition=shape, obligation=obligation, gaps=gaps,
            carrier=d["execution"]["kind"], original_expectations=d.get("expectations"),
            original_certification=d.get("certification"),
            original_status_not_pass=d.get("status"), implementation="UNVERIFIED"))
    assert len(library) == 28 and {r["id"] for r in library} == set(LIBRARY)
    assert all(r["sr_ref"] is None or r["sr_ref"] in {x[0] for x in rows} for r in library)
    irows = re.findall(r"^\| ([ABC]\d) \| ([^|]+) \|", (ROOT / interaction_path).read_text(), re.M)
    assert len(irows) == 18 and {r[0] for r in irows} == set(INTERACTIONS)
    interactions = [dict(id=sid, title=title.strip(), composition=INTERACTIONS[sid][0],
        scope=INTERACTIONS[sid][1], obligation=INTERACTIONS[sid][2], implementation="UNVERIFIED")
        for sid, title in irows]
    ui_fields = []; ui_text = (ROOT / ui_path).read_text()
    for record, count in [("UiNodeObservation", 22), ("UiWindowOccurrence", 7)]:
        body = re.search(r"public sealed record " + record + r"\((.*?)\);", ui_text, re.S).group(1)
        fields = [part.strip().split("=")[0].strip().split()[-1] for part in body.split(",")]
        assert len(fields) == len(set(fields)) == count
        for field in fields:
            ui_fields.append(dict(record=record, field=field, obligation=FIELD_REQUIREMENTS[field],
                                  implementation="UNVERIFIED"))
    cases = [dict(id=sid, pressure=pressure, trace=trace, required=required, gaps=gaps,
                  nature="AUTHORED_COUNTEREXAMPLE_NOT_PRODUCT_EXECUTION")
             for sid, pressure, trace, required, gaps in H_CASES]
    summary = dict(reasoning=150, library=28, golden=sum(r["carrier"] == "golden-bundle" for r in library),
        descriptive=sum(r["carrier"] == "none" for r in library), interactions=18,
        node_fields=22, window_fields=7, new_handler_cases=len(cases),
        reasoning_scopes=dict(sorted(Counter(r["scope"] for r in reasoning).items())),
        interaction_scopes=dict(sorted(Counter(r["scope"] for r in interactions).items())),
        omitted_input_ids=0, product_scenarios_executed=0, support_rate="NOT_COMPUTED")
    return dict(kind="CURRENT_SOURCE_TRACEABILITY_AND_DERIVATION_ONLY", baseline="D1-D16",
        sources={p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in sorted(sources)},
        reasoning=reasoning, library=library, interactions=interactions, ui_fields=ui_fields,
        handler_cases=cases, summary=summary, implementation_verdict="NOT_PROVEN")


def table(headers, rows):
    def clean(value):
        return str(value).replace("|", "\\|").replace("\n", " ")
    return "\n".join(["| " + " | ".join(headers) + " |", "|" + "|".join("---" for _ in headers) + "|"] +
                     ["| " + " | ".join(clean(v) for v in row) + " |" for row in rows])


def matrix(d):
    sections = ["# D1-D16机制：重新推导的逐项矩阵", "",
        "> 当前源重新解析；只是归属、条件和反例义务。UNVERIFIED不等于已支持，拒绝/暂停也不算任务成功。旧passing和锚点没有转为本次PASS。", "",
        "## 28条场景库：逐条处理与原期望", "",
        table(["ID", "新机制组合", "必须保持的行为/缺口", "原载体", "原效果数/终态分类", "实现"],
            [(r["id"], r["composition"], r["obligation"] + "；" + r["gaps"], r["carrier"],
              str(r["original_expectations"].get("effects")) + "/" + str(r["original_expectations"].get("classification")), r["implementation"])
             for r in d["library"]]), "",
        "原expectations、咨询次数和certification完整保存在inventory；没有执行新机制，不主张保持旧成本。", "",
        "## 150条SR：当前机制中的义务", "",
        table(["ID", "场景", "处理位置/决定", "推导义务", "范围/缺口", "实现"],
            [(r["id"], r["title"], r["position"] + "/" + r["decisions"], r["obligation"],
              r["scope"] + "/" + r["gaps"], r["implementation"]) for r in d["reasoning"]]), "",
        "## 18个交互样例：任务形状与适配边界", "",
        table(["ID", "需求", "新机制组合", "范围", "尚需证明"],
            [(r["id"], r["title"], r["composition"], r["scope"], r["obligation"]) for r in d["interactions"]]), "",
        "## UI逐字段：22节点＋7窗口", "",
        table(["记录", "字段", "新机制输入义务"],
            [(r["record"], r["field"], r["obligation"]) for r in d["ui_fields"]]), "",
        "## 16个新反例：专门检验handlers机制", "",
        table(["ID", "压力", "轨迹", "所需行为", "缺口"],
            [(r["id"], r["pressure"], r["trace"], r["required"], r["gaps"]) for r in d["handler_cases"]]), "",
        "以上清单相互重叠，不合并成支持率。H场景是作者构造的反例需求，不是产品已执行测试。", ""]
    return "\n".join(sections)


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--check", action="store_true")
    args = parser.parse_args(); d = build()
    outputs = {"inventory.json": json.dumps(d, ensure_ascii=False, indent=2) + "\n", "scenario-matrix.md": matrix(d)}
    for name, value in outputs.items():
        if args.check:
            assert (HERE / name).read_text() == value, f"stale input or mapping: {name}"
        else:
            (HERE / name).write_text(value)
    print(json.dumps(dict(mode="check" if args.check else "generate", **d["summary"],
                         verdict=d["implementation_verdict"]), ensure_ascii=False))


if __name__ == "__main__":
    main()
