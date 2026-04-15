namespace Parchment;

public sealed class ParchmentRenderException(
    string templateName,
    string message,
    string? partUri = null,
    string? contextSnippet = null,
    string? tokenSource = null,
    string? modelPath = null,
    string? scopeChain = null,
    Exception? inner = null) :
    ParchmentException(BuildMessage(templateName, message, partUri, contextSnippet, tokenSource, modelPath, scopeChain), inner)
{
    public string TemplateName { get; } = templateName;
    public string? PartUri { get; } = partUri;
    public string? ContextSnippet { get; } = contextSnippet;
    public string? TokenSource { get; } = tokenSource;
    public string? ModelPath { get; } = modelPath;
    public string? ScopeChain { get; } = scopeChain;

    static string BuildMessage(
        string name,
        string message,
        string? partUri,
        string? contextSnippet,
        string? tokenSource,
        string? modelPath,
        string? scopeChain)
    {
        var builder = new StringBuilder();
        builder.Append("Parchment render failed for template '");
        builder.Append(name);
        builder.Append("': ");
        builder.Append(message);
        if (partUri != null)
        {
            builder.AppendLine();
            builder.Append("  part: ");
            builder.Append(partUri);
        }

        if (contextSnippet != null)
        {
            builder.AppendLine();
            builder.Append("  near: …");
            builder.Append(contextSnippet);
            builder.Append('…');
        }

        if (tokenSource != null)
        {
            builder.AppendLine();
            builder.Append("  token: ");
            builder.Append(tokenSource);
        }

        if (modelPath != null)
        {
            builder.AppendLine();
            builder.Append("  model path: ");
            builder.Append(modelPath);
        }

        if (scopeChain != null)
        {
            builder.AppendLine();
            builder.Append("  scope: ");
            builder.Append(scopeChain);
        }

        return builder.ToString();
    }
}
