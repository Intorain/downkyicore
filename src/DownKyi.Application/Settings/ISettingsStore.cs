namespace DownKyi.Application.Settings;

public interface ISettingsStore : IDisposable, IAsyncDisposable
{
    ApplicationSettings Current { get; }

    ApplicationSettings Update(Func<ApplicationSettings, ApplicationSettings> update);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
