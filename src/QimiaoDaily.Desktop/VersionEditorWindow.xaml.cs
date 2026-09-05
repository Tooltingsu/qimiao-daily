using System.Windows;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop;

public partial class VersionEditorWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public VersionEditorWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.AddManualVersionCommand.ExecuteAsync(null);
        if (_viewModel.ImportMessage.StartsWith("版本已保存", StringComparison.Ordinal)
            || _viewModel.ImportMessage.StartsWith("版本已更新", StringComparison.Ordinal))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
