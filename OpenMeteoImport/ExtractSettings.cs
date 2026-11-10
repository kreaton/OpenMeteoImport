sealed class ExtractSettings
{
    public DateOnly StartDate { get; set; } = new(2024, 1, 1);
    public DateOnly? EndDate { get; set; }
}
