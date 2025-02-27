﻿using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using CommunityToolkit.Mvvm.ComponentModel;
using GIMI_ModManager.WinUI.Contracts.Services;
using GIMI_ModManager.WinUI.Models.Options;
using Microsoft.UI.Xaml;
using Serilog;
using Windows.Storage.Pickers;

namespace GIMI_ModManager.WinUI.Services;

public abstract partial class BaseProcessManager<TProcessOptions> : ObservableObject
    where TProcessOptions : ProcessOptionsBase, new()
{
    private protected readonly ILogger _logger;
    private protected readonly ILocalSettingsService _localSettingsService;

    private Process? _process;
    private protected string _prcoessPath = null!;
    private protected string _workingDirectory = string.Empty;

    private bool _exitHandlerRegistered;
    private protected bool _isGenshinClass;

    public string ProcessName { get; protected set; } = string.Empty;


    [ObservableProperty] private string? _errorMessage = null;

    [ObservableProperty] private ProcessStatus _processStatus = ProcessStatus.NotInitialized;


    protected BaseProcessManager(ILogger logger, ILocalSettingsService localSettingsService)
    {
        _logger = logger;
        _localSettingsService = localSettingsService;
    }

    // Runs on JASM startup
    public async Task<bool> TryInitialize()
    {
        var processOptions = await ReadProcessOptions();

        if (processOptions.GetType() == typeof(GenshinProcessOptions))
        {
            _isGenshinClass = true;
        }

        if (!File.Exists(processOptions.ProcessPath))
        {
            return false;
        }

        _prcoessPath = processOptions.ProcessPath;
        ProcessStatus = ProcessStatus.NotRunning;
        return true;
    }

    public async Task ResetProcessOptions()
    {
        await _SetStartupPath(null!, null!, null);
        ProcessStatus = ProcessStatus.NotInitialized;
        _logger.Information($"Reset {typeof(TProcessOptions).Name} path settings");
    }


    // Runs when the start process is clicked and options are not set
    public async Task SetPath(string processName, string path, string? workingDirectory = null)
    {
        await _SetStartupPath(processName, path, workingDirectory);
        ProcessStatus = ProcessStatus.NotRunning;
        _logger.Information($"Saved {typeof(TProcessOptions).Name} path to settings");
    }

    private async Task _SetStartupPath(string processName, string path, string? workingDirectory = null)
    {
        ProcessName = processName;
        _prcoessPath = path;
        _workingDirectory = workingDirectory ?? Path.GetDirectoryName(path) ?? "";

        var processOptions = await ReadProcessOptions();

        processOptions.ProcessPath = path;
        processOptions.WorkingDirectory = _workingDirectory;

        await _localSettingsService.SaveSettingAsync(processOptions.Key, processOptions);
    }

    private async Task<TProcessOptions> ReadProcessOptions()
    {
        var processOptions = new TProcessOptions();
        processOptions = await _localSettingsService.ReadSettingAsync<TProcessOptions>(processOptions.Key) ??
                         new TProcessOptions();
        return processOptions;
    }

    public void StartProcess()
    {
        if (_process is { HasExited: false })
        {
            _logger.Information($"{ProcessName} is already running");
            return;
        }

        if (ProcessStatus == ProcessStatus.NotInitialized)
        {
            _logger.Warning($"{ProcessName} is not initialized");
            return;
        }

        try
        {
            _process = Process.Start(new ProcessStartInfo(_prcoessPath)
            {
                WorkingDirectory = _workingDirectory == string.Empty
                    ? Path.GetDirectoryName(_prcoessPath) ?? ""
                    : _workingDirectory,
                Arguments = _isGenshinClass ? "runas" : "",
                UseShellExecute = _isGenshinClass
            });
        }
        catch (Win32Exception e)
        {
            _logger.Error(e, $"Failed to start {ProcessName}, this is likely due to the user cancelling the UAC (admin) prompt");
            ErrorMessage = $"Failed to start {ProcessName}";
            return;
        }


        if (_process == null || _process.HasExited)
        {
            ProcessStatus = ProcessStatus.NotInitialized;
            ErrorMessage = $"Failed to start {ProcessName}";
            _logger.Error($"Failed to start {ProcessName}");
            return;
        }

        ProcessStatus = ProcessStatus.Running;

        _process.Exited += (sender, args) =>
        {
            ProcessStatus = ProcessStatus.NotRunning;
            _logger.Information("{ProcessName} exited with exit code: {ExitCode}", ProcessName, _process.ExitCode);
        };

        if (_exitHandlerRegistered) return;


        //App.MainWindow.Closed += MainWindowExitHandler;
        //_exitHandlerRegistered = true;
    }

    private void MainWindowExitHandler(object sender, WindowEventArgs args)
        => StopProcess();


    public void CheckStatus()
    {
        if (ProcessStatus == ProcessStatus.NotInitialized) return;
        ProcessStatus = _process is { HasExited: false } ? ProcessStatus.Running : ProcessStatus.NotRunning;
    }

    public void StopProcess()
    {
        if (_process is { HasExited: false })
        {
            _logger.Information($"Killing {ProcessName}");
            _process.Kill();
            _logger.Debug($"{ProcessName} killed");
        }
    }

    /// <summary>
    /// Set path with SetPath()
    /// </summary>
    /// <param name="windowHandle"></param>
    /// <returns></returns>
    public async Task<string?> PickProcessPathAsync(Window windowHandle)
    {
        var exeFilePicker = new FileOpenPicker();
        exeFilePicker.FileTypeFilter.Add(".exe");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(windowHandle);
        WinRT.Interop.InitializeWithWindow.Initialize(exeFilePicker, hwnd);
        var chosenExePath = await exeFilePicker.PickSingleFileAsync();

        if (chosenExePath is null)
        {
            _logger.Information("User cancelled 3Dmigoto exe selection");
            return null;
        }

        if (chosenExePath.FileType != ".exe")
        {
            _logger.Warning("User selected a file that is not an exe");
            return null;
        }

        if (!File.Exists(chosenExePath.Path))
        {
            _logger.Warning("User selected a file that does not exist");
            return null;
        }

        return chosenExePath.Path;
    }
}

public enum ProcessStatus
{
    /// <summary>
    /// Process path not set and process is not running
    /// </summary>
    NotInitialized,

    /// <summary>
    /// Path set but process not running
    /// </summary>
    NotRunning,

    /// <summary>
    /// Path set and process running
    /// </summary>
    Running,
}

public class GenshinProcessManager : BaseProcessManager<GenshinProcessOptions>
{
    public GenshinProcessManager(ILogger logger, ILocalSettingsService localSettingsService) : base(
        logger.ForContext<GenshinProcessManager>(),
        localSettingsService)
    {
    }
}

public class ThreeDMigtoProcessManager : BaseProcessManager<MigotoProcessOptions>
{
    public ThreeDMigtoProcessManager(ILogger logger, ILocalSettingsService localSettingsService) : base(
        logger.ForContext<ThreeDMigtoProcessManager>(),
        localSettingsService)
    {
    }
}