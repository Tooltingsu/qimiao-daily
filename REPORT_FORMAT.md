# 绮喵日报格式

标题：`绮喵日报 YYMMDD`

正文首行：`今天是 YYYY 年 M 月 D 日，星期 X`。

当天存在时依次输出：节气、传统节日、角色生日、游戏周年和纪念日。之后输出：

1. 游戏活动预览（只读 CONFIRMED 且满足验证规则的内容）。
2. BGI 本体更新（用户选中的 Commit）。
3. BGI 脚本仓库更新（用户选中的 Commit）。
4. 美图分享（CONFIRMED 且 SelectedForReport 的作品）。

PENDING、RETURNED、ARCHIVED、UNVERIFIED、CONFLICT 和未选中的 BGI/美图不得进入正式日报。
