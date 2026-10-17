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
    }
}
