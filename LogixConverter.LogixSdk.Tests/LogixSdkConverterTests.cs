namespace LogixConverter.LogixSdk.Tests;

public class LogixSdkConverterTests
{
    [Test]
    public async Task ConvertAsync_ValidFileAndPackages_ShouldWork()
    {
        var converter = new LogixSdkConverter();

        var result = await converter.ConvertAsync(@"Files\Test.ACD", @"Output\Test.L5X");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.SourceFile, Does.EndWith("Test.ACD"));
            Assert.That(result.DesitnationFile, Does.EndWith("Test.L5X"));
            Assert.That(result.Duration, Is.GreaterThan(TimeSpan.FromSeconds(0)));
            Assert.That(result.TimeStamp, Is.AtLeast(DateTime.UtcNow.AddMinutes(-5)));
            Assert.That(result.Error, Is.Null);
        });
    }
}