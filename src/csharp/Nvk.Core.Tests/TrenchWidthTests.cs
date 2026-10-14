using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class TrenchWidthTests
    {
        [Theory]
        [InlineData(0.3, JointType.Socket,    0.9)]
        [InlineData(0.3, JointType.Weld,      0.8)]
        [InlineData(0.3, JointType.Coupling,  1.1)]
        [InlineData(1.0, JointType.Socket,    2.0)]
        [InlineData(1.0, JointType.Weld,      1.8)]
        [InlineData(1.0, JointType.Coupling,  2.2)]
        [InlineData(2.0, JointType.Socket,    3.4)]
        public void MatchesPipeBuildFormula(double diam, JointType joint, double expected)
        {
            Assert.Equal(expected, TrenchWidth.For(diam, joint), 6);
        }

        [Fact]
        public void NeverBelowMinimum()
        {
            Assert.Equal(0.7, TrenchWidth.For(0.05, JointType.Weld), 6);
        }
    }
}
