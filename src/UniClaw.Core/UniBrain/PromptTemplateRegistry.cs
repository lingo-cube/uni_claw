using System.Collections.Immutable;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// Prompt 模板单点真源 — 3 个模板集中定义，消除 5 个测试文件中的内联副本。
/// 设计决策: static properties（非 DI），测试和生产通过同一引用获取模板文本。
/// </summary>
public static class PromptTemplateRegistry
{
    public static PromptTemplate ParseInstruction { get; } = new(
        ModelCapabilities.ParseInstruction,
        "你是助手",
        "解析：{text} 上下文：{context}",
        ImmutableArray.Create("text", "context"));

    public static PromptTemplate DecideNextAction { get; } = new(
        ModelCapabilities.DecideNextAction,
        SystemPrompt: "You are a mobile UI traversal decision advisor. Given a goal, the current page state (JSON), the current node id, and traversal depth, decide the single next action that best advances the goal. Respond ONLY with a JSON object: result (one of Success/Unsure/GiveUp), action (verb such as tap/scroll/input/back/wait), target (element id or null), params (flat object of primitive values, optional), reasoning (one sentence), confidence (0-1), safety_verified (boolean).",
        UserPrompt: "Goal: {goal}\n\nCurrent page analysis (JSON):\n{page_analysis}\n\nCurrent node id: {current_node_id}\nTraversal depth: {depth}\n\nDecide the next action.",
        Variables: ImmutableArray.Create("goal", "page_analysis", "current_node_id", "depth"));

    public static PromptTemplate AnalyzeVisual { get; } = new(
        ModelCapabilities.AnalyzeVisual,
        SystemPrompt: "You are analyzing a mobile app screen for UI traversal. Analyze this screenshot and provide: " +
            "(1) menu structure (level 1 and level 2 menus with positions and active state), " +
            "(2) current path (which menus are active/highlighted), " +
            "(3) all clickable items in the content area each classified by `type`, " +
            "(4) any popups/dialogs/special UI elements. " +
            "Item `type` vocabulary (exactly one per item): `menu_item` (list items navigating to sub-pages), " +
            "`tab` (top-level view switch), `back_button` (back/return), `switch` (on/off toggle with sliding animation), " +
            "`toggle` (state-toggle buttons e.g. favorite), `button` (generic action), " +
            "`link` (navigation links/hypertext), `icon` (icon-only buttons), `text` (non-interactive text), " +
            "`readonly` (display-only). " +
            "Return ONLY JSON with this exact structure (coordinates normalized 0-1): " +
            "{ \"level1_dir\": \"left|right|top|bottom\", \"level1_menus\": [{\"name\",\"coordinate\":{\"x\",\"y\"},\"active\"}], " +
            "\"level2_dir\": \"left|right|top|bottom\", \"level2_menus\": [/* same shape */], " +
            "\"current_path\": [\"...\"], \"items\": [{\"name\",\"type\",\"coordinate\":{\"x\",\"y\"},\"parent\"}], " +
            "\"is_popup\": false, \"popup_info\": {\"title\",\"content\",\"close_button\":{\"x\",\"y\"}} or null, " +
            "\"close_button\": {\"x\",\"y\"} or null, \"back_button\": {\"x\",\"y\"} or null, " +
            "\"has_scroll\": false, \"is_end_of_list\": false }. " +
            "Important: coordinates normalized 0-1; mark parent-child via `parent`; `current_path` indicates active menus; " +
            "name icons like \"[icon] description\"; include all interactive elements; " +
            "level1_dir/level2_dir MUST be a single value from left/right/top/bottom (NEVER pipe-separated; choose ONE).",
        UserPrompt: "Analyze the current app screenshot and return the PageAnalysis JSON above.",
        Variables: ImmutableArray<string>.Empty);
}
