#!/usr/bin/env python3
"""离线追溯清单审计；不执行产品，不从旧 passing/✅ 推导 PlanTree PASS。"""
import argparse
import hashlib
import json
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SR_SOURCE = "docs/design/scenario-reasoning-v0.1.md"
INTERACTION_SOURCE = "docs/design/decision-granularity-scenarios-v0.1.md"
UI_SOURCE = "src/UniClaw.Kernel/Perception/UiHierarchy/UiHierarchyObservation.cs"

# family 是待证责任分解，不是已实现/通过的声明。
GROUPS = [
    (1, 8, "G1", "顺序、遍历、单步与零动作", "任务策略", "动作后验证与目标证明"),
    (9, 24, "G2", "观察缺失、错误与冲突", "共同执行闭环", "新鲜观察、补证或停止，不凭置信度继续"),
    (25, 36, "G3", "目标绑定与歧义", "共同执行闭环", "唯一当前目标与有效依据，否则零新动作"),
    (37, 50, "G4", "权限与安全", "共同执行闭环", "授权与作用域逐动作检查；撤销/抢占另外补证"),
    (51, 62, "G5", "在途、迟到与重启", "执行可靠性", "结果对账；不盲重发；跨进程续行不得冒充已支持"),
    (63, 74, "G6", "终局与目标证明", "任务证明", "计划结束、receipt成功、任务完成分别判定"),
    (75, 88, "G7", "身份、连续性与跨域", "World与领域适配", "revision-local引用与证据支持的连续性"),
    (89, 98, "G8", "证据、历史与更正", "Evidence与World", "保留历史/冲突；不把相邻当因果"),
    (99, 108, "G9", "遍历、预算与恢复", "任务策略与执行可靠性", "有限展开、覆盖缺口、准确续行和安全中断"),
    (109, 116, "G10", "激活与基数", "Run生命周期", "不重复激活、不在终局后复活"),
    (117, 122, "G11", "记忆与开发治理", "单列范围", "不把Memory/开发流程伪装成UI树执行能力"),
    (123, 130, "G12", "对抗与完整性", "共同执行闭环", "页面内容不是授权；完整性与来源信任分别检验"),
    (131, 150, "G13", "交叉组合", "组合验证", "分别覆盖两轴不足以证明组合无竞态，必须有交叉轨迹"),
]

GROUP_SHAPES = {
    "G1": "任务策略节点与动作/观察叶子",
    "G2": "同一任务计划＋共用观察/补证/停止分支",
    "G3": "同一任务计划＋当前目标接地分支",
    "G4": "同一任务计划＋每步安全/范围执法",
    "G5": "同一任务计划＋在途/等待/对账出口",
    "G6": "局部计划结果→既有任务证明链",
    "G7": "Traverse/任务节点＋World身份适配",
    "G8": "Owner事实演化输入，不新增历史控制节点",
    "G9": "Traverse＋预算/中断/续行",
    "G10": "既有Run激活与终态控制，不新增任务节点",
    "G11": "独立Memory/生命周期依赖或非产品治理",
    "G12": "同一任务计划＋完整性/安全拒绝出口",
    "G13": "相关策略与共用执行分支的交叉轨迹",
}

INTERACTIONS = {
    "A1": ("Traverse", "导航/记录/返回", "高层Traverse缺失，需动态结构与安全分类证据"),
    "A2": ("Achieve", "减号/值状态", "已有Policy仿真，不等于真实车机温度适配"),
    "A3": ("Sequence", "导航/输入/提交", "文本输入与地图状态适配未证明"),
    "A4": ("Achieve", "物理按键/音量反馈", "物理输入与量化反馈未证明"),
    "A5": ("Sequence", "双屏状态与联合判据", "窗口字段不等于双屏采集和联合验证已支持"),
    "A6": ("Traverse", "禁用项/驾驶上下文", "检查禁用走观察；负向点击测试需要单独明确授权"),
    "B1": ("Sequence", "导航/期望开关态", "WiFi实证只限原范围，不验证新PlanTree协议"),
    "B2": ("ForEach", "通知集合/清除", "清除有状态影响；单独授权与不可清除项退出待证"),
    "B3": ("Sequence", "搜索/输入/发送", "发送授权、目标消歧和执行适配待证"),
    "B4": ("ForEach", "字段/输入/校验", "字段依赖、错误反馈、敏感值脱敏和提交待证"),
    "B5": ("语义决策后续行", "验证码/未知内容", "不能凭任意策略叶子声称已适配；需具体感知能力"),
    "B6": ("Traverse", "列表发现/筛选", "找到即返回匹配证据；购买不继承搜索授权"),
    "B7": ("Sequence+Await", "升级/等待/重启", "Await不等于跨进程恢复，升级需另行授权"),
    "C1": ("Achieve", "物理按钮/灯状态", "跨域推演，无本轮真实适配证据"),
    "C2": ("ForEach+Sequence", "物体集合/抓放", "跨域推演，动作/反馈适配未证明"),
    "C3": ("专用反馈算法候选", "力反馈/连续控制", "不放进菜单DFS；无具体执行协议不能计覆盖"),
    "C4": ("语义决策后续行", "偏移后重定位", "必须回到新观察和新绑定，跨域适配未证明"),
    "C5": ("ForEach", "采样点/触达/结果", "跨域推演，点位与反馈适配未证明"),
}

FIELD_GROUPS = {
    "OccurrenceRef": "当前目标引用；禁止跨capture复用",
    "ParentOccurrenceRef": "页面内结构特征；不是页面导航边",
    "WindowOccurrenceRef": "窗口作用域；Unsupported不可假设单窗口",
    "SiblingOrder": "枚举顺序特征；不是稳定身份",
    "DrawingOrder": "遮挡/次序依据；缺失保持未知",
    "Class": "类型线索；不单独授权或建立身份",
    "ResourceId": "关联/定位线索；不单独建立跨revision身份",
    "Package": "应用作用域证据",
    "Text": "语义线索；不是授权来源",
    "ContentDescription": "语义线索；敏感内容按策略脱敏",
    "Hint": "输入语义线索；Unsupported保持缺口",
    "Checkable": "可选状态能力；不等于允许切换",
    "Checked": "期望态/记录依据；partial与未知不当false",
    "Enabled": "动作前提；禁用节点仍可能需要记录",
    "Selected": "选择状态；不等同Checked",
    "Focused": "输入焦点与观察前提",
    "Scrollable": "继续发现能力；不能单独证明已穷尽",
    "Clickable": "可点击能力；不能推导navigate或安全",
    "Focusable": "可聚焦能力；不等于输入/提交已支持",
    "VisibleToUser": "可见性依据；Unknown不推Absent",
    "Password": "敏感标志；脱敏与输入授权分别执法",
    "Bounds": "坐标空间内定位依据；不当身份或授权",
    "Title": "窗口语义线索；不能单独建立身份",
    "Visible": "窗口可见性；不等于当前可操作",
}


def build():
    sources = {SR_SOURCE, INTERACTION_SOURCE, UI_SOURCE,
        "src/UniClaw.Kernel/Perception/UiHierarchy/ObservedValue.cs",
        "src/UniClaw.Host/UiHierarchyOccurrenceStrategy.cs",
        "src/UniClaw.Kernel/Runtime/ConsultationTypes.cs",
        "src/UniClaw.Kernel/Runtime/AgentDecision.cs",
        "src/UniClaw.Kernel/Runtime/KernelRunDriver.cs",
        "src/UniClaw.Kernel/Runtime/RunDriverInputs.cs",
        "src/UniClaw.Kernel/Control/ControlPolicy.cs",
        "src/UniClaw.Kernel/Effects/TargetBinding.cs",
        "src/UniClaw.Kernel/World/UiRealization/UiEntityModel.cs",
        "src/UniClaw.Kernel/World/UiRealization/ContainerAssociation.cs",
        "src/UniClaw.Host/SettingsCoverage/SettingsActionPolicy.cs",
        "src/UniClaw.Host/SettingsCoverage/SettingsPlanFacts.cs",
        "src/UniClaw.Host/SettingsPageIdentity.cs",
        "src/UniClaw.Host/SettingsTraversalLiveFeed.cs",
        "src/UniClaw.Kernel/Effects/AdbEffectDriver.cs",
        "product/policy/android-settings-forbidden-actions.json",
        "docs/architecture/product-architecture-baseline-l0-l3.md",
        "docs/adr/0053-composable-product-plan-with-bounded-local-execution.md",
        "evidence/runtime-plan-001/composition-20261018/results.md"}
    text = (ROOT / SR_SOURCE).read_text()
    rows = re.findall(r"^\| (SR-\d{3}) \| ([^|]+) \| ([^|]+) \| ([^|]+) \|", text, re.M)
    assert [r[0] for r in rows] == [f"SR-{n:03}" for n in range(1, 151)]
    reasoning = []
    for sid, title, axes, old_anchor in rows:
        n = int(sid[3:])
        lo, hi, group, family, owner, obligation = next(g for g in GROUPS if g[0] <= n <= g[1])
        overrides = {
            5: "文本输入能力不能由tap推导，需具体执行及验证适配",
            6: "系统返回与工具栏返回需分别验证；当前Android driver无独立PressBack词汇",
            38: "Grant过期：必须按当前授权机制补证，不沿用旧D/✅作为当前结论",
            46: "用户抢占检测及在途对账需实证，树结构不负责解决",
            81: "文件系统跨域反例；需具体文件观察和执行适配，不能计为Android UI支持",
            82: "机器人地图跨域反例；当前无真实第二域适配证据",
            87: "需验证滚动/返回后的身份；已发现Settings返回签名与receipt形状不一致",
            99: "开放遍历必须证明发现与覆盖条件，不能靠ForEach可嵌套推导",
            107: "同实例恢复也要验证位置、事实及在途动作，不能重放已消费动作",
            108: "Plan跨进程恢复超出当前已接受首版承诺，不能以journal存在判通过",
            117: "Memory是独立适配依赖，不是PlanTree节点结构购买的能力",
            118: "历史EvidenceRef不能直接建立current claim",
            119: "跨session延续须按授权和基数规则处理，不由PlanTree扩大范围",
            120: "开发Harness学习流程：非产品UI执行适配，单列不计支持率",
            121: "跨Run记忆影响需独立能力及契约，不计菜单遍历支持",
            122: "ADR治理流程：非产品UI执行适配，单列不计支持率",
            136: "弹窗与预算交叉：必须有组合运行轨迹，不能仅取独立结论合取",
            137: "抢占与在途交叉：不假设halt等于撤销已投递Effect",
            141: "撤销与在途交叉：已投递结果对账，不允许回滚承诺或盲重发",
            144: "Provider失联与恢复交叉：有限等待、位置对账和不能续行出口待证",
        }
        reasoning.append(dict(id=sid, title=title.strip(), axes=axes.strip(),
            historical_anchor=old_anchor.strip(), group=group, family=family, responsibility=owner,
            plan_shape={1: "Achieve(开关期望态)", 2: "Sequence(导航阶段)",
                3: "Traverse(列表发现目标)", 4: "Traverse(发现穷尽)",
                5: "Sequence(聚焦、输入、校验)", 6: "Step(返回并验证)",
                7: "Observe(已满足则零动作)", 8: "Sequence(开App、设置、验证)",
                81: "Traverse(文件域；需具体适配)", 82: "地图域策略未适配",
                120: "非产品UI任务", 122: "非产品UI任务"}.get(n, GROUP_SHAPES[group]),
            proof_obligation=overrides.get(n, obligation),
            plantree_result="NOT_APPLICABLE_GOVERNANCE" if n in (120, 122) else "UNVERIFIED"))
    library = []
    for p in sorted((ROOT / "scenarios").glob("SCN-*.json")):
        rel = str(p.relative_to(ROOT)); sources.add(rel)
        d = json.loads(p.read_text())
        family = ("期望态动作、零动作与生命周期" if "WIFI" in d["id"] else
                  "逐动作屏障" if "BARRIER" in d["id"] else
                  "有界期望态策略及拒绝分支" if "POLICY" in d["id"] else
                  "感知、冲突与终局区别")
        library.append(dict(id=d["id"], name=d["name"], source=rel, sr_ref=d.get("srRef"),
            family=family, execution_kind=d["execution"]["kind"],
            agent_realization=d.get("agentDecisionRealization"),
            goal_evaluation_realization=d.get("goalEvaluationRealization"),
            expectations=d.get("expectations"), declared_status_not_consumed_as_pass=d.get("status"),
            proof_obligation="原carrier与期望保留；增加新PlanTree输入后的同语义轨迹才算新适配验证",
            plantree_result="UNVERIFIED"))
    assert len(library) == 28 and len({x["id"] for x in library}) == 28
    assert all(x["sr_ref"] is None or x["sr_ref"] in {r[0] for r in rows} for x in library)
    interaction_rows = re.findall(r"^\| ([ABC]\d) \| ([^|]+) \|", (ROOT / INTERACTION_SOURCE).read_text(), re.M)
    assert len(interaction_rows) == 18 and {r[0] for r in interaction_rows} == set(INTERACTIONS)
    interactions = [dict(id=sid, title=title.strip(), shape=INTERACTIONS[sid][0],
        required_semantics=INTERACTIONS[sid][1], gap=INTERACTIONS[sid][2], plantree_result="UNVERIFIED")
        for sid, title in interaction_rows]
    ui = []
    ui_text = (ROOT / UI_SOURCE).read_text()
    for record, count in (("UiNodeObservation", 22), ("UiWindowOccurrence", 7)):
        body = re.search(r"public sealed record " + record + r"\((.*?)\);", ui_text, re.S).group(1)
        fields = [p.strip().split("=")[0].strip().split()[-1] for p in body.split(",")]
        assert len(fields) == count and len(set(fields)) == count
        for field in fields:
            assert field in FIELD_GROUPS, field
            ui.append(dict(record=record, field=field, requirement=FIELD_GROUPS[field],
                field_state="Observed/Unknown/Unsupported" if "ObservedValue" in next(
                    part for part in body.split(",") if part.strip().split("=")[0].strip().split()[-1] == field)
                    else "结构/可选特征，不冒充跨revision真值",
                plantree_result="UNVERIFIED"))
    fingerprints = {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in sorted(sources)}
    return dict(schema="plantree-derivation-audit.v1", kind="STATIC_TRACEABILITY_ONLY",
        implementation_verdict="NOT_PROVEN", sources=fingerprints, reasoning=reasoning,
        library=library, interactions=interactions, ui_fields=ui,
        summary=dict(reasoning=len(reasoning), library=len(library),
            golden=sum(x["execution_kind"] == "golden-bundle" for x in library),
            descriptive=sum(x["execution_kind"] == "none" for x in library),
            unique_sr_refs=len({x["sr_ref"] for x in library if x["sr_ref"]}),
            interaction_examples=len(interactions), node_fields=22, window_fields=7,
            new_plantree_executed_scenarios=0, unmapped_input_rows=0))


def matrix(data):
    def table(columns, rows):
        clean = lambda x: str(x).replace("|", "\\|").replace("\n", " ")
        return "\n".join(["| " + " | ".join(columns) + " |",
            "|" + "|".join("---" for _ in columns) + "|"] +
            ["| " + " | ".join(clean(v) for v in row) + " |" for row in rows])
    parts = ["# PlanTree 输入逐项追溯矩阵", "",
        "> 静态分解，不是运行结果。UNVERIFIED 表示尚无新高层 PlanTree 的适配证明；旧文档锚点和 JSON status 不用于判 PASS。", "",
        "## 150 条推理场景", "",
        table(["ID", "场景", "候选形状/处理位置", "责任", "必须证明", "PlanTree结果"],
            [(x["id"], x["title"], x["plan_shape"], x["responsibility"], x["proof_obligation"], x["plantree_result"]) for x in data["reasoning"]]),
        "", "## 28 条场景库条目", "",
        table(["ID", "场景", "载体", "SR引用", "PlanTree结果"],
            [(x["id"], x["name"], x["execution_kind"], x["sr_ref"] or "未标注", x["plantree_result"]) for x in data["library"]]),
        "", "## 18 个交互需求样例（另一个推导视角，不与前两表相加计覆盖率）", "",
        table(["ID", "需求", "候选形状", "适配缺口"],
            [(x["id"], x["title"], x["shape"], x["gap"]) for x in data["interactions"]]),
        "", "## UI 定义逐字段（22 节点字段、7 窗口字段）", "",
        table(["记录", "字段", "计划消费义务", "输入语义"],
            [(x["record"], x["field"], x["requirement"], x["field_state"]) for x in data["ui_fields"]]), ""]
    return "\n".join(parts)


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--check", action="store_true")
    args = parser.parse_args(); data = build()
    outputs = {"inventory.json": json.dumps(data, ensure_ascii=False, indent=2) + "\n",
               "scenario-matrix.md": matrix(data)}
    if args.check:
        for name, expected in outputs.items():
            assert (HERE / name).read_text() == expected, f"stale: {name}"
    else:
        for name, content in outputs.items(): (HERE / name).write_text(content)
    print(json.dumps(dict(mode="check" if args.check else "generate", **data["summary"],
        verdict=data["implementation_verdict"]), ensure_ascii=False))


if __name__ == "__main__": main()
