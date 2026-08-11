using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// The caller's business expression. It deliberately carries no UI or execution detail.
/// </summary>
public sealed record BusinessIntent
{
    /// <summary>Creates an intent from its nonblank caller expression.</summary>
    public BusinessIntent(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        Expression = expression;
    }

    /// <summary>Exact caller expression; it is not an execution instruction.</summary>
    public string Expression { get; }
}

/// <summary>
/// The deterministic result of compiling a bounded business expression.
/// </summary>
public abstract record IntentCompilationResult
{
    private IntentCompilationResult()
    {
    }

    /// <summary>A complete semantic goal, with no execution representation attached.</summary>
    public sealed record Resolved : IntentCompilationResult
    {
        /// <summary>Creates a resolved, executable semantic receipt.</summary>
        public Resolved(BusinessIntent intent, SemanticGoalInput goal)
        {
            ArgumentNullException.ThrowIfNull(intent);
            ArgumentNullException.ThrowIfNull(goal);
            Intent = intent;
            Goal = goal;
        }

        /// <summary>Original caller intent.</summary>
        public BusinessIntent Intent { get; }
        /// <summary>Exactly one compiled semantic goal.</summary>
        public SemanticGoalInput Goal { get; }
    }

    /// <summary>An explicit non-executable receipt. It intentionally contains no goal.</summary>
    public sealed record Insufficient : IntentCompilationResult
    {
        /// <summary>Creates a non-executable insufficiency receipt.</summary>
        public Insufficient(BusinessIntent intent, string reason)
        {
            ArgumentNullException.ThrowIfNull(intent);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            Intent = intent;
            Reason = reason;
        }

        /// <summary>Original caller intent.</summary>
        public BusinessIntent Intent { get; }
        /// <summary>Explicit reason no executable goal was compiled.</summary>
        public string Reason { get; }
    }
}

/// <summary>
/// Bounded, stateless compilation from caller wording to an already-supported semantic goal.
/// It never observes the world, chooses UI work, or creates a route.
/// </summary>
public static class IntentCompiler
{
    /// <summary>
    /// Resolves exactly one declared object alias and one supported Enabled state term.
    /// The alias map is keyed by SemanticObject identity and contains caller/domain aliases.
    /// </summary>
    public static IntentCompilationResult Compile(
        BusinessIntent intent,
        ImmutableArray<SemanticObject> objects,
        ImmutableDictionary<string, string> objectAliases)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var normalized = Normalize(intent.Expression);
        var matchedObjects = objects
            .Where(obj => objectAliases.TryGetValue(obj.Identity, out var alias)
                && ContainsAlias(normalized, Normalize(alias)))
            .ToArray();

        if (matchedObjects.Length != 1)
            return Insufficient(intent, matchedObjects.Length == 0
                ? "No declared object alias was found."
                : "More than one declared object alias was found.");

        var trueRequested = ContainsTrueState(normalized);
        var falseRequested = ContainsFalseState(normalized);
        if (trueRequested == falseRequested)
            return Insufficient(intent, trueRequested
                ? "Conflicting Enabled state terms were found."
                : "No supported Enabled state term was found.");

        var obj = matchedObjects[0];
        if (!obj.StateDimensions.Contains("Enabled", StringComparer.Ordinal))
            return Insufficient(intent, $"Object '{obj.Identity}' does not declare the Enabled dimension.");

        return new IntentCompilationResult.Resolved(
            intent,
            new SemanticGoalInput(obj.Identity, "Enabled", trueRequested));
    }

    private static IntentCompilationResult.Insufficient Insufficient(BusinessIntent intent, string reason)
        => new(intent, reason);

    private static bool ContainsAlias(string expression, string alias)
        => ContainsPhrase(expression, alias);

    private static bool ContainsTrueState(string expression)
        => ContainsPhrase(expression, "enable")
            || ContainsPhrase(expression, "turn on")
            || ContainsPhrase(expression, "开启")
            || ContainsPhrase(expression, "打开")
            || ContainsPhrase(expression, "on");

    private static bool ContainsFalseState(string expression)
        => ContainsPhrase(expression, "disable")
            || ContainsPhrase(expression, "turn off")
            || ContainsPhrase(expression, "关闭")
            || ContainsPhrase(expression, "off");

    private static bool ContainsPhrase(string expression, string phrase)
    {
        if (string.IsNullOrEmpty(phrase))
            return false;

        var searchFrom = 0;
        while (searchFrom <= expression.Length - phrase.Length)
        {
            var index = expression.IndexOf(phrase, searchFrom, StringComparison.Ordinal);
            if (index < 0)
                return false;

            var beforeIsBoundary = index == 0 || !IsAsciiWordCharacter(expression[index - 1]);
            var afterIndex = index + phrase.Length;
            var afterIsBoundary = afterIndex == expression.Length || !IsAsciiWordCharacter(expression[afterIndex]);
            if (beforeIsBoundary && afterIsBoundary)
                return true;

            searchFrom = index + 1;
        }

        return false;
    }

    private static bool IsAsciiWordCharacter(char character)
        => character is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var buffer = new char[normalized.Length];
        var position = 0;
        var pendingSpace = false;
        foreach (var character in normalized)
        {
            var category = char.GetUnicodeCategory(character);
            if (char.IsWhiteSpace(character)
                || category is UnicodeCategory.ConnectorPunctuation
                    or UnicodeCategory.DashPunctuation
                    or UnicodeCategory.OpenPunctuation
                    or UnicodeCategory.ClosePunctuation
                    or UnicodeCategory.InitialQuotePunctuation
                    or UnicodeCategory.FinalQuotePunctuation
                    or UnicodeCategory.OtherPunctuation
                    or UnicodeCategory.MathSymbol
                    or UnicodeCategory.CurrencySymbol
                    or UnicodeCategory.ModifierSymbol
                    or UnicodeCategory.OtherSymbol)
            {
                pendingSpace = position > 0;
                continue;
            }

            if (pendingSpace)
            {
                buffer[position++] = ' ';
                pendingSpace = false;
            }
            buffer[position++] = character;
        }
        return new string(buffer, 0, position);
    }
}
