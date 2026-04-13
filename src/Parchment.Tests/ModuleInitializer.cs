public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyOpenXml.Initialize();
        VerifierSettings.InitializePlugins();
        VerifierSettings.UseStrictJson();
    }
}
