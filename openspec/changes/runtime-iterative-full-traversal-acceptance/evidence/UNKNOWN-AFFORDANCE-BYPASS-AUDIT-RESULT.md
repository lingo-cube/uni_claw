# PROJECT_LEADER_UNKNOWN_AFFORDANCE_BYPASS_AUDIT_AND_REPAIR_RESULT

## Gate

`UNKNOWN_AFFORDANCE_BYPASS_AUDIT_AND_REPAIR`（2026-08-29）

## 1. 审计结果

### 已撤回的未批准 bypass

| Bypass | 状态 | 审计结论 |
|---|---|---|
| "所有 text_block 默认非阻塞"（Runtime completeness check） | **已撤回** | 未经批准的全局规则；可能掩盖漏识别菜单；恢复 Unknown fail-closed |
| "StableKey 相同即消解"（无物理行验证） | **已收紧** | StableKey 匹配本身不证明同一元素；现在要求同帧物理行等价证据 |

### 已审计的消解条件（新的 IsPhysicalRowDuplicate）

消解仅在以下**全部**条件满足时允许：
1. StableKey 匹配（必要但非充分）
2. 同帧恰好一个已知分类元素持有该 StableKey（无歧义）
3. 两元素的 bounds 垂直重叠 ≥ 较短者高度的 50%（物理行等价证据）

任何条件不满足 → 不消解 → Unknown 阻塞（fail-closed）。

### StableKey 分配审计（RowIdentityContext）

**发现**：同文本不同位置的两个元素获得同一 StableKey（如 'Appearance' section header vs 'Appearance' 标签）。
**修复**：StableKey 分配改为 **文本 + 垂直位置带**（`text|band`，band 宽度 0.03）→ 同文本不同位置 = 不同物理行 = 不同 StableKey。

### Python 稳定化器审计

**发现**：同文本多个已知行时，取第一个匹配（可能选错物理行）。
**修复**：同文本多行 → 返回 None（让 C# 位置带消歧；never guess）。

## 2. 多次独立感知采样（Gate 要求 4）

对 Display 子页同一状态连续 5 次截图+分析：

| 采样 | 候选数 | 唯一(text,type)键 |
|---|---|---|
| 1-5 | 35 | 25 |

**结论**：25/25 键跨 5 次完全一致。感知管线**完全确定性**。

**非确定性来源**：不在感知管线，在**模拟器渲染时序** —— 不同时刻截图的像素不同（渲染/动画进行中）→ YOLO 置信度波动 → 某些帧检出/不检出。

## 3. Falsifier 测试（Gate 要求 6）

6/6 通过（`PhysicalRowDuplicateFalsifierTests.cs`）：

| Falsifier | 断言 | 结果 |
|---|---|---|
| 未分类 text_block 非重复 → 阻塞 | MUST block | ✅ |
| 两个不同物理行 StableKey 碰撞 → 阻塞 | MUST block | ✅ |
| 同一物理行重叠 bounds → 安全消解 | Safe | ✅ |
| 同一物理行微小偏移 bounds → 安全消解 | Safe | ✅ |
| 普通说明文字 → 不产生导航义务 | No obligation | ✅ |
| 无 StableKey → 无法证明重复 → 阻塞 | MUST block | ✅ |

## 4. 真机验证

- SettingsStrategyBinding + OpenWorld：**123/123 绿**
- 全量：2277 通过 / 10 预存环境类失败
- Display 子页 campaign：归一化通过 + 进入子页 ✓

**当前失败点**：回到 "Unknown interaction affordances remain"（正确的 fail-closed 行为）。

**根因**（感知/分类层，非 Runtime）：子页中存在未被 capability 分类的 text_block —— 它们的文本不匹配结构化层的可点击行（结构化层在该滚动位置可能不完整），因此 capability 不产生证据 → Unknown。

**需要的修复**（不在本审计范围）：capability 层为"文本匹配结构化行但组合失败"的 text_block 提供分类证据。这是感知/分类层修复，不是 Runtime bypass。

## 5. Deltas

```
AuthorityDelta:     NONE
ArchitectureDelta:  NONE（收紧了消解条件，移除了 bypass）
Phase26_Reentry:    NOT_READY（Unknown 尚未证据化分类）
```

## 6. Decision

```
PHASE26_REENTRY_NOT_READY
Human Gate: REQUIRED（capability 层分类修复完成后重新提交）
```

**Stopped per gate.**
