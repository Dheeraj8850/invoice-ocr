using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace GeminiOcrApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OcrProcessController : ControllerBase
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<OcrProcessController> _logger;

        public OcrProcessController(IHttpClientFactory httpFactory, IConfiguration config, ILogger<OcrProcessController> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Accepts an image (multipart/form-data) and asks Gemini to return ONLY JSON.
        /// </summary>
        [HttpPost("image-to-json")]
        [RequestSizeLimit(50_000_000)] // up to ~50 MB; adjust if needed
        public async Task<IActionResult> ImageToJson([FromForm] IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { error = "Image file is required." });

            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                         ?? _config["GoogleAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
                return StatusCode(500, new { error = "Gemini API key is not configured. Set GEMINI_API_KEY or GoogleAI:ApiKey." });

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await image.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }
            string base64Image = Convert.ToBase64String(imageBytes);

            string prompt = @"
You are an OCR and data extraction engine.
You will receive an image of an invoice, sales order, or purchase order.
Your task:
1. Read all visible text from the image.
2. Identify invoice/order metadata and line items.
3. Output ONLY valid JSON according to the exact schema below.

SCHEMA:
{
  ""vendorName"": string|null,
  ""vendorAddress"": string|null,
  ""vendorGSTIN"": string|null,
  ""invoiceNumber"": string|null,
  ""invoiceDate"": string|null,        // Use ISO 8601 format (YYYY-MM-DD) if possible
  ""buyerName"": string|null,
  ""buyerAddress"": string|null,
  ""buyerGSTIN"": string|null,
  ""currency"": string|null,           // Example: ""INR"", ""USD""
  ""totalAmount"": number|null,
  ""taxAmount"": number|null,
  ""lineItems"": [
    {
      ""serialNumber"": number|null,
      ""particulars"": string|null,           //""description"": string|null,
      ""colour"": string|null,
      ""size"": string|null,
      ""quantity"": number|null,
      ""unitPrice"": number|null,
      ""amount"": number|null
    }
  ],
  ""terms"": string|null
}

INSTRUCTIONS:
- Always include all keys exactly as in the schema, even if values are null.
- Preserve numeric values as numbers (no quotes).
- If a field is not visible, set it to null — do NOT omit it.
- For lineItems, read table columns carefully: description, colour, size, quantity, unit price, and amount.
- If the table is split across two halves or multiple columns, merge them by serial number order.
- Do not add extra text, markdown, or explanations.
- Return ONLY pure JSON.
";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = image.ContentType ?? "image/png",
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    // Model tends to honor this and return raw JSON text
                    responseMimeType = "application/json"
                }
            };

            var client = _httpFactory.CreateClient();

            // You can use any current image-capable Gemini model; flash is fast & cheap.
            // Consider upgrading model name if you have access to newer variants.
            var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            // Gemini accepts the API key via header:
            httpRequest.Headers.Add("x-goog-api-key", apiKey);

            HttpResponseMessage response;
            string responseBody;
            try
            {
                response = await client.SendAsync(httpRequest);
                responseBody = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP request to Gemini failed.");
                return StatusCode(500, new { error = "Failed to call Gemini API", details = ex.Message });
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini returned non-success: {Status} - {Body}", response.StatusCode, responseBody);
                return StatusCode((int)response.StatusCode, new { error = "Gemini API error", details = responseBody });
            }

            // Extract the model's plain text (which should be JSON per our prompt).
            string modelText = ExtractTextFromGeminiResponse(responseBody);

            // If it's valid JSON, return it as application/json; otherwise return raw.
            try
            {
                using var parsed = JsonDocument.Parse(modelText);
                return Content(modelText, "application/json");
            }
            catch (JsonException)
            {
                // Model didn't return strictly valid JSON; return raw for debugging.
                return Ok(new { raw = modelText, fullResponse = responseBody });
            }
        }

        /// <summary>
        /// Helper to extract the candidate text value from Gemini's response structure.
        /// </summary>
        private static string ExtractTextFromGeminiResponse(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var firstPart = parts[0];
                        if (firstPart.TryGetProperty("text", out var textElement))
                            return textElement.GetString() ?? responseBody;
                    }
                }
            }
            catch
            {
                // ignore parsing errors and return the raw response
            }
            return responseBody;
        }
    }
}
