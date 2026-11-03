namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>Receives customer complaints during a shop day.</summary>
public interface IComplaintLog
{
    /// <summary>Records one complaint with its reason.</summary>
    void Add(string reason);
}
