namespace Parchment;

/// <summary>
/// Narrow context passed to <see cref="OpenXmlToken"/> render delegates. Kept intentionally
/// small so the public API does not lock users to a specific DocumentFormat.OpenXml version.
/// </summary>
public interface IOpenXmlContext
{
    MainDocumentPart MainPart { get; }
    int CurrentHeadingLevel { get; }
    string AddImagePart(byte[] bytes, string contentType);
    bool TryGetStyle(string styleId, out StyleType styleType);
    int CreateBulletNumbering();
    int CreateOrderedNumbering(NumberFormatValues numberFormat);
}
