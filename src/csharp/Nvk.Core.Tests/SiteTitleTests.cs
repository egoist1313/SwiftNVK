using System.Collections.Generic;
using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class SiteTitleTests
    {
        [Fact]
        public void TokenTakesTrailingNumber()
        {
            Assert.Equal("3", SiteTitle.Token("КК-3"));
            Assert.Equal("3", SiteTitle.Token("К3"));
            Assert.Equal("4", SiteTitle.Token("Колодец 4"));
            Assert.Equal("5а", SiteTitle.Token("К5а"));
            Assert.Equal("ABC", SiteTitle.Token("ABC"));
            Assert.Equal("", SiteTitle.Token(""));
        }

        [Fact]
        public void FromChainJoinsManholes()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 10, Y1 = 0,
                    InvertStart = 1, InvertEnd = 1, Diam = 0.3,
                    StartName = "К3", EndName = "К4"
                },
                new PipeSeg
                {
                    X0 = 10, Y0 = 0, X1 = 20, Y1 = 0,
                    InvertStart = 1, InvertEnd = 1, Diam = 0.3,
                    StartName = "К4", EndName = "К5"
                },
                new PipeSeg
                {
                    X0 = 20, Y0 = 0, X1 = 30, Y1 = 0,
                    InvertStart = 1, InvertEnd = 1, Diam = 0.3,
                    StartName = "К5", EndName = "К6"
                }
            });
            Assert.Equal("3-4-5-6", SiteTitle.FromChain(chain));
        }

        [Fact]
        public void DuplicateJointNameIsSkipped()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 10, Y1 = 0,
                    InvertStart = 1, InvertEnd = 1, Diam = 0.3,
                    StartName = "3", EndName = "4"
                }
            });
            Assert.Equal("3-4", SiteTitle.FromChain(chain));
        }

        [Fact]
        public void StripParenDropsBracketSuffix()
        {
            Assert.Equal("12", SiteTitle.StripParen("12(1)"));
            Assert.Equal("12", SiteTitle.StripParen("12 (2)"));
            Assert.Equal("К12", SiteTitle.StripParen("К12(1)"));
            Assert.Equal("ГП12", SiteTitle.StripParen("ГП12(1)"));
            Assert.Equal("12", SiteTitle.Token("12(1)", true));
            Assert.Equal("К12", SiteTitle.Token("К12 (2)", true));
            Assert.Equal("ГП12", SiteTitle.Token("ГП12(1)", true));
            Assert.Equal("Выпуск ГП 12", SiteTitle.Token("Выпуск ГП 12(4)", true));
            Assert.Equal("12(1)", SiteTitle.Token("12(1)", false));
            Assert.Equal("ГП12", SiteTitle.Label("ГП12(1)", true));
            Assert.Equal("ГП12(1)", SiteTitle.Label("ГП12(1)", false));
        }

        [Fact]
        public void FromChainStripParenKeepsLetterPrefix()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 10, Y1 = 0,
                    InvertStart = 1, InvertEnd = 1, Diam = 0.3,
                    StartName = "ГП12(1)", EndName = "ГП13(2)"
                }
            });
            Assert.Equal("ГП12-ГП13", SiteTitle.FromChain(chain, true));
            Assert.Equal("ГП12(1)-ГП13(2)", SiteTitle.FromChain(chain, false));
        }

        [Fact]
        public void FromChainStripParenJoinsBareNumbers()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 10, Y1 = 0,
                    InvertStart = 1, InvertEnd = 1, Diam = 0.3,
                    StartName = "12(1)", EndName = "13 (2)"
                }
            });
            Assert.Equal("12-13", SiteTitle.FromChain(chain, true));
            Assert.Equal("12(1)-13 (2)", SiteTitle.FromChain(chain, false));
        }

        [Fact]
        public void StripParenCollapsesDuplicateAfterClean()
        {
            var chain = Chain.Sort(new List<PipeSeg>
            {
                new PipeSeg
                {
                    X0 = 0, Y0 = 0, X1 = 10, Y1 = 0,
                    InvertStart = 1, InvertEnd = 1, Diam = 0.3,
                    StartName = "12(1)", EndName = "12(2)"
                }
            });
            Assert.Equal("12", SiteTitle.FromChain(chain, true));
        }

        [Fact]
        public void CleanTitleKeepsTwoSitesAndDropsParens()
        {
            Assert.Equal("12-13; 67-68",
                SiteTitle.CleanTitle("12(1)-13 (2); 67-68", true));
            Assert.Equal("12", SiteTitle.CleanTitle("12(1)-12(2)", true));
            Assert.Equal("Участок 1", SiteTitle.CleanTitle("Участок 1", true));
            Assert.Equal("12(1)-13", SiteTitle.CleanTitle("12(1)-13", false));
        }

        [Fact]
        public void StampStripsParenDrawingKeeps()
        {
            Assert.Equal("ГП12-ГП13",
                SiteTitle.CleanTitle("ГП12(1)-ГП13(2)", true));
            Assert.Equal("ГП12(1)", SiteTitle.Label("ГП12(1)", false));
        }
    }
}
