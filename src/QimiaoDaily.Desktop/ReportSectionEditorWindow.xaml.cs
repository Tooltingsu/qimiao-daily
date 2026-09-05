using System.Windows;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop;

public partial class ReportSectionEditorWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly ReportSectionCard _card;
    private readonly string _originalText;
    private bool _saved;

    public ReportSectionEditorWindow(ShellViewModel viewModel, ReportSectionCard card)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _card = card;
        _originalText = card.Text;
        DataContext = card;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveReportSectionCommand.ExecuteAsync(_card);
        if (_viewModel.ImportMessage.StartsWith("日报段落已保存", StringComparison.Ordinal))
        {
            _saved = true;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _card.Text = _originalText;
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_saved) _card.Text = _originalText;
        base.OnClosed(e);
    }
}
