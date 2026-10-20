using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class SheetSnapTests
    {
        [Fact]
        public void TryVpHitsMatchingKey()
        {
            var lay = new SheetSnapLayout
            {
                Kind = "Razrabotka",
                Vps =
                {
                    new SheetSnapVp { Key = "EP:0", Cx = 120, Cy = 400, W = 200, H = 80 },
                    new SheetSnapVp { Key = "PLAN:0", Cx = 500, Cy = 200, W = 150, H = 150 }
                }
            };
            double cx, cy, w, h;
            Assert.True(SheetSnap.TryVp(lay, "EP:0", out cx, out cy, out w, out h));
            Assert.Equal(120.0, cx, 6);
            Assert.Equal(400.0, cy, 6);
            Assert.Equal(200.0, w, 6);
            Assert.Equal(80.0, h, 6);
        }

        [Fact]
        public void TryVpLeavesUnknownKey()
        {
            var lay = new SheetSnapLayout
            {
                Kind = "Truba",
                Vps = { new SheetSnapVp { Key = "EP:2", Cx = 10, Cy = 10, W = 40, H = 40 } }
            };
            double cx, cy, w, h;
            Assert.False(SheetSnap.TryVp(lay, "CS:2:0", out cx, out cy, out w, out h));
            Assert.Equal(0.0, cx, 6);
        }

        [Fact]
        public void FindLayoutByKind()
        {
            var file = new SheetSnapFile
            {
                Version = 1,
                Layouts =
                {
                    new SheetSnapLayout { Kind = "Pesok", Name = "Песок 10см" },
                    new SheetSnapLayout { Kind = "Truba", Name = "Труба" }
                }
            };
            Assert.Equal("Труба", SheetSnap.Find(file, "Truba").Name);
            Assert.Null(SheetSnap.Find(file, "Zasypka"));
        }

        [Fact]
        public void TitleEntIsSheetMtext()
        {
            var lay = new SheetSnapLayout
            {
                Ents =
                {
                    new SheetSnapEnt { T = "LINE", Layer = "0" },
                    new SheetSnapEnt { T = "MTEXT", Layer = "NVK-SHEET", X = 8, Y = 580 }
                }
            };
            var t = SheetSnap.TitleEnt(lay);
            Assert.NotNull(t);
            Assert.Equal(8.0, t.X, 6);
            Assert.True(SheetSnap.IsSheetTitle(t));
        }

        [Fact]
        public void HasPageNeedsDevice()
        {
            Assert.False(SheetSnap.HasPage(null));
            Assert.False(SheetSnap.HasPage(new SheetSnapPage()));
            Assert.True(SheetSnap.HasPage(new SheetSnapPage { Device = "DWG To PDF.pc3" }));
        }

        [Fact]
        public void DefaultPagePrefersFileThenRazrabotka()
        {
            var file = new SheetSnapFile
            {
                Layouts =
                {
                    new SheetSnapLayout
                    {
                        Kind = "Truba",
                        Page = new SheetSnapPage { Device = "None" }
                    },
                    new SheetSnapLayout
                    {
                        Kind = "Razrabotka",
                        Page = new SheetSnapPage { Device = "DWG To PDF.pc3", Media = "ISO_A1" }
                    }
                }
            };
            Assert.Equal("DWG To PDF.pc3", SheetSnap.DefaultPage(file).Device);
            file.Page = new SheetSnapPage { Device = "PDF.pc3" };
            Assert.Equal("PDF.pc3", SheetSnap.DefaultPage(file).Device);
            Assert.Equal("DWG To PDF.pc3", SheetSnap.PageOf(file, "Razrabotka").Device);
            Assert.Equal("None", SheetSnap.PageOf(file, "Truba").Device);
            Assert.Equal("PDF.pc3", SheetSnap.PageOf(file, "Zasypka").Device);
        }

        [Fact]
        public void EmptyFileFallsBackToStdA3()
        {
            var p = SheetSnap.DefaultPage(null);
            Assert.NotNull(p);
            Assert.Equal("DWG To PDF.pc3", p.Device);
            Assert.Contains("A3", p.Media);
            Assert.Equal(297.0, p.PaperW, 6);
            Assert.Equal(420.0, p.PaperH, 6);
            Assert.Equal("Degrees090", p.Rotation);
            Assert.Equal(p.Device, SheetSnap.StdPage().Device);
        }
    }
}
