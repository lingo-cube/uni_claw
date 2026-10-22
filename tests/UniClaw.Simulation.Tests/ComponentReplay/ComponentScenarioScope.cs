using System.Text.Json;

namespace UniClaw.Simulation.Tests.ComponentReplay;

internal static class ComponentScenarioScope
{
    public static IReadOnlyList<string> Validate(JsonElement data)
    {
        var errors = new List<string>();
        static JsonElement Child(JsonElement obj, string key) => obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var p) ? p : default;
        static bool IsString(JsonElement obj, string key, string value) { var p = Child(obj, key); return p.ValueKind == JsonValueKind.String && p.GetString() == value; }
        var scope = Child(data, "testScope");
        var component = scope.ValueKind == JsonValueKind.String && scope.GetString() == "component";
        if (scope.ValueKind != JsonValueKind.Undefined && !component) errors.Add("testScope 仅允许 component");
        var decision = Child(data, "agentDecisionRealization");
        if (component)
        {
            if (!IsString(data, "source", "component-test")) errors.Add("component 要求 source=component-test");
            if (!IsString(Child(data, "execution"), "kind", "none")) errors.Add("component 要求 execution.kind=none");
            if (!IsString(data, "agentDecisionRealization", "not-applicable")) errors.Add("component 要求 agentDecisionRealization=not-applicable");
            if (!IsString(data, "goalEvaluationRealization", "not-applicable")) errors.Add("component 要求 goalEvaluationRealization=not-applicable");
            var e = Child(data, "expectations");
            if (!IsString(e, "status", "not-applicable")) errors.Add("component 要求 expectations.status=not-applicable");
            var c = Child(e, "agentConsultations");
            if (c.ValueKind != JsonValueKind.Number || !c.TryGetInt32(out var n) || n != 0) errors.Add("component 要求 expectations.agentConsultations=0");
            if (Child(e, "goalSatisfaction").ValueKind != JsonValueKind.Null) errors.Add("component 要求 expectations.goalSatisfaction=null");
            if (Child(e, "classification").ValueKind != JsonValueKind.Null) errors.Add("component 要求 expectations.classification=null");
            var components = Child(data, "components");
            if (components.ValueKind != JsonValueKind.Array || components.GetArrayLength() == 0 || components.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String || x.GetString() is "agent" or "uni-agent")) errors.Add("component 要求非空 components 且不得包含 agent/uni-agent");
        }
        else if (decision.ValueKind != JsonValueKind.String || decision.GetString() is not ("real" or "double")) errors.Add("非 component 场景 agentDecisionRealization 必须为 real 或 double");
        return errors;
    }
}
