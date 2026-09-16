using System;
using System.Linq;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    public class PrologueTests
    {
        [Test]
        public void EveryMissionHasBeatsWithUniqueIdsAndValidLeads()
        {
            foreach (var m in Campaign.Missions)
            {
                var beats = Prologue.BeatsOf(m.Id);
                Assert.IsTrue(beats.Length >= 4, m.Id);
                foreach (var b in beats)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(b.Title), b.Id);
                    if (b.Lead != "") Assert.IsTrue(m.Leads.Contains(b.Lead), m.Id + "/" + b.Id + " led by someone outside the mission");
                    if (b.Kind == BeatKind.Choice) Assert.IsTrue(b.Options.Length > b.Picks, b.Id);
                    else Assert.AreEqual(0, b.Options.Length, b.Id);
                }
            }
            var all = Campaign.Missions.SelectMany(m => Prologue.BeatsOf(m.Id)).Select(b => b.Id).ToList();
            Assert.AreEqual(all.Count, all.Distinct().Count());
        }

        static PrologueRun PlayThrough(Func<PrologueRun, Beat, int[]> pick)
        {
            var run = new PrologueRun();
            int guard = 0;
            while (!run.Finished)
            {
                Assert.Less(guard++, 200);
                if (run.Beat.Kind == BeatKind.Choice) run.Choose(pick(run, run.Beat)); else run.Advance();
            }
            return run;
        }

        [Test]
        public void FullPlayThroughProducesAProtocolWithDatesChoicesAndDocumentedDiary()
        {
            var run = PlayThrough((r, b) => Enumerable.Range(0, b.Picks).ToArray());
            run.StoveSeconds = 47;
            string p = run.Protocol();
            StringAssert.Contains("23.01", p);
            StringAssert.Contains("01.02", p);
            StringAssert.Contains("Лабаз: 73 кг", p);
            StringAssert.Contains("Печка: 47 с", p);
            StringAssert.Contains("Запись дня Колеватова: Про манси и их знаки, Про снег", p);
            Assert.AreEqual(Prologue.Documented.Count, run.Diary.Count(l => l.Documented));
            Assert.AreEqual(2, run.Diary.Count(l => !l.Documented && l.Author.Contains("Колеватов")));
            var dates = run.Diary.Select(l => l.Date).ToList();
            Assert.IsTrue(dates.SequenceEqual(dates.OrderBy(d => d)), "diary is chronological");
            Assert.IsTrue(run.Diary.All(l => l.Date <= new DateTime(1959, 2, 1)), "nothing after the tent is pitched");
        }

        [Test]
        public void ChoicesAreValidatedAndFatigueFollowsTheTrek()
        {
            var run = new PrologueRun();
            Assert.Throws<InvalidOperationException>(() => run.Choose(0));      // first beat is a walk
            run.Advance(); run.Advance();                                        // corridor, stove
            Assert.AreEqual(BeatKind.Choice, run.Beat.Kind);
            Assert.Throws<InvalidOperationException>(() => run.Advance());
            Assert.Throws<ArgumentException>(() => run.Choose(0, 1));            // one pick expected
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Choose(9));
            run.Choose(1);
            Assert.AreEqual("Завхоз — Колмогорова", run.Choices["roles"][0]);

            var walked = PlayThrough((r, b) => b.Id == "yudin-leg" ? new[] { 0 } : Enumerable.Range(0, b.Picks).ToArray());
            var rode = PlayThrough((r, b) => b.Id == "yudin-leg" ? new[] { 1 } : Enumerable.Range(0, b.Picks).ToArray());
            Assert.Greater(walked.Fatigue["yudin"], rode.Fatigue["yudin"]);
            Assert.AreEqual(0f, rode.Fatigue["yudin"]);
            // Nobody comes out of the last ascent rested, and the day leaders tired more than the chain that day.
            Assert.IsTrue(walked.Fatigue.Where(f => f.Key != "yudin").All(f => f.Value > 0f));
        }

        [Test]
        public void KolevatovPicksTwoDistinctObservations()
        {
            var run = new PrologueRun();
            while (run.Beat.Id != "d3-diary") { if (run.Beat.Kind == BeatKind.Choice) run.Choose(0); else run.Advance(); }
            Assert.Throws<ArgumentException>(() => run.Choose(1, 1));
            Assert.Throws<ArgumentException>(() => run.Choose(1));
            Assert.AreEqual("kolevatov", run.Lead);
            run.Choose(3, 0);
            Assert.AreEqual("Про печку|Про манси и их знаки", string.Join("|", run.Choices["d3-diary"]));
            Assert.AreEqual("zolotaryov", run.Lead);
        }
    }
}
