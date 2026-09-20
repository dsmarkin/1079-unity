// The one material of the imported low-poly props (built-in RP): Standard lighting, the albedo read from the vertex
// colour that the editor pipeline baked in from the palette (docs/art/palette.json, Editor/World/ImportedFactory).
// No texture, no smoothness to speak of — the style is flat colour under real light and fog. The vertex alpha is a
// glow: 0 for everything, 1 for the faces the bake marked as fire, which is how a carved flame keeps its colour at
// night without an emissive material of its own. Fog is the built-in fog of the surface shader; the project keeps
// its variants in builds (ProjectSetup.EnsureFogVariants), so it works in the player as it does in the editor.
Shader "Height1079/PaletteVertex"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _Glow ("Glow of lit faces", Range(0,4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness, _Glow;

        struct Input { float4 color : COLOR; };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // the baked colours are the palette's sRGB values; a linear-space project wants them linear
            fixed3 c = IN.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            c *= _Color.rgb;
            o.Albedo = c;
            o.Emission = c * (IN.color.a * _Glow);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
