using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PerfCounterReporter.Interop
{

    public class PdhPathHandler : IDisposable
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static readonly string[] NOT_FOUND = new string[0];

        private PdhSafeDataSourceHandle _safeDataSourceHandle;

        public PdhPathHandler()
        {
            ConnectToDataSource();

        }

        public void Dispose()
        {
            if ((this._safeDataSourceHandle != null) && !this._safeDataSourceHandle.IsInvalid)
            {
                this._safeDataSourceHandle.Dispose();
            }

        }



        public IEnumerable<PdhCounterPathElement> GetPathElements(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                string[] expandedPaths = ExpandWildCardPath(path);
                if (expandedPaths == null)
                {
                    _log.Error(() => string.Format("Failed to parse: {0}", path));
                    continue;
                }
                foreach (string expandedPath in expandedPaths)
                {
                    if (IsPathPresent(expandedPath))
                    {
                        yield return ParsePath(expandedPath);
                    }
                }
            }
        }

        private void ConnectToDataSource()
        {
            if ((this._safeDataSourceHandle != null) && !this._safeDataSourceHandle.IsInvalid)
            {
                this._safeDataSourceHandle.Dispose();
            }

            uint returnCode = Interop.PdhBindInputDataSource(out this._safeDataSourceHandle, null);
            if (returnCode != PdhResults.PDH_CSTATUS_VALID_DATA)
            {
                throw BuildException(returnCode);
            }
        }

        private bool IsPathPresent(string path)
        {

            uint resultCode = Interop.PdhValidatePathEx(this._safeDataSourceHandle, path);
            if (resultCode == 0)
            {
                return true;
            }
            if (resultCode == PdhResults.PDH_CSTATUS_NO_OBJECT || resultCode == PdhResults.PDH_CSTATUS_NO_COUNTER || resultCode == PdhResults.PDH_CSTATUS_NO_INSTANCE)
            {
                return false;
            }
            throw new Exception(string.Format(CultureInfo.CurrentCulture, ErrorMessages.CounterPathIsInvalid, new object[] { path }));
        }

        private static PdhCounterPathElement ParsePath(string fullPath)
        {
            IntPtr bufferSize = new IntPtr(0);
            uint returnCode = Interop.PdhParseCounterPath(fullPath, IntPtr.Zero, ref bufferSize, 0);
            if (returnCode == PdhResults.PDH_MORE_DATA || returnCode == PdhResults.PDH_CSTATUS_VALID_DATA)
            {
                IntPtr counterPathBuffer = Marshal.AllocHGlobal(bufferSize.ToInt32());
                try
                {
                    returnCode = Interop.PdhParseCounterPath(fullPath, counterPathBuffer, ref bufferSize, 0);	//flags must always be zero
                    if (returnCode == PdhResults.PDH_CSTATUS_VALID_DATA)
                    {
                        return (PdhCounterPathElement)Marshal.PtrToStructure(counterPathBuffer, typeof(PdhCounterPathElement));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(counterPathBuffer);
                }
            }

            throw new Exception(string.Format(CultureInfo.CurrentCulture, ErrorMessages.CounterPathTranslationFailed, returnCode));
        }


        private static Exception BuildException(uint failedReturnCode)
        {
            string message = Win32Messages.FormatMessageFromModule(failedReturnCode, "pdh.dll");
            if (string.IsNullOrEmpty(message))
            {
                message = string.Format(CultureInfo.InvariantCulture, ErrorMessages.CounterApiError, new object[] { failedReturnCode });
            }
            return new Exception(message);
        }

        private string[] ExpandWildCardPath(string path)
        {
            IntPtr pathListLength = new IntPtr(0);
            uint resultCode = Interop.PdhExpandWildCardPathHW(this._safeDataSourceHandle, path, IntPtr.Zero, ref pathListLength, 0);
            if (resultCode == PdhResults.PDH_MORE_DATA)
            {
                IntPtr expandedPathList = Marshal.AllocHGlobal(pathListLength.ToInt32() * 2);
                try
                {
                    resultCode = Interop.PdhExpandWildCardPathHW(this._safeDataSourceHandle, path, expandedPathList, ref pathListLength, 0);
                    if (resultCode == PdhResults.PDH_CSTATUS_VALID_DATA)
                    {
                        return ParseListOfStrings(ref expandedPathList, pathListLength.ToInt32());
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(expandedPathList);
                }
            } else if (resultCode == PdhResults.PDH_CSTATUS_NO_OBJECT || resultCode == PdhResults.PDH_CSTATUS_NO_COUNTER || resultCode == PdhResults.PDH_CSTATUS_NO_INSTANCE)
            {
                return NOT_FOUND;
            }
            return null;
        }

        private string[] ParseListOfStrings(ref IntPtr nativeStringPointer, int strSize)
        {
            var text = Marshal.PtrToStringAuto(nativeStringPointer, strSize);
            return text.TrimEnd(new char[1])
                .Split(new char[1]);
        }
    }
}
