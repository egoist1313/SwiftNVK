using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class ProfileAnnotTests
    {
        [Fact]
        public void SheetTitleUsesNetworkName()
        {
            Assert.Equal("Продольный профиль сети К1", ProfileAnnot.SheetTitle("К1"));
            Assert.Equal("Продольный профиль", ProfileAnnot.SheetTitle(null));
            Assert.Equal("Продольный профиль", ProfileAnnot.SheetTitle(""));
        }

        [Fact]
        public void SlopeTenPermilleDown()
        {
            Assert.Equal("-10.0‰", ProfileAnnot.FmtSlope(100.0, 99.9, 10.0));
        }

        [Fact]
        public void JointsAndSegmentsFromChain()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg { X0 = 0, Y0 = 0, X1 = 10, Y1 = 0, InvertStart = 100, InvertEnd = 99.9, Diam = 0.3 },
                new PipeSeg { X0 = 10, Y0 = 0, X1 = 25, Y1 = 0, InvertStart = 99.9, InvertEnd = 99.6, Diam = 0.3 }
            });
            var segs = ProfileAnnot.Segments(chain);
            Assert.Equal(2, segs.Count);
            Assert.Equal("10.00", segs[0].LengthText);
            Assert.Equal("-10.0‰", segs[0].SlopeText);
            var h = ProfileAnnot.Heights(chain, ProfileKind.Truba, new SectionConfig(), null);
            Assert.Equal(3, h.Count);
            Assert.Equal("100.00", h[0].Text);
            Assert.Equal("99.60", h[2].Text);
        }

        [Fact]
        public void HeightLineMovesWithStack()
        {
            Assert.Equal(NamedLine.TrenchBottom, ProfileAnnot.HeightLine(ProfileKind.Razrabotka));
            Assert.Equal(NamedLine.SandTop, ProfileAnnot.HeightLine(ProfileKind.Pesok));
            Assert.Equal(NamedLine.SandTop, ProfileAnnot.HeightLine(ProfileKind.Truba));
            Assert.Equal(NamedLine.ZashTop, ProfileAnnot.HeightLine(ProfileKind.Zashita));
            Assert.Equal(NamedLine.Surface, ProfileAnnot.HeightLine(ProfileKind.Zasypka));
        }
    }
}
