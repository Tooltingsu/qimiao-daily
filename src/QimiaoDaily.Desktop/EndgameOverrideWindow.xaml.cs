using System.Windows;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop;

public partial class EndgameOverrideWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public EndgameOverrideWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ApplyEndgameOverrideCommand.ExecuteAsync(null);
        if (_viewModel.ImportMessage.StartsWith("单期调整已保存", StringComparison.Ordinal))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
