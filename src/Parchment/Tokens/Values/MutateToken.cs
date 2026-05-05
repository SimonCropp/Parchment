class MutateToken(Action<Paragraph, IOpenXmlContext> mutate) :
    TokenValue
{
    public Action<Paragraph, IOpenXmlContext> Apply { get; } = mutate;
}