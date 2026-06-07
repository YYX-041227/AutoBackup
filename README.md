# 自动备份 (AutoBackup)

文件夹自动备份桌面工具，基于 Avalonia UI + .NET 9。

## 功能

- **文件夹备份** — 选择源文件夹和目标文件夹，递归复制
- **智能文件比对** — 按最后修改时间和文件大小跳过未变更的文件
- **版本化备份** — 每次备份创建独立的时间戳目录，保留历史版本
- **自动清理** — 按保留天数或容量上限自动清理过期备份
- **定时备份** — 支持每 N 分钟/小时、每天/每周/每月固定时间
- **后台运行** — 关闭窗口后最小化到系统托盘，定时备份继续执行
- **命令行模式** — `--autobackup` 参数静默执行一次备份后退出，适合 Windows 任务计划

## 环境要求

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) 或更高版本
- Windows / macOS / Linux

## 运行

```bash
# 克隆项目
git clone https://github.com/<your-name>/AutoBackup.git
cd AutoBackup

# 还原依赖并编译
dotnet restore
dotnet build

# 启动主窗口
dotnet run

# 命令行静默模式（执行一次备份后退出，适合配合 Windows 任务计划使用）
dotnet run -- --autobackup
```

## 发布为单文件

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

输出 `./publish/AutoBackup.exe`（约 82 MB），自带 .NET 运行时，可直接运行。

也可以从 [Releases](https://github.com/<your-name>/AutoBackup/releases) 页面下载预编译版本。

## 使用说明

1. 启动后选择**源文件夹**（要备份的）和**目标文件夹**（备份存到哪）
2. 点击**立即备份**手动执行，或配置定时备份自动运行
3. 开启**版本化备份**后每次备份创建时间戳子目录，同时可设置容量限制和保留天数自动清理
4. 开启**后台运行**后关闭窗口会最小化到系统托盘，定时任务继续执行

## 设置存储

设置文件保存在 `%AppData%\AutoBackup\backup_settings.txt`（Windows）或 `~/.config/AutoBackup/backup_settings.txt`（Linux/macOS）。首次启动时自动从旧版项目目录迁移。
