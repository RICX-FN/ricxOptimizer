using System;
using System.Collections.Generic;

namespace ricxOptimizer.Models
{
    public class ServiceSnapshot
    {
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string StartMode { get; set; } = string.Empty; // Boot, System, Auto, Demand, Disabled
        public string State { get; set; } = string.Empty; // Running, Stopped
    }

    public class BackupMetadata
    {
        public string BackupId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string WindowsVersion { get; set; } = string.Empty;
        public string ComputerName { get; set; } = string.Empty;
        public bool SystemRestorePointCreated { get; set; }
        public string OriginalPowerScheme { get; set; } = string.Empty;
        public string RegistryBackupFile { get; set; } = string.Empty;
        public string BcdBackupFile { get; set; } = string.Empty;
        public List<ServiceSnapshot> Services { get; set; } = new List<ServiceSnapshot>();
    }
}
