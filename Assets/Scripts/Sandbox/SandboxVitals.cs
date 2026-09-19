using UnityEngine;
using Height1079.Core;

namespace Height1079.Sandbox
{
    /// <summary>The one bar's bites, run for the range: hunger with the clock, cold from the snow the body stands in,
    /// sleep slowly with the clock. Each frame the bar's ceiling is handed to the body, so what hunger has bitten
    /// off is strength the body really cannot have. E eats a bar of chocolate: hunger goes back, and a bonus piece
    /// is stuck on the end of the bar for as long as it is not used.</summary>
    public sealed class SandboxVitals : MonoBehaviour
    {
        public readonly Condition Condition = new Condition();
        public readonly Condition.Pressures Pressures = new Condition.Pressures();

        /// <summary>Cold per second on the range with the boots on firm snow. Thigh-deep in a drift it is three
        /// times this. The bar goes in fifteen minutes standing about.</summary>
        public float ColdOnSnow = Condition.Max / 900f;

        void Update()
        {
            var boot = SandboxBoot.Instance;
            if (boot == null || boot.Body == null) return;
            var body = boot.Body;
            float sink = SandboxTerrainSnow.SinkAt(body.Torso.position);
            Pressures.Cold = ColdOnSnow * (1f + 2f * Mathf.Clamp01(sink / .3f));
            Condition.Tick(Time.deltaTime, Pressures);
            body.Ceiling = Condition.CeilingFraction;
        }

        public void Eat()
        {
            var boot = SandboxBoot.Instance;
            if (boot == null || boot.Body == null) return;
            Condition.Ease(Bite.Hunger, Condition.Chocolate.Hunger);
            boot.Body.Extra = Mathf.Min(boot.Body.Extra + Condition.Chocolate.Extra, boot.Tuning.ExtraCap);
            SandboxHud.Say("шоколадка: голод отступил, зелёный кусок на полоске — бонус, тратится первым");
        }
    }
}
