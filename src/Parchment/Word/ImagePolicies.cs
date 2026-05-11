/// <summary>
/// Image resolution policies applied to every HTML / markdown render path. Defaults to
/// <see cref="OpenXmlHtml.ImagePolicy.AllowAll"/> for both local and web sources because Parchment
/// renders developer-bound model content rather than untrusted HTML — locking it down to Deny by
/// default would silently break <c>&lt;img src="..."&gt;</c> in <c>[Html]</c> properties and
/// <c>![alt](path)</c> in markdown templates.
/// </summary>
sealed record ImagePolicies(ImagePolicy LocalImages, ImagePolicy WebImages)
{
    public static ImagePolicies AllowAll { get; } =
        new(ImagePolicy.AllowAll(), ImagePolicy.AllowAll());

    public HtmlConvertSettings BuildSettings(
        int headingOffset = 0,
        HtmlNumberingSession? numberingSession = null) =>
        new()
        {
            LocalImages = LocalImages,
            WebImages = WebImages,
            HeadingLevelOffset = headingOffset,
            NumberingSession = numberingSession
        };
}
