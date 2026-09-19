using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>One bar, bitten from the top (backlog E0.1). Engine-free, runs in dotnet.</summary>
    public class ConditionTests
    {
        [Test]
        public void FreshBarIsWholeAndUnbitten()
        {
            var c = new Condition();
            Assert.AreEqual(Condition.Max, c.Ceiling, .001f);
            Assert.AreEqual(1f, c.CeilingFraction, .001f);
            Assert.AreEqual(0f, c.Bitten, .001f);
            Assert.IsFalse(c.Spent);
        }

        [Test]
        public void BitesComeOffTheTopOfTheBar()
        {
            var c = new Condition();
            c.Add(Bite.Hunger, 20f);
            c.Add(Bite.Cold, 10f);
            Assert.AreEqual(20f, c.Of(Bite.Hunger), .001f);
            Assert.AreEqual(10f, c.Of(Bite.Cold), .001f);
            Assert.AreEqual(70f, c.Ceiling, .001f, "потолок = 100 − сумма кусков");
        }

        [Test]
        public void BitesCannotTakeTheBarBelowNothing()
        {
            var c = new Condition();
            c.Add(Bite.Hunger, 60f);
            c.Add(Bite.Cold, 60f);
            c.Add(Bite.Sleep, 60f);
            Assert.AreEqual(0f, c.Ceiling, .001f);
            Assert.AreEqual(60f, c.Of(Bite.Hunger), .001f);
            Assert.AreEqual(40f, c.Of(Bite.Cold), .001f, "холод получил только то, что оставалось");
            Assert.AreEqual(0f, c.Of(Bite.Sleep), .001f, "сну не досталось ничего");
            Assert.IsTrue(c.Spent);
        }

        [Test]
        public void EasingGivesTheBarBackAndStopsAtZero()
        {
            var c = new Condition();
            c.Add(Bite.Hunger, 30f);
            c.Ease(Bite.Hunger, Condition.Chocolate.Hunger);
            Assert.AreEqual(5f, c.Of(Bite.Hunger), .001f);
            c.Ease(Bite.Hunger, 50f);
            Assert.AreEqual(0f, c.Of(Bite.Hunger), .001f);
            Assert.AreEqual(Condition.Max, c.Ceiling, .001f);
        }

        [Test]
        public void TimeGrowsHungerAndSleepAndColdOnlyWherePressed()
        {
            var c = new Condition();
            var p = new Condition.Pressures { Cold = 0f };
            c.Tick(60f, p);
            Assert.Greater(c.Of(Bite.Hunger), 0f, "голод растёт всегда");
            Assert.Greater(c.Of(Bite.Sleep), 0f, "сон приходит сам");
            Assert.AreEqual(0f, c.Of(Bite.Cold), .001f, "без мороза холода нет");
            Assert.Greater(c.Of(Bite.Hunger), c.Of(Bite.Sleep), "голод приходит раньше сна");
            p.Cold = 2f;
            c.Tick(10f, p);
            Assert.AreEqual(20f, c.Of(Bite.Cold), .001f);
            // a stove is a negative cold
            p.Cold = -5f;
            c.Tick(10f, p);
            Assert.AreEqual(0f, c.Of(Bite.Cold), .001f);
        }

        [Test]
        public void DefaultHungerFillsTheBarInTwelveMinutes()
        {
            var c = new Condition();
            var p = new Condition.Pressures { Sleep = 0f };
            c.Tick(12f * 60f, p);
            Assert.AreEqual(Condition.Max, c.Of(Bite.Hunger), .01f);
            Assert.IsTrue(c.Spent);
        }
    }
}
