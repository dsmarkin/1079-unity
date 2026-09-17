using UnityEngine;

namespace Height1079.Runtime
{
    /// <summary>Per hiker (local and remote): stamps alternating boot prints every stride, a trodden patch every other step,
    /// and sinks the visual into the snow — deeper in virgin powder, shallower on a trodden path.</summary>
    public sealed class SnowTrail : MonoBehaviour
    {
        Vector3 last;
        float stride;
        bool left, started;
        Transform visual;
        float sink;
        public float Sink => sink;
        Vector3 heading = Vector3.forward;

        void Start() { last = transform.position; visual = transform.Find("Visual"); }

        static Terrain Ground => Terrain.activeTerrain;

        void LateUpdate()
        {
            var fx = SnowFx.Instance; var terrain = Ground;
            if (fx == null || terrain == null) return;
            var pos = transform.position;
            float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            bool onSnow = pos.y - groundY < .35f;
            var delta = pos - last; delta.y = 0;
            last = pos;
            if (!started) { started = true; return; }
            float dist = delta.magnitude;
            if (dist > 5f) return; // teleport
            float speed = Time.deltaTime > 0 ? dist / Time.deltaTime : 0;
            if (dist > .001f) heading = Vector3.Lerp(heading, delta / dist, .3f).normalized;

            float depth = SnowFx.Depth(groundY);
            int passes = fx.Passes(pos.x, pos.z);
            float targetSink = onSnow ? depth * (passes >= 3 ? .3f : passes > 0 ? .6f : 1f) : 0f;
            sink = Mathf.Lerp(sink, targetSink, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (visual != null) visual.localPosition = new Vector3(0, -sink, 0);

            if (!onSnow || speed < .2f) return;
            stride += dist;
            float step = speed > 4f ? 1.05f : .72f;
            if (stride < step) return;
            stride = 0;
            left = !left;
            var side = Vector3.Cross(Vector3.up, heading).normalized;
            var foot = pos + side * (left ? -.12f : .12f);
            foot.y = terrain.SampleHeight(foot) + terrain.transform.position.y;
            var tp = terrain.transform.position; var size = terrain.terrainData.size;
            var normal = terrain.terrainData.GetInterpolatedNormal((foot.x - tp.x) / size.x, (foot.z - tp.z) / size.z);
            fx.Step(foot, heading, normal, depth, left);
        }
    }
}
