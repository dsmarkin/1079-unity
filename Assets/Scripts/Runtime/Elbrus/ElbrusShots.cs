using System;
using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;
using static Height1079.Runtime.DemoReel;

namespace Height1079.Runtime
{
    /// <summary>The four shots of the southern slope in the demo reel: the Azau meadow, a gondola cabin passing, the
    /// platform at Mir and the barrels of Gara-Bashi. They live here and not in <see cref="DemoReel"/> because they
    /// name the map — the ropeways, the stations, the summit — and the reel must go on being filmed in a build that
    /// was made without it (docs/ELBRUS.md).</summary>
    /// <remarks>The namespace stays <c>Height1079.Runtime</c> on purpose: a namespace segment named Elbrus would
    /// beat the class <see cref="Elbrus"/> at every call site in this folder (CLAUDE.md §4).</remarks>
    static class ElbrusShots
    {
        /// <summary>Called by <see cref="DemoReel"/> while it assembles its list.</summary>
        public static void Add(List<Shot> shots)
        {
            var azau = Elbrus.Azau; var mir = Elbrus.Mir; var top = Elbrus.WestSummit;
            var barrels = Elbrus.Barrels;
            var summit = new Vector3(top.X, 0f, top.Z);

            // 1 — Azau, 2 350 m: standing in the July meadow where the game drops the visitor, walking towards the station
            var spawn = Elbrus.Start;
            shots.Add(new Shot
            {
                Caption = "Поляна Азау, 2350 м · июльский день",
                Place = Place.Elbrus, Seconds = 3.6f,
                Fly = t =>
                {
                    var walk = new Vector3(azau.X - spawn.x, 0f, azau.Z - spawn.z).normalized;
                    var from = new Vector3(spawn.x, 0f, spawn.z) - walk * 4f;
                    var on = from + walk * (8f * Ease(t));
                    var eye = Over(on.x, on.z, Eye);
                    // ahead along the path, lifting towards the summit as the station opens up
                    var ahead = on + walk * 30f;
                    var look = Vector3.Lerp(Over(ahead.x, ahead.z, 3f), summit + Vector3.up * (Ground(top.X, top.Z) + 30f), Ease(t) * .45f);
                    return (eye, look);
                },
            });

            // 2 — a gondola cabin passing close by: the glazing, the grips, the rope and the valley falling away under it
            RopewayRig gondola = null; int car = -1;
            shots.Add(new Shot
            {
                Caption = "Канатная дорога работает · шесть очередей",
                Place = Place.Elbrus, Seconds = 3.6f, Fov = 50f,
                Prepare = () =>
                {
                    gondola = null; car = -1;
                    foreach (var r in ElbrusWorld.Lines)
                        if (r != null && r.Spec.Id == "gondola1") gondola = r;
                    if (gondola == null && ElbrusWorld.Lines.Count > 0) gondola = ElbrusWorld.Lines[0];
                    if (gondola == null) return;
                    float best = float.MaxValue;
                    for (int i = 0; i < gondola.Line.Cars; i++)
                    {
                        var c = gondola.Line.CarAt(i, RopewayRig.Clock);
                        if (!c.Up) continue;
                        float d = Mathf.Abs(c.S / gondola.Line.Length - .72f);
                        if (d < best) { best = d; car = i; }
                    }
                },
                Each = _ => RopewayRig.Clock += Time.deltaTime * 2.2,     // on top of the line's own pace
                Fly = t =>
                {
                    var cabin = gondola != null && car >= 0 ? gondola.Car(car) : null;
                    if (cabin == null) return (Over(azau.X, azau.Z - 60f, 30f), Over(azau.X, azau.Z, 4f));
                    var p = cabin.position;
                    // close alongside, drifting in and dropping to the cabin's own level as it climbs
                    var side = Vector3.Cross(Vector3.up, cabin.forward).normalized;
                    // the transform sits at the grip; the cabin hangs a good four metres under it, so the shot has to be
                    // aimed at the body and flown at the body's own height — above the canopy, clear of the branches
                    var body = p - Vector3.up * (Ropeway.Drop(gondola.Spec.Kind) - Ropeway.Hang(gondola.Spec.Kind)) * .6f;
                    var eye = body + side * Mathf.Lerp(7.5f, 5f, Ease(t))
                                   - cabin.forward * Mathf.Lerp(3.4f, .6f, Ease(t))
                                   + Vector3.up * Mathf.Lerp(1.6f, .2f, Ease(t));
                    return (eye, body);
                },
            });

            // 3 — Mir, 3 500 m: on the platform in front of the hall, the cafes and the stalls sliding past
            Transform mirHall = null;
            shots.Add(new Shot
            {
                Caption = "Станция «Мир», 3500 м · кафе, музей, ратраки",
                Place = Place.Elbrus, Seconds = 3.4f,
                Prepare = () => mirHall = Nearest("Elb_Terminal_Mir", new Vector3(mir.X, 0f, mir.Z), 260f),
                Fly = t =>
                {
                    var c = mirHall != null ? mirHall.position : Over(mir.X, mir.Z, 0f);
                    var down = Downhill(c, summit);
                    var side = Vector3.Cross(Vector3.up, down).normalized;
                    var on = c + down * Mathf.Lerp(34f, 27f, Ease(t)) + side * Mathf.Lerp(26f, -16f, Ease(t));
                    // the station stands on a pad: an eye on the snow below its edge sees only the plinth
                    float y = Mathf.Max(Ground(on.x, on.z) + Eye, c.y + 1.5f);
                    return (new Vector3(on.x, y, on.z), c + Vector3.up * 6f);
                },
            });

            // 4 — Gara-Bashi, 3 847 m: between the barrels, the summit opening up over the roof of one
            Transform barrel = null;
            shots.Add(new Shot
            {
                Caption = "Гара-Баши, 3847 м · бочки, приюты, ратраки",
                Place = Place.Elbrus, Seconds = 3.4f,
                Prepare = () => barrel = Nearest("Elb_Barrel", new Vector3(barrels.X, 0f, barrels.Z), 300f),
                Fly = t =>
                {
                    var c = barrel != null ? barrel.position : Over(barrels.X, barrels.Z, 0f);
                    var down = Downhill(c, summit);
                    var side = Vector3.Cross(Vector3.up, down).normalized;
                    var on = c + down * Mathf.Lerp(23f, 17f, Ease(t)) + side * Mathf.Lerp(-15f, 7f, Ease(t));
                    var look = Vector3.Lerp(c + Vector3.up * 1.6f,
                        new Vector3(top.X, Ground(top.X, top.Z) + 30f, top.Z), Ease(t) * .5f);
                    return (Over(on.x, on.z, Eye), look);
                },
            });
        }
    }
}
