namespace MedFlow.Infrastructure.Waitlist;

public class WaitlistSettings
{
    public const string SectionName = "Waitlist";
    public const int DefaultOfferHours = 24;
    public const int DefaultMaxOffersPerSlot = 5;

    public int OfferHours { get; set; } = DefaultOfferHours;
    public int MaxOffersPerSlot { get; set; } = DefaultMaxOffersPerSlot;

    /// <summary>Out-of-range values fall back to the default instead of failing the app.</summary>
    public int EffectiveOfferHours => OfferHours is >= 1 and <= 168 ? OfferHours : DefaultOfferHours;
    public int EffectiveMaxOffersPerSlot => MaxOffersPerSlot is >= 1 and <= 20 ? MaxOffersPerSlot : DefaultMaxOffersPerSlot;
}
