using System;
using System.IO;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using ZebraPrintUtility.Models;

namespace ZebraPrintUtility.Services
{
    public class ZebraPrinterService
    {
        public void Print(PrinterSettings settings, string zplData)
        {
            if (string.IsNullOrWhiteSpace(zplData))
            {
                throw new ArgumentException("ZPL content cannot be empty.");
            }

            switch (settings.ConnectionMethod)
            {
                case PrinterConnectionMethod.Network:
                    PrintToNetwork(settings.NetworkIpAddress, settings.NetworkPort, zplData);
                    break;

                case PrinterConnectionMethod.WindowsSpooler:
                    PrintToWindowsSpooler(settings.SpoolerPrinterName, zplData);
                    break;

                case PrinterConnectionMethod.Serial:
                    PrintToSerial(settings.SerialPortName, settings.SerialBaudRate, zplData);
                    break;

                default:
                    throw new NotSupportedException($"Connection method {settings.ConnectionMethod} is not supported.");
            }
        }

        public bool TestConnection(PrinterSettings settings, out string statusMessage)
        {
            try
            {
                switch (settings.ConnectionMethod)
                {
                    case PrinterConnectionMethod.Network:
                        // Ping the IP first
                        using (var ping = new Ping())
                        {
                            var reply = ping.Send(settings.NetworkIpAddress, 1500);
                            if (reply.Status != IPStatus.Success)
                            {
                                statusMessage = $"Ping failed: {reply.Status}";
                                return false;
                            }
                        }

                        // Try to open a TCP port connection
                        using (var client = new TcpClient())
                        {
                            var result = client.BeginConnect(settings.NetworkIpAddress, settings.NetworkPort, null, null);
                            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                            if (!success)
                            {
                                statusMessage = $"Could not open connection to port {settings.NetworkPort}.";
                                return false;
                            }
                            client.EndConnect(result);
                        }
                        statusMessage = "Network printer is reachable.";
                        return true;

                    case PrinterConnectionMethod.WindowsSpooler:
                        // Check if printer exists in system spooler
                        IntPtr hPrinter;
                        if (RawPrinterHelper.OpenPrinter(settings.SpoolerPrinterName, out hPrinter, IntPtr.Zero))
                        {
                            RawPrinterHelper.ClosePrinter(hPrinter);
                            statusMessage = "Printer found in local system.";
                            return true;
                        }
                        statusMessage = $"Printer '{settings.SpoolerPrinterName}' was not found. Please verify the exact name.";
                        return false;

                    case PrinterConnectionMethod.Serial:
                        // Try opening COM port
                        using (var port = new SerialPort(settings.SerialPortName, settings.SerialBaudRate))
                        {
                            port.Open(); // Will throw error if it fails
                            statusMessage = "Serial Port opened successfully.";
                            return true;
                        }

                    default:
                        statusMessage = "Unknown connection method.";
                        return false;
                }
            }
            catch (Exception ex)
            {
                statusMessage = $"Error: {ex.Message}";
                return false;
            }
        }

        private void PrintToNetwork(string ipAddress, int port, string zplData)
        {
            using (var client = new TcpClient())
            {
                var result = client.BeginConnect(ipAddress, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                if (!success)
                {
                    throw new TimeoutException($"Connection timed out trying to reach network printer at {ipAddress}:{port}");
                }
                client.EndConnect(result);

                using (var stream = client.GetStream())
                {
                    byte[] data = Encoding.UTF8.GetBytes(zplData);
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
            }
        }

        private void PrintToWindowsSpooler(string printerName, string zplData)
        {
            RawPrinterHelper.SendStringToPrinter(printerName, zplData);
        }

        private void PrintToSerial(string portName, int baudRate, string zplData)
        {
            using (var port = new SerialPort(portName, baudRate))
            {
                port.Open();
                port.Write(zplData);
            }
        }
    }
}
