// Shared by the sky shaders (Height1079/Sky*). Globals are set every frame by Runtime/SkyDome.cs.
#ifndef H1079_SKY_COMMON
#define H1079_SKY_COMMON
float3 _SkyZenith, _SkyHorizon, _SkyGlow, _SunDirW, _MoonDirW, _GalPoleW, _GalCentreW;
float _SkyStorm, _MilkyWay, _StarVis, _CloudCover, _CloudTime, _Aurora, _AuroraTime, _MoonLit, _SunDisc;
float2 _CloudWind;

float h1079_hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
float h1079_noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float a = h1079_hash(i), b = h1079_hash(i + float2(1, 0)), c = h1079_hash(i + float2(0, 1)), d = h1079_hash(i + float2(1, 1));
    float2 u = f * f * (3 - 2 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}
float h1079_fbm(float2 p)
{
    float v = 0, a = .5;
    for (int k = 0; k < 3; k++) { v += a * h1079_noise(p); p = p * 2.03 + 17.1; a *= .5; }
    return v / .875;
}
// sky colour for a view direction: zenith/horizon gradient and the twilight glow on the sun's side
float3 h1079_skyColor(float3 dir)
{
    float h = saturate(dir.y);
    float3 c = lerp(_SkyHorizon, _SkyZenith, pow(h, .45));
    float2 sd = normalize(_SunDirW.xz + 1e-5), vd = normalize(dir.xz + 1e-5);
    float toward = saturate(dot(sd, vd) * .5 + .5);
    float glow = pow(toward, 5) * exp(-h * 7) + pow(toward, 2) * exp(-h * 2.5) * .25;
    c += _SkyGlow * glow;
    // daylight (Elbrus): the sun itself, its halo and the bright aureole around it
    if (_SunDisc > .001)
    {
        float cd = dot(dir, normalize(_SunDirW));
        float disc = smoothstep(.99975, .99991, cd);
        float halo = pow(saturate(cd), 1400) * .45 + pow(saturate(cd), 90) * .14 + pow(saturate(cd), 8) * .03;
        c += _SunDisc * (disc * 9 + halo);
    }
    if (dir.y < 0) c = lerp(c, _SkyHorizon * .8, saturate(-dir.y * 8));
    return c;
}
#endif
