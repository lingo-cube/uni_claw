using System.Text.RegularExpressions;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// ARCH-DOC-015 — docs/ 放置规则的确定性执法（FROZEN 规则的执行体）。
/// 五目录分类学：prd（LOCKED/ACCEPTED）/ design（候选）/ analysis
/// （调查，Authority: NONE）/ architecture（FROZEN/CLOSED/COMPONENT-
/// BASELINE）/ adr（文件名 \d{4}-）。各目录 README.md（索引本体）豁免。
/// 违规 = RED；不允许豁免提交（docs/README.md §4）。
/// </summary>
public sealed class DocsMetadataTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private static IEnumerable<(string Path, string Text)> MarkdownFiles(string subdirectory)
    {
        var root = Path.Combine(RepoRoot(), "docs", subdirectory);
        if (!Directory.Exists(root))
            return [];
        return Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            .Select(path => (path, File.ReadAllText(path)));
    }

    private static string Relative(string path) => path[(RepoRoot().Length + 1)..];

    [Fact]
    public void AnalysisAndDesign_DeclareStatusAndNoneAuthority_NoFrozenWords()
    {
        var violations = new List<string>();
        foreach (var directory in new[] { "analysis", "design" })
        {
            foreach (var (path, text) in MarkdownFiles(directory))
            {
                var firstLines = string.Join('\n', text.Split('\n').Take(40));
                if (!Regex.IsMatch(firstLines, @"^>\s*Status[:：]", RegexOptions.Multiline))
                    violations.Add($"{Relative(path)}: 缺 '> Status:' 头");
                if (!Regex.IsMatch(firstLines, @"^>\s*Authority[:：]", RegexOptions.Multiline))
                    violations.Add($"{Relative(path)}: 缺 '> Authority:' 头");
                else if (!Regex.IsMatch(firstLines, @"^>\s*Authority[:：].*\bNONE\b", RegexOptions.Multiline))
                    violations.Add($"{Relative(path)}: Authority 非 NONE");
                var statusLine = firstLines.Split('\n')
                    .FirstOrDefault(l => Regex.IsMatch(l, @"^>\s*Status[:：]")) ?? "";
                if (Regex.IsMatch(statusLine, @"\bFROZEN\b") || Regex.IsMatch(statusLine, @"\bCLOSED\b"))
                    violations.Add($"{Relative(path)}: 候选目录声明冻结态（{statusLine.Trim()}）");
            }
        }
        Assert.True(violations.Count == 0,
            "docs 分类学违规（docs/README.md §2/§3）：\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Architecture_DeclaresFrozenAuthority()
    {
        var violations = MarkdownFiles("architecture")
            .Where(entry => !entry.Text.Contains("FROZEN")
                && !entry.Text.Contains("CLOSED")
                && !entry.Text.Contains("COMPONENT-BASELINE"))
            .Select(entry => $"{Relative(entry.Path)}: 无 FROZEN/CLOSED/COMPONENT-BASELINE 声明")
            .ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void Prd_DeclaresLockedOrAccepted()
    {
        var violations = MarkdownFiles("prd")
            .Where(entry => !Regex.IsMatch(entry.Text, @"LOCKED|ACCEPTED"))
            .Select(entry => $"{Relative(entry.Path)}: 无 LOCKED/ACCEPTED 声明")
            .ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void Adr_FilenamesFollowPattern()
    {
        var root = Path.Combine(RepoRoot(), "docs", "adr");
        if (!Directory.Exists(root))
            return;
        var violations = Directory.EnumerateFiles(root, "*.md")
            .Where(path => !Path.GetFileName(path).Equals("README.md", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Where(name => !Regex.IsMatch(name, @"^\d{4}-"))
            .ToList();
        Assert.True(violations.Count == 0, "ADR 文件名须为 \\d{4}- 前缀：" + string.Join(", ", violations));
    }
}
