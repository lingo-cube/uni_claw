namespace UniClaw.Core.UniBrain;

/// <summary>
/// Schemas — UniBrain 子接口消费的结构化输出 JSON schema 常量。
/// 经 ModelRequest.Schema 透传；传输 provider 把非 null Schema 视为请求结构化（json_object）输出的信号。
/// </summary>
public static class Schemas
{
    /// <summary>
    /// TextUnderstanding parse_instruction 输出 schema：
    /// { category, confidence (0-1), entities[], summary }。
    /// </summary>
    public const string ParseInstruction = """
        {
          "type": "object",
          "properties": {
            "category": { "type": "string" },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
            "entities": { "type": "array", "items": { "type": "string" } },
            "summary": { "type": "string" }
          },
          "required": ["category", "confidence", "entities", "summary"]
        }
        """;

    /// <summary>
    /// TraversalAdvisor decide_next_action 输出 schema：
    /// { result (enum Success/Unsure/GiveUp), action, target, params (flat object), reasoning, confidence (0-1), safety_verified }。
    /// result/confidence 必填；DecisionResult 3 值锁定（仅引用，不新增 enum）。
    /// </summary>
    public const string DecideNextAction = """
        {
          "type": "object",
          "properties": {
            "result": { "type": "string", "enum": ["Success", "Unsure", "GiveUp"] },
            "action": { "type": "string" },
            "target": { "type": "string" },
            "params": { "type": "object" },
            "reasoning": { "type": "string" },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
            "safety_verified": { "type": "boolean" }
          },
          "required": ["result", "confidence"]
        }
        """;

    /// <summary>
    /// PageAnalyzer analyze_visual 输出 schema：
    /// { level1_dir (enum left/right/top/bottom), level1_menus[], level2_dir, level2_menus[],
    ///   current_path[], items[] (name/type/coordinate/parent only — 不含 expected_action/expects_*),
    ///   is_popup, popup_info?, close_button?, back_button?, has_scroll, is_end_of_list }。
    /// items.type 不在 schema enum 硬约束（ElementTypeMapper 宽松映射 + 回落）。
    /// popup_info/close_button/back_button 可空。
    /// </summary>
    public const string AnalyzeVisual = """
        {
          "type": "object",
          "properties": {
            "level1_dir": { "type": "string", "enum": ["left", "right", "top", "bottom"] },
            "level1_menus": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "coordinate": {
                    "type": "object",
                    "properties": {
                      "x": { "type": "number" },
                      "y": { "type": "number" }
                    },
                    "required": ["x", "y"]
                  },
                  "active": { "type": "boolean" }
                },
                "required": ["name", "coordinate"]
              }
            },
            "level2_dir": { "type": "string", "enum": ["left", "right", "top", "bottom"] },
            "level2_menus": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "coordinate": {
                    "type": "object",
                    "properties": {
                      "x": { "type": "number" },
                      "y": { "type": "number" }
                    },
                    "required": ["x", "y"]
                  },
                  "active": { "type": "boolean" }
                },
                "required": ["name", "coordinate"]
              }
            },
            "current_path": { "type": "array", "items": { "type": "string" } },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "type": { "type": "string" },
                  "coordinate": {
                    "type": "object",
                    "properties": {
                      "x": { "type": "number" },
                      "y": { "type": "number" }
                    },
                    "required": ["x", "y"]
                  },
                  "parent": { "type": "string" }
                },
                "required": ["name", "type", "coordinate"]
              }
            },
            "is_popup": { "type": "boolean" },
            "popup_info": {
              "type": "object",
              "properties": {
                "title": { "type": "string" },
                "content": { "type": "string" },
                "close_button": {
                  "type": "object",
                  "properties": {
                    "x": { "type": "number" },
                    "y": { "type": "number" }
                  },
                  "required": ["x", "y"]
                }
              }
            },
            "close_button": {
              "type": "object",
              "properties": {
                "x": { "type": "number" },
                "y": { "type": "number" }
              },
              "required": ["x", "y"]
            },
            "back_button": {
              "type": "object",
              "properties": {
                "x": { "type": "number" },
                "y": { "type": "number" }
              },
              "required": ["x", "y"]
            },
            "has_scroll": { "type": "boolean" },
            "is_end_of_list": { "type": "boolean" }
          },
          "required": ["items", "is_popup", "has_scroll", "is_end_of_list"]
        }
        """;
}
