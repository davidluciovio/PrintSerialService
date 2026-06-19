using System;

namespace ZebraPrintUtility.Models
{
    public class PrintHistoryItem
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string LabelName { get; set; } = string.Empty;
        public string ZplContent { get; set; } = string.Empty;
        public string PrinterUsed { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
