namespace Parchment;

internal abstract class RegisteredTemplate(string name, Type modelType)
{
    public string Name { get; } = name;
    public Type ModelType { get; } = modelType;

    public abstract Task<byte[]> Render(object model, Cancel cancel);
}
