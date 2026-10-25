using DownKyi.Application.Media;
using DownKyi.Application.Settings;
using DownKyi.Core.BiliApi.Users;
using DownKyi.Infrastructure.Bilibili;
using DownKyi.Infrastructure.Downloads;
using DownKyi.Infrastructure.FFmpeg;
using DownKyi.Infrastructure.Settings;
using DownKyi.Infrastructure.Storage;
using DownKyi.Infrastructure.Storage.Database;

namespace DownKyi.Architecture.Tests;

public sealed class MediaStorageOwnershipTests
{
    [Theory]
    [InlineData(typeof(FfmpegProcessor))]
    [InlineData(typeof(TlsFailureClassifier))]
    [InlineData(typeof(SettingsStore))]
    [InlineData(typeof(SettingsManager))]
    [InlineData(typeof(ApplicationStorage))]
    [InlineData(typeof(LoginHelper))]
    [InlineData(typeof(BilibiliCookieProvider))]
    [InlineData(typeof(SqliteDatabase))]
    public void ExternalImplementationsBelongToInfrastructure(Type implementation)
    {
        ArgumentNullException.ThrowIfNull(implementation);
        Assert.Equal("DownKyi.Infrastructure", implementation.Assembly.GetName().Name);
    }

    [Theory]
    [InlineData(typeof(IFfmpegMediaMuxer))]
    [InlineData(typeof(FfmpegOperationResult))]
    [InlineData(typeof(ISettingsStore))]
    [InlineData(typeof(ApplicationSettings))]
    public void SharedContractsBelongToApplication(Type contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        Assert.Equal("DownKyi.Application", contract.Assembly.GetName().Name);
    }

    [Fact]
    public void MigratedOwnersDoNotIntroduceReverseAssemblyDependencies()
    {
        var application = typeof(ISettingsStore).Assembly;
        var infrastructure = typeof(FfmpegProcessor).Assembly;
        var core = typeof(UserInfo).Assembly;

        Assert.DoesNotContain(application.GetReferencedAssemblies(), reference =>
            reference.Name is "DownKyi.Core" or "DownKyi.Infrastructure" or "DownKyi.Desktop");
        Assert.DoesNotContain(infrastructure.GetReferencedAssemblies(), reference =>
            reference.Name is "DownKyi.Core" or "DownKyi.Desktop");
        Assert.DoesNotContain(core.GetReferencedAssemblies(), reference =>
            reference.Name is "DownKyi.Infrastructure" or "DownKyi.Desktop" or "Microsoft.Data.Sqlite");
        Assert.DoesNotContain(core.GetTypes(), type =>
            type.Namespace is not null &&
            (type.Namespace.Contains(".FFmpeg", StringComparison.Ordinal) ||
             type.Namespace.Contains(".Storage", StringComparison.Ordinal) ||
             type.Namespace.Contains(".Settings", StringComparison.Ordinal)));
    }
}
