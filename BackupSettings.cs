namespace AutoBackup
{
    public sealed class BackupSettings
    {
        public string SourceFolder { get; set; } = "";
        public string TargetFolder { get; set; } = "";
        public bool CompareFiles { get; set; } = true;
        public bool ScheduledBackup { get; set; }
        public bool MinimizeToTray { get; set; } = true;
        public int Frequency { get; set; }
        public int HourIndex { get; set; } = 11;
        public int MinuteIndex { get; set; }
        public int AmPmIndex { get; set; }
        public string Interval { get; set; } = "10";
        public int DayOfWeekIndex { get; set; } = -1;
        public int DayOfMonthIndex { get; set; } = -1;
        public bool VersionedBackup { get; set; }
        public double MaxCapacityGB { get; set; } = 10.0;
        public int RetentionDays { get; set; } = 30;
        public string NamingFormat { get; set; } = "yyyy-MM-dd_HH-mm-ss";
        public bool AutoCleanup { get; set; } = true;
    }
}
