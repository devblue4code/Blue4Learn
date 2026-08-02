namespace Blue4Learn.Web.Services.Ai;

public class AiTutorOptions
{
    public const string SectionName = "AiTutor";

    public bool Enabled { get; set; } = true;
    public string? ApiKey { get; set; }
    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
}
