# 层级状态机设计（支持分支恢复）

> 核心思想：状态机 + 层级树 = 可恢复的遍历系统

---

## 一、核心概念

### 1.1 双层状态模型

```
全局状态机状态 (TraversalState)
    ↓ 维护
当前层级路径 (current_path: ["车辆设置", "DiLink", "互联"])
    ↓ 指向
层级节点状态 (NodeState: 每个节点的遍历状态)
```

### 1.2 层级节点状态

```python
class NodeState(Enum):
    PENDING      # 待访问
    VISITING     # 正在访问（当前焦点）
    COMPLETED    # 已完成
    FAILED       # 失败（可重测）
    SKIPPED      # 跳过（无法访问）
    BLOCKED      # 阻塞（子节点有问题）
```

### 1.3 节点数据结构

```python
@dataclass
class TreeNode:
    """结构树节点"""
    name: str                          # 节点名称
    level: int                         # 层级深度 (0=APP, 1=一级菜单, 2=二级标签, 3=内容)
    node_type: NodeType               # MENU / TAB / ITEM / POPUP / SWITCH
    state: NodeState = PENDING        # 节点状态
    parent: Optional[TreeNode] = None # 父节点引用
    children: List[TreeNode] = []     # 子节点列表
    coordinates: Optional[tuple] = None  # 坐标 (x, y)
    metadata: dict = {}               # 元数据

    # 遍历相关
    retry_count: int = 0              # 重试次数
    last_error: Optional[str] = None # 最后错误
    visit_time: Optional[datetime] = None  # 访问时间

    # 分支相关
    branch_failed: bool = False       # 分支是否失败
    can_retry: bool = True           # 是否可重试
```

---

## 二、状态机与层级树的融合

### 2.1 状态机增强

```python
class HierarchicalStateMachine:
    """融合层级数据的状态机"""

    def __init__(self):
        # 全局状态
        self.global_state: TraversalState = TraversalState.IDLE

        # 层级树（状态真相源）
        self.root: Optional[TreeNode] = None

        # 当前路径（节点引用链）
        self.current_path: List[TreeNode] = []

        # 失败栈（记录失败的分支）
        self.failed_branches: List[TreeNode] = []

    @property
    def current_node(self) -> Optional[TreeNode]:
        """获取当前节点"""
        return self.current_path[-1] if self.current_path else None

    @property
    def current_level1(self) -> Optional[TreeNode]:
        """获取当前一级菜单"""
        return self.current_path[1] if len(self.current_path) > 1 else None

    @property
    def current_level2(self) -> Optional[TreeNode]:
        """获取当前二级标签"""
        return self.current_path[2] if len(self.current_path) > 2 else None
```

### 2.2 层级路径操作

```python
    def descend_to(self, node: TreeNode) -> bool:
        """向下进入节点"""
        if node.state not in [NodeState.PENDING, NodeState.FAILED]:
            return False
        node.state = NodeState.VISITING
        self.current_path.append(node)
        return True

    def ascend_to_parent(self) -> bool:
        """向上回到父节点"""
        if len(self.current_path) <= 1:
            return False
        current = self.current_path.pop()
        if current.state == NodeState.VISITING:
            current.state = NodeState.COMPLETED
        return True

    def retreat_to_level(self, level: int) -> bool:
        """回退到指定层级"""
        while len(self.current_path) > level + 1:
            if not self.ascend_to_parent():
                return False
        return True

    def jump_to_node(self, node: TreeNode) -> bool:
        """跳转到指定节点（跨层级）"""
        # 找到从根到目标节点的路径
        path = self._find_path_to(node)
        if not path:
            return False

        # 重置当前路径
        self.current_path = path
        return True
```

---

## 三、异常恢复策略

### 3.1 分支失败处理

```python
    def handle_branch_failure(self, failed_node: TreeNode, error: str) -> None:
        """处理分支失败"""
        failed_node.state = NodeState.FAILED
        failed_node.last_error = error
        failed_node.retry_count += 1

        # 记录失败分支
        if failed_node not in self.failed_branches:
            self.failed_branches.append(failed_node)

        # 决策：是否回退
        if failed_node.retry_count >= self.MAX_RETRIES:
            # 超过最大重试次数，标记为跳过
            failed_node.state = NodeState.SKIPPED
            failed_node.can_retry = False

            # 回退到上级分支
            self.ascend_to_parent()

            # 标记父节点有失败子节点
            if self.current_node:
                self.current_node.branch_failed = True
        else:
            # 留在当前状态，准备重试
            pass
```

### 3.2 链路未通处理

```python
    def handle_path_blocked(self) -> None:
        """处理当前链路未通"""
        current = self.current_node

        if not current:
            return

        # 标记当前节点为阻塞
        current.state = NodeState.BLOCKED

        # 回退到父节点
        if self.ascend_to_parent():
            parent = self.current_node

            # 检查父节点是否有其他未访问的子节点
            unvisited = self._get_unvisited_children(parent)
            if unvisited:
                # 有其他分支，切换到下一个
                next_node = unvisited[0]
                self.descend_to(next_node)
            else:
                # 无其他分支，继续回退
                self.handle_path_blocked()
```

### 3.3 分支重测

```python
    def retry_failed_branches(self) -> bool:
        """重测失败的分支"""
        if not self.failed_branches:
            return False

        # 取出最早失败的分支
        failed_node = self.failed_branches.pop(0)

        # 重置状态
        failed_node.state = NodeState.PENDING
        failed_node.retry_count = 0
        failed_node.last_error = None

        # 跳转到该节点
        return self.jump_to_node(failed_node)
```

---

## 四、状态转换与层级操作对照表

| 操作 | 层级变化 | 节点状态变化 | 全局状态变化 |
|------|----------|-------------|-------------|
| 进入 APP | current_path = [root] | root.state = VISITING | INITIALIZING |
| 点击一级菜单 | append(l1_node) | l1_node.state = VISITING | SWITCHING_MENU |
| 点击二级标签 | append(l2_node) | l2_node.state = VISITING | SWITCHING_TAB |
| 点击内容元素 | append(item_node) | item_node.state = VISITING | TRAVERSING_ITEM |
| 元素访问完成 | pop() | item_node.state = COMPLETED | TRAVERSING_ITEM |
| 弹窗处理完成 | pop() × 2 | popup.state = COMPLETED | TRAVERSING_ITEM |
| 分支失败 | pop() | node.state = FAILED/SKIPPED | RECOVERING |
| 链路未通 | pop() × N | node.state = BLOCKED | RECOVERING |
| 切换下一分支 | pop() + append(next) | - | TRAVERSING_ITEM |

---

## 五、完整的状态转换流程

### 5.1 正常遍历流程

```
IDLE
  ↓ [用户输入]
TARGET_SEARCH
  ↓ [找到入口]
INITIALIZING (root.state = VISITING)
  ↓ [初始化完成]
SWITCHING_MENU (current_path = [root, l1])
  ↓ [切换成功]
SWITCHING_TAB (current_path = [root, l1, l2])
  ↓ [切换成功]
TRAVERSING_ITEM (current_path = [root, l1, l2, item])
  ↓ [点击]
WAITING_RESPONSE
  ↓ [正常]
COMPLETED_ITEM (item.state = COMPLETED, ascend)
  ↓ [继续]
TRAVERSING_ITEM (下一个 item)
  ↓ [无更多]
SWITCHING_TAB (下一个 l2)
  ↓ [无更多]
SWITCHING_MENU (下一个 l1)
  ↓ [全部完成]
COMPLETED
```

### 5.2 异常恢复流程

```
TRAVERSING_ITEM (current_path = [root, l1, l2, item])
  ↓ [点击]
WAITING_RESPONSE
  ↓ [检测到异常]
RECOVERING
  ↓ [重试 < 3次]
WAITING_RESPONSE
  ↓ [仍失败]
handle_branch_failure(item)
  ↓ [item.state = FAILED]
ascend_to_parent() (current_path = [root, l1, l2])
  ↓
检查 l2 是否有其他子节点
  ↓ 有
切换到下一个 item (descend_to(next_item))
  ↓ 无
ascend_to_parent() (current_path = [root, l1])
  ↓
检查 l1 是否有其他子节点
  ↓ 有
切换到下一个 l2 (descend_to(next_l2))
  ↓ 无
ascend_to_parent() (current_path = [root])
  ↓
切换到下一个 l1 (descend_to(next_l1))
```

### 5.3 链路未通流程

```
TRAVERSING_ITEM (current_path = [root, l1, l2, item])
  ↓ [点击无响应，且无子控件]
item.state = NO_RESPONSE
  ↓
handle_path_blocked()
  ↓ [item.state = BLOCKED]
ascend_to_parent() (current_path = [root, l1, l2])
  ↓ [检查 l2 的其他子节点]
  ↓ 有其他节点
descend_to(next_item)
  ↓ 无其他节点
l2.state = BLOCKED
ascend_to_parent() (current_path = [root, l1])
  ↓ [检查 l1 的其他子节点]
  ↓ 有其他节点
descend_to(next_l2)
  ↓
继续遍历
```

---

## 六、关键算法实现

### 6.1 选择下一个待访问节点

```python
    def select_next_node(self) -> Optional[TreeNode]:
        """选择下一个待访问节点（深度优先）"""
        current = self.current_node
        if not current:
            return None

        # 1. 优先：当前节点的未访问子节点
        unvisited_children = [
            child for child in current.children
            if child.state == NodeState.PENDING
        ]
        if unvisited_children:
            return unvisited_children[0]

        # 2. 其次：兄弟节点的未访问子节点
        parent = current.parent
        while parent:
            # 检查兄弟节点
            siblings = [sib for sib in parent.children if sib != current]
            for sibling in siblings:
                unvisited = [
                    child for child in sibling.children
                    if child.state == NodeState.PENDING
                ]
                if unvisited:
                    return unvisited[0]
            # 继续向上找
            current = parent
            parent = current.parent

        # 3. 最后：检查失败分支
        if self.failed_branches:
            return self.failed_branches[0]

        return None
```

### 6.2 回溯到可继续的层级

```python
    def backtrack_to_continuable_level(self) -> bool:
        """回溯到可以继续的层级"""
        while self.current_path:
            current = self.current_node

            # 检查当前节点是否有可重测的分支
            unvisited = [
                child for child in current.children
                if child.state in [NodeState.PENDING, NodeState.FAILED]
            ]
            if unvisited:
                # 切换到下一个
                return self.descend_to(unvisited[0])

            # 检查当前节点本身是否可重测
            if current.state == NodeState.FAILED and current.can_retry:
                return True

            # 回退到父节点
            if not self.ascend_to_parent():
                break

        return False
```

### 6.3 状态持久化格式

```json
{
  "global_state": "TRAVERSING_ITEM",
  "root": {
    "name": "车辆设置",
    "level": 0,
    "state": "VISITING",
    "children": [
      {
        "name": "DiLink",
        "level": 1,
        "state": "COMPLETED",
        "children": [
          {
            "name": "互联",
            "level": 2,
            "state": "VISITING",
            "children": [
              {"name": "移动数据", "level": 3, "state": "COMPLETED"},
              {"name": "无线网络", "level": 3, "state": "FAILED", "retry_count": 2},
              {"name": "个人热点", "level": 3, "state": "PENDING"}
            ]
          }
        ]
      }
    ]
  },
  "current_path": ["车辆设置", "DiLink", "互联"],
  "failed_branches": ["车辆设置|DiLink|互联|无线网络"],
  "timestamp": "2026-05-31T15:00:00"
}
```

---

## 七、使用示例

```python
# 初始化
sm = HierarchicalStateMachine()

# 进入 APP
sm.descend_to(root_node)

# 初始化：建立结构树
sm.build_tree_from_ai_analysis(ai_result)

# 开始遍历
while True:
    # 选择下一个节点
    next_node = sm.select_next_node()
    if not next_node:
        break

    # 进入节点
    sm.descend_to(next_node)

    try:
        # 执行操作
        result = execute_operation(next_node)

        # 成功：标记完成
        next_node.state = COMPLETED

    except Exception as e:
        # 失败：处理分支
        sm.handle_branch_failure(next_node, str(e))

        # 回溯到可继续的位置
        sm.backtrack_to_continuable_level()

# 检查失败分支
if sm.failed_branches:
    print(f"有 {len(sm.failed_branches)} 个失败分支可重测")
    sm.retry_failed_branches()
```

---

## 八、与现有代码集成

### 8.1 扩展 ContentTree

```python
# src/state/content_tree.py

class TreeNode:
    """增强的树节点，支持状态机"""
    def __init__(self, ...):
        # 原有字段
        self.name: str
        self.parent: Optional[TreeNode]
        self.children: List[TreeNode]

        # 新增状态字段
        self.state: NodeState = NodeState.PENDING
        self.retry_count: int = 0
        self.last_error: Optional[str] = None
        self.can_retry: bool = True
```

### 8.2 扩展 TraversalEngine

```python
# src/engine/traversal_engine.py

class TraversalEngine:
    def __init__(self, adb_client, vision_service):
        self.adb = adb_client
        self.vision = vision_service

        # 状态机
        self.sm = HierarchicalStateMachine()

        # 错误回调集成状态机
        self.adb.set_error_callback(self._handle_adb_error)

    def _handle_adb_error(self, operation, message, exception):
        """ADB 错误回调，触发状态机恢复"""
        if self.sm.current_node:
            self.sm.handle_branch_failure(
                self.sm.current_node,
                f"ADB error: {message}"
            )
            self.sm.backtrack_to_continuable_level()
```
