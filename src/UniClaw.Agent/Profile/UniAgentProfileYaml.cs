using UniClaw.Kernel.Capability;

namespace UniClaw.Agent.Profile;

/// <summary>
/// PRF-002/ADR-0041 — host-neutral 产品 UniAgent Profile loader。读取
/// <c>product/profiles/uniagent-prod.yaml</c>（或 <c>UNICLAW_UNIAGENT_PROD_CONFIG</c>
/// 指定路径），对编译身份（<see cref="UniagentProdProfile"/> +
/// <see cref="CapabilityManifest"/>）fail-closed 交叉核对，模型角色值域
/// 对编译 <see cref="LogicalProfileId"/> 词汇校验。本 loader 与其加载的文件
/// 永不出现 provider/model 名或服务端点（那是宿主 realization 绑定）。
/// </summary>
public static class UniAgentProfileYaml
{
    public const string SchemaVersion = "uniagent.profile/v1";
    public const string DefaultConfigRelativePath = "product/profiles/uniagent-prod.yaml";
    public const string ConfigPathEnvironmentVariable = "UNICLAW_UNIAGENT_PROD_CONFIG";

    /// <summary>Loads from the environment-configured path, or the repository
    /// profile file discovered by walking up from the current assembly.</summary>
    public static UniAgentProfile LoadDefault()
    {
        var configured = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
        return Load(configured is { Length: > 0 } ? configured : ResolveDefaultPath());
    }

    public static string ResolveDefaultPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, DefaultConfigRelativePath);
            if (File.Exists(candidate))
                return candidate;
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                throw new FileNotFoundException(
                    $"uniagent-prod profile not found at repository root: {DefaultConfigRelativePath}");
            directory = directory.Parent!;
        }
        throw new FileNotFoundException(
            $"uniagent-prod profile not found; set {ConfigPathEnvironmentVariable} or run from the repository");
    }

    public static UniAgentProfile Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("profile path is required", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("uniagent-prod profile file not found", path);

        var document = ProfileYamlDocument.Parse(File.ReadAllLines(path));

        var schemaVersion = document.RequireScalar(new[] { "schemaVersion" });
        if (!string.Equals(schemaVersion, SchemaVersion, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"config-profile-mismatch:schemaVersion:{schemaVersion}!={SchemaVersion} ({path})");

        // The compiled profile identity is the authority; a profile file
        // claiming a different identity is drift and fails closed.
        var profileId = document.RequireScalar(new[] { "profileId" });
        var profileVersion = document.RequireScalar(new[] { "profileVersion" });
        if (!string.Equals(profileId, UniagentProdProfile.ProfileId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"config-profile-mismatch:profileId:{profileId}!={UniagentProdProfile.ProfileId} ({path})");
        if (!string.Equals(profileVersion, UniagentProdProfile.ProfileVersion, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"config-profile-mismatch:profileVersion:{profileVersion}!={UniagentProdProfile.ProfileVersion} ({path})");
        var capabilities = document.RequireList(new[] { "capabilities" });
        var expectedCapabilities = CapabilityManifest.ProductHeadless.NormalizedCapabilities;
        var reportedCapabilities = capabilities
            .Where(static c => !string.IsNullOrWhiteSpace(c))
            .Select(static c => c.Trim())
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!expectedCapabilities.SequenceEqual(reportedCapabilities, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"config-profile-mismatch:capabilities ({path}): expected [{string.Join(",", expectedCapabilities)}]");

        // profileRevision: declaration site (Q8); pinning enforcement lands in
        // PRF-005. Must be a positive integer so later enforcement can bind.
        var revisionText = document.RequireScalar(new[] { "profileRevision" });
        if (!int.TryParse(revisionText, out var revision) || revision < 1)
            throw new InvalidOperationException(
                $"config-invalid:profileRevision:{revisionText} ({path}): positive integer required");

        var name = document.RequireScalar(new[] { "identity", "name" });
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException($"config-invalid:identity.name ({path})");

        return new UniAgentProfile(
            UniagentProdProfile.Current,
            revision,
            ReadModelRoles(document, path),
            ReadAssembly(document, path),
            ReadAllowedSkillRefs(document, path));
    }

    private static IReadOnlyList<SkillReference> ReadAllowedSkillRefs(
        ProfileYamlDocument document, string profilePath)
    {
        var node = document.TryWalk(new[] { "allowedSkillRefs" });
        if (node is null)
            return Array.Empty<SkillReference>();
        if (node is not Dictionary<string, object> entries || entries.Count == 0)
            throw new InvalidOperationException($"config-invalid:allowedSkillRefs ({profilePath}): non-empty mapping required");

        var repoRoot = ResolveRepoRoot();
        var result = new List<SkillReference>(entries.Count);
        foreach (var (name, value) in entries)
        {
            if (string.IsNullOrWhiteSpace(name) || value is not Dictionary<string, object> entry)
                throw new InvalidOperationException($"config-invalid:allowedSkillRefs.{name} ({profilePath})");
            var revisionText = RequireEntryScalar(entry, "revision", $"allowedSkillRefs.{name}", profilePath);
            if (!int.TryParse(revisionText, out var revision) || revision < 1)
                throw new InvalidOperationException($"config-invalid:allowedSkillRefs.{name}.revision ({profilePath}): positive integer required");
            var skillPath = RequireEntryScalar(entry, "path", $"allowedSkillRefs.{name}", profilePath);
            var sha256 = RequireEntryScalar(entry, "sha256", $"allowedSkillRefs.{name}", profilePath).ToLowerInvariant();
            if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
                throw new InvalidOperationException($"config-invalid:allowedSkillRefs.{name}.sha256 ({profilePath}): 64-hex hash required");
            EnsureSafeRelativePath(skillPath, $"allowedSkillRefs.{name}.path", profilePath);
            var reference = new SkillReference(name, revision, skillPath, sha256);
            VerifySkill(reference, repoRoot, profilePath);
            result.Add(reference);
        }
        return result;
    }

    private static string RequireEntryScalar(
        Dictionary<string, object> entry, string key, string field, string profilePath)
    {
        if (!entry.TryGetValue(key, out var value) || value is not string text || string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"config-missing:{field}.{key} ({profilePath})");
        return text;
    }

    private static void VerifySkill(SkillReference reference, string repoRoot, string profilePath)
    {
        var directory = ResolveWithinRoot(repoRoot, reference.Path, $"allowedSkillRefs.{reference.Name}.path", profilePath);
        var manifestPath = Path.Combine(directory, "manifest.yaml");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"skill-not-found:allowedSkillRefs.{reference.Name} ({profilePath})");

        var manifest = ProfileYamlDocument.Parse(File.ReadAllLines(manifestPath));
        var schema = manifest.RequireScalar(new[] { "schemaVersion" });
        if (!string.Equals(schema, "uniagent.skill/v1", StringComparison.Ordinal))
            throw new InvalidOperationException($"skill-invalid:{reference.Name}.schema ({profilePath})");
        var manifestName = manifest.RequireScalar(new[] { "name" });
        if (!string.Equals(manifestName, reference.Name, StringComparison.Ordinal))
            throw new InvalidOperationException($"skill-name-mismatch:{reference.Name}!={manifestName} ({profilePath})");
        var manifestRevisionText = manifest.RequireScalar(new[] { "revision" });
        if (!int.TryParse(manifestRevisionText, out var manifestRevision) || manifestRevision < 1
            || manifestRevision != reference.Revision)
            throw new InvalidOperationException($"skill-revision-mismatch:{reference.Name} ({profilePath})");

        var guidance = manifest.RequireScalar(new[] { "guidance" });
        EnsureSafeRelativePath(guidance, $"skill.{reference.Name}.guidance", profilePath);
        if (!string.Equals(guidance, "guidance.md", StringComparison.Ordinal))
            throw new InvalidOperationException($"skill-guidance-mismatch:{reference.Name} ({profilePath})");
        var guidancePath = ResolveWithinRoot(directory, guidance, $"skill.{reference.Name}.guidance", profilePath);
        if (!File.Exists(guidancePath))
            throw new InvalidOperationException($"skill-guidance-not-found:{reference.Name} ({profilePath})");

        var referencePaths = manifest.TryWalk(new[] { "references" }) switch
        {
            null or "" => Array.Empty<string>(),
            string[] values => values,
            _ => throw new InvalidOperationException($"skill-invalid:{reference.Name}.references ({profilePath})")
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var stream = new MemoryStream();
        stream.Write(File.ReadAllBytes(manifestPath));
        stream.Write(File.ReadAllBytes(guidancePath));
        foreach (var relative in referencePaths)
        {
            if (string.IsNullOrWhiteSpace(relative) || !seen.Add(relative))
                throw new InvalidOperationException($"skill-invalid:{reference.Name}.references duplicate/empty ({profilePath})");
            var referenceFile = ResolveWithinRoot(directory, relative, $"skill.{reference.Name}.references", profilePath);
            if (!File.Exists(referenceFile))
                throw new InvalidOperationException($"skill-reference-not-found:{reference.Name}:{relative} ({profilePath})");
            stream.Write(File.ReadAllBytes(referenceFile));
        }
        var computed = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray())).ToLowerInvariant();
        if (!string.Equals(computed, reference.Sha256, StringComparison.Ordinal))
            throw new InvalidOperationException($"skill-hash-mismatch:{reference.Name} ({profilePath})");
    }

    private static void EnsureSafeRelativePath(string path, string field, string profilePath)
    {
        if (Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(static part => part is "" or "." or ".."))
            throw new InvalidOperationException($"config-invalid:{field}: path must be repository-relative ({profilePath})");
    }

    private static string ResolveWithinRoot(string root, string relative, string field, string profilePath)
    {
        EnsureSafeRelativePath(relative, field, profilePath);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(fullRoot, StringComparison.Ordinal))
            throw new InvalidOperationException($"config-invalid:{field}: path escapes root ({profilePath})");
        return full;
    }

    /// <summary>assembly（PRF-005）：产品工件引用，路径相对仓库根。一旦
    /// 声明即 fail-closed 执法：文件必须存在，hash 必须与工件内容指纹一致
    /// （promptManifest=目录 digest，与 tools/prompt-manifest-hash.py 同一
    /// canonical 规则；safetyPolicy=文件字节 sha256）。</summary>
    private static ProfileAssembly? ReadAssembly(ProfileYamlDocument document, string path)
    {
        if (document.TryWalk(new[] { "assembly" }) is not Dictionary<string, object> assembly)
            return null;

        AssemblyReference? ReadReference(object? node, string field)
        {
            if (node is null or "")
                return null;
            if (node is not Dictionary<string, object> entry
                || !entry.TryGetValue("path", out var pathValue) || pathValue is not string referencePath
                || string.IsNullOrWhiteSpace(referencePath)
                || !entry.TryGetValue("hash", out var hashValue) || hashValue is not string referenceHash
                || referenceHash.Length != 64
                || !referenceHash.All(static c => Uri.IsHexDigit(c)))
                throw new InvalidOperationException(
                    $"config-invalid:assembly.{field} ({path}): path and 64-hex hash required");
            return new AssemblyReference(referencePath, referenceHash.ToLowerInvariant());
        }

        var promptManifest = ReadReference(assembly.GetValueOrDefault("promptManifest"), "promptManifest");
        var safetyPolicy = ReadReference(assembly.GetValueOrDefault("safetyPolicy"), "safetyPolicy");
        var result = new ProfileAssembly(promptManifest, safetyPolicy);

        var repoRoot = ResolveRepoRoot();
        if (result.PromptManifest is not null)
            VerifyPromptManifest(result.PromptManifest, repoRoot, path);
        if (result.SafetyPolicy is not null)
            VerifySafetyPolicy(result.SafetyPolicy, repoRoot, path);
        return result;
    }

    private static void VerifyPromptManifest(AssemblyReference reference, string repoRoot, string profilePath)
    {
        var directory = Path.GetFullPath(Path.Combine(repoRoot, reference.Path));
        if (!Directory.Exists(directory))
            throw new InvalidOperationException(
                $"assembly-not-found:assembly.promptManifest.path:{reference.Path} ({profilePath})");
        var recordedPath = Path.Combine(directory, "prompt-hash.txt");
        if (!File.Exists(recordedPath))
            throw new InvalidOperationException(
                $"assembly-invalid:assembly.promptManifest: prompt-hash.txt missing under {reference.Path} ({profilePath})");
        var recorded = File.ReadAllText(recordedPath).Trim();
        if (!string.Equals(recorded, reference.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"assembly-hash-mismatch:assembly.promptManifest (profile {reference.Hash[..8]}… != artifact {recorded[..8]}…; "
                + $"bump profileRevision and refresh the reference) [{profilePath}]");

        // Full recompute (manifest.json bytes + segments in declared order) —
        // canonical rule identical to tools/prompt-manifest-hash.py.
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"assembly-invalid:assembly.promptManifest: manifest.json missing ({profilePath})");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!document.RootElement.TryGetProperty("segments", out var segments)
            || segments.ValueKind != System.Text.Json.JsonValueKind.Array
            || segments.GetArrayLength() == 0)
            throw new InvalidOperationException(
                $"assembly-invalid:assembly.promptManifest: manifest.segments must be a non-empty list ({profilePath})");
        using var stream = new MemoryStream();
        stream.Write(File.ReadAllBytes(manifestPath));
        foreach (var segment in segments.EnumerateArray())
        {
            var segmentPath = Path.Combine(directory, segment.GetString() ?? "");
            if (!File.Exists(segmentPath))
                throw new InvalidOperationException(
                    $"assembly-invalid:assembly.promptManifest: segment missing: {segment.GetString()} ({profilePath})");
            stream.Write(File.ReadAllBytes(segmentPath));
        }
        stream.Position = 0;
        var computed = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(computed, reference.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"assembly-hash-mismatch:assembly.promptManifest (declared {reference.Hash[..8]}… != recomputed {computed[..8]}…; "
                + $"regenerate via tools/prompt-manifest-hash.py) [{profilePath}]");
    }

    private static void VerifySafetyPolicy(AssemblyReference reference, string repoRoot, string profilePath)
    {
        var policyPath = Path.GetFullPath(Path.Combine(repoRoot, reference.Path));
        if (!File.Exists(policyPath))
            throw new InvalidOperationException(
                $"assembly-not-found:assembly.safetyPolicy.path:{reference.Path} ({profilePath})");
        var computed = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(policyPath))).ToLowerInvariant();
        if (!string.Equals(computed, reference.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"assembly-hash-mismatch:assembly.safetyPolicy (declared {reference.Hash[..8]}… != file {computed[..8]}…; "
                + $"bump safetyPolicyRevision and refresh the reference) [{profilePath}]");
    }

    /// <summary>仓库根（AGENTS.md + solution marker），自当前程序集位置向上
    /// 解析——与默认 profile 发现同规则；装配引用路径相对该根。</summary>
    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("repo root not found above the assembly location");
    }

    /// <summary>modelRoles：key 必须是编译 LogicalProfileId 值域（产品词汇，
    /// Q4）；未知角色/缺 required 字段/非布尔值一律 fail-closed。</summary>
    private static IReadOnlyList<ModelRoleDeclaration> ReadModelRoles(
        ProfileYamlDocument document, string path)
    {
        if (document.TryWalk(new[] { "modelRoles" }) is not Dictionary<string, object> roles
            || roles.Count == 0)
            throw new InvalidOperationException($"config-missing:modelRoles ({path})");

        // 编译值域：LogicalProfileId 的静态属性是唯一词汇源。
        var knownRoles = new[]
        {
            LogicalProfileId.AgentDecision.Value,
            LogicalProfileId.Text.Value,
            LogicalProfileId.Visual.Value,
        };
        var declared = new List<ModelRoleDeclaration>();
        foreach (var (role, value) in roles)
        {
            if (!knownRoles.Contains(role, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"model-role-invalid: unknown role '{role}' ({path}); "
                    + $"valid: {string.Join(", ", knownRoles.Order(StringComparer.Ordinal))}");
            if (value is not Dictionary<string, object> entry
                || !entry.TryGetValue("required", out var requiredText)
                || requiredText is not string required
                || !bool.TryParse(required, out var isRequired))
                throw new InvalidOperationException(
                    $"model-role-invalid:{role}.required ({path}): boolean required");
            declared.Add(new ModelRoleDeclaration(role, isRequired));
        }
        return declared;
    }
}
