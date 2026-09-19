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
            yield return Drive(body, new Vector2(0, 1), 4f);
            Check(body.Torso.position.y - beforeY > .35f, "поднимается по ступеням 0,2 м",
                $"набрал {body.Torso.position.y - beforeY:0.00} м за 4 с");

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
            float bounce = 0f;
            for (float w = 0f; w < 4f; w += Time.deltaTime)
            {
                bounce = Mathf.Max(bounce, body.Torso.position.y - landedAt);
                yield return null;
            }
            Check(bounce < 1.6f, "ноги не подбрасывают тело после падения", $"подъём {bounce:0.00} м");
            Check(!body.Limp, "встаёт после падения", $"обмякшее тело: {body.Limp}");

            Debug.Log($"selftest: итог — провалов {failed}");
            yield return new WaitForSeconds(.5f);
            Application.Quit(failed == 0 ? 0 : 1);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

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
