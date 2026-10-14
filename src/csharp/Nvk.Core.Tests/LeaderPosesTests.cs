using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class LeaderPosesTests
    {
        [Fact]
        public void RoundtripKeepsXyRotAndArrow()
        {
            var map = LeaderPoses.Parse("");
            map["Т1"] = new LeaderPose { Key = "Т1", X = 10.5, Y = -3.25, Rot = 1.5708, Ax = 8.0, Ay = -1.0, HasArrow = true };
            map["КК-43"] = new LeaderPose { Key = "КК-43", X = 1, Y = 2, Rot = 0 };
            var back = LeaderPoses.Parse(LeaderPoses.Format(map));
            Assert.Equal(2, back.Count);
            Assert.Equal(10.5, back["Т1"].X, 6);
            Assert.Equal(-3.25, back["Т1"].Y, 6);
            Assert.Equal(1.5708, back["Т1"].Rot, 4);
            Assert.True(back["Т1"].HasArrow);
            Assert.Equal(8.0, back["Т1"].Ax, 6);
            Assert.Equal(-1.0, back["Т1"].Ay, 6);
            Assert.Equal(1.0, back["КК-43"].X, 6);
            Assert.False(back["КК-43"].HasArrow);
        }

        [Fact]
        public void OverlayDrawingWins()
        {
            var cache = LeaderPoses.Parse("Т1|0|0|0");
            var draw = LeaderPoses.Parse("Т1|5|7|0.5");
            LeaderPoses.Overlay(cache, draw);
            Assert.Equal(5.0, cache["Т1"].X, 6);
            Assert.Equal(0.5, cache["Т1"].Rot, 6);
            Assert.False(cache["Т1"].HasArrow);
        }

        [Fact]
        public void OverlayDrawingArrowWinsOverFourFieldCache()
        {
            var cache = LeaderPoses.Parse("Т1|0|0|0");
            var draw = LeaderPoses.Parse("Т1|5|7|0.5|12.25|-4.5");
            LeaderPoses.Overlay(cache, draw);
            Assert.True(cache["Т1"].HasArrow);
            Assert.Equal(12.25, cache["Т1"].Ax, 6);
            Assert.Equal(-4.5, cache["Т1"].Ay, 6);
        }

        [Fact]
        public void ParseFourFieldHasNoArrow()
        {
            var map = LeaderPoses.Parse("КК-1|1|2|0");
            Assert.False(map["КК-1"].HasArrow);
        }
    }
}