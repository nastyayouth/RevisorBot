using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;



public class OpenAiProductExtractor : IOpenAiProductExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _cfg;

    public OpenAiProductExtractor(IHttpClientFactory httpClientFactory, IConfiguration cfg)
    {
        _httpClientFactory = httpClientFactory;
        _cfg = cfg;
    }

    public async Task<ExtractedProduct> ExtractAsync(byte[] imageBytes, CancellationToken ct)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            throw new ArgumentException("Image bytes are empty.");

        var openAiModel = _cfg["OpenAI:Model"] ?? "gpt-4o-mini";
        var openai = _httpClientFactory.CreateClient("openai");

        var base64 = Convert.ToBase64String(imageBytes);

        // JSON Schema for Structured Outputs
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                product_name = new { type = "string" },
                expiry_date = new
                {
                    type = new object[] { "string", "null" },
                    description = "Expiration date in YYYY-MM-DD, or null if unknown"
                },
                confidence = new { type = "number", minimum = 0, maximum = 1 },
                notes = new { type = new object[] { "string", "null" } }
            },
            required = new[] { "product_name", "expiry_date", "confidence", "notes" }
        };

        var requestBody = new
        {
            model = openAiModel,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "You extract product name (brand + type) and expiration date from packaging photos. " +
                        "Return ONLY structured output. If expiration is unclear, set expiry_date=null and explain in notes. " +
                        "If only month/year is present, convert to the first day of that month (YYYY-MM-01) and explain in notes."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = "Find product name and expiration date on this image." },
                        // Assume JPEG. If you support other formats, detect mime type.
                        new { type = "input_image", image_url = $"data:image/jpeg;base64,{base64}" }
                    }
                }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "product_expiry_extraction", // REQUIRED
                    strict = true,
                    schema = schema
                }
            }
        };

        using var resp = await openai.PostAsync(
            "responses",
            new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
            ct);

        var raw = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new OpenAiBadRequestException(resp.StatusCode, raw);

        // Parse structured JSON from Responses API result
        var structured = ExtractStructuredJsonFromResponses(raw);
        if (structured is null)
            throw new OpenAiParseException("Failed to locate structured JSON in Responses API response.", raw);

        var root = structured.Value;

        var productName = root.TryGetProperty("product_name", out var pn) && pn.ValueKind == JsonValueKind.String
            ? pn.GetString() ?? "Unknown"
            : "Unknown";

        DateTime? expiry = null;
        if (root.TryGetProperty("expiry_date", out var ex))
        {
            if (ex.ValueKind == JsonValueKind.String)
            {
                var s = ex.GetString();
                if (!string.IsNullOrWhiteSpace(s) &&
                    DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                {
                    expiry = dt.Date;
                }
            }
        }

        var confidence = root.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.Number
            ? conf.GetDouble()
            : 0;

        string? notes = null;
        if (root.TryGetProperty("notes", out var nt) && nt.ValueKind == JsonValueKind.String)
            notes = nt.GetString();

        return new ExtractedProduct(productName, expiry, confidence, notes);
    }

    /// <summary>
    /// Properly extracts JSON text from Responses API:
    /// root.output[] -> message.content[] -> output_text.text (string containing JSON)
    /// Then parses that JSON string into JsonElement.
    /// </summary>
    public static JsonElement? ExtractStructuredJsonFromResponses(string responsesApiRawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responsesApiRawJson);
            var root = doc.RootElement;

            // Primary path: output[] items
            if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in output.EnumerateArray())
                {
                    // We expect: { type: "message", content: [ { type:"output_text", text:"{...}" } ] }
                    if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var contentItem in content.EnumerateArray())
                    {
                        if (!contentItem.TryGetProperty("type", out var typeProp) ||
                            typeProp.ValueKind != JsonValueKind.String)
                            continue;

                        var type = typeProp.GetString();
                        if (!string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!contentItem.TryGetProperty("text", out var textProp) ||
                            textProp.ValueKind != JsonValueKind.String)
                            continue;

                        var text = textProp.GetString();
                        if (string.IsNullOrWhiteSpace(text))
                            continue;

                        // text is expected to be a JSON object string
                        using var jsonDoc = JsonDocument.Parse(text);
                        return jsonDoc.RootElement.Clone();
                    }
                }
            }

            // Fallback: sometimes there is a top-level "output_text" or similar helper field
            // We try to locate any string field that looks like JSON and parse it.
            foreach (var candidate in EnumerateAllStringValues(root))
            {
                if (candidate.Length < 2) continue;
                if (!candidate.TrimStart().StartsWith("{")) continue;

                try
                {
                    using var jsonDoc = JsonDocument.Parse(candidate);
                    return jsonDoc.RootElement.Clone();
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateAllStringValues(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    foreach (var s in EnumerateAllStringValues(prop.Value))
                        yield return s;
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var s in EnumerateAllStringValues(item))
                        yield return s;
                }

                break;

            case JsonValueKind.String:
                yield return element.GetString() ?? "";
                break;
        }
    }
}

public sealed class OpenAiBadRequestException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Body { get; }

    public OpenAiBadRequestException(HttpStatusCode statusCode, string body)
        : base($"OpenAI request failed: {(int)statusCode} {statusCode}")
    {
        StatusCode = statusCode;
        Body = body;
    }
}

public sealed class OpenAiParseException : Exception
{
    public string RawResponse { get; }

    public OpenAiParseException(string message, string rawResponse)
        : base(message)
    {
        RawResponse = rawResponse;
    }
}