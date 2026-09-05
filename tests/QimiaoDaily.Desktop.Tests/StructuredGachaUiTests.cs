using QimiaoDaily.Desktop.ViewModels;
using QimiaoDaily.Desktop.Localization;

namespace QimiaoDaily.Desktop.Tests;

public sealed class StructuredGachaUiTests
{
    [Fact]
    public void Mapper_LocalizesKnownAndUnknownPoolValues()
    {
        Assert.Equal("\u89d2\u8272\u6c60", DisplayNameMapper.GachaPoolKind("CHARACTER"));
        Assert.Equal("光锥池", DisplayNameMapper.GachaPoolKind("LIGHT_CONE"));
        Assert.Equal("\u4e0a\u534a", DisplayNameMapper.GachaPoolPhase("FIRST_HALF"));
        Assert.Equal("全版本", DisplayNameMapper.GachaPoolPhase("FULL_VERSION"));
        Assert.Equal("\u5f85\u786e\u8ba4", DisplayNameMapper.GachaPoolKind("UNKNOWN"));
    }

    [Fact]
    public void Card_OnlyDisplaysPoolForGacha()
    {
        var gacha = new GameActivityCard(Guid.NewGuid(), "GENSHIN", "GACHA · NEW", "title", "time", "remaining", "url", "summary", "Unverified", null, null, "CHARACTER", "FIRST_HALF");
        var eventCard = new GameActivityCard(Guid.NewGuid(), "GENSHIN", "EVENT", "title", "time", "remaining", "url", "summary", "Unverified", null, null, "CHARACTER", "FIRST_HALF");
        Assert.Contains("\u89d2\u8272\u6c60", gacha.DisplayGachaPool);
        Assert.Equal(string.Empty, eventCard.DisplayGachaPool);
    }

    [Fact]
    public void Converter_LocalizesGachaEditorOptions()
    {
        var converter = new DisplayNameConverter();
        Assert.Equal("\u89d2\u8272\u6c60", converter.Convert("CHARACTER", typeof(string), "gacha-kind", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("\u4e0a\u534a", converter.Convert("FIRST_HALF", typeof(string), "gacha-phase", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Card_LocalizesNevernessProviderInEvidenceSummary()
    {
        var card = new GameActivityCard(Guid.NewGuid(), "NTE", "EVENT", "title", "time", "remaining", "url", "NteNevernessGgBirthday: Birthdate", "Unverified");
        Assert.Contains("Neverness.gg", card.DisplayEvidenceSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("NteNevernessGgBirthday", card.DisplayEvidenceSummary, StringComparison.Ordinal);
    }
}
