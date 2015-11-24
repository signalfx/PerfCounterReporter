using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace PerfCounterReporter.Interop
{
    internal sealed class PdhSafeDataSourceHandle : SafeHandle
    {
        private PdhSafeDataSourceHandle()
            : base(IntPtr.Zero, true)
        { }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            return (Interop.PdhCloseLog(base.handle, 0) == 0);
        }

        public override bool IsInvalid
        {
            get { return (base.handle == IntPtr.Zero); }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX()
        {
            this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }
        
    internal class Interop
    {
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        public static extern uint PdhBindInputDataSource(out PdhSafeDataSourceHandle phDataSource, string szLogFileNameList);
        [DllImport("pdh.dll")]
        internal static extern uint PdhCloseLog(IntPtr logHandle, uint dwFlags);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        public static extern uint PdhExpandWildCardPathHW(PdhSafeDataSourceHandle hDataSource, string szWildCardPath, IntPtr mszExpandedPathList, ref IntPtr pcchPathListLength, uint dwFlags);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        public static extern uint PdhValidatePathEx(PdhSafeDataSourceHandle hDataSource, string szFullPathBuffer);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        public static extern uint PdhParseCounterPath(string szFullPathBuffer, IntPtr pCounterPathElements, ref IntPtr pdwBufferSize, uint dwFlags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    }
}
