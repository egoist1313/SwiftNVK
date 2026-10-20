using Nvk.Core;
using Xunit;

namespace Nvk.Core.Tests
{
    public class ScaleTextTests
    {
        [Fact]
        public void PlanM1FollowsScale()
        {
            string src = "{\\fGOST Common|b0|i0|c204|p34;\\C256;План\\PМ1:500}";
            Assert.Equal(
                "{\\fGOST Common|b0|i0|c204|p34;\\C256;План\\PМ1:200}",
                ScaleText.SubstPlan(src, 200));
            Assert.Equal(
                "{\\fGOST Common|b0|i0|c204|p34;\\C256;План\\PМ1:1000}",
                ScaleText.SubstPlan(src, 1000));
        }

        [Fact]
        public void SituationPlanStays()
        {
            string src = "\\pxqc;{\\C256;Ситуационный план \\PМ1:5000}";
            Assert.Equal(src, ScaleText.SubstPlan(src, 200));
        }

        [Fact]
        public void SpaceAfterM()
        {
            Assert.Equal("План М 1:200", ScaleText.SubstPlan("План М 1:500", 200));
        }

        [Fact]
        public void OtherTextUntouched()
        {
            Assert.Equal("профиль 1:1000", ScaleText.SubstPlan("профиль 1:1000", 200));
            Assert.Equal("", ScaleText.SubstPlan("", 200));
            Assert.Equal("План М1:500", ScaleText.SubstPlan("План М1:500", 0));
        }
    }
}
