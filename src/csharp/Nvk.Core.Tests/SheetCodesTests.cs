using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class SheetCodesTests
    {
        [Fact]
        public void SubstReplacesAllFourCodes()
        {
            string s = SheetCodes.Subst(
                "сеть %%NET%% участок %%SEG%% тип %%TYPE%%",
                "В1", "43-44-46", "TOL", "водоснабжения");
            Assert.Equal("сеть В1 участок 43-44-46 тип водоснабжения", s);
            Assert.DoesNotContain("%%", s);
        }

        [Fact]
        public void WaterNetGivesSupplyAndPressureTol()
        {
            Assert.Equal("водоснабжения", SheetCodes.TypeName("В1-город"));
            Assert.Equal("водоснабжения", SheetCodes.TypeName("сеть В2"));
            string tol = SheetCodes.TolText("B1");
            Assert.Contains("Напорные", tol);
            Assert.Contains("\\U+00B1100мм", tol);
            Assert.Contains("\\U+00B130мм", tol);
            Assert.DoesNotContain("лотков", tol);
        }

        [Fact]
        public void SewerNetGivesSewageAndInvertTol()
        {
            Assert.Equal("канализации", SheetCodes.TypeName("К1"));
            Assert.Equal("канализации", SheetCodes.TypeName("коллектор K2"));
            string tol = SheetCodes.TolText("К2-ливневка");
            Assert.Contains("Безнапорные", tol);
            Assert.Contains("лотков", tol);
            Assert.Contains("\\U+00B15мм", tol);
        }

        [Fact]
        public void UnknownNetLeavesTolAndTypeEmpty()
        {
            Assert.Equal("", SheetCodes.TypeName("Т1"));
            Assert.Equal("", SheetCodes.TolText(""));
            string s = SheetCodes.Subst("%%TOL%%%%TYPE%%", "Т1", "x", "", "");
            Assert.Equal("", s);
        }

        [Fact]
        public void HasCodeDetectsPlaceholders()
        {
            Assert.True(SheetCodes.HasCode("штамп %%SEG%%"));
            Assert.False(SheetCodes.HasCode("43-44-46"));
            Assert.False(SheetCodes.HasCode(null));
        }
    }
}
