using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using DownKyi.Application.Desktop;
using DownKyi.Services.Download;
using DownKyi.Utils;

namespace DownKyi.ViewModels.Dialogs;

internal sealed class DownloadDuplicateBatchDialogViewModel : BaseDialogViewModel
{
    private string _message = string.Empty;
    private string _activeDuplicateMessage = string.Empty;
    private string _affectedItemsHeader = string.Empty;
    private string _redownloadText = string.Empty;
    private string _skipText = string.Empty;
    private bool _hasActiveDuplicates;
    private IReadOnlyList<string> _completedItemNames = [];

    public DownloadDuplicateBatchDialogViewModel()
    {
        Title = DictionaryResource.GetString("DownloadDuplicateBatchTitle");
        RedownloadCommand = new RelayCommand(() => Close(DownloadDuplicateBatchAction.RedownloadCompleted));
        SkipCommand = new RelayCommand(() => Close(DownloadDuplicateBatchAction.SkipCompleted));
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string ActiveDuplicateMessage
    {
        get => _activeDuplicateMessage;
        private set => SetProperty(ref _activeDuplicateMessage, value);
    }

    public bool HasActiveDuplicates
    {
        get => _hasActiveDuplicates;
        private set => SetProperty(ref _hasActiveDuplicates, value);
    }

    public string AffectedItemsHeader
    {
        get => _affectedItemsHeader;
        private set => SetProperty(ref _affectedItemsHeader, value);
    }

    public string RedownloadText
    {
        get => _redownloadText;
        private set => SetProperty(ref _redownloadText, value);
    }

    public string SkipText
    {
        get => _skipText;
        private set => SetProperty(ref _skipText, value);
    }

    public IReadOnlyList<string> CompletedItemNames
    {
        get => _completedItemNames;
        private set => SetProperty(ref _completedItemNames, value);
    }

    public RelayCommand RedownloadCommand { get; }

    public RelayCommand SkipCommand { get; }

    public override bool CanCloseDialog() => false;

    public override void OnDialogOpened(AppDialogRequest request)
    {
        var prompt = GetRequiredParameter<DownloadDuplicateBatchPrompt>(
            request,
            DownloadDuplicateBatchDialogContract.PromptParameter);
        var count = prompt.CompletedItemNames.Count.ToString(CultureInfo.CurrentCulture);
        CompletedItemNames = prompt.CompletedItemNames;
        Message = Format("DownloadDuplicateBatchMessage", count);
        AffectedItemsHeader = Format("DownloadDuplicateBatchAffectedItems", count);
        RedownloadText = Format("RedownloadDuplicateBatch", count);
        SkipText = Format("SkipDuplicateBatch", count);
        ActiveDuplicateMessage = prompt.ActiveDuplicateCount > 0
            ? Format(
                "DownloadDuplicateBatchActiveMessage",
                prompt.ActiveDuplicateCount.ToString(CultureInfo.CurrentCulture))
            : string.Empty;
        HasActiveDuplicates = prompt.ActiveDuplicateCount > 0;
    }

    private void Close(DownloadDuplicateBatchAction action) =>
        CloseDialog(
            AppDialogOutcome.Accepted,
            DownloadDuplicateBatchDialogContract.Encode(action));

    private static string Format(string resourceKey, string count) =>
        DictionaryResource.GetString(resourceKey)
            .Replace("{0}", count, StringComparison.Ordinal);
}
