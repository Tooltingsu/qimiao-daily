using System.Windows;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop;

public partial class BannerEditorWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public BannerEditorWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.AddManualBannerCommand.ExecuteAsync(null);
        if (_viewModel.ImportMessage.StartsWith("卡池已保存", StringComparison.Ordinal)
            || _viewModel.ImportMessage.StartsWith("卡池已更新", StringComparison.Ordinal))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
