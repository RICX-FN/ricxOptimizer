using System;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ricxOptimizer.Core
{
    public static class Win32Native
    {
        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSetTimerResolution(uint desiredResolution, bool setResolution, out uint currentResolution);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryTimerResolution(out uint minimumResolution, out uint maximumResolution, out uint currentResolution);

        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsUserAnAdmin();

        [DllImport("ntdll.dll")]
        public static extern uint NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);

        // SystemMemoryListInformation = 80
        // MemoryPurgeStandbyList = 4
        public const int SystemMemoryListInformation = 80;
        public const int MemoryPurgeStandbyList = 4;
        public const int MemoryEmptyWorkingSets = 2;

        public static bool IsAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return IsUserAnAdmin();
            }
        }

        public static bool SetHighPrecisionTimer(uint resolution100ns = 5000) // 5000 = 0.5ms
        {
            try
            {
                int status = NtSetTimerResolution(resolution100ns, true, out uint currentRes);
                return status == 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool PurgeStandbyList()
        {
            try
            {
                int command = MemoryPurgeStandbyList;
                IntPtr pCommand = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(pCommand, command);
                uint status = NtSetSystemInformation(SystemMemoryListInformation, pCommand, sizeof(int));
                Marshal.FreeHGlobal(pCommand);
                return status == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
