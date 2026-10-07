namespace MedFlow.Infrastructure.Reminders;

public class ReminderSettings
{
    public const string SectionName = "Reminders";
    public const int DefaultLeadTimeHours = 24;
    public const int MaxLeadTimeHours = 168;

    public int LeadTimeHours { get; set; } = DefaultLeadTimeHours;
    public int IntervalMinutes { get; set; } = 15;
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Out-of-range values fall back to the default instead of failing the app.</summary>
    public int EffectiveLeadTimeHours =>
        LeadTimeHours is >= 1 and <= MaxLeadTimeHours ? LeadTimeHours : DefaultLeadTimeHours;
    public int EffectiveIntervalMinutes => IntervalMinutes >= 1 ? IntervalMinutes : 15;
    public int EffectiveMaxAttempts => MaxAttempts is >= 1 and <= 10 ? MaxAttempts : 3;
}
