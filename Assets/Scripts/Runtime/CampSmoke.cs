using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Wood smoke for the 31 Jan camp: the stove pipe (always) and the fire (while it burns). Material from SnowFxFactory ("Smoke").</summary>
    public static class CampSmoke
    {
        /// <summary>Flames licking up from the stack: additive flame sprites, fast rise, shrinking, plus a few sparks.</summary>
        public static ParticleSystem CreateFlames(Transform parent, Vector3 localPos)
        {
            var mat = Resources.Load<Material>("World/Materials/SnowFx/Flame");
            if (mat == null) { Debug.LogWarning("1079: flame material missing"); return null; }
            var ps = new GameObject("Flames", typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            ps.transform.SetParent(parent, false); ps.transform.localPosition = localPos;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 2f; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(.22f, .42f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.3f, .3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .55f, .18f), new Color(1f, .8f, .4f));
            main.maxParticles = 120; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = 40f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .22f; shape.rotation = new Vector3(-90, 0, 0);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, .15f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, .9f, .6f), 0), new GradientColorKey(new Color(1f, .45f, .12f), .5f), new GradientColorKey(new Color(.6f, .15f, .05f), 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.9f, .15f), new GradientAlphaKey(.5f, .6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var noise = ps.noise; noise.enabled = true; noise.strength = .6f; noise.frequency = 1.5f; noise.scrollSpeed = 1f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard; r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;

            var sparks = new GameObject("Sparks", typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            sparks.transform.SetParent(ps.transform, false);
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm = sparks.main; sm.loop = true; sm.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.8f); sm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            sm.startSize = new ParticleSystem.MinMaxCurve(.015f, .03f); sm.startColor = new Color(1f, .6f, .2f); sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.maxParticles = 40;
            var se = sparks.emission; se.rateOverTime = 6f;
            var ss = sparks.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 18f; ss.radius = .1f; ss.rotation = new Vector3(-90, 0, 0);
            var sn = sparks.noise; sn.enabled = true; sn.strength = 1f; sn.frequency = 2f;
            var sr = sparks.GetComponent<ParticleSystemRenderer>(); sr.sharedMaterial = mat; sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play(true);
            return ps;
        }

        public static ParticleSystem Create(string name, Transform parent, Vector3 localPos, float rate, float size, float rise)
        {
            var mat = Resources.Load<Material>("World/Materials/SnowFx/Smoke");
            if (mat == null) { Debug.LogWarning("1079: smoke material missing"); return null; }
            var ps = new GameObject(name, typeof(ParticleSystem)).GetComponent<ParticleSystem>();
            ps.transform.SetParent(parent, false);
            ps.transform.localPosition = localPos;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 5f; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(rise * .6f, rise);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .6f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
            main.maxParticles = 200; main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = rate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8f; shape.radius = .05f;
            shape.rotation = new Vector3(-90, 0, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(.5f, 1.3f); vel.y = new ParticleSystem.MinMaxCurve(0f, .2f); vel.z = new ParticleSystem.MinMaxCurve(-.2f, .3f);
            var growth = ps.sizeOverLifetime; growth.enabled = true; growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .4f, 1, 3f));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            var noise = ps.noise; noise.enabled = true; noise.strength = .3f; noise.frequency = .4f; noise.scrollSpeed = .2f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.7f, .12f), new GradientAlphaKey(.35f, .6f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard; r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }
    }
}
