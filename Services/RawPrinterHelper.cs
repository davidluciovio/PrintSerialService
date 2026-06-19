using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ZebraPrintUtility.Services
{
    public static class RawPrinterHelper
    {
        // Structure and API declarations:
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string? pDocName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)]
            public string? pDatatype;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        // SendBytesToPrinter()
        // When the function is given a printer name and an unmanaged array of
        // bytes, the function sends those bytes to the print spooler.
        // Returns true on success, false on failure.
        public static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, int dwCount)
        {
            int dwError = 0;
            IntPtr hPrinter = IntPtr.Zero;
            DOCINFOA di = new DOCINFOA();
            bool bSuccess = false;

            di.pDocName = "Zebra Raw Label Print";
            di.pDatatype = "RAW";

            if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out int dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }

            if (!bSuccess)
            {
                dwError = Marshal.GetLastWin32Error();
                throw new IOException($"Failed to write raw data to printer spooler. Win32 Error Code: {dwError}");
            }
            return bSuccess;
        }

        public static bool SendFileToPrinter(string szPrinterName, string szFileName)
        {
            if (!File.Exists(szFileName)) return false;

            using FileStream fs = new FileStream(szFileName, FileMode.Open, FileAccess.Read);
            using BinaryReader br = new BinaryReader(fs);
            
            int len = (int)fs.Length;
            byte[] bytes = br.ReadBytes(len);
            
            IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(len);
            Marshal.Copy(bytes, 0, pUnmanagedBytes, len);
            
            try
            {
                return SendBytesToPrinter(szPrinterName, pUnmanagedBytes, len);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pUnmanagedBytes);
            }
        }

        public static bool SendStringToPrinter(string szPrinterName, string szString)
        {
            int len = szString.Length;
            // Use ASCII encoding since Zebra ZPL is character-based
            byte[] bytes = Encoding.ASCII.GetBytes(szString);
            
            IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);
            
            try
            {
                return SendBytesToPrinter(szPrinterName, pUnmanagedBytes, bytes.Length);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pUnmanagedBytes);
            }
        }
    }
}
