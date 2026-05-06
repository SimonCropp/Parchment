namespace Parchment;

/// <summary>
/// Image resolution policies applied to every HTML / markdown render path. Defaults to
/// <see cref="OpenXmlHtml.ImagePolicy.AllowAll"/> for both local and web sources because Parchment
/// renders developer-bound model content rather than untrusted HTML — locking it down to Deny by
/// default would silently break <c>&lt;img src="..."&gt;</c> in <c>[Html]</c> properties and
/// <c>![alt](path)</c> in markdown templates.
/// </summary>
sealed record ImagePolicies(OpenXmlHtml.ImagePolicy LocalImages, OpenXmlHtml.ImagePolicy WebImages)
{
    public static ImagePolicies AllowAll { get; } =
        new(OpenXmlHtml.ImagePolicy.AllowAll(), OpenXmlHtml.ImagePolicy.AllowAll());

    public OpenXmlHtml.HtmlConvertSettings BuildSettings(
        int headingOffset = 0,
        OpenXmlHtml.HtmlNumberingSession? numberingSession = null) =>
        new()
        {
            LocalImages = LocalImages,
            WebImages = WebImages,
            HeadingLevelOffset = headingOffset,
            NumberingSession = numberingSession
        };
}
