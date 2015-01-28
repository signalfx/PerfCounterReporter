
using System.Runtime.InteropServices;
namespace PerfCounterReporter.Interop
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PdhCounterPathElement
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string MachineName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ObjectName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string InstanceName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ParentInstance;
        public uint InstanceIndex;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string CounterName;
    }
}
