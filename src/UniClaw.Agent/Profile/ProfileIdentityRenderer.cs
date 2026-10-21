namespace UniClaw.Agent.Profile;

/// <summary>Deterministically renders the Profile charter into the identity
/// prompt segment. This deliberately small format is mirrored by
/// tools/render-profile-identity.py.</summary>
public static class ProfileIdentityRenderer
{
    /// <summary>Renders the charter using the canonical identity segment format.</summary>
    public static string Render(ProfileCharter charter)
    {
        ArgumentNullException.ThrowIfNull(charter);
        return string.Join('\n',
            "# UniAgent",
            "Role: " + charter.Role,
            "## Responsibilities",
            RenderItems(charter.Responsibilities),
            "## Boundaries",
            RenderItems(charter.Boundaries),
            "## Planning principles",
            RenderItems(charter.PlanningPrinciples),
            "## Skill use",
            RenderItems(charter.SkillUse),
            "");
    }

    /// <summary>Fails closed when a prompt identity differs from its Profile charter.</summary>
    public static void EnsureMatches(ProfileCharter charter, string promptText, string source)
    {
        var expected = Render(charter);
        if (!string.Equals(promptText, expected, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"identity-charter-mismatch:{source}: identity prompt must be rendered from the Profile charter");
    }

    private static string RenderItems(IReadOnlyList<string> items) =>
        string.Join('\n', items.Select(static item => "- " + item));
}
