using System;
using System.Collections.Generic;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{

    /// <summary>The file a night writes. Until <see cref="CampSave"/> existed this rule lived on a
    /// <c>NetworkBehaviour</c> and no test could reach it; two of the bugs the audit of 19.09.2026 found were here.</summary>
    public class CampSaveTests
    {
        static CampSave.Night ANightAt(float ele, bool slept, float elapsed = 40000f, int day = 9000)
            => new CampSave.Night
            {
                Place = Place.Elbrus,
                SavedUtc = new DateTime(2026, 9, 19, 18, 0, 0, DateTimeKind.Utc),
                Elapsed = elapsed,
                Day = day,
                Storm = true,
                FreshSnowCm = 12f,
                Seed = 4242,
                X = 100f, Y = ele, Z = -200f, Yaw = 30f, Ele = ele,
                Burner = true,
                Tent = true,
                Where = "косая полка",
                Slept = slept,
            };

        static CampSave.Sleeper AClimber(string key, string name = "Дон")
            => new CampSave.Sleeper
            {
                Key = key,
                Name = name,
                Body = new Climber { Acclimatisation = .62f, HighestEle = 5100f, ThermosSips = 3, CramponsOn = true },
                Heat = 71f, Hands = 64f, Clarity = 88f, Strength = .4f,
                Roubles = 175,
                Pack = new List<ItemStack> { new ItemStack(ItemId.Tent), new ItemStack(ItemId.Thermos) },
                Hand = new ItemStack(ItemId.IceAxe),
            };

        [Test]
        public void ANightSleptStartsTheMorningAndCarriesTheBodyIntoTheFile()
        {
            var night = ANightAt(4800f, slept: true);
            var save = CampSave.Build(night, new List<CampSave.Sleeper> { AClimber("don") }, null);

            // the morning: the clock goes back, the day moves on by one, yesterday's snow stops counting
            Assert.AreEqual(0f, save.Elapsed, 1e-4f, "ночь спали — часы должны вернуться к утру");
            Assert.AreEqual(Forecast.DateOf(9001), save.Date, "ночь спали — дата должна сдвинуться на день");
            Assert.AreEqual(0f, save.FreshSnowCm, 1e-4f);
            // and the day is recoverable from the date alone, which is all the file keeps
            Assert.AreEqual(9001, Forecast.DayIndex(save.Date));

            var entry = save.Find("don");
            Assert.IsNotNull(entry, "сейв должен знать ключ игрока");
            Assert.AreEqual("Дон", entry.Name);
            Assert.AreEqual(.62f, entry.Acclim, 1e-4f);
            Assert.AreEqual(5100f, entry.Highest, 1e-3f);
            Assert.AreEqual(175, entry.Roubles);
            Assert.AreEqual(3, entry.Sips);
            Assert.IsTrue(entry.Crampons);
            Assert.AreEqual(2, entry.Pack.Count);
            Assert.AreEqual(ItemId.IceAxe, entry.Hand.Id);
            // the camp, so that everybody wakes up beside it
            Assert.AreEqual(4800f, save.CampEle, 1e-3f);
            Assert.AreEqual(100f, save.CampX, 1e-3f);
            Assert.IsTrue(save.Tent);
        }

        [Test]
        public void ASecondNightInTheSameCampMovesNothing()
        {
            var night = ANightAt(4800f, slept: false, elapsed: 40000f, day: 9000);
            var save = CampSave.Build(night, new List<CampSave.Sleeper> { AClimber("don") }, null);

            // the evening stands exactly where it stood: this is what used to put a few minutes before dusk into a file
            // called «ночёвка» and push the good morning save out of the five slots the store keeps
            Assert.AreEqual(40000f, save.Elapsed, 1e-3f, "не спали — часы стоят");
            Assert.AreEqual(Forecast.DateOf(9000), save.Date, "не спали — день не двигается");
            Assert.AreEqual(12f, save.FreshSnowCm, 1e-4f, "не спали — снег вчерашнего дня остаётся");
        }

        [Test]
        public void ASleeperWithNoKeyOfHisOwnIsNotWrittenAtAll()
        {
            var night = ANightAt(4800f, slept: true);
            var save = CampSave.Build(night, new List<CampSave.Sleeper> { AClimber(""), AClimber("don") }, null);

            // «c1» instead of a name was the bug: the night went into the file under the client id, no load ever found it
            // again, and the orphan was then copied into every save after it by the carry-over
            Assert.AreEqual(1, save.Climbers.Count, "безымянный ночевать не должен");
            Assert.AreEqual("don", save.Climbers[0].Key);
        }

        [Test]
        public void AFriendWhoIsNotHereTonightKeepsHisLineAndComesBackThroughTheFile()
        {
            var yesterday = CampSave.Build(ANightAt(4100f, slept: true, day: 8999),
                new List<CampSave.Sleeper> { AClimber("don"), AClimber("kv", "Квадрантул") }, null);

            // tonight only one of them is online
            var tonight = CampSave.Build(ANightAt(4800f, slept: true), new List<CampSave.Sleeper> { AClimber("don") }, yesterday);
            Assert.AreEqual(2, tonight.Climbers.Count, "вчерашний напарник должен остаться в файле");
            Assert.IsNotNull(tonight.Find("kv"));
            Assert.AreEqual("Квадрантул", tonight.Find("kv").Name);

            // and the whole thing survives the round trip to disk and back
            // through the text, so the writer and the parser are both in the loop
            var back = SaveGame.Parse(tonight.Text);
            Assert.IsNotNull(back, "свой же файл должен читаться");
            Assert.AreEqual(tonight.Climbers.Count, back.Climbers.Count);
            Assert.AreEqual(tonight.Date, back.Date);
            Assert.AreEqual(tonight.Elapsed, back.Elapsed, 1e-3f);
            Assert.AreEqual(Place.Elbrus, back.Place);
            var don = back.Find("don");
            Assert.IsNotNull(don);
            Assert.AreEqual(.62f, don.Acclim, 1e-3f);
            Assert.AreEqual(175, don.Roubles);
            Assert.AreEqual(2, don.Pack.Count);
            Assert.AreEqual(ItemId.IceAxe, don.Hand.Id);
        }
    }
}
