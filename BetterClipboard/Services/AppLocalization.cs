using System.Globalization;
using System.Windows;
using BetterClipboard.Models;

namespace BetterClipboard.Services;

public static class AppLocalization
{
    public static AppLanguage Language { get; private set; } = AppLanguage.Chinese;

    private static readonly Dictionary<string, string> English = new()
    {
        ["{0} 字"] = "{0} chars",
        ["{0} 字符"] = "{0} chars",
        ["Better Clipboard 私有内存，不含共享运行库"] = "Private committed memory; excludes shared libraries",
        ["上个月"] = "Last month",
        ["上周"] = "Last week",
        ["今天"] = "Today",
        ["保存图片和截图到历史"] = "Save images and screenshots",
        ["保存设置"] = "Save settings",
        ["保留天数请输入 1 到 3650 之间的整数。"] = "Enter a whole number between 1 and 3650 for retention days.",
        ["全部"] = "All",
        ["全部收藏夹"] = "All folders",
        ["关闭"] = "Close",
        ["内存 {0:0} MB"] = "Memory {0:0} MB",
        ["删除"] = "Delete",
        ["删除当前收藏夹"] = "Delete current folder",
        ["删除所选 ({0})"] = "Delete ({0})",
        ["删除所选 (0)"] = "Delete (0)",
        ["删除所选记录"] = "Delete selected items",
        ["删除收藏夹"] = "Delete folder",
        ["双击或按 Enter 粘贴；星标后永久保留。"] = "Double-click or Enter to paste; star to keep forever.",
        ["取消"] = "Cancel",
        ["取消固定窗口"] = "Unpin window",
        ["右键查看大图"] = "Right-click to preview",
        ["固定窗口"] = "Pin window",
        ["图片"] = "Image",
        ["图片预览"] = "Image preview",
        ["基础收藏夹"] = "Default folder",
        ["外观"] = "Appearance",
        ["外观已更新。"] = "Appearance updated.",
        ["将所选内容移出收藏夹"] = "Remove selected items from favorites",
        ["已删除 {0} 条记录。"] = "Deleted {0} items.",
        ["已删除收藏夹“{0}”，并将 {1} 条收藏移动到“基础收藏夹”。"] = "Deleted folder “{0}”; {1} favorites moved to the default folder.",
        ["已删除收藏夹“{0}”。"] = "Deleted folder “{0}”.",
        ["已存在同名收藏夹。"] = "A folder with this name already exists.",
        ["已将 {0} 条内容加入“{1}”。"] = "Moved {0} items to “{1}”.",
        ["已将 {0} 条内容移出收藏夹。"] = "Removed {0} items from favorites.",
        ["已恢复默认值。"] = "Defaults restored.",
        ["已新建收藏夹“{0}”。"] = "Created folder “{0}”.",
        ["已暂停，直到手动恢复。"] = "Paused until manually resumed.",
        ["已暂停，预计恢复时间：{0:yyyy-MM-dd HH:mm}"] = "Paused until {0:yyyy-MM-dd HH:mm}.",
        ["已选择 {0} 条内容；可加入目标收藏夹或移出收藏。"] = "{0} selected; move to a folder or unfavorite.",
        ["应用黑名单"] = "Blocked apps",
        ["恢复"] = "Reset",
        ["恢复记录"] = "Resume capture",
        ["恢复默认值"] = "Reset defaults",
        ["恢复默认值会重置保留天数、图片保存开关、应用黑名单、外观及语言，并恢复剪贴板记录。是否继续？"] = "Reset retention, image capture, blocked apps, appearance and language, and resume capture?",
        ["所选内容已经在这个收藏夹中。"] = "The selected items are already in this folder.",
        ["收藏"] = "Favorites",
        ["收藏夹"] = "Folder",
        ["收藏夹名称需要 1 到 40 个字符。"] = "Folder names must contain 1 to 40 characters.",
        ["收藏夹未删除。"] = "Folder was not deleted.",
        ["收藏或取消收藏"] = "Toggle favorite",
        ["收藏管理"] = "Manage",
        ["收藏项会一直保留。"] = "Favorites are kept forever.",
        ["文件"] = "Files",
        ["文本"] = "Text",
        ["新建收藏夹"] = "New folder",
        ["明暗模式"] = "Color mode",
        ["昨天"] = "Yesterday",
        ["普通记录保留天数"] = "Retention days",
        ["暂停 30 分钟"] = "Pause 30 min",
        ["暂停 5 分钟"] = "Pause 5 min",
        ["暂停直到恢复"] = "Pause indefinitely",
        ["暂停设置已更新。"] = "Pause settings updated.",
        ["更早"] = "Earlier",
        ["本周"] = "This week",
        ["本月"] = "This month",
        ["柔和玻璃"] = "Glass",
        ["正在记录剪贴板变化。"] = "Capturing clipboard changes.",
        ["每行一个进程名或关键词。"] = "One process name or keyword per line.",
        ["没有匹配内容"] = "No matching items",
        ["没有可移出的收藏内容。"] = "No favorites to remove.",
        ["浅色"] = "Light",
        ["深色"] = "Dark",
        ["清爽标准"] = "Standard",
        ["清空未收藏"] = "Clear nonfavorites",
        ["玻璃透明度已更新。"] = "Glass opacity updated.",
        ["界面风格"] = "Style",
        ["疑似密码/密钥"] = "Possible password/key",
        ["疑似银行卡号"] = "Possible card number",
        ["疑似验证码"] = "Possible verification code",
        ["知道了"] = "OK",
        ["确定"] = "OK",
        ["确定删除收藏夹“{0}”吗？"] = "Delete folder “{0}”?",
        ["确定删除收藏夹“{0}”吗？其中 {1} 条收藏将移动到“基础收藏夹”。"] = "Delete folder “{0}”? {1} favorites will move to the default folder.",
        ["确定删除选中的 {0} 条记录吗？此操作无法撤销。"] = "Delete {0} selected items? This cannot be undone.",
        ["确认移动所选收藏"] = "Move selected favorites",
        ["移出收藏夹"] = "Unfavorite",
        ["移动所选到"] = "Move to",
        ["窗口已取消固定。"] = "Window unpinned.",
        ["窗口已固定，将保持在最上层。"] = "Window pinned; it will stay on top.",
        ["记录已恢复。"] = "Capture resumed.",
        ["记录状态"] = "Capture status",
        ["设置"] = "Settings",
        ["设置变更需要点击保存。"] = "Click Save settings to save changes.",
        ["设置已保存。"] = "Settings saved.",
        ["设置未保存"] = "Settings not saved",
        ["语言"] = "Language",
        ["语言已更新。"] = "Language updated.",
        ["请先选择目标收藏夹。"] = "Select a destination folder first.",
        ["请先选择要移出的收藏内容。"] = "Select favorites to remove first.",
        ["请先选择要移动的收藏内容。"] = "Select favorites to move first.",
        ["跟随系统"] = "System",
        ["输入新收藏夹名称"] = "Enter a new folder name",
        ["选择收藏后，可将它移动到其他收藏夹。"] = "Select a favorite to move it to another folder.",
        ["选择此记录"] = "Select this item",
        ["透明度"] = "Opacity",
    };

    public static string Text(string chinese, params object?[] arguments)
    {
        var template = Language == AppLanguage.English && English.TryGetValue(chinese, out var english)
            ? english : chinese;
        return arguments.Length == 0 ? template : string.Format(CultureInfo.CurrentCulture, template, arguments);
    }

    public static void Apply(AppLanguage language)
    {
        Language = language == AppLanguage.English ? AppLanguage.English : AppLanguage.Chinese;
        if (Application.Current is not { } app) return;
        foreach (var chinese in English.Keys)
        {
            app.Resources["Loc." + string.Concat(chinese.Select(c => char.IsWhiteSpace(c) ? '_' : c))] = Text(chinese);
        }
        app.Resources["Loc.GroupCountSuffix"] = Language == AppLanguage.English ? " items" : " 条";
    }
}
