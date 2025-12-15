using System.Reflection;
using System.Text.Json;

namespace Cookbook.Platform.Orchestrator.Tests.GoldenSet;

/// <summary>
/// Loads golden set test fixtures from embedded resources.
/// </summary>
public static class FixtureLoader
{
    private static readonly Assembly Assembly = typeof(FixtureLoader).Assembly;
    private const string BaseNamespace = "Cookbook.Platform.Orchestrator.Tests.GoldenSet.Fixtures";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Loads the fixture manifest describing all available test fixtures.
    /// </summary>
    public static async Task<FixtureManifest> LoadManifestAsync()
    {
        var manifestPath = Path.Combine(GetFixturesDirectory(), "manifest.json");
        var json = await File.ReadAllTextAsync(manifestPath);
        return JsonSerializer.Deserialize<FixtureManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize manifest");
    }

    /// <summary>
    /// Loads a JSON-LD fixture file.
    /// </summary>
    public static async Task<string> LoadJsonLdFixtureAsync(string filename)
    {
        var path = Path.Combine(GetFixturesDirectory(), filename);
        return await File.ReadAllTextAsync(path);
    }

    /// <summary>
    /// Loads a plain text fixture file.
    /// </summary>
    public static async Task<string> LoadPlainTextFixtureAsync(string filename)
    {
        var path = Path.Combine(GetFixturesDirectory(), filename);
        return await File.ReadAllTextAsync(path);
    }

    /// <summary>
    /// Loads an expected output JSON file.
    /// </summary>
    public static async Task<T?> LoadExpectedAsync<T>(string filename) where T : class
    {
        var path = Path.Combine(GetFixturesDirectory(), filename);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    /// <summary>
    /// Gets the path to the fixtures directory.
    /// </summary>
    private static string GetFixturesDirectory()
    {
        // Get the directory of the test assembly
        var assemblyLocation = Assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)!;
        
        // Navigate to the project root and then to GoldenSet/Fixtures
        var projectRoot = FindProjectRoot(assemblyDirectory);
        return Path.Combine(projectRoot, "GoldenSet", "Fixtures");
    }

    /// <summary>
    /// Finds the project root by looking for the .csproj file.
    /// </summary>
    private static string FindProjectRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        
        while (directory != null)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
                return directory.FullName;
            
            directory = directory.Parent;
        }

        // Fallback: use the test project path relative to build output
        // This handles the typical bin/Debug/net10.0 structure
        var parts = startDirectory.Split(Path.DirectorySeparatorChar);
        var binIndex = Array.FindIndex(parts, p => p.Equals("bin", StringComparison.OrdinalIgnoreCase));
        
        if (binIndex > 0)
        {
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(binIndex));
        }

        throw new DirectoryNotFoundException($"Could not find project root from {startDirectory}");
    }

    /// <summary>
    /// Gets all JSON-LD fixture descriptors.
    /// </summary>
    public static async IAsyncEnumerable<FixtureDescriptor> GetJsonLdFixturesAsync()
    {
        var manifest = await LoadManifestAsync();
        foreach (var fixture in manifest.Fixtures.JsonLd)
        {
            yield return fixture;
        }
    }

    /// <summary>
    /// Gets all plain text fixture descriptors.
    /// </summary>
    public static async IAsyncEnumerable<FixtureDescriptor> GetPlainTextFixturesAsync()
    {
        var manifest = await LoadManifestAsync();
        foreach (var fixture in manifest.Fixtures.PlainText)
        {
            yield return fixture;
        }
    }
}

/// <summary>
/// Represents the fixture manifest file.
/// </summary>
public record FixtureManifest
{
    public string Version { get; init; } = "";
    public string Description { get; init; } = "";
    public string Created { get; init; } = "";
    public FixtureCollection Fixtures { get; init; } = new();
    public FixtureMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Collection of fixtures by type.
/// </summary>
public record FixtureCollection
{
    public List<FixtureDescriptor> JsonLd { get; init; } = new();
    public List<FixtureDescriptor> PlainText { get; init; } = new();
}

/// <summary>
/// Describes a single test fixture.
/// </summary>
public record FixtureDescriptor
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Source { get; init; } = "";
    public string ExtractionMethod { get; init; } = "";
    public List<string> Features { get; init; } = new();
    public string InputFile { get; init; } = "";
    public string? ExpectedFile { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Metadata about the fixture collection.
/// </summary>
public record FixtureMetadata
{
    public int TotalFixtures { get; init; }
    public int JsonLdFixtures { get; init; }
    public int PlaintextFixtures { get; init; }
    public List<string> Cuisines { get; init; } = new();
    public List<string> Categories { get; init; } = new();
}
