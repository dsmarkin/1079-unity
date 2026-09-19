using UnityEngine;
using Height1079.Puppet;

namespace Height1079.Sandbox
{
    /// <summary>Everything the sandbox shows: what the body is doing right now, and a slider for every number it is
    /// made of. Plain IMGUI on purpose — it is built from code, it needs no assets, and a panel that takes ten lines to
    /// add a knob is a panel people actually use.</summary>
    public sealed class SandboxHud : MonoBehaviour
    {
        public static bool Panel = true;
        static string message = "";
        static float messageUntil;
        public static void Say(string s) { message = s; messageUntil = Time.unscaledTime + 4f; }

        Vector2 scroll;
        GUIStyle head, small;

        void Styles()
        {
            if (head != null) return;
            head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        }

        void OnGUI()
        {
            var boot = SandboxBoot.Instance;
            if (boot == null || boot.Body == null) return;
            Styles();
            var b = boot.Body;
            var t = boot.Tuning;

            // ── state, top left ───────────────────────────────────────────────────────────────────────────────────
            GUILayout.BeginArea(new Rect(10, 10, 330, 200), GUI.skin.box);
            GUILayout.Label($"стенд: {boot.StandName}", head);
            GUILayout.Label($"силы {b.Stamina:0}/{t.Stamina:0}   {(b.Exhausted ? "— РУКИ ОТКАЗАЛИ" : "")}{(b.Limp ? "— ТЕЛО ОБМЯКЛО" : "")}", small);
            GUI.backgroundColor = Color.Lerp(new Color(.8f, .25f, .2f), new Color(.3f, .7f, .35f), b.StaminaFraction);
            GUILayout.HorizontalScrollbar(0f, Mathf.Max(b.StaminaFraction, .001f), 0f, 1f, GUILayout.Height(12));
            GUI.backgroundColor = Color.white;
            var v = b.Torso.linearVelocity;
            GUILayout.Label($"скорость {v.magnitude:0.0} м/с (верт. {v.y:0.0})   высота {b.Torso.position.y:0.0} м", small);
            string footing = b.Sliding ? $"СКОЛЬЗИТ, уклон {b.SlopeAngle:0}°"
                          : b.Grounded ? $"опора {b.GroundDistance:0.00} м, уклон {b.SlopeAngle:0}°"
                          : "в воздухе";
            GUILayout.Label(footing, small);
            if (b.HandsEnabled) GUILayout.Label($"руки: Л {b.Left.Now} {b.Left.Load:0} Н · П {b.Right.Now} {b.Right.Load:0} Н", small);
            else GUILayout.Label("руки выключены (F8) — сейчас настраиваем ноги", small);
            GUILayout.Label($"последнее приземление {b.LastImpact:0.0} м/с   шаг физики {1f / Time.fixedDeltaTime:0} Гц", small);
            if (Time.unscaledTime < messageUntil) GUILayout.Label(message, small);
            GUILayout.EndArea();

            // ── keys, bottom left ─────────────────────────────────────────────────────────────────────────────────
            GUILayout.BeginArea(new Rect(10, Screen.height - 96, 560, 86), GUI.skin.box);
            GUILayout.Label("WASD/стрелки — идти · Shift — бегом · Space — прыжок", small);
            GUILayout.Label("1…9 — стенд · F1 — панель · F2 — пересобрать тело · F3 — вид · F4 — замедление · F5/F6 — сохранить/загрузить · F8 — руки (черновик) · Esc — курсор", small);
            GUILayout.Label("F9 — выйти в меню игры", small);
            GUILayout.Label($"файл настроек: {PuppetTuning.PathFor(t.Name)}", small);
            GUILayout.EndArea();

            if (!Panel) return;

            // ── the knobs, right ──────────────────────────────────────────────────────────────────────────────────
            GUILayout.BeginArea(new Rect(Screen.width - 340, 10, 330, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("физика тела — правится вживую", head);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("масса и сложение", head);
            t.TorsoMass = Row("масса тела, кг", t.TorsoMass, 30f, 140f);
            t.HandMass = Row("масса кисти, кг", t.HandMass, .5f, 12f);
            t.TorsoHeight = Row("высота капсулы, м", t.TorsoHeight, .6f, 1.8f);
            t.TorsoRadius = Row("радиус капсулы, м", t.TorsoRadius, .15f, .5f);
            t.ShoulderUp = Row("плечо выше центра, м", t.ShoulderUp, .1f, .8f);
            t.ShoulderOut = Row("плечо в сторону, м", t.ShoulderOut, .05f, .5f);

            GUILayout.Label("ноги: опора и склоны", head);
            t.HoverHeight = Row("высота парения, м", t.HoverHeight, .5f, 1.6f);
            t.LegProbe = Row("щуп ниже ног, м", t.LegProbe, .1f, 1.2f);
            t.LegSpring = Row("жёсткость ног", t.LegSpring, 20f, 400f);
            t.LegDamper = Row("гашение ног", t.LegDamper, 1f, 60f);
            t.LegMaxAccel = Row("предел ног, м/с²", t.LegMaxAccel, 10f, 200f);
            t.LegRise = Row("ноги встают за, с", t.LegRise, 0f, 1.5f);
            t.LegLift = Row("подъём ног не быстрее, м/с", t.LegLift, .5f, 8f);
            t.FootGrip = Row("держат до, °", t.FootGrip, 20f, 80f);
            t.UphillSpeed = Row("в гору от шага, доля", t.UphillSpeed, .1f, 1f);
            t.DownhillSpeed = Row("под гору от шага, доля", t.DownhillSpeed, 1f, 1.8f);
            t.SlideFriction = Row("скольжение: трение", t.SlideFriction, 0f, .95f);
            t.SlideTop = Row("скольжение: предел, м/с", t.SlideTop, 2f, 25f);
            t.SlideControl = Row("скольжение: управление", t.SlideControl, 0f, 8f);
            t.SlideLift = Row("скольжение: доля ног", t.SlideLift, 0f, 1f);

            GUILayout.Label("стойка", head);
            t.UprightSpring = Row("держать вертикаль", t.UprightSpring, 10f, 400f);
            t.UprightDamper = Row("гашение вертикали", t.UprightDamper, 1f, 60f);
            t.TurnSpring = Row("поворот к взгляду", t.TurnSpring, 5f, 200f);
            t.TurnDamper = Row("гашение поворота", t.TurnDamper, 1f, 40f);

            GUILayout.Label("шаг", head);
            t.WalkSpeed = Row("шаг, м/с", t.WalkSpeed, .5f, 6f);
            t.RunSpeed = Row("бег, м/с", t.RunSpeed, 1f, 10f);
            t.GroundAccel = Row("разгон на земле", t.GroundAccel, 4f, 80f);
            t.AirAccel = Row("управление в воздухе", t.AirAccel, 0f, 14f);

            GUILayout.Label(b.HandsEnabled ? "руки (черновик, F8)" : "руки выключены — F8, чтобы включить", head);
            if (b.HandsEnabled)
            {
            t.ArmReach = Row("длина руки, м", t.ArmReach, .4f, 1.3f);
            t.ArmSpring = Row("рука тянется", t.ArmSpring, 100f, 3000f);
            t.ArmDamper = Row("гашение руки", t.ArmDamper, 5f, 200f);
            t.GripSpring = Row("тело к хвату", t.GripSpring, 200f, 8000f);
            t.GripDamper = Row("гашение подтяга", t.GripDamper, 5f, 400f);
            t.GripBreakForce = Row("хват рвётся при, Н", t.GripBreakForce, 500f, 20000f);
            t.GrabRadius = Row("ладонь, м", t.GrabRadius, .08f, .5f);
            t.PullIn = Row("подтягивание, м", t.PullIn, .05f, .7f);
            }

            GUILayout.Label("силы (одна полоска)", head);
            t.Stamina = Row("всего", t.Stamina, 20f, 300f);
            t.HangCost = Row("вис, /с", t.HangCost, 0f, 40f);
            t.GripCost = Row("хват с опорой, /с", t.GripCost, 0f, 20f);
            t.PullCost = Row("подтягивание, /с", t.PullCost, 0f, 60f);
            t.RunCost = Row("бег, /с", t.RunCost, 0f, 30f);
            t.Recovery = Row("восстановление, /с", t.Recovery, 0f, 60f);
            t.RestDelay = Row("пауза до отдыха, с", t.RestDelay, 0f, 4f);
            t.ExhaustLock = Row("отказ рук, с", t.ExhaustLock, 0f, 6f);

            GUILayout.Label("решатель", head);
            int rate = Mathf.RoundToInt(Row("шаг физики, Гц", t.PhysicsRate, 30f, 200f));
            int iter = Mathf.RoundToInt(Row("итераций", t.SolverIterations, 4f, 40f));
            if (rate != t.PhysicsRate || iter != t.SolverIterations)
            {
                t.PhysicsRate = rate; t.SolverIterations = iter;
                SandboxBoot.Instance.ApplySolver();
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("выйти в игру (F9)")) SandboxBoot.Instance.LeaveToGame();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("сохранить (F5)")) { t.Save("sandbox"); Say("сохранено"); }
            if (GUILayout.Button("сброс к заводским")) { SandboxBoot.Instance.Tuning = new PuppetTuning { Name = "sandbox" }; SandboxBoot.Instance.ApplySolver(); SandboxBoot.Instance.Spawn(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            for (int i = 0; i < SandboxRange.Stands.Count; i++)
                if (GUILayout.Button(SandboxRange.Stands[i].Name)) SandboxBoot.Instance.GoTo(i);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        float Row(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, small, GUILayout.Width(170));
            GUILayout.Label(value.ToString(max > 100f ? "0" : "0.00"), small, GUILayout.Width(46));
            GUILayout.EndHorizontal();
            return GUILayout.HorizontalSlider(value, min, max);
        }
    }
}
