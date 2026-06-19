using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ZebraPrintUtility.Services
{
    public class LabelaryService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<byte[]?> GetLabelImageAsync(string zpl, double widthInches = 4, double heightInches = 6, int dpmm = 8)
        {
            try
            {
                // Labelary endpoint format:
                // POST http://api.labelary.com/v1/printers/{dpmm}dpmm/labels/{width}x{height}/{index}/
                string url = $"http://api.labelary.com/v1/printers/{dpmm}dpmm/labels/{widthInches}x{heightInches}/0/";

                // Send the raw ZPL in the body of the POST request
                using (var content = new StringContent(zpl, Encoding.UTF8, "text/plain"))
                {
                    // Labelary accepts standard headers
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Accept", "image/png");

                    var response = await _httpClient.PostAsync(url, content);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsByteArrayAsync();
                    }
                    else
                    {
                        string errorText = await response.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Labelary API responded with status {response.StatusCode}: {errorText}");
                    }
                }
            }
            catch (Exception ex)
            {
                // In case of network errors or offline, we log and return null
                System.Diagnostics.Debug.WriteLine($"Labelary error: {ex.Message}");
                throw new Exception($"Unable to preview label via Labelary: {ex.Message}", ex);
            }
        }
    }
}
