namespace Parchment.Errors;

public sealed class ParchmentRegistrationException(
    string templateName,
    string message,
    string? partUri = null,
    string? tokenSource = null,
    string? modelPath = null,
    Exception? inner = null) :
    ParchmentException(BuildMessage(templateName, message, partUri, tokenSource, modelPath), inner)
{
    public string TemplateName { get; } = templateName;
    public string? PartUri { get; } = partUri;
    public string? TokenSource { get; } = tokenSource;
    public string? ModelPath { get; } = modelPath;

    static string BuildMessage(string name, string message, string? partUri, string? tokenSource, string? modelPath)
    {
        var builder = new StringBuilder();
        builder.Append("Parchment registration failed for template '");
        builder.Append(name);
        builder.Append("': ");
        builder.Append(message);
        if (partUri != null)
        {
            builder.Append(" (part: ");
            builder.Append(partUri);
            builder.Append(')');
        }

        if (tokenSource != null)
        {
            builder.Append(" [token: ");
            builder.Append(tokenSource);
            builder.Append(']');
        }

        if (modelPath != null)
        {
            builder.Append(" [model path: ");
            builder.Append(modelPath);
            builder.Append(']');
        }

        return builder.ToString();
    }
}
