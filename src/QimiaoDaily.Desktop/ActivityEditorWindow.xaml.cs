using System.Windows;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop;

public partial class ActivityEditorWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public ActivityEditorWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveActivityEditCommand.ExecuteAsync(null);
        if (_viewModel.ImportMessage.StartsWith("游戏候选已保存", StringComparison.Ordinal))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
