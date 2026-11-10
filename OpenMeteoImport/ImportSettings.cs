sealed class ImportSettings
{
    public LocationSettings Location { get; set; } = new();
    public ExtractSettings Extract { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
}
