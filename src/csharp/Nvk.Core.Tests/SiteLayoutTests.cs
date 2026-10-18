using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class SiteLayoutTests
    {
        [Fact]
        public void DefaultOffsetsMatchPipeline()
        {
            var t = new SiteLayout();
            t.SetCs(100, 200);
            var at = t.At(100, 200);
            Assert.Equal(100, at.CsX, 6);
            Assert.Equal(200, at.CsY, 6);
            Assert.Equal(180, at.EpX, 6);
            Assert.Equal(200, at.EpY, 6);
            Assert.Equal(180, at.VolX, 6);
            Assert.Equal(160, at.VolY, 6);
        }

        [Fact]
        public void RebaseKeepsCsEpVolDeltasLike4346()
        {
            var t = new SiteLayout();
            t.SetCs(2342997.1, 476680.0);
            t.SetEp(2343077.1, 476680.0);
            t.SetVol(2342997.1, 476640.0);
            t.SetCsLvl(0, 2343768.9, 476490.0);
            t.SetCsLvl(1, 2343768.9, 475977.5);

            var at = t.At(0, 0);
            Assert.Equal(0.0, at.CsX, 6);
            Assert.Equal(0.0, at.CsY, 6);
            Assert.Equal(80.0, at.EpX, 6);
            Assert.Equal(0.0, at.EpY, 6);
            Assert.Equal(0.0, at.VolX, 6);
            Assert.Equal(-40.0, at.VolY, 6);
            Assert.Equal(2343768.9 - 2342997.1, at.CsLx[0], 6);
            Assert.Equal(476490.0 - 476680.0, at.CsLy[0], 6);
            Assert.Equal(at.CsLx[0], at.CsLx[1], 6);
            Assert.Equal(476490.0 - 476680.0 - 512.5, at.CsLy[1], 4);
        }

        [Fact]
        public void EmptyTemplatePutsCsAtPickAndDefaultEpVol()
        {
            var t = new SiteLayout();
            var at = t.At(10, 20);
            Assert.True(at.HasCs);
            Assert.Equal(10, at.CsX, 6);
            Assert.Equal(20, at.CsY, 6);
            Assert.Equal(90, at.EpX, 6);
            Assert.Equal(20, at.EpY, 6);
            Assert.Equal(90, at.VolX, 6);
            Assert.Equal(-20, at.VolY, 6);
        }
    }
}
