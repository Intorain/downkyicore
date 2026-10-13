namespace DownKyi.Core.Settings;

public partial class SettingsManager
{
    public const int HighSpeedBuiltInSplit = ApplicationSettingsDefaults.HighSpeedBuiltInSplit;

    private const AllowStatus HighSpeedDownloadMode = AllowStatus.No;

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
}
