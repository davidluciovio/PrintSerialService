using System;

namespace ZebraPrintUtility.Models
{
    public enum PrinterConnectionMethod
    {
        Network,
        WindowsSpooler,
        Serial
    }

    public class PrinterSettings
    {
        public PrinterConnectionMethod ConnectionMethod { get; set; } = PrinterConnectionMethod.Network;
        
        // Network printer settings
        public string NetworkIpAddress { get; set; } = "192.168.1.100";
        public int NetworkPort { get; set; } = 9100;

        // Windows Spooler settings
        public string SpoolerPrinterName { get; set; } = "Zebra ZDesigner";

        // Serial Port settings
        public string SerialPortName { get; set; } = "COM1";
        public int SerialBaudRate { get; set; } = 9600;

        // ZPL Template for XML merging
        public string ZplTemplate { get; set; } = 
@"^XA
^CF0,30
^FO50,50^FDItem Code: {ItemCode}^FS
^FO50,100^FDDescription: {Description}^FS
^FO50,150^FDSerial: {Serial}^FS
^FO50,200^BCN,100,Y,N,N^FD{Serial}^FS
^XZ";

        // Database connection settings
        public string DbConnectionString { get; set; } = "Server=UPMAP04\\UPMDATA;Database=UPMWEB;User Id=upmUser;Password=Flg6petoa3z3UEyyglDA;TrustServerCertificate=True;";
        public string DbSelectQuery { get; set; } = "SELECT [Serial] FROM [UPMWEB].[upm_productionControl].[Control200] WHERE [PrintedLabel] IS NULL ORDER BY [CreateDate] ";
        public string DbUpdateQuery { get; set; } = "UPDATE [UPMWEB].[upm_productionControl].[Control200] SET [PrintedLabel] = GETDATE() WHERE CreateDate >= DATEADD(MONTH, -1, GETDATE()) AND [Serial] = @Serial";
        public int DbLoopIntervalSeconds { get; set; } = 10;
        public string DbZplTemplate { get; set; } =
@"^XA
~TA000
~JSN
^LT0
^MNW
^MTT
^PON
^PMN
^LH0,0
^JMA
^PR3,3
~SD30
^JUS
^LRN
^CI27
^PA0,1,1,0
^XZ
^XA
^MMT
^PW160
^LL208
^LS0
^BY3,3,96^FT114,189^BCB,,Y,N
^FH\^FD>;{SERIAL}^FS
^PQ1,0,1,Y
^XZ
";
    }
}
