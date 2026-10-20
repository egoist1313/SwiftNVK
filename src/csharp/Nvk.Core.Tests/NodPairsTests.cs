using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class NodPairsTests
    {
        [Fact]
        public void ParseZonesAndPavAll()
        {
            var m = NodPairs.Parse(new[]
            {
                "THK_ASPH=1.02",
                "PAV_ALL=0",
                "ZONES=\u0410:0.000-5.707 \u0413:5.707-50.120 \u0422:50.120-55.808",
                "FILL_ASPH=SAND"
            });
            Assert.Equal("0", NodPairs.Get(m, "PAV_ALL", "1"));
            Assert.Contains("\u0410:0.000-5.707", NodPairs.Get(m, "ZONES", ""));
            Assert.Equal("1.02", NodPairs.Get(m, "THK_ASPH", ""));
            Assert.Equal("SAND", NodPairs.Get(m, "FILL_ASPH", ""));
        }

        [Fact]
        public void ConcatenatesChunks()
        {
            var m = NodPairs.Parse(new[]
            {
                "ZONES=\u0410:0-1",
                "ZONES#2= \u0413:1-2",
                "ZONES#3= \u0422:2-3"
            });
            Assert.Equal("\u0410:0-1 \u0413:1-2 \u0422:2-3", NodPairs.Get(m, "ZONES", ""));
            Assert.False(m.ContainsKey("ZONES#2"));
        }

        [Fact]
        public void MissingKeyIsDefault()
        {
            var m = NodPairs.Parse(new[] { "THK_ASPH=1.02" });
            Assert.Equal("", NodPairs.Get(m, "ZONES", ""));
            Assert.Equal("x", NodPairs.Get(m, "NOPE", "x"));
        }

        [Fact]
        public void RestoreAbsentKeepsZonesWhenPresetOmitsThem()
        {
            var prev = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "ZONES", "А:0.000-5.707 Г:5.707-50.120" },
                { "DESIGN_H", "ABC" }
            };
            var preset = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "THK_ASPH", "1.02" },
                { "PAV_ALL", "0" }
            };
            var got = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            NodPairs.RestoreAbsent(preset, prev, (k, v) => got[k] = v);
            Assert.Equal("А:0.000-5.707 Г:5.707-50.120", got["ZONES"]);
            Assert.Equal("ABC", got["DESIGN_H"]);
            Assert.Contains("ZONES", NodPairs.DrawingBound);
        }

        [Fact]
        public void RestoreAbsentDoesNotOverridePresentEmptyZones()
        {
            var prev = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "ZONES", "А:0-10" }
            };
            var nod = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "ZONES", "" }
            };
            var got = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            NodPairs.RestoreAbsent(nod, prev, (k, v) => got[k] = v);
            Assert.False(got.ContainsKey("ZONES"));
        }

        [Fact]
        public void EmptyInput()
        {
            var m = NodPairs.Parse(null);
            Assert.Empty(m);
            m = NodPairs.Parse(new string[0]);
            Assert.Empty(m);
        }

        [Fact]
        public void TrenchAndRowsFromDrawing()
        {
            var m = NodPairs.Parse(new[]
            {
                "TRENCH_L=0.450",
                "TRENCH_R=0.600",
                "TBL_ROWS_L1=ROAD|EARTH",
                "TBL_ROWS_L1#2=|BOTTOM"
            });
            Assert.Equal("0.450", NodPairs.Get(m, "TRENCH_L", ""));
            Assert.Equal("0.600", NodPairs.Get(m, "TRENCH_R", ""));
            Assert.Equal("ROAD|EARTH|BOTTOM", NodPairs.Get(m, "TBL_ROWS_L1", ""));
        }

        [Fact]
        public void MissingTrenchAndRowsAreEmpty()
        {
            var m = NodPairs.Parse(new[] { "THK_ASPH=1.02" });
            Assert.Equal("", NodPairs.Get(m, "TRENCH_L", ""));
            Assert.Equal("", NodPairs.Get(m, "TRENCH_R", ""));
            Assert.Equal("", NodPairs.Get(m, "TBL_ROWS_L1", ""));
            Assert.False(m.ContainsKey("TRENCH_L"));
            Assert.False(m.ContainsKey("TBL_ROWS_L1"));
        }
    }
}
