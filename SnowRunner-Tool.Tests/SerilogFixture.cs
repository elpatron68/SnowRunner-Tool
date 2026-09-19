using Serilog;
using Xunit;

namespace SnowRunner_Tool.Tests
{
    [CollectionDefinition(Name)]
    public sealed class SerilogCollection : ICollectionFixture<SerilogFixture>
    {
        public const string Name = "Serilog";
    }

    public sealed class SerilogFixture
    {
        public SerilogFixture()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Fatal()
                .CreateLogger();
        }
    }
}
