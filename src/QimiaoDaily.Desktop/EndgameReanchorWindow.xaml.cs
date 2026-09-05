using System.Windows;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop;

public partial class EndgameReanchorWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public EndgameReanchorWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ApplyEndgameReanchorCommand.ExecuteAsync(null);
        if (_viewModel.ImportMessage.StartsWith("周期锚点已保存", StringComparison.Ordinal))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
