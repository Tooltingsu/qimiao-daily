export function rowsForGuilds(guilds, channelsByGuild) {
  return guilds.flatMap(guild => (channelsByGuild.get(String(guild.id)) ?? []).map(channel => ({
    guildName: String(guild.name ?? "未命名频道"),
    guildId: String(guild.id),
    channelName: String(channel.name ?? "未命名子频道"),
    channelId: String(channel.id),
    channelType: Number(channel.type)
  })));
}

export function escapeMarkdown(value) {
  return String(value).replace(/([\\`|])/g, "\\$1").replace(/[\r\n]+/g, " ");
}

export function renderDiscoveryMarkdown(rows) {
  const render = items => items.length
    ? ["| Guild Name | Guild ID | Channel Name | Channel ID | Channel Type |", "| --- | --- | --- | --- | ---: |",
      ...items.map(x => `| ${escapeMarkdown(x.guildName)} | ${escapeMarkdown(x.guildId)} | ${escapeMarkdown(x.channelName)} | ${escapeMarkdown(x.channelId)} | ${x.channelType} |`)].join("\n")
    : "未找到可访问的子频道。";
  const textChannels = rows.filter(x => x.channelType === 0);
  return [
    "# QQ 测试目标发现",
    "",
    `发现 Guild：${new Set(rows.map(x => x.guildId)).size}；子频道：${rows.length}；文字子频道（type=0）：${textChannels.length}。`,
    "",
    "## 优先选择：文字子频道（type=0）",
    "",
    render(textChannels),
    "",
    "## 全部可访问子频道",
    "",
    render(rows),
    "",
    "将选中的 Guild ID / Channel ID 填入 qq-test Environment Variables：`QQ_TEST_GUILD_ID`、`QQ_TEST_CHANNEL_ID`。此 Summary 不包含 AppSecret 或 access token。"
  ].join("\n");
}
