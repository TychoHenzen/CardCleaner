namespace CardCleaner.Scripts.Core.Devices;

public interface ICable
{
    IJack SourceJack { get; }
    IJack DestinationJack { get; }
    void Connect();
    void Disconnect();
}
