namespace CardCleaner.Scripts.Features.Shop.Customers;

/// <summary>Receives customer complaints during a shop day.</summary>
public interface IComplaintLog
{
    /// <summary>Records one complaint with its reason.</summary>
    void Add(string reason);
}
