using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Wood smoke for the 31 Jan camp: the stove pipe (always) and the fire (while it burns). Material from SnowFxFactory ("Smoke").</summary>
    public static class CampSmoke
    {
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
