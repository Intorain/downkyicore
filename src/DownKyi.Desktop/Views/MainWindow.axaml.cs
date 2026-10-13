using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using DownKyi.Application.Desktop;
using DownKyi.Application.Diagnostics;
using DownKyi.Application.Lifetime;
using DownKyi.Core.Settings;
using DownKyi.Core.Settings.Models;
using DownKyi.Platform;
using DownKyi.ViewModels;
using Microsoft.Extensions.Logging;

namespace DownKyi.Views;

internal partial class MainWindow : Window
{
    private readonly Lazy<MainWindowViewModel> _viewModel;
    private readonly IApplicationLifecycle _applicationLifecycle;
    private readonly IClipboardMonitor _clipboardMonitor;
    private readonly ILogger<MainWindow> _logger;
    private readonly ISettingsStore _settingsStore;
    private WindowSettings _windowSettings;
    private bool _closeConfirmed;
    private bool _closeInProgress;
    private bool _startupWindowCloaked;
    private bool _startupClipboardSubscribed;
    private AppNavigationRequest? _pendingStartupNavigation;
    private string? _pendingStartupInput;
    private bool _pendingStartupInputFromClipboard;

    internal StartupIndexViewModel StartupIndex { get; }

    public MainWindow(
        Lazy<MainWindowViewModel> viewModel,
        ISettingsStore settingsStore,
        IApplicationLifecycle applicationLifecycle,
        IClipboardMonitor clipboardMonitor,
        ILogger<MainWindow> logger)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _applicationLifecycle = applicationLifecycle
            ?? throw new ArgumentNullException(nameof(applicationLifecycle));
        _clipboardMonitor = clipboardMonitor ?? throw new ArgumentNullException(nameof(clipboardMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        StartupIndex = new StartupIndexViewModel(
            _settingsStore.Current.User,
            QueueStartupNavigation,
            QueueStartupSearch);
        InitializeComponent();
        _startupWindowCloaked = !Design.IsDesignMode
            && WindowsWindowCloak.TrySetCloaked(this, cloaked: true);
        var window = _settingsStore.Current.Window;
        _windowSettings = new WindowSettings
        {
            Width = window.Width,
            Height = window.Height,
            X = window.X,
            Y = window.Y
        };
        ApplyWindowSettings();
        StartStartupClipboardMonitoring();
    }

    private void ApplyWindowSettings()
    {
        Width = _windowSettings.Width;
        Height = _windowSettings.Height;
        if (double.IsNaN(_windowSettings.X) || double.IsNaN(_windowSettings.Y))
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            Position = new PixelPoint(
                (int)_windowSettings.X,
                (int)_windowSettings.Y);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        EnsureWindowFitsWorkingArea();
        base.OnOpened(e);
        if (!Design.IsDesignMode)
        {
            RequestAnimationFrame(WaitForStartupSurfaceFrame);
        }
    }

    private void WaitForStartupSurfaceFrame(TimeSpan _)
    {
        if (_closeInProgress || _closeConfirmed)
        {
            return;
        }

        RequestAnimationFrame(RevealStartupSurface);
    }

    private void RevealStartupSurface(TimeSpan _)
    {
        if (_closeInProgress || _closeConfirmed)
        {
            return;
        }

        Opacity = 1;
        RequestAnimationFrame(PresentStartupSurface);
    }

    private void PresentStartupSurface(TimeSpan _)
    {
        if (_closeInProgress || _closeConfirmed)
        {
            return;
        }

        if (_startupWindowCloaked)
        {
            WindowsWindowCloak.TrySetCloaked(this, cloaked: false);
            _startupWindowCloaked = false;
        }

        RequestAnimationFrame(ScheduleContentActivation);
    }

    private void ScheduleContentActivation(TimeSpan _)
    {
        if (_closeInProgress || _closeConfirmed)
        {
            return;
        }

        Dispatcher.UIThread.Post(ActivateContent, DispatcherPriority.Background);
    }

    private void QueueStartupNavigation(AppNavigationRequest request)
    {
        _pendingStartupNavigation = request ?? throw new ArgumentNullException(nameof(request));
        _pendingStartupInput = null;
        _pendingStartupInputFromClipboard = false;
    }

    private void QueueStartupSearch(string input)
    {
        QueueStartupSearch(input, fromClipboard: false);
    }

    private void QueueStartupSearch(string input, bool fromClipboard)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        _pendingStartupNavigation = null;
        _pendingStartupInput = input;
        _pendingStartupInputFromClipboard = fromClipboard;
    }

    private void StartStartupClipboardMonitoring()
    {
        _clipboardMonitor.Changed += StartupClipboardMonitorOnChanged;
        _startupClipboardSubscribed = true;
    }

    private void StopStartupClipboardMonitoring()
    {
        if (!_startupClipboardSubscribed)
        {
            return;
        }

        _clipboardMonitor.Changed -= StartupClipboardMonitorOnChanged;
        _startupClipboardSubscribed = false;
    }

    private void StartupClipboardMonitorOnChanged(object? sender, ClipboardTextChangedEventArgs e)
    {
        if (_settingsStore.Current.Basic.IsListenClipboard != AllowStatus.Yes
            || string.IsNullOrWhiteSpace(e.Text))
        {
            return;
        }

        void QueueClipboardInput()
        {
            if (DataContext is MainWindowViewModel || _closeInProgress || _closeConfirmed)
            {
                return;
            }

            StartupIndex.InputText = e.Text;
            QueueStartupSearch(e.Text, fromClipboard: true);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            QueueClipboardInput();
        }
        else
        {
            Dispatcher.UIThread.Post(QueueClipboardInput);
        }
    }

    internal void ActivateContent()
    {
        if (DataContext is MainWindowViewModel || _closeInProgress || _closeConfirmed)
        {
            return;
        }

        var viewModel = _viewModel.Value;
        DataContext = viewModel;
        MainContentHost.IsVisible = true;
        StartupSurface.IsVisible = false;
        viewModel.Activate(
            _pendingStartupNavigation,
            _pendingStartupInput,
            _pendingStartupInputFromClipboard);
        StopStartupClipboardMonitoring();
        StartupReadinessIndicator.IsVisible = false;
    }

    private void EnsureWindowFitsWorkingArea()
    {
        var screen = Screens.ScreenFromWindow(this)
            ?? Screens.ScreenFromPoint(Position)
            ?? Screens.Primary;
        if (screen is null || FrameSize is not { } frameSize)
        {
            return;
        }

        var placement = ConstrainRestoredPlacement(
            Position,
            ClientSize,
            frameSize,
            screen.WorkingArea,
            screen.Scaling);
        Width = placement.ClientSize.Width;
        Height = placement.ClientSize.Height;
        Position = placement.Position;
    }

    internal static (PixelPoint Position, Size ClientSize) ConstrainRestoredPlacement(
        PixelPoint position,
        Size clientSize,
        Size frameSize,
        PixelRect workingArea,
        double scaling)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scaling, 0);

        var decorationWidth = Math.Max(0, frameSize.Width - clientSize.Width);
        var decorationHeight = Math.Max(0, frameSize.Height - clientSize.Height);
        var maxClientWidth = Math.Max(0, workingArea.Width / scaling - decorationWidth);
        var maxClientHeight = Math.Max(0, workingArea.Height / scaling - decorationHeight);
        var constrainedClientSize = new Size(
            Math.Min(clientSize.Width, maxClientWidth),
            Math.Min(clientSize.Height, maxClientHeight));
        var frameWidthInPixels = (int)Math.Ceiling(
            (constrainedClientSize.Width + decorationWidth) * scaling);
        var frameHeightInPixels = (int)Math.Ceiling(
            (constrainedClientSize.Height + decorationHeight) * scaling);
        var maxX = Math.Max(workingArea.X, workingArea.Right - frameWidthInPixels);
        var maxY = Math.Max(workingArea.Y, workingArea.Bottom - frameHeightInPixels);

        return (
            new PixelPoint(
                Math.Clamp(position.X, workingArea.X, maxX),
                Math.Clamp(position.Y, workingArea.Y, maxY)),
            constrainedClientSize);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (Design.IsDesignMode || _closeConfirmed)
        {
            SaveWindowSettings();
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closeInProgress)
        {
            return;
        }

        _closeInProgress = true;
        StopStartupClipboardMonitoring();
        if (_startupWindowCloaked)
        {
            _ = WindowsWindowCloak.TrySetCloaked(this, cloaked: false);
            _startupWindowCloaked = false;
        }

        SaveWindowSettings();
        Hide();
        ObserveCloseCompletion(CompleteCloseAsync());
    }

    private void SaveWindowSettings()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            _windowSettings.Width = Width;
            _windowSettings.Height = Height;
            _windowSettings.X = Position.X;
            _windowSettings.Y = Position.Y;
        }

        _settingsStore.Update(settings => settings with
        {
            Window = new WindowApplicationSettings(
                _windowSettings.Width,
                _windowSettings.Height,
                _windowSettings.X,
                _windowSettings.Y)
        });
    }

    private async Task CompleteCloseAsync()
    {
        try
        {
            await _applicationLifecycle.RequestShutdownAsync().ConfigureAwait(true);
        }
        finally
        {
            _closeConfirmed = true;
            _closeInProgress = false;
            Close();
        }
    }

    private void ObserveCloseCompletion(Task closeTask)
    {
        _ = closeTask.ContinueWith(
            completed => _logger.LogErrorMessage(
                "Application shutdown failed while closing the main window.",
                completed.Exception!.GetBaseException()),
            System.Threading.CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
