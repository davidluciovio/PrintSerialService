using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ZebraPrintUtility.Models;
using ZebraPrintUtility.Services;

namespace ZebraPrintUtility.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // Services
        private readonly ZebraPrinterService _printerService;
        private readonly LabelaryService _labelaryService;
        private readonly XmlParserService _xmlParserService;

        private const string SettingsFileName = "printer_settings.json";

        // Properties bound to UI
        private PrinterSettings _settings = new();
        public PrinterSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        private string _zplContent = string.Empty;
        public string ZplContent
        {
            get => _zplContent;
            set
            {
                if (SetProperty(ref _zplContent, value))
                {
                    UpdatePreviewAsync();
                }
            }
        }

        private string _xmlFilePath = string.Empty;
        public string XmlFilePath
        {
            get => _xmlFilePath;
            set => SetProperty(ref _xmlFilePath, value);
        }

        private string _xmlFileContent = string.Empty;
        public string XmlFileContent
        {
            get => _xmlFileContent;
            set => SetProperty(ref _xmlFileContent, value);
        }

        private List<Dictionary<string, string>> _xmlRecords = new();
        public List<Dictionary<string, string>> XmlRecords
        {
            get => _xmlRecords;
            set
            {
                if (SetProperty(ref _xmlRecords, value))
                {
                    OnPropertyChanged(nameof(HasMultipleXmlRecords));
                    SelectedXmlRecord = value.FirstOrDefault();
                }
            }
        }

        private Dictionary<string, string>? _selectedXmlRecord;
        public Dictionary<string, string>? SelectedXmlRecord
        {
            get => _selectedXmlRecord;
            set
            {
                if (SetProperty(ref _selectedXmlRecord, value))
                {
                    UpdateMergedZpl();
                }
            }
        }

        public bool HasMultipleXmlRecords => XmlRecords != null && XmlRecords.Count > 1;

        private string _mergedZpl = string.Empty;
        public string MergedZpl
        {
            get => _mergedZpl;
            set
            {
                if (SetProperty(ref _mergedZpl, value))
                {
                    UpdatePreviewAsync();
                }
            }
        }

        private ImageSource? _previewImage;
        public ImageSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        private string _previewStatus = "Ready";
        public string PreviewStatus
        {
            get => _previewStatus;
            set => SetProperty(ref _previewStatus, value);
        }

        private string _consoleLogs = "System ready.\n";
        public string ConsoleLogs
        {
            get => _consoleLogs;
            set => SetProperty(ref _consoleLogs, value);
        }

        // Preview Settings
        private double _labelWidth = 4.0;
        public double LabelWidth
        {
            get => _labelWidth;
            set
            {
                if (SetProperty(ref _labelWidth, value)) UpdatePreviewAsync();
            }
        }

        private double _labelHeight = 6.0;
        public double LabelHeight
        {
            get => _labelHeight;
            set
            {
                if (SetProperty(ref _labelHeight, value)) UpdatePreviewAsync();
            }
        }

        private int _labelDpmm = 8; // 8 dpmm = 203 dpi, standard Zebra
        public int LabelDpmm
        {
            get => _labelDpmm;
            set
            {
                if (SetProperty(ref _labelDpmm, value)) UpdatePreviewAsync();
            }
        }

        // Dropdown Lists
        public ObservableCollection<string> AvailablePrinters { get; } = new();
        public ObservableCollection<string> AvailableSerialPorts { get; } = new();
        public ObservableCollection<int> AvailableBaudRates { get; } = new() { 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
        public ObservableCollection<PrinterConnectionMethod> ConnectionMethods { get; } = new()
        {
            PrinterConnectionMethod.Network,
            PrinterConnectionMethod.WindowsSpooler,
            PrinterConnectionMethod.Serial
        };

        // History
        public ObservableCollection<PrintHistoryItem> PrintHistory { get; } = new();

        // UI Helpers
        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    // Trigger preview update when switching modes
                    UpdatePreviewAsync();
                }
            }
        }

        // Commands
        public ICommand PrintCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand LoadZplFileCommand { get; }
        public ICommand LoadXmlFileCommand { get; }
        public ICommand RefreshPrintersCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand LoadSettingsCommand { get; }
        public ICommand PrintSelectedXmlRecordCommand { get; }
        public ICommand PrintAllXmlRecordsCommand { get; }
        public ICommand ToggleDbLoopCommand { get; }
        public ICommand TestDbConnectionCommand { get; }

        public string DbZplTemplate
        {
            get => Settings.DbZplTemplate;
            set
            {
                if (Settings.DbZplTemplate != value)
                {
                    Settings.DbZplTemplate = value;
                    OnPropertyChanged();
                    UpdatePreviewAsync();
                }
            }
        }

        private bool _isDbLoopRunning;
        public bool IsDbLoopRunning
        {
            get => _isDbLoopRunning;
            set => SetProperty(ref _isDbLoopRunning, value);
        }

        private string _dbLoopStatus = "Stopped";
        public string DbLoopStatus
        {
            get => _dbLoopStatus;
            set => SetProperty(ref _dbLoopStatus, value);
        }

        public ObservableCollection<DbSerialLogItem> DbSerialLogs { get; } = new();

        public MainViewModel()
        {
            // Initialize services
            _printerService = new ZebraPrinterService();
            _labelaryService = new LabelaryService();
            _xmlParserService = new XmlParserService();

            // Set default ZPL to trigger initial preview
            _zplContent = 
@"^XA
^FX Top Section with logo / text
^CF0,60
^FO50,50^FDANTIGRAVITY^FS
^CF0,30
^FO50,130^FDZebra Printing Utility^FS
^FO50,170^FDStatus: Operational^FS

^FX Graphical Line separator
^FO50,220^GB700,5,5^FS

^FX Barcode Section
^FO50,260^BY3
^BCN,100,Y,N,N
^FDZEBRA-NET-PRINT^FS

^FX Box section
^FO50,420^GB700,200,3^FS
^FO80,450^CF0,25^FDConnection Methods Supported:^FS
^FO80,490^FD1. Network Port TCP 9100^FS
^FO80,520^FD2. Windows Print Spooler (USB)^FS
^FO80,550^FD3. Serial Port (COM)^FS

^XZ";

            // Initialize commands
            PrintCommand = new RelayCommand(_ => ExecutePrint());
            TestConnectionCommand = new RelayCommand(_ => ExecuteTestConnection());
            LoadZplFileCommand = new RelayCommand(_ => ExecuteLoadZplFile());
            LoadXmlFileCommand = new RelayCommand(_ => ExecuteLoadXmlFile());
            RefreshPrintersCommand = new RelayCommand(_ => RefreshPrintersAndPorts());
            ClearHistoryCommand = new RelayCommand(_ => PrintHistory.Clear());
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
            LoadSettingsCommand = new RelayCommand(_ => LoadSettings());
            PrintSelectedXmlRecordCommand = new RelayCommand(_ => ExecutePrintSelectedXmlRecord(), _ => SelectedXmlRecord != null);
            PrintAllXmlRecordsCommand = new RelayCommand(_ => ExecutePrintAllXmlRecords(), _ => XmlRecords.Any());
            ToggleDbLoopCommand = new RelayCommand(_ => ExecuteToggleDbLoop());
            TestDbConnectionCommand = new RelayCommand(_ => ExecuteTestDbConnection());

            // Initialize environment
            LoadSettings();
            RefreshPrintersAndPorts();
            UpdatePreviewAsync();
        }

        private void Log(string message, bool isError = false)
        {
            string prefix = isError ? "[ERROR]" : "[INFO]";
            ConsoleLogs = $"{DateTime.Now:HH:mm:ss} {prefix} {message}\n" + ConsoleLogs;
        }

        private void RefreshPrintersAndPorts()
        {
            AvailablePrinters.Clear();
            try
            {
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    AvailablePrinters.Add(printer);
                }
                Log("Local Windows printers scanned.");
            }
            catch (Exception ex)
            {
                Log($"Could not scan Windows printers: {ex.Message}", true);
            }

            AvailableSerialPorts.Clear();
            try
            {
                foreach (string port in SerialPort.GetPortNames())
                {
                    AvailableSerialPorts.Add(port);
                }
                Log("Serial COM ports scanned.");
            }
            catch (Exception ex)
            {
                Log($"Could not scan serial ports: {ex.Message}", true);
            }

            // Sync settings options if they match
            if (AvailablePrinters.Any() && string.IsNullOrEmpty(Settings.SpoolerPrinterName))
            {
                Settings.SpoolerPrinterName = AvailablePrinters.First();
            }
            if (AvailableSerialPorts.Any() && string.IsNullOrEmpty(Settings.SerialPortName))
            {
                Settings.SerialPortName = AvailableSerialPorts.First();
            }
        }

        private async void UpdatePreviewAsync()
        {
            string currentZpl = SelectedTabIndex switch
            {
                0 => ZplContent,
                1 => MergedZpl,
                4 => System.Text.RegularExpressions.Regex.Replace(Settings.DbZplTemplate, @"\{serial\}", "1234567890", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(currentZpl))
            {
                PreviewImage = null;
                PreviewStatus = "Empty ZPL";
                return;
            }

            PreviewStatus = "Refreshing Preview...";
            try
            {
                byte[]? imgBytes = await _labelaryService.GetLabelImageAsync(currentZpl, LabelWidth, LabelHeight, LabelDpmm);
                if (imgBytes != null)
                {
                    var bitmap = new BitmapImage();
                    using (var stream = new MemoryStream(imgBytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze(); // Required for cross-thread binding
                    PreviewImage = bitmap;
                    PreviewStatus = "Preview Ready";
                }
                else
                {
                    PreviewImage = null;
                    PreviewStatus = "Failed to load preview";
                }
            }
            catch (Exception ex)
            {
                PreviewImage = null;
                PreviewStatus = "Preview Error";
                System.Diagnostics.Debug.WriteLine($"Preview load failure: {ex.Message}");
            }
        }

        private void UpdateMergedZpl()
        {
            if (SelectedXmlRecord == null)
            {
                MergedZpl = string.Empty;
                return;
            }
            MergedZpl = _xmlParserService.MergeZplTemplate(Settings.ZplTemplate, SelectedXmlRecord);
        }

        private void ExecutePrint()
        {
            if (SelectedTabIndex == 0)
            {
                // Send Raw ZPL
                PrintZpl(ZplContent, "Raw ZPL");
            }
            else
            {
                // Send Merged ZPL from XML Tab
                if (SelectedXmlRecord == null)
                {
                    MessageBox.Show("Please load an XML file and select a record first.", "No XML Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                PrintZpl(MergedZpl, $"XML Record ({SelectedXmlRecord.Values.FirstOrDefault() ?? "Label"})");
            }
        }

        private void ExecutePrintSelectedXmlRecord()
        {
            if (SelectedXmlRecord == null) return;
            PrintZpl(MergedZpl, $"XML Row - {SelectedXmlRecord.Values.FirstOrDefault() ?? "Record"}");
        }

        private void ExecutePrintAllXmlRecords()
        {
            if (!XmlRecords.Any()) return;
            
            var result = MessageBox.Show($"Are you sure you want to print all {XmlRecords.Count} records?", 
                                         "Batch Print", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            int count = 0;
            int failed = 0;
            foreach (var record in XmlRecords)
            {
                string recordZpl = _xmlParserService.MergeZplTemplate(Settings.ZplTemplate, record);
                string recordLabel = record.Values.FirstOrDefault() ?? $"Label {count + 1}";
                try
                {
                    _printerService.Print(Settings, recordZpl);
                    count++;
                    
                    PrintHistory.Insert(0, new PrintHistoryItem
                    {
                        LabelName = $"Batch: {recordLabel}",
                        ZplContent = recordZpl,
                        PrinterUsed = GetPrinterDescription(),
                        Status = "Success"
                    });
                }
                catch (Exception ex)
                {
                    failed++;
                    PrintHistory.Insert(0, new PrintHistoryItem
                    {
                        LabelName = $"Batch (Fail): {recordLabel}",
                        ZplContent = recordZpl,
                        PrinterUsed = GetPrinterDescription(),
                        Status = "Failed",
                        ErrorMessage = ex.Message
                    });
                    Log($"Failed printing {recordLabel} in batch: {ex.Message}", true);
                }
            }

            Log($"Batch printing completed. Success: {count}, Failed: {failed}");
            MessageBox.Show($"Batch printed completed!\nSuccess: {count}\nFailed: {failed}", "Batch Print Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PrintZpl(string zpl, string labelName)
        {
            var historyItem = new PrintHistoryItem
            {
                LabelName = labelName,
                ZplContent = zpl,
                PrinterUsed = GetPrinterDescription()
            };

            try
            {
                Log($"Sending print job via {Settings.ConnectionMethod}...");
                _printerService.Print(Settings, zpl);
                
                historyItem.Status = "Success";
                PrintHistory.Insert(0, historyItem);
                Log($"Label '{labelName}' printed successfully.");
            }
            catch (Exception ex)
            {
                historyItem.Status = "Failed";
                historyItem.ErrorMessage = ex.Message;
                PrintHistory.Insert(0, historyItem);
                Log($"Print failed: {ex.Message}", true);
                MessageBox.Show($"Printing failed:\n{ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteTestConnection()
        {
            Log($"Testing connection to {Settings.ConnectionMethod} printer...");
            bool isConnected = _printerService.TestConnection(Settings, out string status);
            
            if (isConnected)
            {
                Log($"Test Success: {status}");
                MessageBox.Show($"Connection test succeeded!\n{status}", "Test Connection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Log($"Test Failed: {status}", true);
                MessageBox.Show($"Connection test failed!\n{status}", "Test Connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private CancellationTokenSource? _dbLoopCts;

        private void ExecuteToggleDbLoop()
        {
            if (IsDbLoopRunning)
            {
                // Stop loop
                _dbLoopCts?.Cancel();
                _dbLoopCts = null;
                IsDbLoopRunning = false;
                DbLoopStatus = "Stopped";
                Log("Database auto-print loop stopped.");
            }
            else
            {
                // Start loop
                _dbLoopCts = new CancellationTokenSource();
                IsDbLoopRunning = true;
                DbLoopStatus = "Running";
                Log("Database auto-print loop started.");
                
                // Fire and forget loop execution
                var token = _dbLoopCts.Token;
                Task.Run(() => RunDbLoopAsync(token));
            }
        }

        private async void ExecuteTestDbConnection()
        {
            Log("Testing database connection...");
            try
            {
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(Settings.DbConnectionString))
                {
                    await conn.OpenAsync();
                }
                Log("Database connection test succeeded!");
                MessageBox.Show("Database connection test succeeded!", "SQL Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log($"Database connection test failed: {ex.Message}", true);
                MessageBox.Show($"Database connection test failed:\n{ex.Message}", "SQL Connection Test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RunDbLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() => DbLoopStatus = "Polling database...");
                    await QueryAndPrintSerialsAsync();
                    Application.Current.Dispatcher.Invoke(() => DbLoopStatus = $"Idle - Last run at {DateTime.Now:HH:mm:ss}");
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() => DbLoopStatus = $"Error: {ex.Message}");
                    Log($"DB Loop error: {ex.Message}", true);
                }

                // Wait for interval
                int delaySeconds = Settings.DbLoopIntervalSeconds > 0 ? Settings.DbLoopIntervalSeconds : 10;
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task QueryAndPrintSerialsAsync()
        {
            var serials = new List<string>();
            using (var conn = new Microsoft.Data.SqlClient.SqlConnection(Settings.DbConnectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(Settings.DbSelectQuery, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (reader.FieldCount > 0)
                            {
                                var val = reader.GetValue(0)?.ToString();
                                if (!string.IsNullOrEmpty(val))
                                {
                                    serials.Add(val);
                                }
                            }
                        }
                    }
                }

                if (serials.Count == 0)
                {
                    return;
                }

                Log($"Found {serials.Count} serial(s) in database to print.");

                foreach (var serial in serials)
                {
                    string zpl = System.Text.RegularExpressions.Regex.Replace(Settings.DbZplTemplate, @"\{serial\}", int.Parse(serial).ToString("D4"), System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    bool printSuccess = false;
                    string printError = string.Empty;

                    try
                    {
                        _printerService.Print(Settings, zpl);
                        printSuccess = true;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            PrintHistory.Insert(0, new PrintHistoryItem
                            {
                                LabelName = $"DB Auto-Print: {serial}",
                                ZplContent = zpl,
                                PrinterUsed = GetPrinterDescription(),
                                Status = "Success"
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        printError = ex.Message;
                        Log($"Failed auto-printing serial {serial}: {ex.Message}", true);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            PrintHistory.Insert(0, new PrintHistoryItem
                            {
                                LabelName = $"DB Auto-Print (Fail): {serial}",
                                ZplContent = zpl,
                                PrinterUsed = GetPrinterDescription(),
                                Status = "Failed",
                                ErrorMessage = ex.Message
                            });
                        });
                    }

                    if (printSuccess)
                    {
                        if (!string.IsNullOrWhiteSpace(Settings.DbUpdateQuery))
                        {
                            try
                            {
                                using (var updateCmd = new Microsoft.Data.SqlClient.SqlCommand(Settings.DbUpdateQuery, conn))
                                {
                                    updateCmd.Parameters.AddWithValue("@Serial", serial);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    DbSerialLogs.Insert(0, new DbSerialLogItem
                                    {
                                        Timestamp = DateTime.Now,
                                        Serial = serial,
                                        Status = "Success",
                                        Details = "Printed & Updated DB"
                                    });
                                });
                            }
                            catch (Exception ex)
                            {
                                Log($"Failed updating DB for serial {serial}: {ex.Message}", true);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    DbSerialLogs.Insert(0, new DbSerialLogItem
                                    {
                                        Timestamp = DateTime.Now,
                                        Serial = serial,
                                        Status = "DB Update Failed",
                                        Details = $"Printed. DB Update error: {ex.Message}"
                                    });
                                });
                            }
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                DbSerialLogs.Insert(0, new DbSerialLogItem
                                {
                                    Timestamp = DateTime.Now,
                                    Serial = serial,
                                    Status = "Success",
                                    Details = "Printed (No Update Query)"
                                });
                            });
                        }
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DbSerialLogs.Insert(0, new DbSerialLogItem
                            {
                                Timestamp = DateTime.Now,
                                Serial = serial,
                                Status = "Print Failed",
                                Details = printError
                            });
                        });
                    }
                }
            }
        }

        private void ExecuteLoadZplFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "ZPL Files (*.zpl;*.txt)|*.zpl;*.txt|All Files (*.*)|*.*",
                Title = "Open ZPL Code File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    ZplContent = File.ReadAllText(openFileDialog.FileName);
                    Log($"Loaded ZPL file: {Path.GetFileName(openFileDialog.FileName)}");
                }
                catch (Exception ex)
                {
                    Log($"Error loading ZPL file: {ex.Message}", true);
                }
            }
        }

        private void ExecuteLoadXmlFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                Title = "Open RPT Exported XML File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string xmlContent = File.ReadAllText(openFileDialog.FileName);
                    XmlFilePath = openFileDialog.FileName;
                    XmlFileContent = xmlContent;

                    var parsedRecords = _xmlParserService.ParseXmlToRecords(xmlContent);
                    if (parsedRecords.Any())
                    {
                        XmlRecords = parsedRecords;
                        Log($"Loaded XML file: {Path.GetFileName(openFileDialog.FileName)}. Found {parsedRecords.Count} records.");
                    }
                    else
                    {
                        XmlRecords = new List<Dictionary<string, string>>();
                        Log($"Loaded XML file: {Path.GetFileName(openFileDialog.FileName)} but no record rows could be extracted.", true);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error loading XML file: {ex.Message}", true);
                    MessageBox.Show($"Could not load XML file:\n{ex.Message}", "XML Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GetPrinterDescription()
        {
            return Settings.ConnectionMethod switch
            {
                PrinterConnectionMethod.Network => $"TCP/IP ({Settings.NetworkIpAddress}:{Settings.NetworkPort})",
                PrinterConnectionMethod.WindowsSpooler => $"Spooler ({Settings.SpoolerPrinterName})",
                PrinterConnectionMethod.Serial => $"Serial ({Settings.SerialPortName} - {Settings.SerialBaudRate}bps)",
                _ => "Unknown"
            };
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(Settings, options);
                File.WriteAllText(SettingsFileName, jsonString);
                Log("Configuration settings saved successfully.");
            }
            catch (Exception ex)
            {
                Log($"Failed to save settings: {ex.Message}", true);
            }
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    string jsonString = File.ReadAllText(SettingsFileName);
                    var loadedSettings = JsonSerializer.Deserialize<PrinterSettings>(jsonString);
                    if (loadedSettings != null)
                    {
                        Settings = loadedSettings;
                        Log("Configuration settings loaded successfully.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to load settings: {ex.Message}", true);
            }

            // Fallback default
            Settings = new PrinterSettings();
        }
    }

    public class DbSerialLogItem
    {
        public DateTime Timestamp { get; set; }
        public string Serial { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
