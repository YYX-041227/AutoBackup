using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AutoBackup
{
    public sealed class BackupService
    {
        public BackupResult Execute(BackupRequest request)
        {
            BackupResult result = new BackupResult();

            try
            {
                if (string.IsNullOrWhiteSpace(request.SourceFolder) || string.IsNullOrWhiteSpace(request.TargetFolder))
                {
                    result.StatusText = "备份失败";
                    result.Messages.Add("错误：请选择源文件夹和目标文件夹");
                    return result;
                }

                if (!Directory.Exists(request.SourceFolder))
                {
                    result.StatusText = "备份失败";
                    result.Messages.Add($"错误：源文件夹 {request.SourceFolder} 不存在");
                    return result;
                }

                if (!Directory.Exists(request.TargetFolder))
                {
                    Directory.CreateDirectory(request.TargetFolder);
                    result.Messages.Add($"已创建目标文件夹：{request.TargetFolder}");
                }

                string actualTargetFolder = request.TargetFolder;
                if (request.VersionedBackup)
                {
                    string timestamp = DateTime.Now.ToString(request.NamingFormat, CultureInfo.CurrentCulture);
                    actualTargetFolder = Path.Combine(request.TargetFolder, timestamp);
                    Directory.CreateDirectory(actualTargetFolder);
                    result.Messages.Add($"创建版本化备份文件夹：{actualTargetFolder}");
                }

                result.Messages.Add($"开始备份 {request.SourceFolder} 到 {actualTargetFolder}");

                CopyDirectory(request.SourceFolder, actualTargetFolder, request.CompareFiles, result);

                result.Success = true;
                result.StatusText = "备份完成";
                result.Messages.Add($"备份完成。复制了 {result.CopiedFiles} 个文件，跳过了 {result.SkippedFiles} 个未更改的文件。");

                if (request.VersionedBackup && request.AutoCleanup)
                {
                    PerformAutoCleanup(request, result);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.StatusText = "备份失败";
                result.Messages.Add($"备份过程中发生错误：{ex.Message}");
            }

            return result;
        }

        private void CopyDirectory(string sourceDir, string targetDir, bool compareFiles, BackupResult result)
        {
            var dirs = new System.Collections.Generic.Stack<(string Source, string Target)>();
            dirs.Push((sourceDir, targetDir));

            while (dirs.Count > 0)
            {
                var (src, dst) = dirs.Pop();
                Directory.CreateDirectory(dst);

                foreach (string file in Directory.EnumerateFiles(src))
                {
                    string fileName = Path.GetFileName(file);
                    string targetFile = Path.Combine(dst, fileName);
                    bool shouldCopy = true;

                    if (compareFiles && File.Exists(targetFile))
                    {
                        FileInfo sourceInfo = new FileInfo(file);
                        FileInfo targetInfo = new FileInfo(targetFile);

                        if (sourceInfo.LastWriteTime == targetInfo.LastWriteTime && sourceInfo.Length == targetInfo.Length)
                        {
                            shouldCopy = false;
                            result.SkippedFiles++;
                        }
                    }

                    if (shouldCopy)
                    {
                        File.Copy(file, targetFile, true);
                        File.SetLastWriteTime(targetFile, File.GetLastWriteTime(file));
                        result.CopiedFiles++;
                    }
                }

                foreach (string directory in Directory.EnumerateDirectories(src))
                {
                    string dirName = Path.GetFileName(directory);
                    dirs.Push((directory, Path.Combine(dst, dirName)));
                }
            }
        }

        private void PerformAutoCleanup(BackupRequest request, BackupResult result)
        {
            try
            {
                result.Messages.Add("开始执行自动清理...");

                // Parse directory-name timestamps for reliable ordering.
                // CreationTime is not trustworthy — it changes when directories
                // are moved or copied.
                var backupEntries = Directory.GetDirectories(request.TargetFolder)
                    .Select(dir =>
                    {
                        string name = Path.GetFileName(dir);
                        if (!DateTime.TryParseExact(name, request.NamingFormat,
                                CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsed))
                        {
                            return null;
                        }

                        return new
                        {
                            Path = dir,
                            Name = name,
                            Timestamp = parsed,
                            Size = GetDirectorySize(dir)
                        };
                    })
                    .Where(entry => entry != null)
                    .Cast<dynamic>()
                    .OrderBy(entry => entry.Timestamp)
                    .ToList();

                if (backupEntries.Count == 0)
                {
                    result.Messages.Add("未找到版本化备份目录，跳过清理。");
                    return;
                }

                // Retention-days cleanup: delete backups older than cutoff.
                DateTime cutoffDate = DateTime.Now.AddDays(-request.RetentionDays);
                var expiredEntries = backupEntries
                    .Where(entry => entry.Timestamp < cutoffDate)
                    .ToList();

                foreach (var entry in expiredEntries)
                {
                    Directory.Delete(entry.Path, true);
                    result.Messages.Add($"删除过期备份：{entry.Name}");
                }

                var remainingEntries = backupEntries
                    .Where(entry => entry.Timestamp >= cutoffDate)
                    .ToList();

                // Capacity cleanup: delete oldest backups until under limit.
                long totalSizeBytes = remainingEntries.Sum(entry => (long)entry.Size);
                long maxSizeBytes = (long)(request.MaxCapacityGB * 1024L * 1024L * 1024L);

                if (totalSizeBytes > maxSizeBytes)
                {
                    result.Messages.Add($"当前备份总大小 {totalSizeBytes / (1024.0 * 1024 * 1024):F2} GB 超过限制 {request.MaxCapacityGB} GB，开始清理最早的备份");

                    foreach (var entry in remainingEntries.OrderBy(entry => entry.Timestamp))
                    {
                        if (totalSizeBytes <= maxSizeBytes)
                        {
                            break;
                        }

                        long dirSize = (long)entry.Size;
                        Directory.Delete(entry.Path, true);
                        totalSizeBytes -= dirSize;
                        result.Messages.Add($"删除备份以释放空间：{entry.Name}（释放 {dirSize / (1024.0 * 1024 * 1024):F2} GB）");
                    }
                }

                result.Messages.Add($"自动清理完成。当前备份总大小：{totalSizeBytes / (1024.0 * 1024 * 1024):F2} GB");
            }
            catch (Exception ex)
            {
                result.Messages.Add($"自动清理过程中发生错误：{ex.Message}");
            }
        }

        private long GetDirectorySize(string directoryPath)
        {
            try
            {
                return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }
    }
}
