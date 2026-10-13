using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DownKyi.Presentation;

internal class VideoQuality : ObservableObject
{
    internal bool IsDurl { get; set; }

    private int _quality;

    public int Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, value);
    }

    private string _qualityFormat = string.Empty;

    public string QualityFormat
    {
        get => _qualityFormat;
        set => SetProperty(ref _qualityFormat, value);
    }

    private IList<string> _videoCodecList = new List<string>();

    public IList<string> VideoCodecList
    {
        get => _videoCodecList;
        internal set => SetProperty(ref _videoCodecList, value);
    }

    private string _selectedVideoCodec = string.Empty;

    internal bool HasManualVideoCodecSelection { get; private set; }

    public string SelectedVideoCodec
    {
        get => _selectedVideoCodec;
        set
        {
            if (value != null && SetProperty(ref _selectedVideoCodec, value))
            {
                HasManualVideoCodecSelection = true;
            }
        }
    }

    internal void SetAutomaticVideoCodec(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        SetProperty(ref _selectedVideoCodec, value);
        HasManualVideoCodecSelection = false;
    }
}
