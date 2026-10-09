using Beutl.Extensions.YtDlp.Services;
using Reactive.Bindings;

namespace Beutl.Extensions.YtDlp.ViewModels;

public sealed class YtDlpInstallDialogViewModel(YtDlpEnvironment environment)
{
    private readonly YtDlpInstaller _installer = new();
    private CancellationTokenSource? _cts;

    public ReactiveProperty<bool> IsInstalling { get; } = new(false);

    public ReactiveProperty<double> Progress => _installer.Progress;

    public ReactiveProperty<double> ProgressMax => _installer.ProgressMax;

    public ReactiveProperty<bool> IsIndeterminate => _installer.IsIndeterminate;

    public ReactiveProperty<string> Status => _installer.Status;

    public ReactiveProperty<string> Error => _installer.Error;

    public ReactiveProperty<bool> IsCompleted => _installer.IsCompleted;

    public string InstallDirectory => YtDlpLocator.InstallDirectory;

    public async Task Install()
    {
        _cts = new CancellationTokenSource();
        IsInstalling.Value = true;
        try
        {
            await _installer.InstallAsync(_cts.Token);
            // Re-resolve so open tool tabs pick up the new binary and version.
            await environment.RefreshAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsInstalling.Value = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }
}
