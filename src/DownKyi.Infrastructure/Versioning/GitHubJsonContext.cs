using System.Text.Json.Serialization;

namespace DownKyi.Infrastructure.Versioning;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubRelease[]))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
