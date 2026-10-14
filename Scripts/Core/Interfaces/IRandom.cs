namespace CardCleaner.Scripts.Core.Interfaces;

public interface IRandom
{
    ulong Seed { get; set; }

    uint Randi();

    float Randf();

    int RandiRange(int minimum, int maximum);

    float RandfRange(float minimum, float maximum);

    float Randfn(float mean, float deviation);

    void Randomize();
}
