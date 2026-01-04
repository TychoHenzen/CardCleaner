using CardCleaner.Scripts.Core.Services;
using GdUnit4;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
public class LoggingServiceTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void LoggingService_CanBeInstantiated()
    {
        // Arrange & Act
        var service = new LoggingService();

        // Assert - Should be able to create the service
        Assertions.AssertThat(service).IsNotNull();
        Assertions.AssertThat(service).IsInstanceOf<LoggingService>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LoggingService_ImplementsILogInterface()
    {
        // Arrange & Act
        var service = new LoggingService();

        // Assert - Should implement ILog interface
        Assertions.AssertThat(service).IsInstanceOf<Scripts.Core.Interfaces.ILog>();
    }

    // Note: We cannot safely test the actual logging methods (LogMessage, LogWarning, LogError)
    // because they call GD.Print() which requires full Godot runtime context.
    // These would be better tested as integration tests in a full Godot scene.
}