using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>One texture for the whole climber, painted pixel by pixel in code — face, clothes and skin together —
    /// and a second, tiny one: the weave of the cloth.
    ///
    /// How a texture reaches a model at all: every point of the mesh carries a UV, a pair of numbers saying where on
    /// the picture that point sits. The lathe in <see cref="PuppetMesh"/> writes those while it turns each part, so a
    /// part arrives here already unwrapped. All that is left is to decide which patch of the picture each part gets —
    /// that is the list of tiles below, an atlas — and to paint into those patches.
    ///
    /// One picture for everything means one material and one shader setup for the whole figure, which is why it
    /// costs almost nothing to draw even with a dozen bodies in the sandbox.
    ///
    /// The weave is the second picture. Flat colour on a shape this simple reads as plastic; PEAK's figures wear
    /// cloth with a fine regular knit in it, and that is what this is — a 4 cm tile of soft dots, repeated over every
    /// garment through the second texture coordinate the lathe lays out in metres, so a stitch is the same size on
    /// the shirt as on the pack. Standard's own detail-map slot does the tiling and the multiply. What keeps it off
    /// the skin is the atlas's alpha channel: 1 on the cloth tiles, 0 on the face, hands, arms, boots and hat, and
    /// the shader reads that as the detail mask. The colours below say nothing about it — it is a single pass at
    /// the end of <see cref="Build"/>.
    ///
    /// Two things about the layout are worth knowing before reading the numbers:
    /// • The head and the torso are wrapped like a globe — u covers the whole 360° across the tile while v covers only
    ///   180° up it. The picture is therefore stretched two to one sideways, and anything meant to look round on the
    ///   model has to be drawn twice as tall as it is wide. Every eye and brow below obeys that.
    /// • On a limb or a garment, v = 0 is where the sweep started (a sleeve: inside the chest; a trouser leg: inside
    ///   the seat) and v = 1 where it ended, measured in metres of arc. Cuffs, hems and the waistband are bands in
    ///   that coordinate, at fractions <see cref="PuppetSkin"/> works out from the actual garment.
    ///
    /// The style follows PEAK: flat saturated colour, no baked shading, no gloss.</summary>
    public static class PuppetSkinTexture
    {
        public const int Size = 1024;
        const int Col = Size / 8;       // the eight narrow columns of the lower half

        /// <summary>A rectangle of the atlas in pixels, and the same inset by a texel and a half as a UV rect:
        /// bilinear filtering and mip-maps both read past the edge of a patch, and without the margin a boot would
        /// fetch a sliver of trouser.</summary>
        readonly struct Tile
        {
            public readonly int X, Y, W, H;
            public Tile(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
            public Rect Rect
            {
                get
                {
                    const float pad = 1.5f;
                    return new Rect((X + pad) / Size, (Y + pad) / Size, (W - 2f * pad) / Size, (H - 2f * pad) / Size);
                }
            }
        }

        static readonly Tile HeadT = new Tile(0, Size / 2, Size / 2, Size / 2);
        static readonly Tile TorsoT = new Tile(Size / 2, Size / 2, Size / 2, Size / 2);
        static readonly Tile TrouserLegT = new Tile(Col * 0, 0, Col, Size / 2);
        static readonly Tile SeatT = new Tile(Col * 1, 0, Col, Size / 2);
        static readonly Tile UpperArmT = new Tile(Col * 2, 0, Col, Size / 2);
        static readonly Tile ForearmT = new Tile(Col * 3, 0, Col, Size / 2);
        static readonly Tile HandT = new Tile(Col * 4, 0, Col, Size / 4);          // a hand is small: half a column
        static readonly Tile SleeveT = new Tile(Col * 4, Size / 4, Col, Size / 4);  // and the sleeve has the other half
        static readonly Tile BootT = new Tile(Col * 5, 0, Col, Size / 2);
        static readonly Tile HatT = new Tile(Col * 6, 0, Col, Size / 2);
        static readonly Tile PackT = new Tile(Col * 7, 0, Col, Size / 2);

        public static Rect Head => HeadT.Rect;
        public static Rect Torso => TorsoT.Rect;
        public static Rect TrouserLeg => TrouserLegT.Rect;
        public static Rect Seat => SeatT.Rect;
        public static Rect UpperArm => UpperArmT.Rect;
        public static Rect Forearm => ForearmT.Rect;
        public static Rect Hand => HandT.Rect;
        public static Rect Sleeve => SleeveT.Rect;
        public static Rect Boot => BootT.Rect;
        public static Rect Hat => HatT.Rect;
        public static Rect Pack => PackT.Rect;

        static readonly Color Skin = new Color(.93f, .75f, .58f);
        static readonly Color Cheek = new Color(.95f, .68f, .58f);
        static readonly Color Shirt = new Color(.94f, .47f, .19f);
        static readonly Color ShirtDark = new Color(.79f, .36f, .14f);
        static readonly Color Trouser = new Color(.24f, .31f, .45f);
        static readonly Color TrouserDark = new Color(.18f, .24f, .36f);
        static readonly Color Leather = new Color(.34f, .23f, .16f);
        static readonly Color LeatherDark = new Color(.25f, .17f, .12f);
        static readonly Color Sole = new Color(.80f, .76f, .67f);
        static readonly Color Canvas = new Color(.30f, .33f, .21f);
        static readonly Color CanvasDark = new Color(.22f, .25f, .15f);
        static readonly Color Strap = new Color(.27f, .22f, .18f);
        static readonly Color Buckle = new Color(.72f, .70f, .64f);
        static readonly Color Straw = new Color(.92f, .79f, .44f);
        static readonly Color StrawDark = new Color(.72f, .58f, .29f);
        static readonly Color Ink = new Color(.13f, .12f, .15f);
        static readonly Color White = new Color(.99f, .98f, .95f);

        static Color32[] px;

        /// <summary>Paints the atlas and the weave and hands back a matte Standard material wearing both. The caller
        /// owns all three and is expected to destroy them together (see <see cref="PuppetSkin"/>).</summary>
        public static Material Build()
        {
            px = new Color32[Size * Size];
            Limbs();
            Face();
            Clothes();
            // the detail mask: weave on the cloth, none on the skin, the boots or the hat
            Alpha(TorsoT, 255); Alpha(TrouserLegT, 255); Alpha(SeatT, 255); Alpha(SleeveT, 255); Alpha(PackT, 255);
            Alpha(HeadT, 0); Alpha(UpperArmT, 0); Alpha(ForearmT, 0); Alpha(HandT, 0); Alpha(BootT, 0); Alpha(HatT, 0);

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "PuppetSkin",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            tex.SetPixels32(px);
            tex.Apply(true, false);     // kept readable so the atlas can be dumped to PNG when the look is being judged
            px = null;

            // The material starts from the one the editor saved into Resources, when there is one: a shader variant
            // only reaches a build if some material in the build uses it, and the detail-map variant of Standard is
            // exactly the kind that gets stripped. On a clone that has generated nothing yet the plain shader is
            // used and the weave is simply missing in the player until the project has been set up.
            var proto = Resources.Load<Material>("Sandbox/Cloth");
            var m = proto != null ? new Material(proto) : new Material(Shader.Find("Standard"));
            m.name = "PuppetSkin"; m.hideFlags = HideFlags.HideAndDontSave;
            m.mainTexture = tex;
            m.color = Color.white;
            m.SetFloat("_Glossiness", 0f);   // matte: gloss is what made the first figure look like a plastic toy
            m.SetFloat("_Metallic", 0f);
            var weave = Weave();
            m.SetTexture("_DetailAlbedoMap", weave);
            m.SetTextureScale("_DetailAlbedoMap", Vector2.one);     // UV1 is already laid out in repeats
            m.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
            m.SetTexture("_DetailMask", tex);                        // its alpha: cloth 1, skin 0
            m.SetFloat("_UVSec", 1f);                                // the detail map reads the second UV set
            m.EnableKeyword("_DETAIL_MULX2");
            return m;
        }

        /// <summary>The weave: one 4 cm tile of cloth, as a grey the shader multiplies the colour by — 50 % grey is
        /// "no change", darker is a dip. Sixteen soft dots in a grid, the way a knit or a piqué reads at arm's
        /// length, and a faint diagonal so it is not a screen-door. Tileable by construction: every dot sits whole
        /// inside its own cell.</summary>
        static Texture2D Weave()
        {
            const int S = 64, Cell = 16;
            var w = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float cx = x % Cell - (Cell - 1) * .5f, cy = y % Cell - (Cell - 1) * .5f;
                    float d = Mathf.Sqrt(cx * cx + cy * cy);
                    float dot = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - 2.4f) / 2.6f));    // 1 at the centre, 0 by r = 5
                    float g = .5f - .12f * dot + (((x + y) / 3) % 2 == 0 ? .014f : -.014f);
                    byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f);
                    w[y * S + x] = new Color32(b, b, b, 255);
                }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true)
            {
                name = "PuppetWeave",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            tex.SetPixels32(w);
            tex.Apply(true, true);
            return tex;
        }

        // ── the columns ────────────────────────────────────────────────────────────────────────────────────────────
        // v is arc length along the piece, so "a 3 cm hem" is a number here once and stays right when the garment is
        // re-proportioned; the garments' own fractions are asked of PuppetSkin, which has the outlines.

        static void Limbs()
        {
            Band(TrouserLegT, 0f, 1f, Trouser);                     // v = 0 inside the seat, v = 1 the inside of the hem
            Band(TrouserLegT, PuppetSkin.TrouserHemV, 1f, TrouserDark);

            Band(SeatT, 0f, 1f, Trouser);                           // v = 0 at the waist
            Band(SeatT, 0f, PuppetSkin.SeatBeltV, TrouserDark);     // the waistband

            Band(UpperArmT, 0f, 1f, Skin);                          // under the sleeve, and bare below the cuff
            Band(ForearmT, 0f, 1f, Skin);
            Band(HandT, 0f, 1f, Skin);

            Band(SleeveT, 0f, 1f, Shirt);                           // v = 0 inside the chest
            Band(SleeveT, PuppetSkin.SleeveCuffV, 1f, ShirtDark);   // the cuff, tube end and the turned-in part

            Band(BootT, 0f, 1f, Leather);                           // boot: v = 0 is the sole, the outline starts there
            Band(BootT, 0f, .17f, Sole);
            Band(BootT, .84f, 1f, LeatherDark);                     // cuff

            Band(HatT, 0f, 1f, Straw);                              // hat: the crown's v runs bottom to top, the brim's around its rim
            Band(HatT, .44f, .56f, StrawDark);                      // band on the crown — and the brim's hidden inner edge
            Band(HatT, 0f, .05f, StrawDark);
            Band(HatT, .95f, 1f, StrawDark);                        // the brim's outer rim, which the loop meets at both ends

            Band(PackT, 0f, 1f, Canvas);                            // pack: v = 0 is the bottom of it, v = 1 the top
            Band(PackT, .62f, 1f, CanvasDark);                      // the lid thrown over the top
            Band(PackT, .565f, .605f, Strap);                       // and the strap that holds it down
        }

        static void Band(Tile t, float v0, float v1, Color c)
        {
            int y0 = t.Y + Mathf.RoundToInt(Mathf.Clamp01(v0) * t.H), y1 = t.Y + Mathf.RoundToInt(Mathf.Clamp01(v1) * t.H);
            Fill(t.X, y0, t.W, y1 - y0, c);
        }

        static void Alpha(Tile t, byte a)
        {
            for (int y = t.Y; y < t.Y + t.H; y++)
                for (int x = t.X; x < t.X + t.W; x++) px[y * Size + x].a = a;
        }

        // ── the face ───────────────────────────────────────────────────────────────────────────────────────────────

        static void Face()
        {
            int X = HeadT.X, Y = HeadT.Y, S = HeadT.W;
            Fill(X, Y, S, S, Skin);

            float cx = X + S * .5f;                 // u = 0.5 — dead ahead
            float eyeX = S * .068f;                 // ≈ 24° of longitude apart: about 9 cm on a 42 cm head
            // v = 0.44 is where the drop is widest — the cheekbone. The eyes go there, everything else hangs off it.
            float eyeY = Y + S * .45f, browY = Y + S * .575f, mouthY = Y + S * .31f, cheekY = Y + S * .355f;

            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f, ex = cx + side * eyeX;
                Oval(ex + side * 10f, cheekY, 14f, 24f, Cheek);                 // a flat, barely-there cheek, no gradient
                Oval(ex, eyeY, 21f, 42f, White);                                // twice as tall as wide → round on the head
                Oval(ex - side * 3f, eyeY - 5f, 11f, 23f, Ink);                 // pupil, set slightly inwards
                Oval(ex - side * 8f, eyeY + 12f, 6f, 11f, Color.white);         // the highlight that makes it look alive
                // the inner end lifted by three pixels and no more: tilt them the other way and the climber looks
                // worried, tilt them harder and he looks angry
                Stroke(new Vector2(ex + side * 22f, browY), new Vector2(ex - side * 22f, browY + 3f), 6f, 9f, Ink);
            }
            Arc(cx, mouthY + 22f, 30f, 40f, Mathf.PI * 1.22f, Mathf.PI * 1.78f, 5f, 8f, Ink);
        }

        // ── shirt and the pack's straps ────────────────────────────────────────────────────────────────────────────

        static void Clothes()
        {
            int X = TorsoT.X, Y = TorsoT.Y, S = TorsoT.W;
            Fill(X, Y, S, S, Shirt);                                            // v = 0 is the crotch, v = 1 the neck
            // the trousers' seat is worn over the lower trunk; what is painted under it only has to be the right
            // colour in case a sliver ever shows at the waist
            Fill(X, Y, S, Mathf.RoundToInt(S * .40f), Trouser);
            Fill(X, Y + Mathf.RoundToInt(S * .94f), S, Mathf.RoundToInt(S * .06f), ShirtDark);   // collar

            float cx = X + S * .5f;
            // On a lathe's UV a strap over the shoulder is very nearly a straight line, because u is the angle round
            // the body and v is the height. They stop at v = 0.8: above that the rings shrink into the pole and any
            // stripe drawn there smears to a point.
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                Stroke(new Vector2(cx + side * 46f, Y + S * .80f), new Vector2(cx + side * 28f, Y + S * .36f), 10f, 10f, Strap);
            }
            // the sternum strap reaches from one shoulder strap to the other and no further: drawn right round the
            // body it reads as a harness rather than a pack
            Fill(Mathf.RoundToInt(cx - 54f), Y + Mathf.RoundToInt(S * .545f), 108, Mathf.RoundToInt(S * .035f), Strap);
            Oval(cx, Y + S * .562f, 13f, 13f, Buckle);      // v = 0.545 is the sternum on the torso's outline
        }

        // ── the brush ──────────────────────────────────────────────────────────────────────────────────────────────

        static void Fill(int x0, int y0, int w, int h, Color c)
        {
            Color32 v = c;
            int xe = Mathf.Min(x0 + w, Size), ye = Mathf.Min(y0 + h, Size);
            for (int y = Mathf.Max(0, y0); y < ye; y++)
            {
                int row = y * Size;
                for (int x = Mathf.Max(0, x0); x < xe; x++) px[row + x] = v;
            }
        }

        static void Blend(int x, int y, Color c, float k)
        {
            if (k <= 0f || x < 0 || y < 0 || x >= Size || y >= Size) return;
            int i = y * Size + x;
            px[i] = k >= 1f ? (Color32)c : Color32.Lerp(px[i], c, k);
        }

        /// <summary>A filled ellipse with a softened edge — every feature of the face is one or two of these.</summary>
        static void Oval(float cx, float cy, float rx, float ry, Color c)
        {
            rx = Mathf.Max(rx, .5f); ry = Mathf.Max(ry, .5f);
            float soft = Mathf.Max(1f, Mathf.Min(rx, ry) * .08f);
            int x0 = Mathf.FloorToInt(cx - rx) - 1, x1 = Mathf.CeilToInt(cx + rx) + 1;
            int y0 = Mathf.FloorToInt(cy - ry) - 1, y1 = Mathf.CeilToInt(cy + ry) + 1;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Blend(x, y, c, Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) / soft));
                }
        }

        /// <summary>A line stamped with ellipses. The two half-widths are separate on purpose: on the head's globe
        /// wrap a round stamp comes out twice as wide as it is tall.</summary>
        static void Stroke(Vector2 a, Vector2 b, float wx, float wy, Color c)
        {
            int steps = Mathf.CeilToInt((b - a).magnitude) + 1;
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                Oval(p.x, p.y, wx, wy, c);
            }
        }

        static void Arc(float cx, float cy, float rx, float ry, float from, float to, float wx, float wy, Color c)
        {
            const int steps = 28;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)steps);
                Oval(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry, wx, wy, c);
            }
        }
    }
}
