using System.Reflection;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// Q21-Q23 architecture guard for the evaluated Product Agent.Dsh project
/// reference closure (Dsh → Kernel → Core). MSBuild evaluates the project graph
/// (including imported/conditional ProjectReference items), and the check also
/// scans copied DLLs from the current net10.0 build outputs; it does not claim
/// to cover external plugins, deployment injection, or an unbuilt target framework.
/// </summary>
public sealed class DecisionChannelClosureTests
{
    private static readonly string[] ForbiddenTransportNames =
    [
        "StdioDecisionChannel",
        "FakeDecisionChannel",
        "ReplayDecisionChannel",
        "JsonRpcStdioTransport",
        "StdioJsonRpcPeer",
        "DeterministicDshPeer",
        "FakeDshTransport",
        "ReplayDshTransport",
        "IDshTransport",
    ];

    private static readonly string[] DynamicRegistrationMarkers =
    [
        "Type.GetType(",
        "Assembly.Load",
        "Assembly.GetTypes(",
        "GetExportedTypes(",
        "Activator.CreateInstance(",
        "IServiceCollection",
        "ServiceDescriptor",
        "AddSingleton",
        "AddScoped",
        "AddTransient",
        "ImplementationType",
        "__Type",
    ];

    [Fact]
    public void AgentDsh_ProductProjectReferenceClosure_ContainsNo_TestTransportConcreteTypes()
    {
        var closure = BuildProjectClosure(typeof(UniClaw.Agent.Dsh.DshAgentAdapter).Assembly.Location);
        var assemblies = closure
            .Select(project => Assembly.LoadFrom(project.AssemblyPath))
            .DistinctBy(assembly => assembly.FullName)
            .ToArray();

        Assert.Equal<string>(
            new[] { "UniClaw.Agent.Dsh", "UniClaw.Core", "UniClaw.Kernel" },
            closure.Select(project => Path.GetFileNameWithoutExtension(project.AssemblyPath))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());

        foreach (var project in closure)
        {
            var inputs = ReadProductInputs(project);
            var source = string.Join("\n", inputs
                .Where(path => Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
            var configuration = string.Join("\n", inputs
                .Where(path => !Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

            foreach (var forbidden in ForbiddenTransportNames)
            {
                Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
                Assert.DoesNotContain(forbidden, configuration, StringComparison.Ordinal);
            }

            foreach (var marker in DynamicRegistrationMarkers)
            {
                Assert.DoesNotContain(marker, source, StringComparison.Ordinal);
                Assert.DoesNotContain(marker, configuration, StringComparison.Ordinal);
            }

            // A config/resource fallback is a separate path from a C# default.
            // Do not apply this to comments: Kernel legitimately discusses
            // semantic fallback in implementation documentation.
            Assert.DoesNotContain("fallback", configuration, StringComparison.OrdinalIgnoreCase);

            var projectFile = XDocument.Load(project.ProjectPath);
            foreach (var edge in projectFile.Descendants().Where(element =>
                element.Name.LocalName is "ProjectReference" or "Reference" or "PackageReference"))
            {
                var include = edge.Attribute("Include")?.Value;
                if (include is not null)
                    Assert.False(
                        include.Contains("tests", StringComparison.OrdinalIgnoreCase)
                            || include.Contains("Harness", StringComparison.OrdinalIgnoreCase),
                        $"Product project contains a test/Harness dependency: {include}");
            }
        }

        foreach (var assembly in assemblies)
        {
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => reference.Name == typeof(DecisionChannelClosureTests).Assembly.GetName().Name);
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => reference.Name?.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal) == true
                    || reference.Name?.StartsWith("Microsoft.Extensions.Configuration", StringComparison.Ordinal) == true);

            var typeReferences = ReadTypeReferences(assembly.Location);
            Assert.False(
                typeReferences.Any(name => ForbiddenTransportNames.Contains(name, StringComparer.Ordinal)),
                $"{assembly.GetName().Name} contains a forbidden test transport TypeRef: {string.Join(", ", typeReferences)}");
        }

        foreach (var outputAssembly in closure
            .Select(project => Path.GetDirectoryName(project.AssemblyPath)!)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories)))
        {
            var typeReferences = ReadTypeReferences(outputAssembly);
            Assert.False(
                typeReferences.Any(name => ForbiddenTransportNames.Contains(name, StringComparer.Ordinal)),
                $"{outputAssembly} contains a forbidden test transport TypeRef: {string.Join(", ", typeReferences)}");
            var typeDefinitions = ReadTypeDefinitions(outputAssembly);
            Assert.False(
                typeDefinitions.Any(name => ForbiddenTransportNames.Contains(name, StringComparer.Ordinal)),
                $"{outputAssembly} defines a forbidden test transport type: {string.Join(", ", typeDefinitions)}");
            Assert.DoesNotContain(
                ReadAssemblyReferences(outputAssembly),
                reference => reference == typeof(DecisionChannelClosureTests).Assembly.GetName().Name
                    || reference.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal)
                    || reference.StartsWith("Microsoft.Extensions.Configuration", StringComparison.Ordinal));
        }

        var productTypes = assemblies.SelectMany(assembly => assembly.GetTypes()).ToArray();
        var channelImplementers = productTypes
            .Where(type => typeof(UniClaw.Agent.Dsh.IDecisionChannel).IsAssignableFrom(type))
            .Where(type => !type.IsInterface && !type.IsAbstract)
            .Select(type => type.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var peerImplementers = productTypes
            .Where(type => typeof(UniClaw.Agent.Dsh.IDshOpenedChannelPeer).IsAssignableFrom(type))
            .Where(type => !type.IsInterface && !type.IsAbstract)
            .Select(type => type.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal<string>(["DshOpenedDecisionChannel"], channelImplementers);
        Assert.Empty(peerImplementers);
    }

    private static IReadOnlyList<ProjectNode> BuildProjectClosure(string rootAssemblyPath)
    {
        var rootProjectDirectory = FindProjectDirectory(rootAssemblyPath);
        var outputDirectory = new DirectoryInfo(Path.GetDirectoryName(rootAssemblyPath)!);
        var targetFramework = outputDirectory.Name;
        var configuration = outputDirectory.Parent!.Name;
        var projects = new Dictionary<string, ProjectNode>(StringComparer.Ordinal);
        VisitProject(Path.Combine(rootProjectDirectory, "UniClaw.Agent.Dsh.csproj"));

        return projects.Values.OrderBy(project => project.AssemblyPath, StringComparer.Ordinal).ToArray();

        void VisitProject(string projectPath)
        {
            projectPath = Path.GetFullPath(projectPath);
            if (projects.ContainsKey(projectPath))
                return;

            var projectDirectory = Path.GetDirectoryName(projectPath)!;
            var assemblyPath = Path.Combine(
                projectDirectory, "bin", configuration, targetFramework,
                Path.GetFileNameWithoutExtension(projectPath) + ".dll");
            Assert.True(File.Exists(assemblyPath), $"Product assembly is not built: {assemblyPath}");
            projects[projectPath] = new ProjectNode(projectPath, assemblyPath);

            foreach (var reference in EvaluateProjectReferences(projectPath, configuration, targetFramework))
                VisitProject(reference);
        }
    }

    private static IReadOnlyList<string> EvaluateProjectReferences(
        string projectPath, string configuration, string targetFramework) =>
        EvaluateProjectItemPaths(projectPath, configuration, targetFramework, "ProjectReference");

    private static IReadOnlyList<string> EvaluateProjectInputPaths(ProjectNode project)
    {
        var outputDirectory = new DirectoryInfo(Path.GetDirectoryName(project.AssemblyPath)!);
        return EvaluateProjectItemPaths(
            project.ProjectPath,
            outputDirectory.Parent!.Name,
            outputDirectory.Name,
            "Compile,Content,EmbeddedResource,None");
    }

    private static IReadOnlyList<string> EvaluateProjectItemPaths(
        string projectPath, string configuration, string targetFramework, string itemTypes)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-nologo");
        startInfo.ArgumentList.Add("-verbosity:minimal");
        startInfo.ArgumentList.Add($"-p:Configuration={configuration}");
        startInfo.ArgumentList.Add($"-p:TargetFramework={targetFramework}");
        startInfo.ArgumentList.Add($"-getItem:{itemTypes}");

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet msbuild");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"MSBuild project evaluation failed for {projectPath}: {error}");

        using var document = JsonDocument.Parse(output);
        if (!document.RootElement.TryGetProperty("Items", out var items)
            || items.ValueKind != JsonValueKind.Object)
            return [];

        return items.EnumerateObject()
            .SelectMany(item => item.Value.ValueKind == JsonValueKind.Array
                ? item.Value.EnumerateArray()
                : [])
            .Select(item => item.TryGetProperty("FullPath", out var fullPath)
                ? fullPath.GetString()
                : null)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadProductInputs(ProjectNode project)
    {
        var projectDirectory = Path.GetDirectoryName(project.ProjectPath)!;
        var physicalInputs = Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !IsUnder(path, Path.Combine(projectDirectory, "bin")))
            .Where(path => !IsUnder(path, Path.Combine(projectDirectory, "obj")));
        var evaluatedInputs = EvaluateProjectInputPaths(project)
            .Where(File.Exists)
            .Where(path => !IsUnder(path, Path.Combine(projectDirectory, "bin")))
            .Where(path => !IsUnder(path, Path.Combine(projectDirectory, "obj")));

        return physicalInputs.Concat(evaluatedInputs)
            .Distinct(StringComparer.Ordinal)
            .Where(path =>
            {
                var extension = Path.GetExtension(path);
                return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".config", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ReadTypeReferences(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            return reader.TypeReferences
                .Select(handle => reader.GetString(reader.GetTypeReference(handle).Name))
                .ToArray();
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ReadTypeDefinitions(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            return reader.TypeDefinitions
                .Select(handle => reader.GetString(reader.GetTypeDefinition(handle).Name))
                .ToArray();
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ReadAssemblyReferences(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            return reader.AssemblyReferences
                .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
                .ToArray();
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    private static bool IsUnder(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative == "."
            || (!relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relative));
    }

    private static string FindProjectDirectory(string assemblyPath)
    {
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(assemblyPath)!);
             directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "UniClaw.Agent.Dsh");
            if (File.Exists(Path.Combine(candidate, "UniClaw.Agent.Dsh.csproj")))
                return candidate;
        }

        throw new InvalidOperationException("UniClaw.Agent.Dsh project directory not found");
    }

    private sealed record ProjectNode(string ProjectPath, string AssemblyPath);
}
