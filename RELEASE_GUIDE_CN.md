# Better Clipboard 发布与更新

本项目通过 GitHub Actions 和 GitHub Releases 自动生成 Windows 安装包及在线更新文件，不需要单独租用服务器。

## 第一次发布 v1.0.4

1. 将本次改动推送到 `SNP-LDN/better_clipboard` 仓库的默认分支。
2. 打开 GitHub 仓库的 **Actions** 页面，选择 **Build and publish Windows release**。
3. 点击 **Run workflow**，版本号填写 `1.0.4`。
4. 工作流完成后，在仓库 **Releases** 页面下载并分发 `BetterClipboard-win-Setup.exe`。

用户只需安装一次。安装程序会创建桌面、开始菜单和开机启动快捷方式。

Velopack 默认采用一键安装，不显示传统安装向导，安装位置为：

```text
%LocalAppData%\BetterClipboard
```

首次安装并成功启动后，应用会显示安装完成提示。需要指定其他安装目录时，可以从命令行运行：

```text
BetterClipboard-win-Setup.exe --installto "D:\BetterClipboard"
```

## 发布后续版本

1. 在 `BetterClipboard/BetterClipboard.csproj` 中将 `<VersionPrefix>` 改为新版本，例如 `1.0.5`。
2. 推送代码后，在 GitHub Actions 中运行发布工作流，填写相同的新版本号 `1.0.5`。
3. 发布完成后，已安装的客户端会在启动时检测 GitHub Release，并提示用户更新。

版本号必须使用 `主版本.次版本.修订号` 格式，例如 `1.2.3`，且不能重复发布已经存在的版本号。

## 注意事项

- GitHub 仓库需要保持公开，客户端才能在不携带密钥的情况下检查更新。
- GitHub Actions 工作流需要仓库的 Actions 权限以及 `contents: write` 权限。
- 正式对外分发前建议购买 Windows 代码签名证书，否则 Windows 可能显示“未知发布者”提示。
