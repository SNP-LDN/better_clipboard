# Better Clipboard

**Language:** [English](#english) | [中文](#中文)

## English

Better Clipboard is a Windows clipboard history app built with WPF. It runs quietly from the system tray, records useful clipboard changes, and lets you reopen recent clips with `Ctrl+Alt+V`.

The app is designed for day-to-day copying across text, file paths, and images, while keeping privacy-sensitive content out of long-term history whenever possible.

### Features

- Runs in the background with a Windows system tray icon.
- Captures clipboard changes for text, file lists, screenshots, web images, and common image files copied from File Explorer.
- Opens a compact clipboard history popup near the cursor with `Ctrl+Alt+V`.
- Searches history by clip preview text or source application.
- Groups history by time: Today, Yesterday, This Week, Last Week, This Month, Last Month, and Older.
- Supports tabs for all history, favorites, and settings.
- Pastes the selected item back into the previous target window by double-clicking or pressing `Enter`.
- Shows image thumbnails in history and opens a larger image preview with right-click.
- Tracks source application, copy count, copied time, content size, and item type.
- Lets you favorite clips so they are kept permanently until deleted.
- Lets you delete individual clips or clear all non-favorite clips.
- Supports pausing capture for 5 minutes, 30 minutes, or until manual resume.
- Provides settings for retention days, image capture, blocked applications, theme mode, and visual preset.
- Supports light, dark, system theme, standard style, and glass style.
- Writes diagnostic logs for troubleshooting.

### Privacy And Security

- Clipboard history is stored under the current Windows user profile and protected with Windows DPAPI.
- Text content is encrypted before it is written to `clips.json`.
- Images are encrypted and stored as separate files in the app data folder.
- Common password managers are blocked by default, including 1Password, Bitwarden, KeePass, LastPass, Dashlane, NordPass, and Proton Pass.
- Browser private windows such as Incognito and InPrivate are detected and skipped.
- Private keys, API keys, JWTs, GitHub tokens, and Slack tokens are skipped.
- Password-like or token-like assignments are masked and kept only temporarily.
- Short numeric codes, such as possible verification codes, are masked and kept for 5 minutes.
- Bank-card-like numbers are detected with a Luhn check and saved in masked form.

### Default Behavior

- Normal clipboard records are kept for 20 days.
- Favorite records do not expire automatically.
- Re-copying the same content updates the existing record instead of creating duplicates.
- Self-written clipboard changes from paste actions are ignored to avoid feedback loops.
- Empty content and blocked sources are not saved.

### Data Location

After the app runs, local data is stored in:

```text
%APPDATA%\BetterClipboard
```

Important files and folders:

- `clips.json`: encrypted clipboard history metadata and encrypted text payloads.
- `images`: encrypted image payloads.
- `settings.json`: retention, pause state, theme, image capture, and blocked app settings.
- `logs`: daily diagnostic log files.

### Requirements

- Windows
- .NET 9 SDK

### Run From Source

```powershell
cd BetterClipboard
dotnet run
```

After startup, the main window stays hidden. Look for **Better Clipboard** in the Windows system tray or hidden icons area.

### Basic Usage

1. Start the app.
2. Copy text, files, or images as usual.
3. Press `Ctrl+Alt+V` to open clipboard history near the cursor.
4. Search or select a clip.
5. Double-click the clip or press `Enter` to paste it into the previous target window.
6. Use the star button to keep important clips permanently.

### Project Structure

```text
BetterClipboard/
  Models/              Data models for clipboard records, sources, and settings
  Services/            Clipboard storage, privacy filtering, encryption, themes, and diagnostics
  Themes/              Light, dark, standard, and glass resource dictionaries
  MainWindow.*         Hidden host window, tray icon, clipboard listener, hotkey, and paste flow
  PopupWindow.*        Searchable history popup, favorites, settings tab, and item actions
  SettingsWindow.*     Standalone settings window
  ImagePreviewWindow.* Large image preview window
```

### Roadmap Ideas

- Add finer-grained privacy policy settings.
- Add image storage quotas and automatic cleanup rules.
- Add a startup-at-login option.
- Add highlighted search results, source-app filters, and batch deletion.
- Add installer or single-file release packaging.

---

## 中文

Better Clipboard 是一个基于 WPF 的 Windows 剪贴板历史工具。它会安静地驻留在系统托盘中，记录有用的剪贴板变化，并支持用 `Ctrl+Alt+V` 快速打开历史窗口。

这个应用面向日常复制场景，支持文本、文件路径和图片，同时尽量避免把隐私敏感内容长期保存到历史记录里。

### 功能特性

- 后台运行，并显示 Windows 系统托盘图标。
- 监听剪贴板变化，保存文本、文件列表、截图、网页图片，以及从资源管理器复制的常见图片文件。
- 使用 `Ctrl+Alt+V` 在鼠标附近打开紧凑的剪贴板历史窗口。
- 支持按内容预览或来源应用搜索历史记录。
- 按时间分组历史记录：今天、昨天、本周、上周、本月、上个月、更早。
- 提供全部历史、收藏、设置三个标签页。
- 双击条目或按 `Enter`，即可把选中的内容放回剪贴板并粘贴到之前的目标窗口。
- 图片历史会显示缩略图，右键图片条目可以打开大图预览。
- 显示来源应用、复制次数、复制时间、内容大小和条目类型。
- 支持星标收藏，收藏内容会永久保留，直到用户手动删除。
- 支持删除单条记录，或清空所有未收藏记录。
- 支持暂停记录 5 分钟、30 分钟，或暂停到手动恢复。
- 设置中可调整保留天数、图片保存开关、应用黑名单、主题模式和界面风格。
- 支持浅色、深色、跟随系统主题，以及标准和玻璃两种界面风格。
- 提供诊断日志，方便排查问题。

### 隐私与安全

- 剪贴板历史保存在当前 Windows 用户目录下，并使用 Windows DPAPI 保护。
- 文本内容在写入 `clips.json` 前会被加密。
- 图片会加密后作为独立文件保存在应用数据目录中。
- 默认排除常见密码管理器，包括 1Password、Bitwarden、KeePass、LastPass、Dashlane、NordPass 和 Proton Pass。
- 会检测并跳过浏览器的无痕或 InPrivate 窗口。
- 疑似私钥、API key、JWT、GitHub token、Slack token 会被跳过，不保存。
- 疑似密码或 token 赋值内容会被打码，并且只短暂保存。
- 疑似验证码的短数字内容会被打码，并只保留 5 分钟。
- 疑似银行卡号会通过 Luhn 校验检测，并以打码形式保存。

### 默认行为

- 普通剪贴板记录默认保留 20 天。
- 收藏记录不会自动过期。
- 重复复制相同内容时，会更新已有记录，而不是生成重复条目。
- 应用自己写入剪贴板用于粘贴的内容会被忽略，避免循环记录。
- 空内容和被屏蔽来源不会保存。

### 数据位置

运行后，本地数据会保存在：

```text
%APPDATA%\BetterClipboard
```

主要文件和目录：

- `clips.json`：剪贴板历史元数据，以及加密后的文本内容。
- `images`：加密后的图片内容。
- `settings.json`：保留天数、暂停状态、主题、图片保存开关和应用黑名单等设置。
- `logs`：按日期保存的诊断日志。

### 运行要求

- Windows
- .NET 9 SDK

### 从源码运行

```powershell
cd BetterClipboard
dotnet run
```

启动后主窗口不会显示，应用会出现在 Windows 系统托盘或隐藏图标区域中。

### 基本使用

1. 启动应用。
2. 像平常一样复制文本、文件或图片。
3. 按 `Ctrl+Alt+V` 在鼠标附近打开剪贴板历史。
4. 搜索或选择一个记录。
5. 双击记录，或按 `Enter`，把它粘贴回之前的目标窗口。
6. 点击星标按钮，可以让重要记录永久保留。

### 项目结构

```text
BetterClipboard/
  Models/              剪贴板记录、来源信息和设置相关的数据模型
  Services/            剪贴板存储、隐私过滤、加密、主题和诊断日志服务
  Themes/              浅色、深色、标准和玻璃风格资源字典
  MainWindow.*         隐藏宿主窗口、托盘图标、剪贴板监听、快捷键和粘贴流程
  PopupWindow.*        可搜索历史窗口、收藏页、设置页和条目操作
  SettingsWindow.*     独立设置窗口
  ImagePreviewWindow.* 图片大图预览窗口
```

### 后续建议

- 增加更细粒度的敏感内容策略设置。
- 增加图片总容量限制和自动清理策略。
- 增加开机自动启动选项。
- 增加搜索高亮、按来源应用过滤和批量删除。
- 增加安装包或单文件发布。
