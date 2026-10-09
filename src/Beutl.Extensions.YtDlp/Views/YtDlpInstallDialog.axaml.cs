using Avalonia.Input;
using Beutl.Extensions.YtDlp.ViewModels;
using FluentAvalonia.UI.Controls;

namespace Beutl.Extensions.YtDlp.Views;

public partial class YtDlpInstallDialog : FAContentDialog
{
    public YtDlpInstallDialog()
    {
        InitializeComponent();
    }

    protected override Type StyleKeyOverride => typeof(FAContentDialog);

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
        }

        base.OnKeyUp(e);
    }

    protected override async void OnPrimaryButtonClick(FAContentDialogButtonClickEventArgs args)
    {
        base.OnPrimaryButtonClick(args);
        if (DataContext is not YtDlpInstallDialogViewModel viewModel) return;
        args.Cancel = true;

        // Notice page -> download page
        if (Root.Root.SelectedIndex == 0)
        {
            Root.Root.SelectedIndex = 1;
            PrimaryButtonText = "インストール";
        }
        // Download page (not started yet)
        else if (Root.Root.SelectedIndex == 1)
        {
            if (!viewModel.IsCompleted.Value)
            {
                IsPrimaryButtonEnabled = false;
                await viewModel.Install();
                CloseButtonText = "閉じる";
            }
        }
    }

    protected override void OnCloseButtonClick(FAContentDialogButtonClickEventArgs args)
    {
        base.OnCloseButtonClick(args);
        if (DataContext is not YtDlpInstallDialogViewModel viewModel) return;
        if (Root.Root.SelectedIndex == 1 && viewModel.IsInstalling.Value)
        {
            viewModel.Cancel();
        }
    }
}
