using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>One texture for the whole climber, painted pixel by pixel in code — face, clothes and skin together.
    ///
    /// How a texture reaches a model at all: every point of the mesh carries a UV, a pair of numbers saying where on
    /// the picture that point sits. The lathe in <see cref="PuppetMesh"/> writes those while it turns each part, so a
    /// part arrives here already unwrapped. All that is left is to decide which patch of the picture each part gets —
    /// that is the list of <see cref="Rect"/>s below, an atlas — and to paint into those patches.
    ///
    /// One picture for everything means one material and one shader setup for fourteen pieces, which is why the figure
    /// costs almost nothing to draw even with a dozen bodies in the sandbox.
    ///
    /// Two things about the layout are worth knowing before reading the numbers:
    /// • The head and the torso are wrapped like a globe — u covers the whole 360° across the tile while v covers only
    ///   180° up it. The picture is therefore stretched two to one sideways, and anything meant to look round on the
    ///   model has to be drawn twice as tall as it is wide. Every eye and brow below obeys that.
    /// • On a limb, v = 0 is the joint the bone starts from (thigh: the hip; shin: the knee) and v = 1 is the joint it
    ///   ends at, because that is the order the lathe swept the outline in. Shorts, socks and sleeves are bands in that
    ///   coordinate, so the clothes are texture, not extra geometry.
    ///
    /// The style follows PEAK: flat saturated colour, no baked shading, no gloss. Skin is green because a climber has
    /// to be findable against snow and rock, and because a realistic flesh tone on a shape this stylised looks ill.</summary>
    public static class PuppetSkinTexture
    {
        public const int Size = 1024;
        const int Col = Size / 8;       // the eight narrow columns of the lower half, one per limb piece

        /// <summary>Inset by a texel and a half: bilinear filtering and mip-maps both read past the edge of a patch,
        /// and without the margin a boot would fetch a sliver of sock.</summary>
        static Rect Tile(int x, int y, int w, int h)
        {
            const float pad = 1.5f;
            return new Rect((x + pad) / Size, (y + pad) / Size, (w - 2f * pad) / Size, (h - 2f * pad) / Size);
        }

        public static readonly Rect Head = Tile(0, Size / 2, Size / 2, Size / 2);
        public static readonly Rect Torso = Tile(Size / 2, Size / 2, Size / 2, Size / 2);
        public static readonly Rect Thigh = Tile(Col * 0, 0, Col, Size / 2);
        public static readonly Rect Shin = Tile(Col * 1, 0, Col, Size / 2);
        public static readonly Rect UpperArm = Tile(Col * 2, 0, Col, Size / 2);
        public static readonly Rect Forearm = Tile(Col * 3, 0, Col, Size / 2);
        public static readonly Rect Hand = Tile(Col * 4, 0, Col, Size / 2);
        public static readonly Rect Boot = Tile(Col * 5, 0, Col, Size / 2);
        public static readonly Rect Hat = Tile(Col * 6, 0, Col, Size / 2);
        public static readonly Rect Pack = Tile(Col * 7, 0, Col, Size / 2);

        static readonly Color Skin = new Color(.93f, .75f, .58f);
        static readonly Color Cheek = new Color(.95f, .68f, .58f);
        static readonly Color Shirt = new Color(.94f, .47f, .19f);
        /// <summary>The three clothing tones, public because the worn pieces (sleeve, shorts, sock) are their own
        /// flat-coloured geometry and must match what the atlas paints on the body underneath them.</summary>
        public static Color ShirtColour => Shirt;
        public static Color ShortsColour => Shorts;
        public static Color SockColour => Sock;
        static readonly Color ShirtDark = new Color(.79f, .36f, .14f);
        static readonly Color Shorts = new Color(.24f, .31f, .45f);
        static readonly Color ShortsDark = new Color(.18f, .24f, .36f);
        static readonly Color Sock = new Color(.95f, .93f, .86f);
        static readonly Color Stripe = new Color(.85f, .28f, .25f);
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

        /// <summary>Paints the atlas and hands back a matte Standard material wearing it. The caller owns both and is
        /// expected to destroy them together (see <see cref="PuppetSkin"/>).</summary>
        public static Material Build()
        {
            px = new Color32[Size * Size];
            Limbs();
            Face();
            Clothes();

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

            var m = new Material(Shader.Find("Standard")) { name = "PuppetSkin", hideFlags = HideFlags.HideAndDontSave, mainTexture = tex };
            m.color = Color.white;
            m.SetFloat("_Glossiness", 0f);   // matte: gloss is what made the first figure look like a plastic toy
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        // ── the eight limb columns ─────────────────────────────────────────────────────────────────────────────────
        // The fractions come straight from the outlines in PuppetSkin: v is arc length, so "shorts end 14 cm below the
        // hip" becomes a number here once, and stays right when the leg is re-proportioned.

        static void Limbs()
        {
            Strip(0, 0f, 1f, Skin);                     // thigh: v = 0 at the hip
            Strip(0, 0f, .45f, Shorts);
            Strip(0, .42f, .455f, ShortsDark);          // hem, 14 cm below the hip

            Strip(1, 0f, 1f, Skin);                     // shin: v = 0 at the knee, v = 1 at the ankle
            // the boot swallows the last 11 cm of the shin, so a short sock shows almost nothing. v = 0.31 puts the
            // sock top 26 cm above the ankle — and note the shin's outline has no cap at the knee (see
            // PuppetMesh.Bone), which is why its v runs shorter than the thigh's for the same length of leg.
            Strip(1, .31f, 1f, Sock);
            Strip(1, .335f, .383f, Stripe);
            Strip(1, .420f, .468f, Stripe);

            Strip(2, 0f, 1f, Skin);                     // upper arm: v = 0 at the shoulder
            Strip(2, 0f, .45f, Shirt);
            Strip(2, .425f, .455f, ShirtDark);          // cuff of the short sleeve, 11 cm below the shoulder

            Strip(3, 0f, 1f, Skin);                     // forearm
            Strip(4, 0f, 1f, Skin);                     // hand

            Strip(5, 0f, 1f, Leather);                  // boot: v = 0 is the sole, the outline starts there
            Strip(5, 0f, .17f, Sole);
            Strip(5, .84f, 1f, LeatherDark);            // cuff

            Strip(6, 0f, 1f, Straw);                    // hat: the crown's v runs bottom to top, the brim's around its rim
            Strip(6, .44f, .56f, StrawDark);            // band on the crown — and the brim's hidden inner edge
            Strip(6, 0f, .05f, StrawDark);
            Strip(6, .95f, 1f, StrawDark);              // the brim's outer rim, which the loop meets at both ends

            Strip(7, 0f, 1f, Canvas);                   // pack: v = 0 is the bottom of it, v = 1 the top
            Strip(7, .62f, 1f, CanvasDark);             // the lid thrown over the top
            Strip(7, .565f, .605f, Strap);              // and the strap that holds it down
        }

        static void Strip(int col, float v0, float v1, Color c)
        {
            int y0 = Mathf.RoundToInt(v0 * (Size / 2)), y1 = Mathf.RoundToInt(v1 * (Size / 2));
            Fill(col * Col, y0, Col, y1 - y0, c);
        }

        // ── the face ───────────────────────────────────────────────────────────────────────────────────────────────

        static void Face()
        {
            const int X = 0, Y = Size / 2, S = Size / 2;
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

        // ── shirt, shorts and the pack's straps ────────────────────────────────────────────────────────────────────

        static void Clothes()
        {
            const int X = Size / 2, Y = Size / 2, S = Size / 2;
            Fill(X, Y, S, S, Shirt);                                            // v = 0 is the crotch, v = 1 the neck
            Fill(X, Y, S, Mathf.RoundToInt(S * .33f), Shorts);
            Fill(X, Y + Mathf.RoundToInt(S * .30f), S, Mathf.RoundToInt(S * .04f), ShortsDark);
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
