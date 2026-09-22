# PER-009 机制冻结图（Mechanism Freeze v1）

> 状态：FROZEN（2026-09-22，用户终审 + 两处修正后冻结；台账 #19）
> 本图是 `state.md` D1–D14 的图示等价物；冲突时以 state.md 文字为准。
> 修正谱系：#16 立项 → #17 语义权威制 → #18 字段表冻结 → #19 本图冻结。

## 七段职责链（总纲）

```text
Observation 负责取证（快/中/慢阶梯，永不越权裁决）
   ↓
P2 负责收证（唯一证据门）
   ↓
WorldModel 负责对质（同 key 印证/冲突）
   ↓
Resolver 负责判现有证据够不够（只裁决/请求补证，不自己看）
   ↓
Control 决定是否值得补证（升档发起权）
   ↓
Effect Gate 决定能不能动（最终硬闸，证据没闭环就不能动）
   ↓
Verifier 判断动作是否真的成功（类别路由验证）
```

## 六条不动摇原则

```text
升档 ≠ confidence 变低；升档 = 当前证据体系无法定案
XML Tier 0 ≠ XML 永远正确；= XML 在自己负责的语义字段上有类别权威
XML 权威失效 ≠ Vision 自动获胜；= 回视觉阶梯继续取证
Deep ≠ 最终真理；Deep 仍是 claim，仍走 P2
Resolver ≠ 感知器；只裁决/请求补证，不自己看
Effect Gate = 最终硬闸；证据没闭环就不能动
```

## 主流程图

```text
══════════════════════════════════════════════════════════════════
① 观察阶梯              【升档=体系无法定案，与 confidence 无关】
══════════════════════════════════════════════════════════════════
  快档（每周期·免费·常开）
  ├─ 截图 → 快视识别 → claims                       [现]
  └─ dump → XML 解析 → claims                       [新]
       状态字段：checked / enabled / selected / focused
       能力字段：checkable / clickable / scrollable / focusable
                 （checkable = checked 有效性前置，≠ 状态）
       checked ∈ {false, true, partial}（api35 布尔，partial 前向兼容）
       身份字段：resource-id / class / package
       失败：瞬时→下周期重试｜未启用→60s×≤3 探测｜降级记 degraded:no-xml
                    ▲ 中档请求(subjects+bounds)       ▲ 慢档请求(D6)
  中档 Focused：裁剪争议区→同一快模型重扫（免费）[新]   │
  慢档 Deep：同一截图→VLM；时间锚=capture；      [后置]─┘
            迟到到达→claim evolution，世界照常纠错
══════════════════════════════════════════════════════════════════
                        ▼ 全部经 P2（唯一证据门，现）
② 合状态（WorldModel，现）：同 key 多源 → 一致=印证 / 不同=Conflict
   碰头点 = 共享三件套（ui.screen / *.state / screen.frame，常量类）
══════════════════════════════════════════════════════════════════
     │无冲突                              │有冲突
     ▼                                   ▼
③ 孤证采信【底牌表】                ④ 裁决器【字段权威制】
   (源×类别)→A/B/C                    同 key 冲突
   级联：包名特例>类别>源默认            ▼
   A：孤证即可动                      字段属于哪类？
   B＋不可逆：升档补佐证 ─────────►  ┌────────────────────────┐
   C：线索→升档补看 ─────────────►  │ 状态权威: checked(∧     │
                                      │  checkable=true)/       │
                                      │  enabled/selected/      │
                                      │  focused                │
                                      │ 语义文本: text(仅控件   │
                                      │  语义，像素文字不裁)     │
                                      │ 非权威域: 颜色/图标/    │
                                      │  canvas/动画/图片/      │
                                      │  像素文字               │
                                      └───────────┬────────────┘
                                          权威域   │   非权威域
                                             ▼     │      ▼
                                  三道门：          │  XML 不参战
                                  IdentityMatched  │      │
                                  (subject→节点    │      ▼
                                   唯一解析)       │  视觉域阶梯
                                  FreshEnough      │  快视能定→定
                                  (同周期∧Δ≤视觉   │  Insufficient/
                                   +余量)          │  Ambiguous
                                  PropertyValid    │   → Focused(免费)
                                  (checkable=true  │  仍悬案∧≥B级∧
                                   等)             │   (不可逆∨跨2周期)
                                     │      │      │   → Deep(D6,后置)
                                    过     不过     │
                                     ▼      ▼      │
                          ┌────────────┐ 剥夺本次权威│
                          │ Tier 0 定案 │ (≠视觉获胜)│
                          │confidence盲 │──────┬────┘
                          │0.55≡0.999  │      │
                          │不升档       │      │
                          │记 overruled │      │
                          │ =vision     │      ▼
                          └─────┬──────┘ 统一销案记录{key,tier,依据}
                                ▼        ·留档不删
══════════════════════════════════════════════════════════════════
⑤ 决策（现+新）：悬案与目标相交 → ReobserveFocused 回①中档
⑥ 授权链（现·不可绕）：Assurance→Effect Gate
   冲突未销=拒；B 级孤证+不可逆=未佐证拒
   ▼ dispatch → adb tap
⑦ 事后验证路由【新·四门收紧】
   目标属性∈XML权威域 ∧ dispatch后重新唯一解析目标节点（防同名错配）
   ∧ PropertyValid ∧ dump 时序在 dispatch 之后
     → XML 验证：dispatch→post-action dump→重新唯一解析→查权威属性→判定
   事后新鲜度=时序约束（操作前 FreshEnough=同周期窗口，两者定义不同）
   不过门 → 截图走视觉验证（现路径）
   ▼
⑧ 终局（现）：Completion｜失败边回环｜悬案不解→SafeStop（诚实终局）
   全程留痕：销案/冲突/演化链可回放
══════════════════════════════════════════════════════════════════
```

## 冻结规则速查

```text
字段表    状态权威=checked(∧checkable=true)/enabled/selected/focused；
          text=语义文本权威；checkable·clickable·scrollable·focusable=
          能力属性；bounds=空间证据；颜色/图标/canvas/动画/像素文字=非权威域
三道门    IdentityMatched(subject→节点唯一解析) ∧ FreshEnough(同周期
          Δ≤视觉+余量) ∧ PropertyValid
两类冲突  权威域内：Tier 0 销案·confidence 盲·不升档·记 overruled
          权威域外/资格不满足：XML 闭嘴→视觉域阶梯（失效≠视觉获胜）
升档      触发=体系无法定案；Deep 另有 D6 三条件（≥B级∧不可逆∨跨2周期）
对抗域    防线=底牌级联包名覆盖 + 权限层（Grant），不在升档
值域      *.state ∈ {on,off,partial}
验证      事后 XML 验证四门（权威域+重解析+属性+时序）；不过门走截图
```

## 实现期纪律

规则自本图冻结起，实现只对表填格；任何规则改动需新裁决（state.md
status log + 台账），不静默改图。
