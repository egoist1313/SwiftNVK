using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class AnnoScalesTests
    {
        [Fact]
        public void DefaultFourScales()
        {
            Assert.Equal(new[] { 200.0, 500.0, 1000.0, 5000.0 }, AnnoScales.Default);
            Assert.True(AnnoScales.IsDefault(200));
            Assert.True(AnnoScales.IsDefault(500));
            Assert.True(AnnoScales.IsDefault(1000));
            Assert.True(AnnoScales.IsDefault(5000));
            Assert.False(AnnoScales.IsDefault(100));
            Assert.False(AnnoScales.IsDefault(50));
            Assert.Equal("1:5000", AnnoScales.NameOf(5000));
            Assert.Equal("1:200", AnnoScales.NameOf(200));
        }
    }
}
