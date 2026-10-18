using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class WinClusterTests
    {
        [Fact]
        public void TwoFarProfilesStayTwoWindows()
        {
            var boxes = new List<WinBox>
            {
                new WinBox(2337273, 467884, 2337400, 468050),
                new WinBox(2343700, 476600, 2343787, 476748)
            };
            var c = WinCluster.Cluster(boxes, WinCluster.EpGapM);
            Assert.Equal(2, c.Count);
            Assert.Equal(2337273.0, c[0].MinX, 3);
            Assert.Equal(2343700.0, c[1].MinX, 3);
        }

        [Fact]
        public void OverlappingBoxesMerge()
        {
            var boxes = new List<WinBox>
            {
                new WinBox(0, 0, 100, 80),
                new WinBox(40, 20, 140, 90)
            };
            var c = WinCluster.Cluster(boxes, WinCluster.EpGapM);
            Assert.Single(c);
            Assert.Equal(0.0, c[0].MinX, 6);
            Assert.Equal(140.0, c[0].MaxX, 6);
            Assert.Equal(0.0, c[0].MinY, 6);
            Assert.Equal(90.0, c[0].MaxY, 6);
        }

        [Fact]
        public void ChainWithinGapBecomesOne()
        {
            var boxes = new List<WinBox>
            {
                new WinBox(0, 0, 10, 10),
                new WinBox(20, 0, 30, 10),
                new WinBox(40, 0, 50, 10)
            };
            var c = WinCluster.Cluster(boxes, 15.0);
            Assert.Single(c);
            Assert.Equal(0.0, c[0].MinX, 6);
            Assert.Equal(50.0, c[0].MaxX, 6);
        }

        [Fact]
        public void ManholeGapsStayOneProfile()
        {
            var boxes = new List<WinBox>
            {
                new WinBox(0, 0, 8, 40),
                new WinBox(10, 0, 18, 40),
                new WinBox(20, 0, 55, 40)
            };
            var c = WinCluster.Cluster(boxes, WinCluster.EpGapM);
            Assert.Single(c);
        }

        [Fact]
        public void UnionOfTwoSitesIsKilometresWide()
        {
            var a = new WinBox(2337273, 467884, 2337400, 468050);
            var b = new WinBox(2343700, 476600, 2343787, 476748);
            var u = a.Union(b);
            Assert.True(u.Width > 6000.0);
            Assert.True(u.Height > 8000.0);
            var c = WinCluster.Cluster(new[] { a, b }, WinCluster.EpGapM);
            Assert.Equal(2, c.Count);
            Assert.True(c[0].Width < 200.0);
            Assert.True(c[1].Width < 200.0);
        }
    }
}
