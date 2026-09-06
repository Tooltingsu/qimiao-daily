export function forumThreadPayload(title, content, format = 3) {
  if (!String(title).trim()) throw new Error("论坛帖子标题不能为空。");
  if (!String(content).trim()) throw new Error("论坛帖子内容不能为空。");
  if (![1, 2, 3, 4].includes(format)) throw new Error("论坛帖子 format 必须是 QQ 官方定义的 1、2、3 或 4。");
  return { title: String(title), content: String(content), format };
}

export function forumImagePayload(title, caption, imageUrl) {
  if (!/^https:\/\//.test(imageUrl)) throw new Error("论坛图片测试需要 HTTPS 的图片 URL。");
  const richText = {
    paragraphs: [{
      elems: [
        { type: 1, text: { text: String(caption) } },
        { type: 2, image: { third_url: imageUrl, width_percent: 1 } }
      ]
    }]
  };
  return forumThreadPayload(title, JSON.stringify(richText), 4);
}

export function forumTitle(mode, date, sequence = 1, total = 1) {
  const labels = { text: "连接测试", long: "长文本测试", image: "图片测试", report: "完整日报测试" };
  const suffix = total > 1 ? `（${sequence}/${total}）` : "";
  return `【测试】绮喵日报 V4-C ${labels[mode] ?? mode} ${date}${suffix}`;
}
