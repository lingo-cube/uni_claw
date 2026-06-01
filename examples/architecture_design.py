"""抽象遍历图 + 实际树 + 状态机架构

核心思想:
1. 抽象图 - 定义通用的遍历策略模式
2. 实际树 - 记录真实发现的 UI 结构
3. 状态机 - 控制遍历执行，确保所有状态可穷举
"""

from enum import Enum
from dataclasses import dataclass
from typing import Dict, List, Optional, Callable
from abc import ABC, abstractmethod


# ============================================================
# 第一层: 抽象遍历图 (Traversal Pattern Graph)
# ============================================================

class TraversalPattern(str, Enum):
    """抽象的遍历模式"""
    BREADTH_FIRST = "breadth_first"  # 广度优先
    DEPTH_FIRST = "depth_first"      # 深度优先
    LEVEL_ORDER = "level_order"      # 层级遍历
    STAR_PATTERN = "star"            # 星形模式（从中心辐射）


class PatternNode(str, Enum):
    """抽象遍历模式的节点类型"""
    START = "start"           # 开始遍历
    ANALYZE = "analyze"       # 分析当前页面
    CLASSIFY = "classify"     # 分类识别到的元素
    SELECT = "select"         # 选择下一个目标
    EXECUTE = "execute"       # 执行操作（点击等）
    VERIFY = "verify"         # 验证结果
    HANDLE_EVENT = "handle"   # 处理事件（弹窗、跳转等）
    BACKTRACK = "backtrack"   # 回退
    BRANCH = "branch"         # 分支选择
    END = "end"              # 结束


@dataclass
class PatternState:
    """模式状态 - 当前在遍历模式中的位置"""
    current_node: PatternNode
    context: Dict  # 运行时上下文

    def can_transition_to(self, node: PatternNode) -> bool:
        """检查是否可以转换到指定节点"""
        # 定义合法的状态转换
        transitions = {
            PatternNode.START: [PatternNode.ANALYZE],
            PatternNode.ANALYZE: [PatternNode.CLASSIFY, PatternNode.END],
            PatternNode.CLASSIFY: [PatternNode.SELECT, PatternNode.BRANCH],
            PatternNode.SELECT: [PatternNode.EXECUTE],
            PatternNode.EXECUTE: [PatternNode.VERIFY, PatternNode.HANDLE_EVENT],
            PatternNode.VERIFY: [PatternNode.SELECT, PatternNode.BACKTRACK, PatternNode.ANALYZE],
            PatternNode.HANDLE_EVENT: [PatternNode.SELECT, PatternNode.ANALYZE, PatternNode.BACKTRACK],
            PatternNode.BACKTRACK: [PatternNode.ANALYZE, PatternNode.SELECT],
            PatternNode.BRANCH: [PatternNode.SELECT],
            PatternNode.END: [],
        }
        return node in transitions.get(self.current_node, [])


# ============================================================
# 第二层: 实际的树 (ContentTree - 已存在)
# ============================================================

# ContentTree 已经存在于 src/state/content_tree.py
# 这里展示如何与抽象图配合

@dataclass
class TraversalContext:
    """遍历上下文 - 连接抽象图和实际树"""
    # 抽象层
    pattern: TraversalPattern
    pattern_state: PatternState

    # 实际层
    content_tree: "ContentTree"  # 实际的 UI 树
    current_node_id: Optional[str]  # 当前在树中的位置

    # 执行状态
    visited_fingerprints: set = set()  # 已访问的元素指纹
    branch_stack: List[str] = []  # 分支栈（用于回溯）


# ============================================================
# 第三层: 状态机 (Execution State Machine)
# ============================================================

class ExecutionState(str, Enum):
    """执行状态 - 所有可能的状态"""
    IDLE = "idle"                      # 空闲
    INITIALIZING = "initializing"      # 初始化中
    ANALYZING_PAGE = "analyzing_page"  # 分析页面
    SELECTING_TARGET = "selecting"      # 选择目标
    EXECUTING_ACTION = "executing"     # 执行操作
    VERIFYING_RESULT = "verifying"      # 验证结果
    HANDLING_POPUP = "handling_popup"   # 处理弹窗
    HANDLING_REDIRECT = "handling_redirect"  # 处理跳转
    RECOVERING = "recovering"           # 恢复中
    BACKTRACKING = "backtracking"       # 回退中
    BRANCHING = "branching"             # 分支选择
    COMPLETED = "completed"             # 完成
    ERROR = "error"                     # 错误
    FATAL = "fatal"                     # 致命错误


@dataclass
class StateTransition:
    """状态转换"""
    from_state: ExecutionState
    to_state: ExecutionState
    trigger: str  # 触发条件
    action: Optional[Callable] = None  # 转换时执行的动作


class TraversalStateMachine:
    """遍历状态机 - 确保所有状态可穷举和覆盖"""

    def __init__(self):
        self.current_state = ExecutionState.IDLE
        self.transitions: Dict[str, StateTransition] = {}
        self._build_complete_transitions()

    def _build_complete_transitions(self):
        """构建完整的状态转换图 - 确保所有状态都可到达"""

        # 从 IDLE 开始的转换
        self._add(ExecutionState.IDLE, ExecutionState.INITIALIZING, "start")

        # 初始化后的转换
        self._add(ExecutionState.INITIALIZING, ExecutionState.ANALYZING_PAGE, "initialized")
        self._add(ExecutionState.INITIALIZING, ExecutionState.ERROR, "init_failed")

        # 分析页面的转换
        self._add(ExecutionState.ANALYZING_PAGE, ExecutionState.SELECTING_TARGET, "analysis_complete")
        self._add(ExecutionState.ANALYZING_PAGE, ExecutionState.HANDLING_POPUP, "popup_detected")
        self._add(ExecutionState.ANALYZING_PAGE, ExecutionState.HANDLING_REDIRECT, "redirect_detected")
        self._add(ExecutionState.ANALYZING_PAGE, ExecutionState.ERROR, "analysis_failed")

        # 选择目标的转换
        self._add(ExecutionState.SELECTING_TARGET, ExecutionState.EXECUTING_ACTION, "target_selected")
        self._add(ExecutionState.SELECTING_TARGET, ExecutionState.BRANCHING, "branch_needed")
        self._add(ExecutionState.SELECTING_TARGET, ExecutionState.BACKTRACKING, "no_more_targets")
        self._add(ExecutionState.SELECTING_TARGET, ExecutionState.COMPLETED, "all_done")

        # 执行操作的转换
        self._add(ExecutionState.EXECUTING_ACTION, ExecutionState.VERIFYING_RESULT, "action_complete")
        self._add(ExecutionState.EXECUTING_ACTION, ExecutionState.HANDLING_POPUP, "popup_triggered")
        self._add(ExecutionState.EXECUTING_ACTION, ExecutionState.RECOVERING, "action_failed")
        self._add(ExecutionState.EXECUTING_ACTION, ExecutionState.ERROR, "execute_error")

        # 验证结果的转换
        self._add(ExecutionState.VERIFYING_RESULT, ExecutionState.SELECTING_TARGET, "verified_continue")
        self._add(ExecutionState.VERIFYING_RESULT, ExecutionState.BACKTRACKING, "verified_backtrack")
        self._add(ExecutionState.VERIFYING_RESULT, ExecutionState.ANALYZING_PAGE, "page_changed")

        # 处理弹窗的转换
        self._add(ExecutionState.HANDLING_POPUP, ExecutionState.SELECTING_TARGET, "popup_closed")
        self._add(ExecutionState.HANDLING_POPUP, ExecutionState.BACKTRACKING, "popup_close_failed")
        self._add(ExecutionState.HANDLING_POPUP, ExecutionState.ERROR, "popup_handle_failed")

        # 处理跳转的转换
        self._add(ExecutionState.HANDLING_REDIRECT, ExecutionState.SELECTING_TARGET, "redirect_handled")
        self._add(ExecutionState.HANDLING_REDIRECT, ExecutionState.BACKTRACKING, "redirect_back")

        # 恢复的转换
        self._add(ExecutionState.RECOVERING, ExecutionState.SELECTING_TARGET, "recovered")
        self._add(ExecutionState.RECOVERING, ExecutionState.BACKTRACKING, "recovery_failed")
        self._add(ExecutionState.RECOVERING, ExecutionState.ERROR, "recovery_error")

        # 回退的转换
        self._add(ExecutionState.BACKTRACKING, ExecutionState.ANALYZING_PAGE, "backtracked")
        self._add(ExecutionState.BACKTRACKING, ExecutionState.SELECTING_TARGET, "position_changed")
        self._add(ExecutionState.BACKTRACKING, ExecutionState.COMPLETED, "no_more_backtrack")

        # 分支的转换
        self._add(ExecutionState.BRANCHING, ExecutionState.SELECTING_TARGET, "branch_selected")
        self._add(ExecutionState.BRANCHING, ExecutionState.BACKTRACKING, "branch_exhausted")

        # 错误处理
        self._add(ExecutionState.ERROR, ExecutionState.RECOVERING, "can_recover")
        self._add(ExecutionState.ERROR, ExecutionState.BACKTRACKING, "cannot_recover")
        self._add(ExecutionState.ERROR, ExecutionState.FATAL, "too_many_errors")
        self._add(ExecutionState.ERROR, ExecutionState.COMPLETED, "accept_partial")

        # 致命错误
        self._add(ExecutionState.FATAL, ExecutionState.COMPLETED, "terminated")

    def _add(self, from_state: ExecutionState, to_state: ExecutionState,
             trigger: str, action: Optional[Callable] = None):
        """添加状态转换"""
        key = f"{from_state.value}_{trigger}"
        self.transitions[key] = StateTransition(from_state, to_state, trigger, action)

    def transition(self, trigger: str, context: Optional[Dict] = None) -> bool:
        """执行状态转换"""
        key = f"{self.current_state.value}_{trigger}"
        if key not in self.transitions:
            print(f"⚠️  无效的转换: {self.current_state.value} --[{trigger}]--> ?")
            return False

        transition = self.transitions[key]

        # 执行转换动作
        if transition.action and context:
            transition.action(context)

        old_state = self.current_state
        self.current_state = transition.to_state

        print(f"🔄 状态转换: {old_state.value} --[{trigger}]--> {self.current_state.value}")
        return True

    def can_transition(self, trigger: str) -> bool:
        """检查是否可以转换"""
        key = f"{self.current_state.value}_{trigger}"
        return key in self.transitions

    def get_possible_transitions(self) -> List[str]:
        """获取当前状态可能的转换"""
        return [t.trigger for t in self.transitions.values()
                if t.from_state == self.current_state]

    def verify_completeness(self) -> Dict:
        """验证状态机的完整性 - 确保所有状态都可到达"""
        unreachable = set()
        reachable = {ExecutionState.IDLE}
        queue = [ExecutionState.IDLE]

        while queue:
            state = queue.pop(0)
            for transition in self.transitions.values():
                if transition.from_state == state:
                    if transition.to_state not in reachable:
                        reachable.add(transition.to_state)
                        queue.append(transition.to_state)

        # 检查未到达的状态
        all_states = set(ExecutionState)
        unreachable = all_states - reachable

        return {
            "total_states": len(all_states),
            "reachable_states": len(reachable),
            "unreachable_states": list(unreachable),
            "is_complete": len(unreachable) == 0
        }


# ============================================================
# 架构整合
# ============================================================

class GraphDrivenTraversalEngine:
    """图驱动的遍历引擎

    整合三层架构:
    1. 抽象图 - 定义遍历策略模式
    2. 实际树 - ContentTree 记录真实 UI
    3. 状态机 - 控制执行流程
    """

    def __init__(self, adb, vision, content_tree, config):
        self.adb = adb
        self.vision = vision
        self.content_tree = content_tree
        self.config = config

        # 初始化状态机
        self.state_machine = TraversalStateMachine()

        # 选择遍历模式
        self.pattern = TraversalPattern.BREADTH_FIRST

        # 遍历上下文
        self.context = TraversalContext(
            pattern=self.pattern,
            pattern_state=PatternState(PatternNode.START, {}),
            content_tree=content_tree,
            current_node_id="0",
            visited_fingerprints=set(),
            branch_stack=[]
        )

    def run(self) -> Dict:
        """执行遍历"""
        print("=" * 70)
        print("🚀 图驱动遍历开始")
        print("=" * 70)

        # 验证状态机完整性
        completeness = self.state_machine.verify_completeness()
        print(f"\n📊 状态机完整性: {completeness['reachable_states']}/{completeness['total_states']} 状态可达")

        if not completeness['is_complete']:
            print(f"⚠️  不可达状态: {completeness['unreachable_states']}")

        # 开始遍历
        self.state_machine.transition("start", {"context": self.context})

        while self.state_machine.current_state != ExecutionState.COMPLETED:
            self._process_current_state()

        return self._build_summary()

    def _process_current_state(self):
        """根据当前状态执行相应动作"""
        state = self.state_machine.current_state

        if state == ExecutionState.ANALYZING_PAGE:
            self._analyze_page()
        elif state == ExecutionState.SELECTING_TARGET:
            self._select_target()
        elif state == ExecutionState.EXECUTING_ACTION:
            self._execute_action()
        elif state == ExecutionState.VERIFYING_RESULT:
            self._verify_result()
        elif state == ExecutionState.HANDLING_POPUP:
            self._handle_popup()
        elif state == ExecutionState.BACKTRACKING:
            self._backtrack()
        # ... 其他状态处理

    def _analyze_page(self):
        """分析当前页面"""
        print("\n📸 分析当前页面...")
        screenshot = self.adb.capture_screenshot()
        analysis = self.vision.analyze_screenshot(screenshot)

        # 更新 ContentTree
        # ... 更新逻辑

        self.state_machine.transition("analysis_complete", {"analysis": analysis})

    def _select_target(self):
        """选择下一个目标"""
        print("\n🎯 选择下一个目标...")
        # 从 ContentTree 选择下一个未访问的节点
        # ... 选择逻辑

        if self._has_more_targets():
            self.state_machine.transition("target_selected")
        else:
            self.state_machine.transition("no_more_targets")

    def _execute_action(self):
        """执行操作"""
        print("\n▶️  执行操作...")
        # 执行点击等操作

        self.state_machine.transition("action_complete")

    def _verify_result(self):
        """验证结果"""
        print("\n✓ 验证结果...")
        # 验证操作结果

        self.state_machine.transition("verified_continue")

    def _handle_popup(self):
        """处理弹窗"""
        print("\n🔔 处理弹窗...")
        # 处理弹窗逻辑

        self.state_machine.transition("popup_closed")

    def _backtrack(self):
        """回退"""
        print("\n↩️  回退...")
        # 回退逻辑

        self.state_machine.transition("backtracked")

    def _has_more_targets(self) -> bool:
        """检查是否还有更多目标"""
        # 检查 ContentTree 是否还有未访问节点
        return True

    def _build_summary(self) -> Dict:
        """构建遍历摘要"""
        return {
            "pattern": self.pattern.value,
            "visited_count": len(self.context.visited_fingerprints),
            "tree_nodes": len(self.context.content_tree.nodes),
            "final_state": self.state_machine.current_state.value
        }


# ============================================================
# 架构可视化
# ============================================================

def visualize_architecture():
    """可视化三层架构"""
    print("=" * 70)
    print("🏗️  三层架构")
    print("=" * 70)

    print("""
┌─────────────────────────────────────────────────────────────────┐
│                      抽象的图 (Abstract Graph)                      │
│                 通用的遍历策略模式，不针对具体应用                   │
├─────────────────────────────────────────────────────────────────┤
│  START → ANALYZE → CLASSIFY → SELECT → EXECUTE → VERIFY → END   │
│                                                                  │
│  • 定义遍历的"流程"                                              │
│  • 不关心具体是什么应用                                          │
│  • 可应用于任何 UI 结构                                         │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│              实际的树 (ContentTree - 已存在)                     │
│                    记录真实发现的 UI 结构                          │
├─────────────────────────────────────────────────────────────────┤
│  nodes: {                                                       │
│      "1": "系统设置",                                            │
│      "1.1": "Wi-Fi",                                            │
│      "1.2": "蓝牙",                                             │
│      "1.1.1": "ChinaNet-xxx",                                   │
│      ...                                                        │
│  }                                                              │
│                                                                  │
│  • 从 AI 分析填充                                               │
│  • 记录真实的 UI 层级                                           │
│  • 每个应用的树都不同                                           │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                  状态机 (State Machine)                           │
│                    控制遍历执行流程                                │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐                                             │
│  │ 当前状态      │── 触发 ───→ 下一个状态                        │
│  └──────────────┘                                             │
│                                                                  │
│  • 确保所有状态都可穷举                                          │
│  • 每个状态转换都有明确触发条件                                    │
│  • 异常状态有明确的处理路径                                      │
└─────────────────────────────────────────────────────────────────┘
                              ↓
                    ┌──────────────────┐
                    │  完整的遍历执行  │
                    │   可穷举、可控   │
                    └──────────────────┘
""")


def visualize_state_machine():
    """可视化完整状态机"""
    print("=" * 70)
    print("⚙️  完整状态机图")
    print("=" * 70)

    print("""
所有执行状态:

    [IDLE]
       ↓ start
    [INITIALIZING]
       ↓ initialized / init_failed
    [ANALYZING_PAGE] ←─────────┐
       ↓                         │
    [SELECTING_TARGET] ←────────┤
       ↓                         │
    [EXECUTING_ACTION]            │
       ↓                         │
    [VERIFYING_RESULT] ──────────┘
       ↓                         │
    [HANDLING_POPUP]              │
       ↓                         │
    [RECOVERING]                  │
       ↓                         │
    [BACKTRACKING] ──────────────┘
       ↓
    [BRANCHING]
       ↓
    [COMPLETED]

错误处理路径:
    任何状态 → [ERROR] → [RECOVERING] / [BACKTRACKING] / [FATAL]
    """)


def main():
    """主演示"""
    visualize_architecture()
    print()
    visualize_state_machine()

    print("\n" + "=" * 70)
    print("💡 关键优势")
    print("=" * 70)
    print("""
1. 抽象图的可复用性
   - 一套遍历模式适用于所有应用
   - 不需要为每个应用重新设计

2. 实际树的准确性
   - ContentTree 记录真实的 UI 结构
   - AI 分析填充，不是编造的数据

3. 状态机的完备性
   - 所有可能的状态都被定义
   - 所有状态转换都有明确路径
   - 异常处理路径清晰

4. 可穷举设计
   - 状态机可以验证完整性
   - 不会有"未定义"的状态
   - 确保遍历一定会完成或正确终止
    """)


if __name__ == "__main__":
    main()
