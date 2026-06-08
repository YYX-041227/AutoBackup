# 自动备份 (AutoBackup)

Windows 桌面文件夹备份工具，基于 Avalonia UI + .NET 9。

## 快速安装

从 [Releases](https://github.com/YYX-041227/AutoBackup/releases) 页面下载最新版 `AutoBackup-Setup-vX.X.X.exe`，双击安装即可，无需额外安装 .NET 运行时。

## 功能

- **文件夹备份** — 选择源文件夹和目标文件夹，递归复制
- **智能文件比对** — 按最后修改时间和文件大小跳过未变更的文件
- **版本化备份** — 每次备份创建独立的时间戳目录，保留历史版本
- **自动清理** — 按保留天数或容量上限自动清理过期备份
- **定时备份** — 支持每 N 分钟/小时、每天/每周/每月固定时间
- **后台运行** — 关闭窗口后最小化到系统托盘，定时备份继续执行
- **命令行模式** — `--autobackup` 静默执行一次备份后退出，适合配合 Windows 任务计划

## 使用说明

1. 启动后选择**源文件夹**（要备份的）和**目标文件夹**（备份存到哪）
2. 点击**立即备份**手动执行，或配置定时备份自动运行
3. 开启**版本化备份**后每次备份创建时间戳子目录，可设置容量限制和保留天数自动清理
4. 开启**后台运行**后关闭窗口最小化到系统托盘，定时任务继续执行

## 设置存储

设置保存在 `%AppData%\AutoBackup\backup_settings.txt`，首次启动自动从旧版项目目录迁移。

## 从源码构建

需要 Windows 10+ 和 [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)。

```bash
git clone https://github.com/YYX-041227/AutoBackup.git
cd AutoBackup

# 运行
dotnet run

# 命令行静默模式
dotnet run -- --autobackup

# 发布
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish-folder
```

Avalonia 依赖原生组件（SkiaSharp、HarfBuzzSharp），不支持 `PublishSingleFile` 模式，需以文件夹形式分发。

## 打包安装程序

先执行上面的 `dotnet publish` 命令，确保输出到 `publish-folder` 目录，然后：

使用 [Inno Setup 6](https://www.innosetup.com) 编译 `setup.iss` 即可生成安装包。
