using System.Collections.Immutable;
using DownKyi.Core.Settings.Models;

namespace DownKyi.Core.Settings;

public partial class SettingsManager
{
    internal ApplicationSettings CreateSnapshot(bool persistDefaultValues = true)
    {
        lock (_settingsLock)
        {
            _suppressChangeNotifications++;
            if (!persistDefaultValues)
            {
                _suppressPersistence++;
            }

            try
            {
                return CreateSnapshotCore();
            }
            finally
            {
                if (!persistDefaultValues)
                {
                    _suppressPersistence--;
                }

                _suppressChangeNotifications--;
            }
        }
    }

    internal ApplicationSettings CreateInitialSnapshot()
    {
        return CreateSnapshot(_persistInitialDefaults);
    }

    private ApplicationSettings CreateSnapshotCore()
    {
        var content = GetVideoContent();
        var user = GetUserInfo();
        var window = GetWindowSettings();
        return new ApplicationSettings(
            _appSettings.SchemaVersion,
            new BasicApplicationSettings(
                GetThemeMode(),
                GetAfterDownloadOperation(),
                GetIsListenClipboard(),
                GetDownloadFinishedSort(),
                IsRepeatFileAutoAddNumberSuffix()),
            new NetworkApplicationSettings(
                GetIsLiftingOfRegion(),
                GetUserAgent(),
                GetHighSpeedDownloadMode(),
                GetNetworkProxy(),
                GetCustomProxy(),
                GetMaxCurrentDownloads(),
                GetSplit(),
                GetMaxOverallDownloadLimit(),
                GetMaxDownloadLimit(),
                GetIsHttpProxy(),
                GetHttpProxy(),
                GetHttpProxyListenPort()),
            new VideoApplicationSettings(
                GetVideoCodecs(),
                GetQuality(),
                GetAudioQuality(),
                GetIsTranscodingFlvToMp4(),
                GetIsTranscodingAacToMp3(),
                GetFfmpegHardwareAcceleration(),
                GetFfmpegMaxParallelJobs(),
                GetSaveVideoRootPath(),
                GetHistoryVideoRootPaths().ToImmutableArray(),
                GetIsUseSaveVideoRootPath(),
                new VideoContentApplicationSettings(
                    content.DownloadAudio,
                    content.DownloadVideo,
                    content.DownloadDanmaku,
                    content.DownloadSubtitle,
                    content.DownloadCover,
                    content.GenerateMovieMetadata,
                    content.PreferredSubtitleLanguage,
                    content.PreferredSubtitleType),
                GetFileNameParts().ToImmutableArray(),
                GetFileNamePartTimeFormat(),
                GetOrderFormat()),
            new DanmakuApplicationSettings(
                GetDanmakuOutputFormat(),
                GetDanmakuTopFilter(),
                GetDanmakuBottomFilter(),
                GetDanmakuScrollFilter(),
                GetDanmakuRemoveEmojiAndSpecialCharacters(),
                GetDanmakuBlockedKeywords().ToImmutableArray(),
                GetDanmakuBlockedSenderUids().ToImmutableArray(),
                GetIsCustomDanmakuResolution(),
                GetDanmakuScreenWidth(),
                GetDanmakuScreenHeight(),
                GetDanmakuFontName(),
                GetDanmakuFontSize(),
                GetDanmakuLineCount(),
                GetDanmakuLayoutAlgorithm()),
            new AboutApplicationSettings(
                GetIsReceiveBetaVersion(),
                GetAutoUpdateWhenLaunch(),
                GetSkipVersionOnLaunch()),
            new UserApplicationSettings(
                user.Mid,
                user.Name,
                user.IsLogin,
                user.IsVip,
                user.ImgKey,
                user.SubKey),
            new WindowApplicationSettings(window.Width, window.Height, window.X, window.Y));

    }

    internal void ApplySnapshot(ApplicationSettings snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var validated = ApplicationSettingsValidator.Validate(snapshot).Settings;
        lock (_settingsLock)
        {
            _suppressChangeNotifications++;
            try
            {
                SetProperty(_appSettings.SchemaVersion, validated.SchemaVersion, value => _appSettings.SchemaVersion = value);
                SetThemeMode(validated.Basic.ThemeMode);
                SetAfterDownloadOperation(validated.Basic.AfterDownload);
                SetIsListenClipboard(validated.Basic.IsListenClipboard);
                SetDownloadFinishedSort(validated.Basic.DownloadFinishedSort);
                IsRepeatFileAutoAddNumberSuffix(validated.Basic.RepeatFileAutoAddNumberSuffix);

                SetIsLiftingOfRegion(validated.Network.IsLiftingOfRegion);
                SetUserAgent(validated.Network.UserAgent);
                SetHighSpeedDownloadMode(validated.Network.HighSpeedDownloadMode);
                SetNetworkProxy(validated.Network.NetworkProxy);
                SetProperty(
                    _appSettings.Network.CustomNetworkProxy,
                    validated.Network.CustomNetworkProxy,
                    value => _appSettings.Network.CustomNetworkProxy = value);
                SetMaxCurrentDownloads(validated.Network.MaxCurrentDownloads);
                SetSplit(validated.Network.Split);
                SetMaxOverallDownloadLimit(validated.Network.MaxOverallDownloadLimit);
                SetMaxDownloadLimit(validated.Network.MaxDownloadLimit);
                SetIsHttpProxy(validated.Network.IsHttpProxy);
                SetHttpProxy(validated.Network.HttpProxy);
                SetHttpProxyListenPort(validated.Network.HttpProxyListenPort);

                SetVideoCodecs(validated.Video.VideoCodecs);
                SetQuality(validated.Video.Quality);
                SetAudioQuality(validated.Video.AudioQuality);
                SetIsTranscodingFlvToMp4(validated.Video.IsTranscodingFlvToMp4);
                SetIsTranscodingAacToMp3(validated.Video.IsTranscodingAacToMp3);
                SetFfmpegHardwareAcceleration(validated.Video.FfmpegHardwareAcceleration);
                SetFfmpegMaxParallelJobs(validated.Video.FfmpegMaxParallelJobs);
                SetSaveVideoRootPath(validated.Video.SaveVideoRootPath);
                SetHistoryVideoRootPaths(validated.Video.HistoryVideoRootPaths);
                SetIsUseSaveVideoRootPath(validated.Video.IsUseSaveVideoRootPath);
                SetVideoContent(new VideoContentSettings
                {
                    DownloadAudio = validated.Video.Content.DownloadAudio,
                    DownloadVideo = validated.Video.Content.DownloadVideo,
                    DownloadDanmaku = validated.Video.Content.DownloadDanmaku,
                    DownloadSubtitle = validated.Video.Content.DownloadSubtitle,
                    DownloadCover = validated.Video.Content.DownloadCover,
                    GenerateMovieMetadata = validated.Video.Content.GenerateMovieMetadata,
                    PreferredSubtitleLanguage = validated.Video.Content.PreferredSubtitleLanguage,
                    PreferredSubtitleType = validated.Video.Content.PreferredSubtitleType
                });
                SetFileNameParts(validated.Video.FileNameParts);
                SetFileNamePartTimeFormat(validated.Video.FileNamePartTimeFormat);
                SetOrderFormat(validated.Video.OrderFormat);

                SetDanmakuOutputFormat(validated.Danmaku.OutputFormat);
                SetDanmakuTopFilter(validated.Danmaku.TopFilter);
                SetDanmakuBottomFilter(validated.Danmaku.BottomFilter);
                SetDanmakuScrollFilter(validated.Danmaku.ScrollFilter);
                SetDanmakuRemoveEmojiAndSpecialCharacters(validated.Danmaku.RemoveEmojiAndSpecialCharacters);
                SetDanmakuBlockedKeywords(validated.Danmaku.BlockedKeywords);
                SetDanmakuBlockedSenderUids(validated.Danmaku.BlockedSenderUids);
                SetIsCustomDanmakuResolution(validated.Danmaku.IsCustomResolution);
                SetDanmakuScreenWidth(validated.Danmaku.ScreenWidth);
                SetDanmakuScreenHeight(validated.Danmaku.ScreenHeight);
                SetDanmakuFontName(validated.Danmaku.FontName);
                SetDanmakuFontSize(validated.Danmaku.FontSize);
                SetDanmakuLineCount(validated.Danmaku.LineCount);
                SetDanmakuLayoutAlgorithm(validated.Danmaku.LayoutAlgorithm);

                SetIsReceiveBetaVersion(validated.About.IsReceiveBetaVersion);
                SetAutoUpdateWhenLaunch(validated.About.AutoUpdateWhenLaunch);
                SetProperty(
                    _appSettings.About.SkipVersionOnLaunch,
                    validated.About.SkipVersionOnLaunch,
                    value => _appSettings.About.SkipVersionOnLaunch = value);
                SetUserInfo(new UserInfoSettings
                {
                    Mid = validated.User.Mid,
                    Name = validated.User.Name,
                    IsLogin = validated.User.IsLogin,
                    IsVip = validated.User.IsVip,
                    ImgKey = validated.User.ImgKey,
                    SubKey = validated.User.SubKey
                });
                SettingWindowSettings(new WindowSettings
                {
                    Width = validated.Window.Width,
                    Height = validated.Window.Height,
                    X = validated.Window.X,
                    Y = validated.Window.Y
                });
            }
            finally
            {
                _suppressChangeNotifications--;
            }
        }

        Changed?.Invoke();
    }
}
