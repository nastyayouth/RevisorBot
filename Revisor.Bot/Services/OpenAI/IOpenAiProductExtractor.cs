
public record ExtractedProduct(string ProductName, DateTime? ExpiryDate, double Confidence, string? Notes);

public interface IOpenAiProductExtractor
{
    Task<ExtractedProduct> ExtractAsync(byte[] imageBytes, CancellationToken ct);
}