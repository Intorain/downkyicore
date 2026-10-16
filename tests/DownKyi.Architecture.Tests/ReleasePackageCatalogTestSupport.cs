using System.Diagnostics;
using System.Text.Json;

namespace DownKyi.Architecture.Tests;

internal static class ReleasePackageCatalogTestSupport
{
    public static ReleasePackageDefinition[] LoadPackages(string version)
    {
        using var document = RunProjection(version, "PackageCatalog");
        return document.RootElement.EnumerateArray()
            .Select(package => new ReleasePackageDefinition(
                package.GetProperty("Scope").GetString()!,
                package.GetProperty("Name").GetString()!,
                package.GetProperty("Kind").GetString()!,
                package.GetProperty("RuntimeIdentifier").GetString()!,
                package.GetProperty("Manifest").GetString()!,
                package.GetProperty("Transport").GetString()))
            .ToArray();
    }

    public static ReleaseValidationMatrix LoadValidationMatrix(string version)
    {
        using var document = RunProjection(version, "ValidationMatrix");
        var targets = document.RootElement.GetProperty("include").EnumerateArray()
            .Select(target => new ReleaseValidationTarget(
                target.GetProperty("scope").GetString()!,
                target.GetProperty("os").GetString()!,
                target.GetProperty("pattern").GetString()!,
                target.GetProperty("pattern2").GetString()!,
                target.GetProperty("pattern3").GetString()!))
            .ToArray();
        return new ReleaseValidationMatrix(targets);
    }

    private static JsonDocument RunProjection(string version, string view)
    {
        var repositoryRoot = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "script", "resolve-release-plan.ps1"));
        startInfo.ArgumentList.Add("-ExpectedVersion");
        startInfo.ArgumentList.Add(version);
        startInfo.ArgumentList.Add("-View");
        startInfo.ArgumentList.Add(view);
        startInfo.ArgumentList.Add("-AsJson");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Unable to start the release plan resolver.");
        }

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Release plan resolver failed with exit code {process.ExitCode}: {standardError}");
        }

        return JsonDocument.Parse(standardOutput);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DownKyi.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Could not locate the DownKyi repository root.");
    }
}

internal sealed record ReleasePackageDefinition(
    string Scope,
    string PackageName,
    string Kind,
    string RuntimeIdentifier,
    string ManifestName,
    string? Transport);

internal sealed record ReleaseValidationMatrix(IReadOnlyList<ReleaseValidationTarget> Include);

internal sealed record ReleaseValidationTarget(
    string Scope,
    string Runner,
    string Pattern,
    string Pattern2,
    string Pattern3);
