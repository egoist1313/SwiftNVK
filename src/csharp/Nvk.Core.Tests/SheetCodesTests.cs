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

        [Fact]
        public void JoinPartsTwoSitesWithSemicolon()
        {
            Assert.Equal("40-41-42; 67-68",
                SheetCodes.JoinParts(new[] { "40-41-42", "67-68" }));
        }

        [Fact]
        public void JoinPartsSkipsDupAndBlank()
        {
            Assert.Equal("40-41-42; 67-68",
                SheetCodes.JoinParts(new[] { "40-41-42", " 67-68 ", "40-41-42", "", null }));
            Assert.Equal("", SheetCodes.JoinParts(null));
            Assert.Equal("", SheetCodes.JoinParts(new string[0]));
        }

        [Fact]
        public void ToFieldTextBindsCustomDp()
        {
            string t = SheetCodes.ToFieldText("уч. %%SEG%% сеть %%NET%%");
            Assert.Contains("CustomDP.NVK_SEG", t);
            Assert.Contains("CustomDP.NVK_NET", t);
            Assert.Contains("%<", t);
            Assert.DoesNotContain("%%SEG%%", t);
            Assert.DoesNotContain("%%NET%%", t);
        }

        [Fact]
        public void HasFieldDetectsCustomDp()
        {
            Assert.True(SheetCodes.HasField("%<\\AcVar CustomDP.NVK_SEG>%"));
            Assert.False(SheetCodes.HasField("%%SEG%%"));
            Assert.False(SheetCodes.HasField(null));
        }

        [Fact]
        public void TypeNameManyMixedWaterAndSewer()
        {
            Assert.Equal("водоснабжения", SheetCodes.TypeNameMany(new[] { "В1", "В2" }));
            Assert.Equal("канализации", SheetCodes.TypeNameMany(new[] { "К1" }));
            Assert.Equal("водоснабжения; канализации",
                SheetCodes.TypeNameMany(new[] { "В1", "К1" }));
            Assert.Equal("", SheetCodes.TypeNameMany(new[] { "Т1" }));
        }

        [Fact]
        public void TolTextManyMixedConcatenates()
        {
            string both = SheetCodes.TolTextMany(new[] { "В1", "К1" });
            Assert.Contains("Напорные", both);
            Assert.Contains("лотков", both);
            Assert.Contains("\\P\\P", both);
            Assert.Contains("Напорные", SheetCodes.TolTextMany(new[] { "В1" }));
            Assert.DoesNotContain("лотков", SheetCodes.TolTextMany(new[] { "В1" }));
        }
    }
}
