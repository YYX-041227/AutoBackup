using System;
using System.Globalization;
using System.IO;

namespace AutoBackup
{
    public sealed class SettingsService
    {
        private const string SettingsFileName = "backup_settings.txt";

        public string SettingsFilePath { get; }

        public SettingsService()
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AutoBackup");
            Directory.CreateDirectory(appData);
            SettingsFilePath = Path.Combine(appData, SettingsFileName);
        }

        public BackupSettings Load()
        {
            BackupSettings settings = new BackupSettings();

            try
            {
                MigrateLegacySettingsIfNeeded();
            }
            catch
            {
                // Migration is best-effort; proceed with loading from
                // the current location (which may be empty → defaults).
            }

            if (!File.Exists(SettingsFilePath))
            {
                return settings;
            }

            try
            {
                foreach (string line in File.ReadLines(SettingsFilePath))
                {
                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, separatorIndex);
                    string value = line.Substring(separatorIndex + 1);

                    switch (key)
                    {
                        case "SourceFolder":
                            settings.SourceFolder = value;
                            break;
                        case "TargetFolder":
                            settings.TargetFolder = value;
                            break;
                        case "CompareFiles":
                            if (bool.TryParse(value, out bool compareFiles))
                            {
                                settings.CompareFiles = compareFiles;
                            }
                            break;
                        case "ScheduledBackup":
                            if (bool.TryParse(value, out bool scheduledBackup))
                            {
                                settings.ScheduledBackup = scheduledBackup;
                            }
                            break;
                        case "MinimizeToTray":
                            if (bool.TryParse(value, out bool minimizeToTray))
                            {
                                settings.MinimizeToTray = minimizeToTray;
                            }
                            break;
                        case "Frequency":
                            if (int.TryParse(value, out int frequency))
                            {
                                settings.Frequency = frequency;
                            }
                            break;
                        case "Time":
                            break;
                        case "Hour":
                            if (int.TryParse(value, out int hourIndex))
                            {
                                settings.HourIndex = hourIndex;
                            }
                            break;
                        case "Minute":
                            if (int.TryParse(value, out int minuteIndex))
                            {
                                settings.MinuteIndex = minuteIndex;
                            }
                            break;
                        case "AmPm":
                            if (int.TryParse(value, out int amPmIndex))
                            {
                                settings.AmPmIndex = amPmIndex;
                            }
                            break;
                        case "MinuteInterval":
                        case "Interval":
                            settings.Interval = value;
                            break;
                        case "DayOfWeek":
                            if (int.TryParse(value, out int dayOfWeekIndex))
                            {
                                settings.DayOfWeekIndex = dayOfWeekIndex;
                            }
                            break;
                        case "DayOfMonth":
                            if (int.TryParse(value, out int dayOfMonthIndex))
                            {
                                settings.DayOfMonthIndex = dayOfMonthIndex;
                            }
                            break;
                        case "VersionedBackup":
                            if (bool.TryParse(value, out bool versionedBackup))
                            {
                                settings.VersionedBackup = versionedBackup;
                            }
                            break;
                        case "MaxCapacityGB":
                            if (TryParseDouble(value, out double maxCapacityGB) && maxCapacityGB > 0)
                            {
                                settings.MaxCapacityGB = maxCapacityGB;
                            }
                            break;
                        case "RetentionDays":
                            if (int.TryParse(value, out int retentionDays) && retentionDays > 0)
                            {
                                settings.RetentionDays = retentionDays;
                            }
                            break;
                        case "NamingFormat":
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                settings.NamingFormat = value;
                            }
                            break;
                        case "AutoCleanup":
                            if (bool.TryParse(value, out bool autoCleanup))
                            {
                                settings.AutoCleanup = autoCleanup;
                            }
                            break;
                    }
                }
            }
            catch
            {
                // Corrupt settings file or read error → return defaults.
            }

            return settings;
        }

        public void Save(BackupSettings settings)
        {
            string? directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using StreamWriter writer = new StreamWriter(SettingsFilePath);
            writer.WriteLine($"SourceFolder={settings.SourceFolder}");
            writer.WriteLine($"TargetFolder={settings.TargetFolder}");
            writer.WriteLine($"CompareFiles={settings.CompareFiles}");
            writer.WriteLine($"ScheduledBackup={settings.ScheduledBackup}");
            writer.WriteLine($"MinimizeToTray={settings.MinimizeToTray}");
            writer.WriteLine($"Frequency={settings.Frequency}");
            writer.WriteLine($"Hour={settings.HourIndex}");
            writer.WriteLine($"Minute={settings.MinuteIndex}");
            writer.WriteLine($"AmPm={settings.AmPmIndex}");
            writer.WriteLine($"Interval={settings.Interval}");

            if (settings.DayOfWeekIndex >= 0)
            {
                writer.WriteLine($"DayOfWeek={settings.DayOfWeekIndex}");
            }

            if (settings.DayOfMonthIndex >= 0)
            {
                writer.WriteLine($"DayOfMonth={settings.DayOfMonthIndex}");
            }

            writer.WriteLine($"VersionedBackup={settings.VersionedBackup}");
            writer.WriteLine($"MaxCapacityGB={settings.MaxCapacityGB.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine($"RetentionDays={settings.RetentionDays}");
            writer.WriteLine($"NamingFormat={settings.NamingFormat}");
            writer.WriteLine($"AutoCleanup={settings.AutoCleanup}");
        }

        private void MigrateLegacySettingsIfNeeded()
        {
            // Try multiple possible legacy locations.
            string[] candidatePaths =
            {
                Path.Combine(Environment.CurrentDirectory, SettingsFileName),
                Path.Combine(AppContext.BaseDirectory, SettingsFileName),
            };

            foreach (string legacyPath in candidatePaths)
            {
                if (!File.Exists(legacyPath))
                {
                    continue;
                }

                string legacyFull = Path.GetFullPath(legacyPath);
                string currentFull = Path.GetFullPath(SettingsFilePath);

                if (string.Equals(legacyFull, currentFull, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Copy(legacyPath, SettingsFilePath, false);
                    // Successfully migrated from the first available legacy location.
                    return;
                }
                catch
                {
                    // Try the next candidate.
                }
            }
        }

        private static bool TryParseDouble(string value, out double result)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
                double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
        }
    }
}
