"""Prompt registry for centralized prompt management."""

import logging
from typing import Dict

from .config import AIProviderConfig

logger = logging.getLogger(__name__)


class PromptRegistry:
    """Centralized prompt template registry with variable injection.

    This class manages:
    - All prompt templates for AI capabilities
    - Variable injection into templates
    - Reasoning level injection
    - Custom prompt registration

    Templates support:
    - {variable} placeholders for runtime variables
    - {{REASONING_LEVEL}} for reasoning detail injection
    """

    def __init__(self, config: AIProviderConfig):
        """Initialize the prompt registry.

        Args:
            config: AI provider configuration
        """
        self.config = config
        self._prompts: Dict[str, str] = {}
        self._load_defaults()

    def _load_defaults(self):
        """Load default prompt templates."""
        # Parse task prompts
        self._prompts["parse_task.system"] = self._get_parse_task_system()
        self._prompts["parse_task.user"] = self._get_parse_task_user()

        # Verify page type prompts
        self._prompts["verify_page.system"] = self._get_verify_page_system()
        self._prompts["verify_page.user"] = self._get_verify_page_user()

        # Screen safety prompts
        self._prompts["screen_elements.system"] = self._get_screen_elements_system()
        self._prompts["screen_elements.user"] = self._get_screen_elements_user()

        # Context decision prompts
        self._prompts["make_decision.system"] = self._get_decision_system()
        self._prompts["make_decision.user"] = self._get_decision_user()

        # Vision analysis prompts (for reference, actual calls use Vision Service)
        self._prompts["vision_analysis.system"] = self._get_vision_analysis_system()
        self._prompts["vision_analysis.user"] = self._get_vision_analysis_user()

    def get(self, key: str) -> str:
        """Get a prompt template.

        Args:
            key: Template key (e.g., "parse_task.system")

        Returns:
            Prompt template with {{REASONING_LEVEL}} replaced
        """
        template = self._prompts.get(key, "")
        if template:
            reasoning_level = self._get_reasoning_prompt()
            template = template.replace("{{REASONING_LEVEL}}", reasoning_level)
        return template

    def register(self, key: str, prompt: str) -> None:
        """Register a custom prompt template.

        Args:
            key: Template key
            prompt: Prompt template string
        """
        self._prompts[key] = prompt
        logger.debug(f"Registered custom prompt: {key}")

    def inject_variables(self, template: str, variables: Dict) -> str:
        """Inject variables into a template.

        Args:
            template: Template string with {variable} placeholders
            variables: Variables to inject

        Returns:
            Formatted string with variables replaced
        """
        result = template
        # First replace reasoning level
        reasoning_level = self._get_reasoning_prompt()
        result = result.replace("{{REASONING_LEVEL}}", reasoning_level)

        # Then replace variables
        for key, value in variables.items():
            placeholder = f"{{{key}}}"
            result = result.replace(placeholder, str(value))

        return result

    def _get_reasoning_prompt(self) -> str:
        """Get the reasoning level prompt text.

        Returns:
            Prompt text for the configured reasoning level
        """
        levels = {
            "concise": "简要说明你的分析过程",
            "step_by_step": "分步骤说明你的分析过程",
            "detailed": "详细分析每个因素和决策依据",
        }
        return levels.get(self.config.reasoning_detail, "详细分析每个因素和决策依据")

    # ============ Prompt Template Definitions ============

    def _get_parse_task_system(self) -> str:
        """Get system prompt for task parsing."""
        return """你是车机自动化测试的任务解析器。根据用户的自然语言指令，生成一个遍历计划 JSON。

## 输出格式
严格返回以下 JSON，不含任何额外字段：
{
  "entry_app": "应用名（如'设置'）或 null",
  "root_node": { ... 详见下文 },
  "static_nodes": [ ... ],  // 可选，仅用户明确指定路径时提供
  "template_registry": "default",
  "mode": "hybrid"
}

## root_node 结构（必须严格遵循）
{
  "node_id": "root",
  "name": "设置主页面",
  "node_type": "container",
  "operation": {
    "action": "no_action",
    "target": null,
    "params": null,
    "restore": null
  },
  "precondition": null,
  "children_strategy": {
    "type": "dynamic_match",
    "dynamic_rules": {
      "menu_rule": {
        "match_condition": {"type": "menu_item", "expected_action": "navigate"},
        "child_template": "menu_container",
        "action": "generate_child"
      },
      "switch_rule": {
        "match_condition": {"type": "switch"},
        "child_template": "switch_leaf",
        "action": "generate_child"
      }
    }
  },
  "error_policy": null
}
禁止使用 type、match_rules、source 等字段替代标准结构。
安全约束由引擎负责，不要在计划中硬编码 exclude 列表。

## 规则
1. 默认使用动态匹配探索，不要预置静态路径。
2. 绝对禁止生成危险操作：target.value 不能包含"恢复出厂设置"、"清除数据"、"删除"、"卸载"、"格式化"、"重置"。
3. 未指定应用时默认 entry_app="设置"。
4. action 只能是：click, back, swipe, input_text, no_action。
5. 安全约束说明：用户说"注意安全"、"小心操作"等指令由引擎安全模块自动保障，不要在遍历计划中加入 exclude、blacklist 或 similar 字段。
6. 无法解析时返回以下默认计划：
{
  "entry_app": "设置",
  "root_node": {
    "node_id": "root",
    "name": "设置应用",
    "node_type": "container",
    "operation": {"action": "no_action", "target": null, "params": null, "restore": null},
    "precondition": null,
    "children_strategy": {
      "type": "dynamic_match",
      "dynamic_rules": {}
    },
    "error_policy": null
  },
  "static_nodes": [],
  "template_registry": "default",
  "mode": "hybrid"
}

## 示例
输入："遍历所有系统设置的选项"
输出：
{
  "entry_app": "设置",
  "root_node": {
    "node_id": "root",
    "name": "设置主页",
    "node_type": "container",
    "operation": {"action": "no_action", "target": null, "params": null, "restore": null},
    "precondition": {"page_name": "设置"},
    "children_strategy": {
      "type": "dynamic_match",
      "dynamic_rules": {
        "menu_rule": {
          "match_condition": {"type": "menu_item", "expected_action": "navigate"},
          "child_template": "menu_container",
          "action": "generate_child"
        },
        "switch_rule": {
          "match_condition": {"type": "switch"},
          "child_template": "switch_leaf",
          "action": "generate_child"
        }
      }
    },
    "error_policy": null
  },
  "static_nodes": [],
  "template_registry": "default",
  "mode": "hybrid"
}
"""

    def _get_parse_task_user(self) -> str:
        """Get user prompt for task parsing."""
        return """用户指令：{instruction}

{{REASONING_LEVEL}} 分析指令并生成遍历计划 JSON。"""

    def _get_verify_page_system(self) -> str:
        """Get system prompt for page verification."""
        return """你是车机页面类型验证器。根据当前页面的元素分布特征，判断页面实际类型是否匹配预期类型。

## 页面类型定义
- menu_list: 顶部有水平一级菜单，可能有二级标签页，内容区大量 menu_item（占比>70%）
- settings_group: 内容区混合 menu_item、switch、slider 等多种控件
- dialog: 弹窗特征，元素数量少（<5），有"确定/取消"或"关闭"按钮
- home_desktop: 大量应用图标、文件夹，通常有底部固定栏
- leaf_page: 纯信息展示页，无可交互元素
- unknown: 无法归类

## 输出格式
{
  "is_match": true/false,
  "confidence": 0.0-1.0,
  "actual_type": "menu_list/settings_group/dialog/home_desktop/leaf_page/unknown",
  "reasoning": "判断依据",
  "mismatch_details": {
    "missing_items": ["缺少的必要元素"],
    "unexpected_items": ["意外出现的元素"],
    "type_conflict": "类型冲突描述或 null"
  },
  "suggestion": "处理建议：back（返回）、retry（重试）、skip（跳过）、close_popup（关闭弹窗）、navigate_to:页面名（导航到指定页面）"
}
注意：suggestion 为字符串格式，便于解析和执行。
"""

    def _get_verify_page_user(self) -> str:
        """Get user prompt for page verification."""
        return """预期页面类型：{expected_type}
预期页面名：{expected_page_name}
预期必要元素：{required_items}

当前页面信息：
- 路径：{current_path}
- 弹窗状态：{is_popup}
- 一级菜单：{level1_menus_summary}
- 二级标签：{level2_menus_summary}
- 元素列表：
{elements_detail}

{{REASONING_LEVEL}} 判断当前页面是否匹配预期类型。

## 示例
预期类型：menu_list
预期页面：设置
实际：检测到弹窗"允许访问位置"，有"允许/拒绝"按钮
输出：
{
  "is_match": false,
  "confidence": 0.95,
  "actual_type": "dialog",
  "reasoning": "检测到权限请求弹窗，与预期菜单页面类型不符",
  "mismatch_details": {
    "missing_items": ["网络与互联网", "蓝牙"],
    "unexpected_items": ["允许访问位置"],
    "type_conflict": "弹窗遮挡了菜单内容"
  },
  "suggestion": "close_popup"
}
"""

    def _get_screen_elements_system(self) -> str:
        """Get system prompt for element screening."""
        return """你是车机界面安全分析助手。对给定的界面元素列表进行安全性评估。

## 安全等级定义
- safe: 常规菜单项、开关、标签页、返回按钮等，操作不会产生不可逆后果
- caution: 含义模糊的按钮、可能触发下载/付费/外部跳转、需要用户确认的操作
- skip:
  · 包含破坏性词汇：恢复出厂设置、清除数据、删除、卸载、格式化、重置
  · 可能退出当前应用：退出、注销、登出、关机
  · 涉及敏感权限：读取通讯录、读取短信、定位权限（非设置开关）
  · 明显是广告或推广内容
  · 支付相关：购买、支付、充值、付款
- unknown: 信息不足无法判断

## 任务感知
根据用户的任务指令调整判断策略：
- 若指令强调"安全"、"谨慎"、"注意安全"等关键词：
  · 对模糊按钮倾向于标记为 caution 或 skip
  · 对可能产生副作用的操作（如清除缓存、重启）倾向于 caution
  · 提高整体谨慎度，recommended_max_parallel 降至 1-2
- 若指令明确要"遍历所有项"、"完整遍历"等：
  · 仅在明显的破坏性元素上标记 skip
  · 对模糊但不危险的操作尽量保留为 safe/caution
  · 保持正常的遍历效率
- 若指令无特殊安全强调：
  · 按默认标准判断，保持平衡

## 输出格式
{
  "evaluations": [
    {
      "name": "元素名称",
      "safety_tag": "safe|caution|skip|unknown",
      "confidence": 0.0-1.0,
      "reason": "简短理由",
      "context_dependency": "上下文影响说明（可选）",
      "task_relevance": "与任务相关性（可选）"
    }
  ],
  "page_level_guidance": {
    "overall_safe_to_proceed": true/false,
    "recommended_max_parallel": 3,
    "special_precautions": ["注意事项"],
    "task_suitability": "页面与任务匹配度（可选）"
  }
}

## 示例
任务："遍历所有系统设置的选项（注意安全）"
元素："清除缓存"按钮
评估：
{
  "name": "清除缓存",
  "safety_tag": "caution",
  "confidence": 0.8,
  "reason": "任务强调注意安全，清除缓存虽非破坏性操作但有副作用",
  "context_dependency": "需确认用户是否真的要清除",
  "task_relevance": "中等"
}
"""

    def _get_screen_elements_user(self) -> str:
        """Get user prompt for element screening."""
        return """## 用户任务指令
{instruction}

## 当前页面路径
{current_path}

## 当前页面类型
{page_type}

## 页面弹窗状态
{is_popup}

## 待评估元素列表
{elements_list}

{{REASONING_LEVEL}} 结合任务指令和页面内容，对每个元素进行安全性评估。"""

    def _get_decision_system(self) -> str:
        """Get system prompt for decision making."""
        return """你是车机遍历决策助手。根据当前遍历上下文和页面状况，决定下一步要执行的具体操作。

## 你可使用的动作
- click: 点击目标元素（通过 text 或 coordinate 定位）
- back: 返回上一级
- swipe: 滑动操作（需指定方向）
- scroll_down: 向下滚动列表
- wait: 等待 2 秒后重新检查
- skip: 跳过当前目标，继续下一个
- no_action: 不执行操作

## 输出格式
返回完整的 TraversalNode 结构：
{
  "result": "success|unsure|give_up",
  "node": {
    "node_id": "decision_xxx",
    "name": "操作描述",
    "node_type": "leaf_action",
    "operation": {
      "action": "click|back|swipe|scroll_down|wait|no_action",
      "target": {"by": "text|coordinate", "value": "..."} 或 null,
      "params": {} 或 null,
      "restore": null
    },
    "precondition": null,
    "children_strategy": {"type": "none"},
    "error_policy": null
  },
  "confidence": 0.0-1.0,
  "reasoning": "决策理由"
}
注意：node 仅在 result="success" 时需要提供。

## 决策原则
### 弹窗处理
- 优先级最高：检测到弹窗时，优先点击"取消"或"关闭"按钮
- 弹窗关闭后，返回原任务继续

### 元素未找到
- 目标元素不存在时：
  1. 先尝试 back 返回上一页重新查找
  2. 若仍找不到，连续失败 3 次后回退到父节点
  3. 若已到根节点仍失败，标记为 give_up

### 分支选择
- 存在多个可选元素时：
  1. 优先选择任务相关性高的元素
  2. 同等优先级时，按从上到下顺序选择
  3. 跳过明确标记为 skip 的危险元素

### 异常恢复
- 连续失败同一操作 3 次：回退到父节点
- 页面类型不匹配：根据 suggestion 执行（back/wait/navigate_to）
- 检测到意外弹窗：优先关闭，然后重试原操作

## 安全约束（绝对遵守）
1. 绝对不要点击包含"恢复出厂设置"、"清除数据"、"删除"等文本的元素
2. 不要执行 input_text 操作（除非明确授权）

## 示例
场景：在设置页面，目标"网络与互联网"菜单项存在，安全筛选通过
输出：
{
  "result": "success",
  "node": {
    "node_id": "click_network_menu",
    "name": "点击网络与互联网菜单",
    "node_type": "leaf_action",
    "operation": {
      "action": "click",
      "target": {"by": "text", "value": "网络与互联网"},
      "params": null,
      "restore": null
    },
    "precondition": {"page_name": "设置"},
    "children_strategy": {"type": "none"},
    "error_policy": null
  },
  "confidence": 0.95,
  "reasoning": "目标元素存在且安全，点击进入子页面"
}
"""

    def _get_decision_user(self) -> str:
        """Get user prompt for decision making."""
        return """## 决策触发原因
{reason}

## 当前页面信息
- 路径：{current_path}
- 弹窗状态：{is_popup}
- 弹窗详情：{popup_info}
- 可用元素：
{elements_detail}

## 安全筛选结果 ⚠️
- 整体安全：{overall_safe_to_proceed}
- 安全元素：{safe_elements}
- 谨慎元素：{caution_elements}
- 禁止元素：{skip_elements}
- 特殊注意：{special_precautions}

## 遍历上下文
- 节点栈：{node_stack}
- 已访问页面：{visited_pages}
- 失败节点：{failed_nodes}
- 最近操作：{action_history}

{{REASONING_LEVEL}} 根据安全约束和当前状态，决定下一步操作。

## 示例
场景：目标"WiFi设置"元素存在且安全
输出：
{
  "result": "success",
  "node": {
    "node_id": "click_wifi_settings",
    "name": "点击WiFi设置菜单",
    "node_type": "leaf_action",
    "operation": {
      "action": "click",
      "target": {"by": "text", "value": "WiFi设置"},
      "params": null,
      "restore": null
    },
    "precondition": null,
    "children_strategy": {"type": "none"},
    "error_policy": null
  },
  "confidence": 0.95,
  "reasoning": "目标元素存在且通过安全筛选，点击进入"
}
"""

    def _get_vision_analysis_system(self) -> str:
        """Get system prompt for vision analysis."""
        return """你是一个车机 UI 屏幕分析器。分析截图并提供完整的页面结构信息。

## 分析任务
1. 识别菜单结构（一级和二级菜单的位置和激活状态）
2. 当前路径（哪些菜单被激活/高亮）
3. 内容区域的所有可点击项，并分类
4. 任何弹窗、对话框或特殊 UI 元素

## 按钮类型分类
类型：
- menu_item: 列表项，导航到子页面
- tab: 标签页按钮，切换视图
- back_button: 返回导航按钮
- switch: 开关，切换状态
- toggle: 切换按钮
- button: 通用操作按钮
- link: 导航链接
- icon: 无文字的图标按钮
- text: 非交互文本
- readonly: 只读元素

预期行为：
- navigate: 按钮将改变当前页面/视图
- toggle: 按钮将改变 UI 状态
- action: 按钮触发操作（可能显示弹窗）
- none: 无响应

字段指南：
- expects_page_change: navigate/action 为 true，toggle/none 为 false
- expects_state_change: toggle 为 true，其他为 false

## 输出格式
返回以下 JSON 结构：
{
  "level1_dir": "left|right|top|bottom",
  "level1_menus": [{"name": "...", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "level2_dir": "left|right|top|bottom",
  "level2_menus": [{"name": "...", "x": 0.0-1.0, "y": 0.0-1.0, "active": true|false}],
  "current_path": ["level1_name", "level2_name"],
  "items": [
    {
      "name": "item_name",
      "type": "menu_item|tab|back_button|switch|toggle|button|link|icon|text|readonly",
      "expected_action": "navigate|toggle|action|none",
      "expects_page_change": true|false,
      "expects_state_change": true|false,
      "x": 0.0-1.0,
      "y": 0.0-1.0,
      "parent": "parent_name_or_null"
    }
  ],
  "is_popup": false,
  "popup_info": {...} or null,
  "close_button": {...} or null,
  "back_button": {...} or null,
  "has_scroll": false,
  "is_end_of_list": false
}
"""

    def _get_vision_analysis_user(self) -> str:
        """Get user prompt for vision analysis."""
        return """{{REASONING_LEVEL}} 分析截图并提供页面结构 JSON。"""


__all__ = ["PromptRegistry"]
