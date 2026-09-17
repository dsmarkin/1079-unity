using NUnit.Framework;
using Height1079.Core;

namespace Height1079.Tests
{
    /// <summary>Snow as a map property and the four ways of getting through it. The reference numbers these tests
    /// pin down come from the documents and the literature, not from taste — see the comments in
    /// Assets/Scripts/Core/SnowCover.cs and Skiing.cs for each source.</summary>
    public class SkiTests
    {
        static HeightField Dem => TestData.Dem;

        // the valley forest of the diary (1.2 m of powder), the wind-blown slope, and a made лыжня
        const float ForestDepth = 1.2f, ForestCrust = .18f;
        const float SlopeDepth = .45f, SlopeCrust = .85f;
        const float Pack = 25f;

        [Test]
        public void RidgeIsBlownBareAndTheForestKeepsMetresOfPowder()
        {
            float camp = SnowCover.Depth(Dem, WorldData.Camp.x, WorldData.Camp.z);
            float tent = SnowCover.Depth(Dem, WorldData.Tent.X, WorldData.Tent.Z);
            var top = WorldData.Get("summit");
            float summit = SnowCover.Depth(Dem, top.X, top.Z);

            Assert.Greater(camp, .9f, "diary 30–31.01: снег до 120 см in the Auspiya forest");
            Assert.Less(camp, 1.6f);
            Assert.Less(tent, camp, "the open slope is scoured, the forest is loaded");
            Assert.Greater(tent, .2f); Assert.Less(tent, .7f, "Gaume & Puzrin: a slab 0.5 m at the tent cut");
            Assert.Less(summit, .15f, "diary 31.01 on the ridge: наст, голые места");

            Assert.Less(SnowCover.Crust(Dem, WorldData.Camp.x, WorldData.Camp.z), .3f, "forest powder");
            Assert.Greater(SnowCover.Crust(Dem, top.X, top.Z), .7f, "wind board on the bare top");
            Assert.Greater(SnowCover.Forest(WorldData.Camp.x, WorldData.Camp.z, 641f), .8f);
            Assert.AreEqual(0f, SnowCover.Forest(top.X, top.Z, 1094f), 1e-6, "no canopy above the tree line");
        }

        [Test]
        public void TheStreamRavineIsASnowCollector()
        {
            var r = WorldData.Ravine;
            float ravine = SnowCover.Depth(Dem, r.X, r.Z);
            float aside = SnowCover.Depth(Dem, r.X + 60f, r.Z);
            Assert.Greater(ravine, 2f, "the four lay under 2–2.5 m (search protocols, May 1959)");
            Assert.Less(ravine, 3f);
            Assert.Greater(ravine, aside * 1.8f, "the same forest 60 m away holds half as much");
            Assert.Greater(SnowCover.Hollow(Dem, r.X, r.Z, 40f), 0f, "the ravine is a hollow in the terrain");
        }

        [Test]
        public void BeltsSetTheDepthAndARasterCanReplaceIt()
        {
            Assert.Less(SnowCover.Belt(560f), SnowCover.Belt(640f), "+10–12 cm per 100 m in the taiga");
            Assert.Less(SnowCover.Belt(640f), SnowCover.Belt(700f), "+35 cm per 100 m in the subalpine belt");
            Assert.Less(SnowCover.Belt(900f), SnowCover.Belt(640f), "the wind clears the tundra above the tree line");
            Assert.Less(SnowCover.Belt(1090f), SnowCover.Belt(900f));

            float a = SnowCover.Depth(Dem, 120f, -340f);
            Assert.AreEqual(a, SnowCover.Depth(Dem, 120f, -340f), 1e-6, "the model is deterministic");
            try
            {
                SnowCover.Raster = (x, z) => 0.77f;
                Assert.AreEqual(.77f, SnowCover.Depth(Dem, 120f, -340f), 1e-6, "a measured raster wins");
            }
            finally { SnowCover.Raster = null; }
            Assert.AreEqual(a, SnowCover.Depth(Dem, 120f, -340f), 1e-6, "and the hook lets go again");
        }

        [Test]
        public void PowderSwallowsBootsAndHoldsSkis()
        {
            float foot = Skiing.Sink(Travel.Foot, ForestDepth, ForestCrust, 0f, Pack);
            float poles = Skiing.Sink(Travel.Poles, ForestDepth, ForestCrust, 0f, Pack);
            float skis = Skiing.Sink(Travel.Skis, ForestDepth, ForestCrust, 0f, Pack);
            float haul = Skiing.Sink(Travel.Hauling, ForestDepth, ForestCrust, 0f, Pack);

            Assert.Greater(foot, .3f, "knee-deep and worse on foot");
            Assert.Less(foot, ForestDepth, "you never go through to the ground");
            Assert.Less(poles, foot, "the poles carry a little of you");
            Assert.Less(skis, poles * .5f, "a ski spreads the same man over four times the area");
            Assert.Less(haul, skis, "with the pack on the sled almost nothing of it presses down");

            Assert.Greater(Skiing.PressureKPa(Travel.Foot, Pack), 20f, "a loaded walker makes some 27 kPa");
            Assert.Less(Skiing.PressureKPa(Travel.Skis, Pack), 10f);
            Assert.Greater(Skiing.HoldKPa(SlopeCrust, 0f), 20f, "a wind crust bears a loaded walker");

            Assert.AreEqual("По колено", Skiing.SinkTitle(foot));
            Assert.AreEqual("По поверхности", Skiing.SinkTitle(.01f));
            Assert.AreEqual("По пояс", Skiing.SinkTitle(.8f));
            StringAssert.Contains("волокуш", Skiing.Title(Travel.Hauling));
            StringAssert.Contains("палками", Skiing.Title(Travel.Poles));
        }

        [Test]
        public void WindCrustCarriesAWalker()
        {
            float sink = Skiing.Sink(Travel.Foot, SlopeDepth, SlopeCrust, 0f, Pack);
            Assert.Less(sink, .03f, "you walk on top of it — that is why the footprints of 1–2 Feb were raised columns");
            float v = Skiing.Speed(Travel.Foot, SlopeDepth, SlopeCrust, 0f, 0f, Pack);
            Assert.Greater(v, 1f, "a normal loaded walking pace");
            Assert.Less(Skiing.Effort(Travel.Foot, SlopeDepth, SlopeCrust, 0f, 0f, Pack),
                        Skiing.Effort(Travel.Foot, ForestDepth, ForestCrust, 0f, 0f, Pack) * .25f,
                        "and it costs a quarter of what breaking trail costs");
        }

        [Test]
        public void SkisAreTheOnlyWayThroughDeepSnow()
        {
            float foot = Skiing.Speed(Travel.Foot, ForestDepth, ForestCrust, 0f, 0f, Pack);
            float skis = Skiing.Speed(Travel.Skis, ForestDepth, ForestCrust, 0f, 0f, Pack);
            Assert.Greater(foot, .3f); Assert.Less(foot, .55f, "тропёжка is 0.3–0.5 m/s and no more");
            Assert.Greater(skis, .9f); Assert.Less(skis, 1.7f, "1.1–1.6 m/s on virgin snow");
            Assert.Greater(skis, foot * 2f, "putting the skis on more than doubles the pace");
            Assert.Less(Skiing.Effort(Travel.Skis, ForestDepth, ForestCrust, 0f, 0f, Pack),
                        Skiing.Effort(Travel.Foot, ForestDepth, ForestCrust, 0f, 0f, Pack) * .5f,
                        "and costs less than half as much");
            Assert.AreEqual(Travel.Skis, Skiing.Best(ForestDepth, ForestCrust, 0f, 0f, Pack),
                "deep snow with no track: the skis go on");
        }

        [Test]
        public void PolesBuyATenthOfThePaceAndFewerFalls()
        {
            float foot = Skiing.Speed(Travel.Foot, ForestDepth, ForestCrust, 0f, 0f, Pack);
            float poles = Skiing.Speed(Travel.Poles, ForestDepth, ForestCrust, 0f, 0f, Pack);
            Assert.Greater(poles / foot, 1.08f); Assert.Less(poles / foot, 1.2f, "+10–15 % in deep snow");

            float hard = Skiing.Speed(Travel.Poles, SlopeDepth, SlopeCrust, 0f, 0f, Pack)
                       / Skiing.Speed(Travel.Foot, SlopeDepth, SlopeCrust, 0f, 0f, Pack);
            Assert.Less(hard, 1.05f, "on a crust there is nothing for them to hold you out of");

            Assert.Less(Skiing.Stumble(Travel.Poles, ForestDepth, ForestCrust, 0f, 0f, Pack),
                        Skiing.Stumble(Travel.Foot, ForestDepth, ForestCrust, 0f, 0f, Pack) * .6f,
                        "three points of support, fewer falls");
            Assert.Less(Skiing.Speed(Travel.Poles, ForestDepth, ForestCrust, 0f, 0f, Pack, 0f),
                        poles * .95f, "hands you cannot close cannot work the poles");
        }

        [Test]
        public void AFinishedTrackBeatsVirginSnowForEveryone()
        {
            foreach (Travel mode in new[] { Travel.Foot, Travel.Poles, Travel.Skis, Travel.Hauling })
                Assert.Greater(Skiing.Speed(mode, ForestDepth, ForestCrust, 1f, 0f, Pack),
                               Skiing.Speed(mode, ForestDepth, ForestCrust, 0f, 0f, Pack),
                               "the лыжня helps " + Skiing.Title(mode));

            float track = Skiing.Speed(Travel.Skis, ForestDepth, ForestCrust, 1f, 0f, Pack);
            Assert.Greater(track, 1.8f); Assert.Less(track, 2.6f, "up to 2.5 m/s on a made track");

            float following = Skiing.EffortPerMetre(Travel.Foot, ForestDepth, ForestCrust, 1f, 0f, Pack);
            float breaking = Skiing.EffortPerMetre(Travel.Foot, ForestDepth, ForestCrust, 0f, 0f, Pack);
            Assert.Less(following, breaking / 3f, "backlog E1.2: the one who follows spends at most a third");
        }

        [Test]
        public void TheTrackHardensOnTheSecondPassAndIsSofterAtItsEdges()
        {
            var t = new SkiTrack();
            t.Stamp(0f, 0f, 0d, Travel.Skis);
            float once = t.Packed(0f, 0f, 0d);
            Assert.Greater(once, .4f); Assert.Less(once, 1f, "one pass does not make a road");

            t.Stamp(0f, 0f, 1d, Travel.Skis);
            float twice = t.Packed(0f, 0f, 1d);
            Assert.Greater(twice, once, "the second pass presses it harder");
            Assert.Less(twice, 1f, "with diminishing returns");
            Assert.AreEqual(1, t.Count, "both passes live in the same one-metre cell");

            Assert.AreEqual(twice, t.Packed(0f, .25f, 1d), 1e-6, "inside the pressed strip it is all the same");
            float edge = t.Packed(0f, Skiing.TrackWidth * .5f + Skiing.Feather * .5f, 1d);
            Assert.Greater(edge, 0f); Assert.Less(edge, twice, "the edges are softer");
            Assert.AreEqual(0f, t.Packed(0f, Skiing.TrackWidth * .5f + Skiing.Feather + .01f, 1d), 1e-6,
                "and a step off the track is virgin snow again");

            t.Stamp(3f, 0f, 2d, Travel.Skis);
            Assert.AreEqual(2, t.Count, "a print three metres on is its own cell");
            t.Clear();
            Assert.AreEqual(0, t.Count);
        }

        [Test]
        public void WindAndSnowfallSweepTheTrackAway()
        {
            Assert.AreEqual(Skiing.CalmLife, Skiing.DriftSeconds(0f, 0f), 1e-3, "still air: a track outlives the night");
            Assert.Less(Skiing.DriftSeconds(12f, 0f), Skiing.DriftSeconds(7f, 0f), "wind fills it in");
            Assert.Less(Skiing.DriftSeconds(4f, .8f), Skiing.DriftSeconds(4f, 0f), "so does falling snow");
            float storm = Skiing.DriftSeconds(18f, 1f);
            Assert.Greater(storm, 0f); Assert.Less(storm, 60f, "a пурга wipes it within a blizzard front");

            var t = new SkiTrack();
            t.Life = 100f;
            t.Stamp(0f, 0f, 0d, Travel.Skis);
            float fresh = t.Packed(0f, 0f, 0d);
            float half = t.Packed(0f, 0f, 50d);
            Assert.Less(half, fresh, "it fades");
            Assert.Greater(half, 0f);
            Assert.AreEqual(0f, t.Packed(0f, 0f, 101d), 1e-6, "and is gone by the end of its life");
            Assert.AreEqual(1, t.Count, "but the cell is still held");
            t.Forget(101d);
            Assert.AreEqual(0, t.Count, "until Forget throws the drifted prints out");
        }

        [Test]
        public void HaulingSpendsLessOnFirmSnowAndMoreInTheForest()
        {
            float haulHard = Skiing.Effort(Travel.Hauling, SlopeDepth, SlopeCrust, 0f, 0f, Pack);
            float skisHard = Skiing.Effort(Travel.Skis, SlopeDepth, SlopeCrust, 0f, 0f, Pack);
            Assert.Less(haulHard, skisHard * .8f, "the pack is off the shoulders: on a crust the sled is cheap");
            float vHaul = Skiing.Speed(Travel.Hauling, SlopeDepth, SlopeCrust, 0f, 0f, Pack);
            float vSkis = Skiing.Speed(Travel.Skis, SlopeDepth, SlopeCrust, 0f, 0f, Pack);
            Assert.Less(vHaul, vSkis, "and a little slower for the friction behind");
            Assert.Greater(vHaul, vSkis * .8f, "only a little");

            Assert.Greater(Skiing.EffortPerMetre(Travel.Hauling, ForestDepth, ForestCrust, 0f, 0f, Pack),
                           Skiing.EffortPerMetre(Travel.Skis, ForestDepth, ForestCrust, 0f, 0f, Pack),
                           "in loose snow the sled digs in and costs more per metre");
            Assert.Greater(Skiing.Effort(Travel.Hauling, SlopeDepth, SlopeCrust, 0f, 25f, Pack),
                           Skiing.Effort(Travel.Skis, SlopeDepth, SlopeCrust, 0f, 25f, Pack),
                           "and on a steep climb the rope pulls back");

            // and the warmth: wading wets you, the sled keeps your back dry
            Assert.AreEqual(1f, SurvivalRules.TravelHeatFactor(Travel.Foot, 0f), 1e-6, "nothing sinking costs nothing");
            Assert.Greater(SurvivalRules.TravelHeatFactor(Travel.Foot, .5f), SurvivalRules.TravelHeatFactor(Travel.Skis, .5f));
            Assert.Greater(SurvivalRules.TravelHeatFactor(Travel.Skis, .5f), SurvivalRules.TravelHeatFactor(Travel.Hauling, .5f));

            var wading = new Participant();
            var riding = new Participant();
            SurvivalRules.Tick(wading, 60f, new SurvivalRules.Conditions { Moving = true, Mode = Travel.Foot, Sink = .6f });
            SurvivalRules.Tick(riding, 60f, new SurvivalRules.Conditions { Moving = true, Mode = Travel.Hauling, Sink = .6f });
            Assert.Less(wading.Heat, riding.Heat, "тропёжка by the knees costs warmth");

            var plain = new Participant();
            var explicitFoot = new Participant();
            SurvivalRules.Tick(plain, 60f, new SurvivalRules.Conditions { Moving = true });
            SurvivalRules.Tick(explicitFoot, 60f, new SurvivalRules.Conditions { Moving = true, Mode = Travel.Foot, Sink = 0f });
            Assert.AreEqual(explicitFoot.Heat, plain.Heat, 1e-6, "conditions that say nothing about snow are unchanged");
        }

        [Test]
        public void DownhillRunsAndACrustedClimbIsWalked()
        {
            float flat = Skiing.Speed(Travel.Skis, SlopeDepth, SlopeCrust, 0f, 0f, Pack);
            float down = Skiing.Speed(Travel.Skis, SlopeDepth, SlopeCrust, 0f, -15f, Pack);
            float up = Skiing.Speed(Travel.Skis, SlopeDepth, SlopeCrust, 0f, 15f, Pack);
            Assert.Greater(down, flat * 1.5f, "skis run on the descent");
            Assert.Less(down, Skiing.MaxSpeed);
            Assert.Less(up, flat * .5f, "and lose the climb");

            Assert.Greater(Skiing.Speed(Travel.Foot, SlopeDepth, SlopeCrust, 0f, 15f, Pack), up,
                "a wooden ski slides back down a hard crust: the steep wind-blown climb is walked");
            Assert.Greater(Skiing.Speed(Travel.Skis, ForestDepth, ForestCrust, 0f, 15f, Pack),
                           Skiing.Speed(Travel.Foot, ForestDepth, ForestCrust, 0f, 15f, Pack),
                "in powder the ski bites and still wins");

            Assert.AreEqual(1f, Skiing.Tobler(0f), 1e-4, "Tobler is normalised to the flat");
            Assert.Greater(Skiing.Tobler(-.05f), 1f, "a walker is quickest on a slight downhill");
            Assert.Less(Skiing.Tobler(.4f), .3f);
            Assert.Greater(Skiing.Stumble(Travel.Skis, SlopeDepth, SlopeCrust, 0f, -25f, Pack),
                           Skiing.Stumble(Travel.Skis, SlopeDepth, SlopeCrust, 0f, 0f, Pack),
                "a crust on a steep slope is where you go down");
        }
    }
}
