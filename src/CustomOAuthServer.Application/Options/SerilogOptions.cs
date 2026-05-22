namespace CustomOAuthServer.Application.Options;

public sealed class SerilogOptions
{
    public const string SectionName = "Serilog";

    public string LogFilePath { get; set; } = "logs/customoauth-.log";
    public string MinimumLevel { get; set; } = "Information";
}
