using System;
using System.Linq;
using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    public class CampaignTests
    {
        [Test]
        public void RosterHasTenDocumentedMembersAndOneWhoLeft()
        {
            Assert.AreEqual(10, Campaign.Roster.Count);
            Assert.AreEqual(10, Campaign.Roster.Select(m => m.Id).Distinct().Count());
            Assert.AreEqual(1, Campaign.Roster.Count(m => m.LeftAtSecondNorthern));
            Assert.AreEqual("yudin", Campaign.Roster.Single(m => m.LeftAtSecondNorthern).Id);
            Assert.IsTrue(Campaign.Roster.All(m => m.Age >= 20 && m.Age <= 37 && m.Traits.Length > 0));
        }

        [Test]
        public void MissionsFollowTheDocumentedChronologyAndEveryMemberLeadsOnce()
        {
            var missions = Campaign.Missions;
            Assert.AreEqual(8, missions.Count);
            for (int i = 1; i < missions.Count; i++)
            {
                Assert.AreEqual(i + 1, missions[i].Number);
                Assert.IsTrue(missions[i].Date >= missions[i - 1].Date, missions[i].Id);
                Assert.IsTrue(missions[i].DateEnd >= missions[i].Date, missions[i].Id);
            }
            Assert.AreEqual(new DateTime(1959, 1, 23), missions.First().Date);
            Assert.AreEqual(new DateTime(1959, 2, 1), missions.Last().Date);
            var leads = missions.SelectMany(m => m.Leads).ToList();
            foreach (var id in leads) Assert.DoesNotThrow(() => Campaign.Find(id));
            foreach (var m in Campaign.Roster) Assert.IsTrue(leads.Contains(m.Id), m.Id + " never leads a mission");
            // Yudin leads only before he leaves; nobody leads after the tent is pitched.
            Assert.IsTrue(missions.Where(m => m.Leads.Contains("yudin")).All(m => m.DateEnd <= new DateTime(1959, 1, 28)));
        }
    }
}
