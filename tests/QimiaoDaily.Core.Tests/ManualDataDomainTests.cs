using QimiaoDaily.Core;

namespace QimiaoDaily.Core.Tests;

public sealed class ManualDataDomainTests
{
    [Fact]
    public void Banner_AddCharacter_PreservesExplicitOrder()
    {
        var start = new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(8));
        var banner = new Banner("GENSHIN", "月之一", "上半卡池", start, start.AddDays(21), DataOrigin.Manual);

        banner.AddCharacter("哥伦比娅");
        banner.AddCharacter("雷电将军");

        Assert.Equal(["哥伦比娅", "雷电将军"], banner.Characters.Select(x => x.Name));
        Assert.Equal([0, 1], banner.Characters.Select(x => x.SortOrder));
    }
}
