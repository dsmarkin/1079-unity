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
    /// the jacket as on the pack. Standard's own detail-map slot does the tiling and the multiply. What keeps it off
    /// the skin is the atlas's alpha channel: 1 on the cloth tiles (and the knitted hat), 0 on the face, hands and
    /// boots, and the shader reads that as the detail mask. The colours below say nothing about it — it is a single
    /// pass at the end of <see cref="Build"/>.
    ///
    /// Two things about the layout are worth knowing before reading the numbers:
    /// • The head and the torso are wrapped like a globe — u covers the whole 360° across the tile while v covers only
    ///   180° up it. The picture is therefore stretched two to one sideways, and anything meant to look round on the
    ///   model has to be drawn twice as tall as it is wide. Every eye and brow below obeys that.
    /// • On a limb or a garment, v = 0 is where the sweep started (the jacket: inside its hem; a trouser leg: inside
    ///   the seat) and v = 1 where it ended, measured in metres of arc. Cuffs, hems and the waistband are bands in
    ///   that coordinate, at fractions <see cref="PuppetSkin"/> works out from the actual garment. The arm is the
    ///   one exception: its column is laid out in metres counted back from the fingertips (<see cref="PuppetArm.ArmTile"/>),
    ///   because the arm bends and stretches at the shoulder end and the wrist has to stay where the cuff is.
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
        static readonly Tile ArmT = new Tile(Col * 2, 0, Col, Size / 2);            // the whole arm, fingertips at the top
        static readonly Tile HandT = new Tile(Col * 3, 0, Col, Size / 2);           // the fingers
        static readonly Tile BootT = new Tile(Col * 4, 0, Col, Size / 2);
        static readonly Tile HatT = new Tile(Col * 5, 0, Col, Size / 2 - 64);
        static readonly Tile PompomT = new Tile(Col * 5, Size / 2 - 64, Col, 64);
        // the pack gets two columns: its face is the one piece of cloth the camera behind the climber always sees
        static readonly Tile PackT = new Tile(Col * 6, 0, Col * 2, Size / 2 - 128);
        static readonly Tile LidT = new Tile(Col * 6, Size / 2 - 128, Col * 2, 64);
        static readonly Tile PocketT = new Tile(Col * 6, Size / 2 - 64, Col * 2, 64);

        public static Rect Head => HeadT.Rect;
        public static Rect Torso => TorsoT.Rect;
        public static Rect TrouserLeg => TrouserLegT.Rect;
        public static Rect Seat => SeatT.Rect;
        public static Rect Arm => ArmT.Rect;
        public static Rect Hand => HandT.Rect;
        public static Rect Boot => BootT.Rect;
        public static Rect Hat => HatT.Rect;
        public static Rect Pompom => PompomT.Rect;
        public static Rect Pack => PackT.Rect;
        public static Rect Lid => LidT.Rect;
        public static Rect Pocket => PocketT.Rect;

        static readonly Color Skin = new Color(.93f, .75f, .58f);
        static readonly Color Cheek = new Color(.95f, .68f, .58f);
        static readonly Color Shirt = new Color(.94f, .47f, .19f);
        static readonly Color ShirtDark = new Color(.79f, .36f, .14f);
        static readonly Color Trouser = new Color(.24f, .31f, .45f);
        static readonly Color TrouserDark = new Color(.18f, .24f, .36f);
        static readonly Color Leather = new Color(.34f, .23f, .16f);
        static readonly Color LeatherDark = new Color(.25f, .17f, .12f);
        static readonly Color Sole = new Color(.80f, .76f, .67f);
        static readonly Color Rubber = new Color(.16f, .15f, .14f);
        static readonly Color Lace = new Color(.88f, .84f, .74f);
        static readonly Color Canvas = new Color(.30f, .33f, .21f);
        static readonly Color CanvasDark = new Color(.22f, .25f, .15f);
        static readonly Color Strap = new Color(.27f, .22f, .18f);
        static readonly Color Buckle = new Color(.72f, .70f, .64f);
        static readonly Color Wool = new Color(.72f, .20f, .18f);
        static readonly Color WoolDark = new Color(.56f, .15f, .14f);
        static readonly Color Fleece = new Color(.93f, .90f, .82f);
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
            Rucksack();
            // the detail mask: weave on the cloth and the knitted hat, none on the skin or the boots
            Alpha(TorsoT, 255); Alpha(TrouserLegT, 255); Alpha(SeatT, 255);
            Alpha(PackT, 255); Alpha(LidT, 255); Alpha(PocketT, 255); Alpha(HatT, 255); Alpha(PompomT, 255);
            Alpha(HeadT, 0); Alpha(HandT, 0); Alpha(BootT, 0);
            Alpha(ArmT, 255); AlphaBand(ArmT, PuppetArm.HandV, 1f, 0);      // sleeve to the wrist, skin from there

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

            // the arm: v = 1 at the fingertips and metres of tube back from there. A sleeve down to a knitted cuff at
            // the wrist, then the hand
            Band(ArmT, 0f, 1f, Shirt);
            Band(ArmT, PuppetArm.CuffV, PuppetArm.HandV, ShirtDark);
            Band(ArmT, PuppetArm.HandV, 1f, Skin);
            Band(HandT, 0f, 1f, Skin);

            PaintBoot();

            // the hat: v = 0 inside the head, out round the fold and up to the pole. The fold is ribbed — a darker
            // band with the hat's own colour drawn through it in thin vertical lines
            Band(HatT, 0f, 1f, Wool);
            Band(HatT, PuppetSkin.HatFoldV0, PuppetSkin.HatFoldV1, WoolDark);
            Ribs(HatT, PuppetSkin.HatFoldV0, PuppetSkin.HatFoldV1, Wool);
            Band(PompomT, 0f, 1f, Fleece);
        }

        // ── the boot ───────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The boot's tile holds two pieces (<see cref="PuppetSkin.MakeBoot"/>): the sole in the lower part —
        /// black rubber with a cream midsole line just under the upper — and the upper above it: brown leather with
        /// a darker toe cap round the front, a welt stitch along the bottom edge, a darker tongue up the front with a
        /// column of eyelets each side of it and a cream lace zigzagging between them from the instep to the collar,
        /// the collar itself darker and padded, and the inside of the mouth near black. u = 0.5 is the front of every
        /// ring, so the laces go on the front of the boot and stay there. The v marks are asked of the boot's own
        /// rings (<see cref="PuppetSkin.BootV"/>).</summary>
        static void PaintBoot()
        {
            // the sole: v runs from the middle of the underside, out and up the wall, over the lip and in
            float sole = PuppetSkin.BootSoleTile;
            Band(BootT, 0f, sole, Rubber);
            Band(BootT, sole * .49f, sole * .58f, Sole);            // the midsole line, on the wall just under the lip

            // the upper, in its own v
            float u0 = PuppetSkin.BootUpperTile, span = 1f - u0;
            float V(float v) => u0 + v * span;
            Band(BootT, u0, 1f, Leather);
            float toe = PuppetSkin.BootV(2), lace0 = PuppetSkin.BootV(3), lace1 = PuppetSkin.BootV(9);
            float collar0 = PuppetSkin.BootV(8), collar1 = PuppetSkin.BootV(11);
            Patch(BootT, .34f, .66f, V(0f), V(toe), LeatherDark);   // the toe cap, round the front third
            Band(BootT, V(0f), V(.018f), Sole);                       // the welt stitch
            Patch(BootT, .44f, .56f, V(lace0), V(collar0), LeatherDark);   // the tongue
            Band(BootT, V(collar0), V(collar1), LeatherDark);         // the padded collar
            Band(BootT, V(collar0), V(collar0 + .012f), Sole);        // stitched to the shaft
            Band(BootT, V(collar1), 1f, Ink);                         // the inside
            // eyelets and the lace: five pairs from the instep up to under the collar
            const int pairs = 5;
            const float eye = .085f;
            for (int k = 0; k < pairs; k++)
            {
                float v = Mathf.Lerp(lace0 + .02f, lace1 - .015f, k / (float)(pairs - 1));
                float y = BootT.Y + BootT.H * V(v);
                float xl = BootT.X + BootT.W * (.5f - eye), xr = BootT.X + BootT.W * (.5f + eye);
                if (k + 1 < pairs)
                {
                    float y2 = BootT.Y + BootT.H * V(Mathf.Lerp(lace0 + .02f, lace1 - .015f, (k + 1) / (float)(pairs - 1)));
                    Stroke(new Vector2(xl, y), new Vector2(xr, y2), 2f, 2f, Lace);
                    Stroke(new Vector2(xr, y), new Vector2(xl, y2), 2f, 2f, Lace);
                }
                Oval(xl, y, 3.5f, 3.5f, Buckle); Oval(xr, y, 3.5f, 3.5f, Buckle);
                Oval(xl, y, 1.5f, 1.5f, Ink); Oval(xr, y, 1.5f, 1.5f, Ink);
            }
        }

        /// <summary>A rectangle of a tile in its own u and v.</summary>
        static void Patch(Tile t, float u0, float u1, float v0, float v1, Color c)
        {
            int x0 = t.X + Mathf.RoundToInt(Mathf.Clamp01(u0) * t.W), x1 = t.X + Mathf.RoundToInt(Mathf.Clamp01(u1) * t.W);
            int y0 = t.Y + Mathf.RoundToInt(Mathf.Clamp01(v0) * t.H), y1 = t.Y + Mathf.RoundToInt(Mathf.Clamp01(v1) * t.H);
            Fill(x0, y0, Mathf.Max(x1 - x0, 1), Mathf.Max(y1 - y0, 1), c);
        }

        /// <summary>Thin vertical lines across a band: the ribbing of a knitted fold. Two texels every four, which
        /// is a rib about every five centimetres round the head — a cartoon's ribbing, and it blends to a darker
        /// band at any distance, which is also right.</summary>
        static void Ribs(Tile t, float v0, float v1, Color c)
        {
            int y0 = t.Y + Mathf.RoundToInt(Mathf.Clamp01(v0) * t.H), y1 = t.Y + Mathf.RoundToInt(Mathf.Clamp01(v1) * t.H);
            for (int x = 1; x + 1 < t.W; x += 4) Fill(t.X + x, y0, 2, y1 - y0, c);
        }

        // ── the pack ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The rucksack: canvas with a darker reinforced bottom, a darker lid, a pocket with a flap, and two
        /// straps running up the face from under the bag over the pocket and the lid, buckled where they meet the
        /// lid's edge. u = 0.5 is the middle of the face on all three pieces (<see cref="PuppetSkin"/> turns the pack
        /// so that face looks away from the body); the strap offsets are asked of the pieces themselves, because the
        /// same seven centimetres is a different share of u round the bag, the lid and the pocket.</summary>
        static void Rucksack()
        {
            Band(PackT, 0f, 1f, Canvas);                            // v = 0 the bottom, v = 1 under the lid
            Band(PackT, 0f, PuppetSkin.PackV(.04f), CanvasDark);    // the reinforced bottom
            Band(LidT, 0f, 1f, CanvasDark);
            Band(PocketT, 0f, 1f, Canvas);
            Band(PocketT, PuppetSkin.PocketV(.16f), 1f, CanvasDark);       // the flap
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                Vertical(PackT, .5f + side * PuppetSkin.PackStrapU, PuppetSkin.PackStrapHalfU, Strap);
                Vertical(PocketT, .5f + side * PuppetSkin.PocketStrapU, PuppetSkin.PocketStrapHalfU, Strap);
                Vertical(LidT, .5f + side * PuppetSkin.LidStrapU, PuppetSkin.LidStrapHalfU, Strap);
                Oval(LidT.X + LidT.W * (.5f + side * PuppetSkin.LidStrapU), LidT.Y + LidT.H * PuppetSkin.LidV(.425f), 5f, 4f, Buckle);
            }
            Oval(PocketT.X + PocketT.W * .5f, PocketT.Y + PocketT.H * PuppetSkin.PocketV(.15f), 5f, 4f, Buckle);
        }

        /// <summary>A stripe the full height of a tile, <paramref name="halfU"/> either side of <paramref name="u"/>.</summary>
        static void Vertical(Tile t, float u, float halfU, Color c)
        {
            int x0 = t.X + Mathf.RoundToInt(Mathf.Clamp01(u - halfU) * t.W), x1 = t.X + Mathf.RoundToInt(Mathf.Clamp01(u + halfU) * t.W);
            Fill(x0, t.Y, Mathf.Max(x1 - x0, 1), t.H, c);
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

        static void AlphaBand(Tile t, float v0, float v1, byte a)
        {
            int y0 = t.Y + Mathf.RoundToInt(Mathf.Clamp01(v0) * t.H), y1 = t.Y + Mathf.RoundToInt(Mathf.Clamp01(v1) * t.H);
            for (int y = y0; y < y1; y++)
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

        // ── the jacket ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The jacket: one colour, a knitted hem, a zip up the front with a pull under the collar, two
        /// slanted hand pockets, and a darker collar. Nothing else on it — the pack's straps used to be painted down
        /// the chest and read as braces. v runs from inside the hem (0) up the outside to the collar (1);
        /// <see cref="PuppetSkin.JacketV"/> says where a height lands.</summary>
        static void Clothes()
        {
            int X = TorsoT.X, Y = TorsoT.Y, S = TorsoT.W;
            Fill(X, Y, S, S, Shirt);
            float hem = PuppetSkin.JacketV(-.02f), collar = PuppetSkin.JacketV(.475f);
            Band(TorsoT, 0f, hem, ShirtDark);                                   // the inside of the hem
            Band(TorsoT, hem, PuppetSkin.JacketV(.01f), ShirtDark);             // the knitted hem
            Band(TorsoT, collar, 1f, ShirtDark);                                // the collar, top and inside included

            float cx = X + S * .5f;
            Stroke(new Vector2(cx, Y + S * PuppetSkin.JacketV(.01f)), new Vector2(cx, Y + S * collar), 4f, 4f, ShirtDark);   // the zip
            Oval(cx, Y + S * PuppetSkin.JacketV(.455f), 4f, 6f, Buckle);                                                    // its pull
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                Stroke(new Vector2(cx + side * 84f, Y + S * PuppetSkin.JacketV(.09f)),
                       new Vector2(cx + side * 60f, Y + S * PuppetSkin.JacketV(.17f)), 3f, 3f, ShirtDark);
            }
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
