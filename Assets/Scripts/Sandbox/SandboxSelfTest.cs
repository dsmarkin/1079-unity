using System.Collections;
using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>`1079-sandbox -selftest` — drives the body through the stands by script, writes what happened to the
    /// player log and quits. Nobody can watch a slope being walked in a batch build, and "it starts without
    /// exceptions" is not a test of physics.
    ///
    /// Only the legs are checked: walking, steps, grades, sliding off something too steep, falling and getting up.
    /// The hands are switched off — climbing is not worth testing until a man can walk.</summary>
    public sealed class SandboxSelfTest : MonoBehaviour
    {
        public static bool Requested
        {
            get
            {
                foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-selftest") return true;
                return false;
            }
        }

        int failed;

        void Check(bool ok, string what, string detail)
        {
            if (!ok) failed++;
            Debug.Log($"selftest {(ok ? "ok  " : "FAIL")} · {what} · {detail}");
        }

        IEnumerator Start()
        {
            var boot = SandboxBoot.Instance;
            SandboxHud.Panel = false;
            boot.Scripted = true;
            yield return null;
            var body = boot.Body;
            var t = boot.Tuning;

            // ── 1. standing on flat ground ─────────────────────────────────────────────────────────────────────────
            boot.GoTo(0);
            yield return new WaitForSeconds(1.5f);
            float y0 = body.Torso.position.y;
            var p0 = body.Torso.position;
            yield return new WaitForSeconds(1.5f);
            float drift = Flat(body.Torso.position - p0).magnitude;
            Check(body.Grounded, "стоит на опоре", $"опора {body.GroundDistance:0.00} м, уклон {body.SlopeAngle:0}°");
            Check(body.Tilt < 4f, "в покое стоит прямо", $"корпус отклонён на {body.Tilt:0.0}° от вертикали");
            Check(Mathf.Abs(body.Torso.position.y - y0) < .08f, "не проваливается и не подпрыгивает", $"Δy {body.Torso.position.y - y0:0.000} м");
            Check(drift < .15f, "не ползёт стоя", $"снос {drift:0.000} м за 1,5 с");

            // ── 2. walking ─────────────────────────────────────────────────────────────────────────────────────────
            var from = body.Torso.position;
            yield return Drive(body, new Vector2(0, 1), 3f);
            float walked = Flat(body.Torso.position - from).magnitude;
            float pace = walked / 3f;
            Check(pace > t.WalkSpeed * .8f && pace < t.WalkSpeed * 1.15f, "идёт с заданной скоростью",
                $"{pace:0.00} м/с при шаге {t.WalkSpeed:0.00} м/с");

            // ── 3. steps ───────────────────────────────────────────────────────────────────────────────────────────
            boot.GoTo(1);
            yield return new WaitForSeconds(1f);
            float beforeY = body.Torso.position.y;
            var stepFrom = body.Torso.position;
            // the highest it got, not where it ended up: four steps of 0.2 m are climbed in two seconds and walked
            // off the far end in the next two, and measuring the finish scored that as having climbed nothing
            float topOfSteps = beforeY;
            for (float w = 0f; w < 4f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Move = new Vector2(0, 1), Look = Quaternion.identity });
                topOfSteps = Mathf.Max(topOfSteps, body.Torso.position.y);
                yield return null;
            }
            body.Drive(PuppetInput.Idle);
            Check(topOfSteps - beforeY > .35f, "поднимается по ступеням 0,2 м",
                $"поднялся на {topOfSteps - beforeY:0.00} м, прошёл {Flat(body.Torso.position - stepFrom).magnitude:0.0} м");

            // ── 4. walking up a thirty-degree slope: possible, and slower than flat ────────────────────────────────
            var ramp30 = SandboxRange.Ramps[1];
            body.Place(ramp30.Foot);
            yield return new WaitForSeconds(1f);
            var upFrom = body.Torso.position;
            yield return Drive(body, new Vector2(0, 1), 3.5f);
            float gained = body.Torso.position.y - upFrom.y;
            float upPace = Flat(body.Torso.position - upFrom).magnitude / 3.5f;
            Check(gained > .8f, "поднимается по склону 30°", $"набрал {gained:0.00} м за 3,5 с");
            Check(upPace < pace * .95f, "в гору медленнее, чем по ровному", $"{upPace:0.00} против {pace:0.00} м/с");

            // ── 5. the last slope the boots hold ───────────────────────────────────────────────────────────────────
            var ramp46 = SandboxRange.Ramps[3];
            body.Place(ramp46.Middle);
            yield return new WaitForSeconds(2f);
            var heldAt = body.Torso.position;
            yield return new WaitForSeconds(1.5f);
            float slipped = (heldAt - body.Torso.position).y;
            Check(body.Grounded && !body.Sliding, "держится на 46° (предел 48°)",
                $"уклон {body.SlopeAngle:0}°, скольжение: {body.Sliding}");
            Check(slipped < .2f, "не сползает стоя на пределе", $"сполз {slipped:0.00} м за 1,5 с");

            // ── 6. too steep: slides down the fall line, at a speed that has a limit ───────────────────────────────
            var ramp60 = SandboxRange.Ramps[5];
            body.Place(ramp60.Middle);
            // measured from the moment it is put down: waiting first meant the body had already reached the bottom
            var slideFrom = body.Torso.position;
            float top = 0f, topDown = 0f;
            bool keptTouch = true, sawSlide = false;
            float slidFor = 0f;
            for (float w = 0f; w < 2.5f; w += Time.deltaTime)
            {
                body.Drive(PuppetInput.Idle);
                if (body.Sliding) { sawSlide = true; slidFor += Time.deltaTime; }
                if (body.Sliding && !body.Footing) keptTouch = false;
                top = Mathf.Max(top, Flat(body.Torso.linearVelocity).magnitude);
                topDown = Mathf.Max(topDown, -body.Torso.linearVelocity.y);
                yield return null;
            }
            float dropped = slideFrom.y - body.Torso.position.y;
            Check(sawSlide, "на 60° тело соскальзывает", $"скользило {slidFor:0.0} с из 2,5");
            // the ramp is only nine metres long, so the body reaches the bottom and stops: what matters is that it
            // went, and that it built real speed doing it
            Check(dropped > 1.5f && topDown > 3f, "соскальзывание уносит вниз по склону",
                $"спустился на {dropped:0.0} м за 2,5 с, вниз до {topDown:0.0} м/с");
            Check(top <= t.SlideTop + .5f, "скорость скольжения ограничена", $"{top:0.0} при пределе {t.SlideTop:0.0} м/с (по горизонтали)");
            Check(keptTouch, "при скольжении тело держится склона, а не летит", keptTouch ? "касание сохранялось" : "отрывалось от склона");

            // ── 7. a fall over open ground ─────────────────────────────────────────────────────────────────────────
            body.Place(new Vector3(45f, 18f, -16f));
            float startY = body.Torso.position.y;
            bool fell = false;
            for (float w = 0f; w < 6f; w += Time.deltaTime)
            {
                body.Drive(PuppetInput.Idle);
                if (!body.Footing) fell = true;
                if (fell && body.Grounded) break;
                yield return null;
            }
            float impact = body.LastImpact, landedAt = body.Torso.position.y;
            Check(impact > 14f, "падение с 17 м засчитано целиком", $"удар {impact:0.0} м/с, с {startY:0.0} до {landedAt:0.0} м");
            // the same four seconds now answer three questions at once: does the body get thrown over, does it get up,
            // and does getting up take time. A fall the player cannot see is the whole complaint being fixed here.
            float bounce = 0f, tilted = 0f, stoodAt = -1f;
            for (float w = 0f; w < 4f; w += Time.deltaTime)
            {
                bounce = Mathf.Max(bounce, body.Torso.position.y - landedAt);
                tilted = Mathf.Max(tilted, body.Tilt);
                if (stoodAt < 0f && tilted > 50f && body.Tilt < 20f) stoodAt = w;
                yield return null;
            }
            Check(bounce < 1.6f, "ноги не подбрасывают тело после падения", $"подъём {bounce:0.00} м");
            Check(!body.Limp, "встаёт после падения", $"обмякшее тело: {body.Limp}");
            Check(tilted > 50f, "падение валит тело на бок", $"корпус уходил на {tilted:0}° от вертикали");
            Check(stoodAt > .8f, "встаёт не рывком",
                stoodAt < 0f ? "так и не выпрямился за 4 с" : $"выпрямился через {stoodAt:0.0} с после удара");

            // ── 8. a fall a man walks away from: the knees give and unfold again ──────────────────────────────────
            // two metres, which is over the stagger threshold and under the knock-down one: the body must keep its
            // feet, sink on them and come back up. Before the squash layer it simply arrived and stood there.
            // The drop is measured from the feet, so the height is the ride height plus the two metres: the body was
            // made shorter and dropping it from the old mark would quietly have been a longer fall each time.
            body.Place(new Vector3(0f, t.HoverHeight + 2.05f, -10f));
            float knees = 0f, deepestRide = float.PositiveInfinity, foldedFor = 0f;
            bool touched = false;
            for (float w = 0f; w < 2.5f; w += Time.deltaTime)
            {
                body.Drive(PuppetInput.Idle);
                if (body.Grounded) { touched = true; deepestRide = Mathf.Min(deepestRide, body.GroundDistance); }
                knees = Mathf.Max(knees, body.Crouch);
                if (body.Crouch > .02f) foldedFor += Time.deltaTime;
                yield return null;
            }
            Check(touched && knees > .05f, "удар сажает тело на ноги", $"колени подались на {knees:0.00} м при ударе {body.LastImpact:0.0} м/с");
            Check(foldedFor > .15f, "приседание разгибается не мгновенно", $"держалось {foldedFor:0.00} с");
            Check(deepestRide < t.HoverHeight - .08f, "тело при этом действительно просело",
                $"опора падала до {deepestRide:0.00} м при росте {t.HoverHeight:0.00} м");
            Check(!body.Limp && Mathf.Abs(body.GroundDistance - t.HoverHeight) < .12f, "и снова встаёт в рост",
                $"опора {body.GroundDistance:0.00} м");

            // ── 9. the torso has weight: it leans into the start and hangs back on the stop ───────────────────────
            body.Place(new Vector3(0f, 1.2f, -10f));
            yield return new WaitForSeconds(1f);
            float intoStart = 0f;
            for (float w = 0f; w < .8f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Move = new Vector2(0f, 1f), Look = Quaternion.identity });
                intoStart = Mathf.Max(intoStart, Lean(body, Vector3.forward));
                yield return null;
            }
            yield return Drive(body, new Vector2(0, 1), 1.2f);      // let the lean settle back at a steady pace
            float backOnStop = 0f;
            for (float w = 0f; w < .8f; w += Time.deltaTime)
            {
                body.Drive(PuppetInput.Idle);
                backOnStop = Mathf.Min(backOnStop, Lean(body, Vector3.forward));
                yield return null;
            }
            Check(intoStart > 3f, "корпус заваливается вперёд на разгоне", $"{intoStart:0.0}° вперёд");
            Check(backOnStop < -2f, "и откидывается назад на остановке", $"{backOnStop:0.0}° назад");

            // ── 10. the jump ───────────────────────────────────────────────────────────────────────────────────────
            // Two separate things used to eat it, and both are checked here. The press was read once a frame and the
            // body steps ninety times a second, so a frame with no step in it threw the press away — hence exactly
            // one frame of the key below, and the latch inside the body has to carry it. And the leg spring, which
            // still counts the body as standing for the first quarter metre of the rise, met the launch with its full
            // downward ceiling: the body lifted about fifteen centimetres and was pulled straight back down.
            body.Place(new Vector3(0f, 1.2f, -10f));
            yield return new WaitForSeconds(1.2f);
            float footY = body.Torso.position.y;
            body.Drive(new PuppetInput { Look = Quaternion.identity, Jump = true });
            yield return null;
            float apex = footY;
            bool leftGround = false;
            for (float w = 0f; w < 1.5f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Look = Quaternion.identity });
                apex = Mathf.Max(apex, body.Torso.position.y);
                if (!body.Footing) leftGround = true;
                yield return null;
            }
            body.Drive(PuppetInput.Idle);
            float lifted = apex - footY;
            Check(leftGround, "прыжок отрывает тело от земли", leftGround ? "щуп терял опору" : "ноги не оторвались");
            Check(lifted > t.JumpHeight * .7f && lifted < t.JumpHeight * 1.6f, "прыжок поднимает на заданную высоту",
                $"поднялся на {lifted:0.00} м при заданных {t.JumpHeight:0.00} м");
            Check(body.Grounded && !body.Limp, "после прыжка снова на ногах", $"опора {body.GroundDistance:0.00} м");

            // ── 11. the body faces the camera, never the sticks ────────────────────────────────────────────────────
            // A man asked to walk left keeps looking where the player looks and goes sideways; he does not swing round
            // to face his own feet. That is a fact about the physics and not about the drawing, so it can be measured:
            // hold the look still, press left, and the heading must stay put while the body travels left.
            body.Place(new Vector3(0f, 1.2f, -10f));
            yield return new WaitForSeconds(1f);
            float heading = body.FacingYaw;
            float swung = 0f, sideDrift = 0f, aheadDrift = 0f;
            var sideFrom = body.Torso.position;
            for (float w = 0f; w < 2f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Move = new Vector2(-1f, 0f), Look = Quaternion.identity });
                swung = Mathf.Max(swung, Mathf.Abs(Mathf.DeltaAngle(heading, body.FacingYaw)));
                sideDrift = Mathf.Min(sideDrift, body.Drift.x);
                aheadDrift = Mathf.Max(aheadDrift, Mathf.Abs(body.Drift.y));
                yield return null;
            }
            body.Drive(PuppetInput.Idle);
            var went = Flat(body.Torso.position - sideFrom);
            Check(swung < 10f, "идёт вбок, не разворачиваясь по направлению шага",
                $"корпус ушёл от взгляда на {swung:0}°");
            Check(went.magnitude > 2f && Vector3.Dot(went.normalized, Vector3.left) > .9f, "приставной шаг уносит влево",
                $"прошёл {went.magnitude:0.0} м, отклонение от «влево» {Vector3.Angle(went, Vector3.left):0}°");
            Check(sideDrift < -t.WalkSpeed * .6f && aheadDrift < t.WalkSpeed * .35f, "снос читается как боковой",
                $"Drift вбок {sideDrift:0.0}, вперёд не больше {aheadDrift:0.0} м/с");

            // ── 12. the stature both halves of the body are cut to ─────────────────────────────────────────────────
            // The figure's bones and the physics ride height are one number seen twice (PuppetTuning.StandHeight), and
            // they live in different files. This is the check that says they still agree — measured off the renderers,
            // so it is the height a player sees rather than a constant reading itself back.
            // The tolerance is not slack: the body stands on a spring, and holding its own weight costs it about seven
            // centimetres of ride height (mass g over LegSpring). The knees take that, so a standing man measures that
            // much under the height his bones were cut to, and the check must not call that a mismatch.
            boot.FirstPerson = false;      // your own figure is kept out of your own eye; it must be drawn to be measured
            body.Place(new Vector3(0f, 1.2f, -10f));
            yield return new WaitForSeconds(1.5f);
            var drawn = FigureBounds(body);
            float floorY = body.Torso.position.y - body.GroundDistance;
            Check(drawn.HasValue, "фигура нарисована", drawn.HasValue ? "меши на месте" : "ни одного рендерера");
            if (drawn.HasValue)
            {
                Check(Mathf.Abs(drawn.Value.size.y - t.StandHeight) < .12f, "рост тела соответствует заданному",
                    $"{drawn.Value.size.y:0.00} м при заданных {t.StandHeight:0.00} м");
                Check(Mathf.Abs(drawn.Value.min.y - floorY) < .15f, "подошвы стоят на опоре, а не висят над ней",
                    $"низ фигуры на {drawn.Value.min.y - floorY:0.00} м от опоры");
            }

            Debug.Log($"selftest: итог — провалов {failed}");
            yield return new WaitForSeconds(.5f);
            Application.Quit(failed == 0 ? 0 : 1);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>The world box every drawn part of the climber fits in. Taken off the renderers and not off the
        /// bone constants, because the question being asked is how tall the man on the screen is — and the meshes are
        /// turned in another file, from numbers this one is not allowed to read.</summary>
        static Bounds? FigureBounds(Puppet.Puppet body)
        {
            var parts = body.GetComponentsInChildren<Renderer>();
            Bounds box = default;
            bool any = false;
            foreach (var r in parts)
            {
                if (r == null) continue;
                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }
            return any ? box : (Bounds?)null;
        }

        /// <summary>How far the torso is leaning toward a direction, degrees, signed: positive is leaning into it,
        /// negative is hanging back from it. Read off the real torso attitude — the point of the lean being physics
        /// and not decoration is that a script can measure it.</summary>
        static float Lean(Puppet.Puppet body, Vector3 towards)
            => Mathf.Asin(Mathf.Clamp(Vector3.Dot(body.Torso.transform.up, towards.normalized), -1f, 1f)) * Mathf.Rad2Deg;

        static IEnumerator Drive(Puppet.Puppet body, Vector2 move, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                body.Drive(new PuppetInput { Move = move, Look = Quaternion.identity });
                t += Time.deltaTime;
                yield return null;
            }
            body.Drive(PuppetInput.Idle);
        }
    }
}
