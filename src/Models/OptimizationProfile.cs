using System;

namespace ricxOptimizer.Models
{
    public enum OptimizationMode
    {
        None,
        WindowsDefault,
        WindowsGamingMode,
        WindowsGamingUltra
    }

    public class OptimizationStepLog
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool Success { get; set; } = true;

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] [{Category}] {(Success ? "[OK]" : "[FAIL]")} {Message}";
        }
    }
}
