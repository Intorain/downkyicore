using Newtonsoft.Json;

namespace DownKyi.Core.Settings.Models;

/// <summary>
/// 网络
/// </summary>
public class NetworkSettings
{
    public AllowStatus IsLiftingOfRegion { get; set; } = AllowStatus.None;

    [JsonProperty("UseSsl")]
    private AllowStatus LegacyUseSsl
    {
        set => HasLegacyUseSsl = true;
    }

    [JsonIgnore]
    internal bool HasLegacyUseSsl { get; private set; }

    internal bool ConsumeLegacyHttpsSwitch()
    {
        var wasPresent = HasLegacyUseSsl;
        HasLegacyUseSsl = false;
        return wasPresent;
    }

    public string UserAgent { get; set; } = string.Empty;

    public AllowStatus HighSpeedDownloadMode { get; set; } = AllowStatus.None;

    public NetworkProxy NetworkProxy { get; set; } = NetworkProxy.None;

    public string CustomNetworkProxy { get; set; } = string.Empty;

    #region built-in

    public int MaxCurrentDownloads { get; set; } = -1;
    public int Split { get; set; } = -1;
    public AllowStatus IsHttpProxy { get; set; } = AllowStatus.None;
    public string? HttpProxy { get; set; }
    public int HttpProxyListenPort { get; set; } = -1;

    #endregion
}
