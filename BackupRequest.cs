namespace AutoBackup
{
    public sealed class BackupRequest
    {
        public string SourceFolder { get; set; } = "";
        public string TargetFolder { get; set; } = "";
        public bool CompareFiles { get; set; }
        public bool VersionedBackup { get; set; }
        public double MaxCapacityGB { get; set; } = 10.0;
        public int RetentionDays { get; set; } = 30;
        public string NamingFormat { get; set; } = "yyyy-MM-dd_HH-mm-ss";
        public bool AutoCleanup { get; set; } = true;
    }
}
