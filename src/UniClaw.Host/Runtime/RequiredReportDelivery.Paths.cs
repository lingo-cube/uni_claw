namespace UniClaw.Host.Runtime;

public sealed record ReportDeliveryStorageOptions
{
    public string StoreRelativePath { get; init; } = ".required-reports";
}

public static class RequiredReportDeliveryPaths
{
    public static string ResolveStoreRoot(string productRoot, ReportDeliveryStorageOptions? options = null)
    {
        var root = ResolveRoot(productRoot, nameof(productRoot));
        var relative = options?.StoreRelativePath ?? ".required-reports";
        ValidateRelativePath(relative);
        var store = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (string.Equals(store, root, PathComparison())) throw new ArgumentException("report-store-root-overlaps-product-root", nameof(options));
        RejectExistingReparsePoints(root, store);
        return store;
    }

    public static string ResolveSourcePath(string sourceDirectory, string relativePath)
    {
        var root = ResolveRoot(sourceDirectory, nameof(sourceDirectory));
        ValidateRelativePath(relativePath);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithin(root, path)) throw new ArgumentException("report-relative-path-escapes-source", nameof(relativePath));
        RejectExistingReparsePoints(root, path);
        return path;
    }

    public static void ValidateRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || Path.IsPathRooted(value)
            || value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0 || value.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("report-relative-path-invalid", nameof(value));
        var segments = value.Split('/', StringSplitOptions.None);
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
            throw new ArgumentException("report-relative-path-invalid", nameof(value));
    }

    private static string ResolveRoot(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("report-root-required", parameterName);
        return Path.GetFullPath(value);
    }

    private static bool IsWithin(string root, string path) => path.StartsWith(
        root.EndsWith(Path.DirectorySeparatorChar.ToString(), PathComparison())
            ? root : root + Path.DirectorySeparatorChar, PathComparison());

    private static void RejectExistingReparsePoints(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        if (File.Exists(current) || Directory.Exists(current)) RejectReparsePoint(current);
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) break;
            RejectReparsePoint(current);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("report-path-link");
    }

    private static StringComparison PathComparison() => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
