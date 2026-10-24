using DownKyi.Application.Settings;
namespace DownKyi.Infrastructure.Settings;

public partial class SettingsManager
{
    public const int HighSpeedBuiltInSplit = ApplicationSettingsDefaults.HighSpeedBuiltInSplit;

    private const AllowStatus HighSpeedDownloadMode = AllowStatus.No;
    private const int MaxOverallDownloadLimit = 0;
    private const int MaxDownloadLimit = 0;

    public AllowStatus GetHighSpeedDownloadMode()
    {
        if (_appSettings.Network.HighSpeedDownloadMode != AllowStatus.None)
        {
            return _appSettings.Network.HighSpeedDownloadMode;
        }

        SetHighSpeedDownloadMode(HighSpeedDownloadMode);
        return HighSpeedDownloadMode;
    }

    public bool SetHighSpeedDownloadMode(AllowStatus highSpeedDownloadMode)
    {
        return SetProperty(
            _appSettings.Network.HighSpeedDownloadMode,
            highSpeedDownloadMode,
            v => _appSettings.Network.HighSpeedDownloadMode = v);
    }

    public int GetMaxOverallDownloadLimit()
    {
        if (_appSettings.Network.MaxOverallDownloadLimit != -1)
        {
            return _appSettings.Network.MaxOverallDownloadLimit;
        }

        SetMaxOverallDownloadLimit(MaxOverallDownloadLimit);
        return MaxOverallDownloadLimit;
    }

    public bool SetMaxOverallDownloadLimit(int maxOverallDownloadLimit)
    {
        return SetProperty(
            _appSettings.Network.MaxOverallDownloadLimit,
            maxOverallDownloadLimit,
            value => _appSettings.Network.MaxOverallDownloadLimit = value);
    }

    public int GetMaxDownloadLimit()
    {
        if (_appSettings.Network.MaxDownloadLimit != -1)
        {
            return _appSettings.Network.MaxDownloadLimit;
        }

        SetMaxDownloadLimit(MaxDownloadLimit);
        return MaxDownloadLimit;
    }

    public bool SetMaxDownloadLimit(int maxDownloadLimit)
    {
        return SetProperty(
            _appSettings.Network.MaxDownloadLimit,
            maxDownloadLimit,
            value => _appSettings.Network.MaxDownloadLimit = value);
    }
}
