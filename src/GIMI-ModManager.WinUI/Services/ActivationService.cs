﻿using System.Security.Principal;
using Windows.Foundation;
using CommunityToolkit.WinUI;
using GIMI_ModManager.Core.Services;
using GIMI_ModManager.WinUI.Activation;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Helpers;
using GIMI_ModManager.WinUI.Models.Options;
using GIMI_ModManager.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GIMI_ModManager.WinUI.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ILogger _logger = Log.ForContext<ActivationService>();
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IGenshinService _genshinService;
    private readonly ElevatorService _elevatorService;
    private readonly GenshinProcessManager _genshinProcessManager;
    private readonly ThreeDMigtoProcessManager _threeDMigtoProcessManager;
    private readonly UpdateChecker _updateChecker;
    private readonly IWindowManagerService _windowManagerService;
    private UIElement? _shell = null;

    private readonly bool IsMsix = RuntimeHelper.IsMSIX;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
        IEnumerable<IActivationHandler> activationHandlers, IThemeSelectorService themeSelectorService,
        ILocalSettingsService localSettingsService,
        IGenshinService genshinService, ElevatorService elevatorService, GenshinProcessManager genshinProcessManager,
        ThreeDMigtoProcessManager threeDMigtoProcessManager, UpdateChecker updateChecker,
        IWindowManagerService windowManagerService)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _localSettingsService = localSettingsService;
        _genshinService = genshinService;
        _elevatorService = elevatorService;
        _genshinProcessManager = genshinProcessManager;
        _threeDMigtoProcessManager = threeDMigtoProcessManager;
        _updateChecker = updateChecker;
        _windowManagerService = windowManagerService;
    }

    public async Task ActivateAsync(object activationArgs)
    {
#if DEBUG
        _logger.Information("JASM starting up in DEBUG mode...");
#elif RELEASE
        _logger.Information("JASM starting up in RELEASE mode...");
#endif
        // Execute tasks before activation.
        await InitializeAsync();

        // Set the MainWindow Content.
        if (App.MainWindow.Content == null)
        {
            _shell = App.GetService<ShellPage>();
            App.MainWindow.Content = _shell ?? new Frame();
        }

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        App.MainWindow.Activate();

        // Set MainWindow Cleanup on Close.
        App.MainWindow.Closed += (_, _) => OnApplicationExit();

        // Execute tasks after activation.
        await StartupAsync();

        // Show admin warning if running as admin.
        AdminWarningPopup();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));


        if (activationHandler is not null)
        {
            _logger.Debug("Handling activation: {ActivationName}",
                activationHandler?.ActivationName);

            await activationHandler?.HandleAsync(activationArgs)!;
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            _logger.Debug("Handling activation: {ActivationName}", _defaultHandler.ActivationName);
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        var jsonPath = IsMsix ? "ms-appx:///Assets/characters.json" : $"{AppDomain.CurrentDomain.BaseDirectory}Assets";
        await SetScreenSize();
        await _genshinService.InitializeAsync(jsonPath).ConfigureAwait(false);
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
        _elevatorService.Initialize();
    }


    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();
        InitScreenSizeSaver();
        await InitCharacterOverviewSettings();
        await _genshinProcessManager.TryInitialize();
        await _threeDMigtoProcessManager.TryInitialize();
        await _updateChecker.InitializeAsync();
    }

    private async Task SetScreenSize()
    {
        var screenSize = await _localSettingsService.ReadSettingAsync<ScreenSize>(ScreenSize.Key);
        if (screenSize != null)
        {
            _logger.Debug($"Window size loaded: {screenSize.Width}x{screenSize.Height}");
            App.MainWindow.SetWindowSize(screenSize.Width, screenSize.Height);
            App.MainWindow.CenterOnScreen();
        }
    }

    private async Task InitCharacterOverviewSettings()
    {
        var characterOverviewSettings =
            await _localSettingsService.ReadSettingAsync<CharacterOverviewOptions>(CharacterOverviewOptions.Key);
        if (characterOverviewSettings == null)
            await _localSettingsService.SaveSettingAsync(CharacterOverviewOptions.Key, new CharacterOverviewOptions());
    }

    private Size _previousScreenSize = new(0, 0);
    private DispatcherTimer? _timer;

    private void InitScreenSizeSaver()
    {
        const int delayMilliseconds = 1000;
        _timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(delayMilliseconds) };
        _timer.Tick += ScreenSizeSavingTimer_Tick;

        App.MainWindow.SizeChanged += (sender, args) =>
        {
            // Reset the timer on every SizeChanged event.
            _timer.Stop();
            if (App.MainWindow.Height == _previousScreenSize.Height &&
                App.MainWindow.Width == _previousScreenSize.Width)
                return;

            _previousScreenSize = new Size(App.MainWindow.Width, App.MainWindow.Height);
            _timer.Start();
        };
    }

    // There has to be a better way to do this. But for now this works.
    // Window does not have the closing event, so I can't save the size on close.
    // Application might exit ungracefully, unsure if this is a problem.
    private void ScreenSizeSavingTimer_Tick(object sender, object e)
    {
        var width = App.MainWindow.Width;
        var height = App.MainWindow.Height;
        var isFullScreen = false; // TODO: Implement fullscreen
        _logger.Debug($"Window size saved: {width}x{height}\t\nIsFullscreen: {isFullScreen}");
        Task.Run(async () => await App.GetService<ILocalSettingsService>()
            .SaveSettingAsync(ScreenSize.Key, new ScreenSize(width, height) { IsFullScreen = isFullScreen }));
        _timer?.Stop();
    }


    private void OnApplicationExit()
    {
        _logger.Debug("JASM shutting down...");
        _updateChecker.Dispose();
        var tmpDir = new DirectoryInfo(App.TMP_DIR);
        if (!tmpDir.Exists) return;

        _logger.Debug("Deleting temporary directory: {Path}", tmpDir.FullName);
        tmpDir.Delete(true);
    }


    private void AdminWarningPopup()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        if (!principal.IsInRole(WindowsBuiltInRole.Administrator)) return;

        App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            await Task.Delay(1000);

            var stackPanel = new StackPanel();
            var textWarning = new TextBlock()
            {
                Text = "You are running JASM as an administrator. This is not recommended.\n" +
                       "JASM was NOT designed to run with administrator privileges.\n" +
                       "Simple bugs, though unlikely, can potentially cause serious damage to your file system.\n\n" +
                       "Please consider running JASM without administrator privileges.\n\n" +
                       "Use at your own risk, you have been warned",
                TextWrapping = TextWrapping.WrapWholeWords
            };
            stackPanel.Children.Add(textWarning);

            //var doNotShowAgain = new CheckBox()
            //{
            //    Content = "Do not show this warning again",
            //    Margin = new Thickness(0, 10, 0, 0)
            //};

            //stackPanel.Children.Add(doNotShowAgain);


            var dialog = new ContentDialog
            {
                Title = "Running as Administrator Warning",
                Content = stackPanel,
                PrimaryButtonText = "I understand",
                SecondaryButtonText = "Exit",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await _windowManagerService.ShowDialogAsync(dialog);

            if (result == ContentDialogResult.Secondary) Application.Current.Exit();
        });
    }
}