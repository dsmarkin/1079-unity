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

            // ── 10a. a tired man still jumps ───────────────────────────────────────────────────────────────────────
            // The bar bitten down to three of a hundred: a run is still allowed (it holds down to one) and the jump
            // used to cost eight outright, so this was the body that could run and could not jump — a space bar
            // that did nothing. Now it jumps what it can pay for, which is lower.
            body.Ceiling = .03f;
            yield return new WaitForSeconds(.6f);          // the clamp brings the bar down to the ceiling
            float tiredY = body.Torso.position.y;
            float tiredStrength = body.Strength;
            body.Drive(new PuppetInput { Look = Quaternion.identity, Jump = true });
            yield return null;
            float tiredApex = tiredY;
            for (float w = 0f; w < 1.5f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Look = Quaternion.identity });
                tiredApex = Mathf.Max(tiredApex, body.Torso.position.y);
                yield return null;
            }
            body.Drive(PuppetInput.Idle);
            body.Ceiling = 1f;
            float tiredLift = tiredApex - tiredY;
            Check(tiredStrength < t.JumpCost, "полоска откушена ниже цены прыжка", $"сил {tiredStrength:0.0} при цене {t.JumpCost:0}");
            Check(tiredLift > t.JumpHeight * .35f, "уставшее тело всё равно прыгает", $"поднялось на {tiredLift:0.00} м");
            Check(tiredLift < lifted, "но ниже, чем со свежими силами", $"{tiredLift:0.00} против {lifted:0.00} м");
            yield return new WaitForSeconds(1.2f);

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

            // ── 11b. from outside, the camera goes round a standing body; the first step turns it ─────────────────
            // Third person (FreeLook): a man who is doing nothing is not turned by the camera going round him, and
            // his first step turns him to where it looks and takes him off that way. First person never asks for
            // this, so the body under the eye turns with the look standing still, as it always did.
            body.Place(new Vector3(0f, 1.2f, -10f));
            yield return new WaitForSeconds(1f);
            var aside = Quaternion.Euler(0f, 120f, 0f);
            float kept = body.FacingYaw;
            for (float w = 0f; w < 1.5f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Look = aside, FreeLook = true });
                yield return null;
            }
            float stood = Mathf.Abs(Mathf.DeltaAngle(kept, body.FacingYaw));
            Check(stood < 8f && body.HoldingHeading, "стоя, тело не крутится за камерой со стороны",
                $"камера ушла на 120°, корпус — на {stood:0}°");
            var turnFrom = body.Torso.position;
            for (float w = 0f; w < 2f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Move = new Vector2(0f, 1f), Look = aside, FreeLook = true });
                yield return null;
            }
            float turned = Mathf.Abs(Mathf.DeltaAngle(120f, body.FacingYaw));
            var wentOff = Flat(body.Torso.position - turnFrom);
            var lookWay = aside * Vector3.forward;
            Check(turned < 12f && !body.HoldingHeading, "первый шаг разворачивает тело за камерой",
                $"корпус в {turned:0}° от взгляда");
            Check(wentOff.magnitude > 2f && Vector3.Angle(wentOff, lookWay) < 25f, "и уводит туда, куда смотрит камера",
                $"прошёл {wentOff.magnitude:0.0} м, отклонение от взгляда {Vector3.Angle(wentOff, lookWay):0}°");
            for (float w = 0f; w < 1.5f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Look = Quaternion.identity });
                yield return null;
            }
            body.Drive(PuppetInput.Idle);
            float inside = Mathf.Abs(Mathf.DeltaAngle(0f, body.FacingYaw));
            Check(inside < 8f, "изнутри головы взгляд поворачивает тело и стоя", $"корпус в {inside:0}° от взгляда");

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

            // ── 12b. down in the snow, on the flat: the ride height comes down with the stance ───────────────────
            boot.GoTo(0);
            yield return new WaitForSeconds(1.2f);
            float flatY = body.Torso.position.y;
            for (float w = 0f; w < 1.5f; w += Time.deltaTime) { body.Drive(new PuppetInput { Look = Quaternion.identity, Prone = true }); yield return null; }
            Check(body.Low && body.Prone && flatY - body.Torso.position.y > .3f, "лёжа тело ниже на полметра (ровное место)",
                $"с {flatY:0.00} до {body.Torso.position.y:0.00} м, stance {body.Stance:0.00}, опора {body.GroundDistance:0.00}, grounded {body.Grounded}, limp {body.Limp}, dead {body.Dead}, корпус {body.Tilt:0}°");
            for (float w = 0f; w < 1.5f; w += Time.deltaTime) { body.Drive(PuppetInput.Idle); yield return null; }

            // ── 13. the night yard: the loop of docs/SANDBOX.md, end to end ───────────────────────────────────────
            // Everything the Menk decides is tested in dotnet (HuntTests); what is checked here is the engine side
            // of it: that the night comes, that the body in the scene is read the way the rules expect, that a
            // thing can be taken to the fire and counted, and that a blow in the scene ends the run.
            boot.FirstPerson = true;
            var hunt = boot.Hunt;
            hunt.Begin(180f);
            yield return new WaitForSeconds(5f);
            body = boot.Body;
            var run = hunt.Run; var menk = hunt.Menk;
            Check(RenderSettings.ambientSkyColor.r < .12f, "ночь опустилась", $"ambient r={RenderSettings.ambientSkyColor.r:0.000}, туман {RenderSettings.fogDensity:0.000}");
            Check(hunt.Active && menk != null, "забег идёт, Менк на площадке", $"режим {menk?.Mode}");
            float mx = menk.X, mz = menk.Z;
            yield return new WaitForSeconds(4f);
            float ring = Height1079.Core.HuntRules.Dist(menk.X, menk.Z, SandboxHuntYard.Tent.x, SandboxHuntYard.Tent.z);
            Check(Height1079.Core.HuntRules.Dist(mx, mz, menk.X, menk.Z) > 2f || menk.Mode == Height1079.Core.HunterBrain.State.Listen,
                "Менк ходит по кольцу вокруг палатки", $"прошёл {Height1079.Core.HuntRules.Dist(mx, mz, menk.X, menk.Z):0.0} м, режим {menk.Mode}");
            Check(Mathf.Abs(ring - Height1079.Core.HuntRules.PatrolRadius) < 4f, "кольцо радиусом 14 м", $"до палатки {ring:0.0} м");

            // a lit torch thirty metres off, in the open: it comes
            var menkAt = new Vector3(menk.X, 0f, menk.Z);
            var toFire = Vector3.ProjectOnPlane(SandboxHuntYard.Fire - menkAt, Vector3.up).normalized;
            var standAt = menkAt + toFire * 30f;
            boot.PlaceAt(new Vector3(standAt.x, 1.2f, standAt.z));
            if (!hunt.Torch) hunt.ToggleTorch();
            yield return new WaitForSeconds(5f);
            Check(menk.Mode == Height1079.Core.HunterBrain.State.ToLight || menk.Mode == Height1079.Core.HunterBrain.State.Chase,
                "свет фонаря зовёт Менка", $"режим {menk.Mode}, на свет {menk.WentToLight} раз");
            if (hunt.Torch) hunt.ToggleTorch();
            // the Menk has been called and is on its way: for the steps that follow it is stood in the far corner
            // of the yard and stood there again before each, or the check of the list becomes a check of luck
            var corner = SandboxHuntYard.Origin + new Vector3(-27f, 0f, -27f);
            void Park() { menk.X = corner.x; menk.Z = corner.z; }
            Park();

            // the diary: it lies just inside the doorway of the tent (the mouth faces the fire, south), so it is
            // taken from the threshold; put down at the fire, counted
            var diary = run.Errand.Items.Find(i => i.Name == "дневник");
            var door = SandboxHuntYard.Tent + new Vector3(0f, 0f, -Height1079.Core.Sites.Tent.Length * .5f - .9f);
            boot.PlaceAt(new Vector3(door.x, 1.2f, door.z));
            yield return new WaitForSeconds(1f);
            Check(Height1079.Core.HuntRules.Dist(diary.X, diary.Z, door.x, door.z) < 2.2f, "дневник лежит у входа в палатку",
                $"{Height1079.Core.HuntRules.Dist(diary.X, diary.Z, door.x, door.z):0.0} м от порога");
            var feet = body.Torso.position + Vector3.down * t.HoverHeight;
            hunt.Take(diary);
            Check(hunt.Held == diary, "дневник взят с порога",
                (hunt.Held != null ? "в руках: " + hunt.Held.Name : "руки пусты") + $" · тело в ({feet.x:0.0}, {feet.z:0.0}), порог ({door.x:0.0}, {door.z:0.0}), дневник ({diary.X:0.0}, {diary.Z:0.0}), {Height1079.Core.HuntRules.Dist(diary.X, diary.Z, feet.x, feet.z):0.0} м, limp {body.Limp}, dead {boot.Dead}");
            boot.PlaceAt(SandboxHuntYard.Spawn);
            yield return new WaitForSeconds(1f);
            hunt.TakeOrPutDown();
            Check(diary.Delivered && run.Errand.Delivered == 1, "положен у костра — засчитан", $"принесено {run.Errand.Delivered}");
            Check(run.LabazOpen && menk.Posts == 2, "первая вещь из палатки открыла лабаз и удлинила обход Менка", $"лабаз {run.LabazOpen}, постов {menk.Posts}");

            // the stove: at the back of the tent, so the body crawls in under the ridge; two hands, and no running with it
            var stove = run.Errand.Items.Find(i => i.Name == "печка");
            Park();
            boot.PlaceAt(new Vector3(door.x, 1.2f, door.z));
            yield return new WaitForSeconds(.8f);
            for (float w = 0f; w < 7f && Height1079.Core.HuntRules.Dist(body.Torso.position.x, body.Torso.position.z, stove.X, stove.Z) > 1.5f; w += Time.deltaTime)
            {
                body.Drive(new PuppetInput { Move = new Vector2(0f, 1f), Look = Quaternion.identity, Prone = true });
                yield return null;
            }
            body.Drive(new PuppetInput { Look = Quaternion.identity, Prone = true });
            yield return null;
            Check(Height1079.Core.HuntRules.Dist(body.Torso.position.x, body.Torso.position.z, stove.X, stove.Z) < 2.2f, "ползком добрался до печки в глубине палатки",
                $"{Height1079.Core.HuntRules.Dist(body.Torso.position.x, body.Torso.position.z, stove.X, stove.Z):0.0} м до печки, тело y {body.Torso.position.y:0.00}, stance {body.Stance:0.00}");
            hunt.Take(stove);
            Check(hunt.Held == stove, "печка взята", hunt.Held != null ? "в руках: " + hunt.Held.Name : "руки пусты");
            Check(!Height1079.Core.Errand.MayRun(stove.Carry) && !Height1079.Core.Errand.MayTorch(stove.Carry), "с печкой — шагом и без фонаря", stove.Carry.ToString());
            hunt.ToggleTorch();
            Check(!hunt.Torch, "фонарь с печкой не включается", hunt.Torch ? "включился" : "не включился");
            hunt.TakeOrPutDown();
            Check(hunt.Held == null && stove.OnSnow && !stove.Delivered, "печка положена в снег у палатки", $"лежит {stove.OnSnow}, сдана {stove.Delivered}");
            boot.PlaceAt(new Vector3(door.x, 1.2f, door.z - 2f));
            yield return new WaitForSeconds(.6f);
            Check(SandboxHuntYard.RealObjects, "площадка собрана из объектов игры", SandboxHuntYard.RealObjects ? "префабы мира на месте" : "заглушки: мир не сгенерирован");

            // the second section: rusks from the labaz, then the Zorkiy — three home is the quota, and the night is
            // called when the body has stood at the fire for five seconds
            var rusks = run.Errand.Items.Find(i => i.Name == "сухари");
            Park();
            boot.PlaceAt(new Vector3(rusks.X, 1.2f, rusks.Z - 1.2f));
            yield return new WaitForSeconds(1f);
            hunt.Take(rusks);
            Check(hunt.Held == rusks, "сухари взяты у лабаза", hunt.Held != null ? "в руках: " + hunt.Held.Name : "руки пусты");
            boot.PlaceAt(SandboxHuntYard.Spawn);
            yield return new WaitForSeconds(1f);
            hunt.TakeOrPutDown();
            // the Zorkiy lies a step further in than the diary: the body stands in the doorway for it
            var zorkiy = run.Errand.Items.Find(i => i.Name == "фотоаппарат");
            Park();
            boot.PlaceAt(new Vector3(door.x, 1.2f, door.z + .8f));
            yield return new WaitForSeconds(1f);
            hunt.Take(zorkiy);
            Check(hunt.Held == zorkiy, "«Зоркий» взят из дверей", hunt.Held != null ? "в руках: " + hunt.Held.Name : $"руки пусты · {Height1079.Core.HuntRules.Dist(zorkiy.X, zorkiy.Z, body.Torso.position.x, body.Torso.position.z):0.0} м до него");
            boot.PlaceAt(SandboxHuntYard.Spawn);
            yield return new WaitForSeconds(1f);
            hunt.TakeOrPutDown();
            Park();
            Check(run.Errand.Delivered == 3 && run.QuotaMet, "три вещи у костра — квота", $"принесено {run.Errand.Delivered}: {string.Join(", ", run.Errand.Items.FindAll(i => i.Delivered).ConvertAll(i => i.Name))}");
            for (float w = 0f; w < Height1079.Core.HuntRules.FinishHold + 1.5f && !run.Over; w += Time.deltaTime) { body.Drive(PuppetInput.Idle); yield return null; }
            Check(run.Over && run.Outcome == "вернулись", "все у костра — ночь окончена до пурги", $"over {run.Over}, исход «{run.Outcome}», {Height1079.Core.HuntRules.Clock(run.Elapsed)}");
            // the debrief is written on the frame after the run ends
            yield return null; yield return null;
            Check(hunt.Report != null && hunt.Report.Exists(l => l.StartsWith("Из палатки 2 из 4, из лабаза 1 из 4")), "итог считает по секциям", hunt.Report != null ? string.Join(" | ", hunt.Report) : "нет итога");

            // a new night for the death: the Menk must be met on a run that is still going
            hunt.Begin(180f);
            yield return new WaitForSeconds(5f);
            body = boot.Body; run = hunt.Run; menk = hunt.Menk;

            // flat in the snow: the body comes down and slows. Stood up first — the last order it was given was
            // to crawl, and a body keeps its last order until it is given another
            boot.PlaceAt(SandboxHuntYard.Spawn);
            for (float w = 0f; w < 1.2f; w += Time.deltaTime) { body.Drive(PuppetInput.Idle); yield return null; }
            float standY = body.Torso.position.y;
            for (float w = 0f; w < 1.5f; w += Time.deltaTime) { body.Drive(new PuppetInput { Look = Quaternion.identity, Prone = true }); yield return null; }
            Check(body.Low && body.Prone && standY - body.Torso.position.y > .3f, "лёжа тело ниже на полметра (у костра)",
                $"с {standY:0.00} до {body.Torso.position.y:0.00} м, stance {body.Stance:0.00}, опора {body.GroundDistance:0.00}, grounded {body.Grounded}, limp {body.Limp}, dead {body.Dead}, корпус {body.Tilt:0}°");
            var proneFrom = body.Torso.position;
            for (float w = 0f; w < 2f; w += Time.deltaTime) { body.Drive(new PuppetInput { Move = new Vector2(0, 1), Run = true, Look = Quaternion.identity, Prone = true }); yield return null; }
            float crawl = Flat(body.Torso.position - proneFrom).magnitude / 2f;
            Check(crawl < t.WalkSpeed * .4f, "ползком медленно и без бега", $"{crawl:0.00} м/с при шаге {t.WalkSpeed:0.00}");
            for (float w = 0f; w < 1.5f; w += Time.deltaTime) { body.Drive(PuppetInput.Idle); yield return null; }

            // standing in front of it in the open: a blow, and the run is over
            var faceIt = new Vector3(menk.X, 0f, menk.Z) + Quaternion.Euler(0f, menk.Yaw, 0f) * Vector3.forward * 4.5f;
            boot.PlaceAt(new Vector3(faceIt.x, 1.2f, faceIt.z));
            var lookAtMenk = Quaternion.Euler(0f, Height1079.Core.HuntRules.Heading(faceIt.x, faceIt.z, menk.X, menk.Z), 0f);
            for (float w = 0f; w < 10f && !hunt.Dead; w += Time.deltaTime) { body.Drive(new PuppetInput { Look = lookAtMenk }); yield return null; }
            yield return null;
            Check(hunt.Dead && run.Over, "стоя перед Менком — удар, забег окончен", $"мёртв {hunt.Dead}, исход «{run.Outcome}», режим {menk.Mode}");
            Check(body.Limp || body.Dead, "тело после удара лежит", $"limp {body.Limp}, dead {body.Dead}, boot.Dead {boot.Dead}, корпус {body.Tilt:0}°, то же тело {ReferenceEquals(body, boot.Body)}");
            Check(hunt.Report != null && hunt.Report.Count >= 4, "итог написан", hunt.Report != null ? string.Join(" | ", hunt.Report) : "нет итога");
            Check(boot.Dead && boot.DeathTitle == "МЕНК ДОГНАЛ", "экран смерти со словами про Менка", $"boot.Dead {boot.Dead}, «{boot.DeathTitle}»");
            // and the morning after is another night on the yard, not a day on the range (the physics sections
            // above walked the stands; the player never leaves the yard)
            boot.GoTo(SandboxRange.YardStand);
            boot.Restart();
            yield return new WaitForSeconds(1f);
            Check(hunt.Active && !boot.Dead && hunt.Run != run, "после смерти — новый забег, не день", $"active {hunt.Active}, dead {boot.Dead}");
            hunt.End();
            yield return new WaitForSeconds(.5f);

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
                // what is on the screen: with a model from a file worn, the sculpted parts are still posed but
                // switched off, and they are not the man being measured
                if (r == null || !r.enabled) continue;
                // the clothes are skinned, and a skinned renderer's box is not measured off its vertices — it is
                // the generous one PuppetFigure sets by hand so the figure is never culled mid-stride, and it
                // reads a metre over the head and under the boots. The head and the boots bound the figure by
                // themselves: nothing of the clothes reaches past either. A worn model's skinned parts are the
                // other case: told to update off screen, their boxes are measured off the skinned vertices every
                // frame, and they are all the model has.
                if (r is SkinnedMeshRenderer smr && !smr.updateWhenOffscreen) continue;
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
