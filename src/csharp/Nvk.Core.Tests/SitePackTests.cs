using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class SitePackTests
    {
        [Fact]
        public void JoinSplitRoundtrip()
        {
            var a = new[]
            {
                new SiteRec("U1", "Участок 1", "ABC;DEF"),
                new SiteRec("U2", "Участок 2", "123")
            };
            string s = SitePack.Join(a);
            var b = SitePack.Split(s);
            Assert.Equal(2, b.Count);
            Assert.Equal("U1", b[0].Id);
            Assert.Equal("Участок 1", b[0].Name);
            Assert.Equal("ABC;DEF", b[0].Pipes);
            Assert.Equal("U2", b[1].Id);
            Assert.Equal("123", b[1].Pipes);
        }

        [Fact]
        public void EmptyIsEmpty()
        {
            Assert.Equal("", SitePack.Join(null));
            Assert.Equal("", SitePack.Join(new SiteRec[0]));
            Assert.Empty(SitePack.Split(null));
            Assert.Empty(SitePack.Split(""));
        }

        [Fact]
        public void NextIdSkipsExisting()
        {
            var list = SitePack.Split("U1::А::||U3::Б::");
            Assert.Equal("U4", SitePack.NextId(list));
            Assert.Equal("U1", SitePack.NextId(null));
        }

        [Fact]
        public void NextNameSkipsUsed()
        {
            var list = new[] { new SiteRec("U1", "Участок 1", "") };
            Assert.Equal("Участок 2", SitePack.NextName(list));
        }

        [Fact]
        public void NameDoesNotBreakSplit()
        {
            var a = new[] { new SiteRec("U1", "Участок::х", "ABC;DEF") };
            var b = SitePack.Split(SitePack.Join(a));
            Assert.Single(b);
            Assert.Equal("U1", b[0].Id);
            Assert.DoesNotContain("::", b[0].Name);
            Assert.Equal("ABC;DEF", b[0].Pipes);
        }

        [Fact]
        public void PreferPackedWinsOverKeysAndMemory()
        {
            var packed = SitePack.Join(new[]
            {
                new SiteRec("U1", "А", "AA"),
                new SiteRec("U2", "Б", "BB")
            });
            var keys = new[] { new SiteRec("U9", "X", "") };
            var mem = new[] { new SiteRec("U1", "старый", "") };
            var r = SitePack.Prefer(packed, keys, mem);
            Assert.Equal(2, r.Count);
            Assert.Equal("U1", r[0].Id);
            Assert.Equal("Б", r[1].Name);
        }

        [Fact]
        public void PreferEmptyPackedUsesSRecords()
        {
            var keys = new[]
            {
                new SiteRec("U1", "61-62", "AA"),
                new SiteRec("U2", "65-66", "BB")
            };
            var r = SitePack.Prefer("", keys, null);
            Assert.Equal(2, r.Count);
            Assert.Equal("U2", r[1].Id);
        }

        [Fact]
        public void PreferEmptyDoesNotWipeMemoryList()
        {
            var mem = new[]
            {
                new SiteRec("U1", "61-62", "AA"),
                new SiteRec("U2", "65-66", "BB")
            };
            var r = SitePack.Prefer("", null, mem);
            Assert.Equal(2, r.Count);
            Assert.Equal("U1", r[0].Id);
            Assert.Equal("U2", r[1].Id);
        }

        [Fact]
        public void PreferEmptySingleMemoryStaysEmpty()
        {
            var mem = new[] { new SiteRec("U1", "Участок 1", "") };
            var r = SitePack.Prefer("", new SiteRec[0], mem);
            Assert.Empty(r);
        }

        [Fact]
        public void PreferLongerSRecordsBeatStalePacked()
        {
            var packed = SitePack.Join(new[] { new SiteRec("U1", "А", "AA") });
            var keys = new[]
            {
                new SiteRec("U1", "А", "AA"),
                new SiteRec("U2", "Б", "BB")
            };
            var r = SitePack.Prefer(packed, keys, null);
            Assert.Equal(2, r.Count);
            Assert.Equal("U2", r[1].Id);
        }

        [Fact]
        public void SameComparesIdNamePipes()
        {
            var a = new[] { new SiteRec("U1", "А", "AA"), new SiteRec("U2", "Б", "BB") };
            var b = new[] { new SiteRec("u1", "А", "aa"), new SiteRec("U2", "Б", "BB") };
            Assert.True(SitePack.Same(a, b));
            Assert.False(SitePack.Same(a, new[] { new SiteRec("U1", "А", "AA") }));
            Assert.False(SitePack.Same(a, new[] { new SiteRec("U1", "А", "AA"), new SiteRec("U2", "В", "BB") }));
        }
    }
}
