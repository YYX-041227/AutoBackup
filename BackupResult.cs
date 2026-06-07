using System.Collections.Generic;

namespace AutoBackup
{
    public sealed class BackupResult
    {
        public bool Success { get; set; }
        public string StatusText { get; set; } = "就绪";
        public int CopiedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public List<string> Messages { get; } = new List<string>();
    }
}
