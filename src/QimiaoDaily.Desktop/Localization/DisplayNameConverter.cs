using System.Globalization;
using System.Windows.Data;

namespace QimiaoDaily.Desktop.Localization;

public sealed class DisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        return parameter?.ToString()?.ToLowerInvariant() switch
        {
            "game" => DisplayNameMapper.Game(text),
            "type" => DisplayNameMapper.ItemType(text),
            "status" => DisplayNameMapper.Verification(text),
            "task" => DisplayNameMapper.Task(text),
            "provider" => DisplayNameMapper.ProviderStatus(text),
            "precision" => DisplayNameMapper.TimePrecision(text),
            "gacha-kind" => DisplayNameMapper.GachaPoolKind(text),
            "gacha-phase" => DisplayNameMapper.GachaPoolPhase(text),
            _ => DisplayNameMapper.Auto(text)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
