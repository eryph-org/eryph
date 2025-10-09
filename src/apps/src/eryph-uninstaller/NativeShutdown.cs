using System;
using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo

namespace Eryph.Runtime.Uninstaller
{
    internal static class NativeShutdown
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool InitiateSystemShutdownEx(
            string? lpMachineName,
            string? lpMessage,
            uint dwTimeout,
            bool bForceAppsClosed,
            bool bRebootAfterShutdown,
            uint dwReason);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LuId lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges,
            ref TokenPrivileges newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LuId
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenPrivileges
        {
            public uint PrivilegeCount;
            // ReSharper disable once IdentifierTypo
            public LuId Luid;
            public uint Attributes;
        }

        private const uint TokenAdjustPrivileges = 0x0020;
        private const uint TokenQuery = 0x0008;
        private const uint SePrivilegeEnabled = 0x00000002;
        private const string SeShutdownName = "SeShutdownPrivilege";

        private const uint ShtdnReasonMajorApplication = 0x00040000;
        private const uint ShtdnReasonMinorInstallation = 0x00000002;
        private const uint ShtdnReasonFlagPlanned = 0x80000000;

        private static bool AcquireShutdownPrivilege()
        {
            var tokenHandle = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out tokenHandle))
                    return false;

                if (!LookupPrivilegeValue(null, SeShutdownName, out var luid))
                    return false;

                TokenPrivileges tokenPrivileges = new()
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SePrivilegeEnabled
                };

                return AdjustTokenPrivileges(tokenHandle, false, ref tokenPrivileges, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                    CloseHandle(tokenHandle);
            }
        }

        public static void InitiateRestart(string message, uint timeoutSeconds)
        {
            if (!AcquireShutdownPrivilege())
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Failed to acquire shutdown privilege");

            const uint reason = ShtdnReasonMajorApplication | ShtdnReasonMinorInstallation | ShtdnReasonFlagPlanned;
            var success = InitiateSystemShutdownEx(
                null, // local machine
                message,
                timeoutSeconds,
                false, // don't force apps to close
                true, // reboot after shutdown
                reason);

            if (!success)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
