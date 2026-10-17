namespace DownKyi.Architecture.Tests;

// PowerShell/PowerShell#26528: concurrent Unix pwsh startups can corrupt their shared cache.
internal static class PowerShellProcessTestGroup
{
    public const string Name = "PowerShell process tests";
}
