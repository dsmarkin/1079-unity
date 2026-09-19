using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The climber's parts, turned once and shared by every body in the scene: the rigid pieces (head with
    /// its hat, boots), the clothes, and the single material they are all painted with. This is where the
    /// silhouettes actually live — the numbers below are the figure, and moving one of them changes the character.
    ///
    /// The clothes are one skinned mesh, the way every game does them. The jacket with the rucksack on it, the seat
    /// of the trousers and two trouser legs are one continuous surface whose vertices are bound to the bones
    /// <see cref="PuppetFigure"/> poses every frame — the body, the thighs and the shins. A trouser leg is swept out
    /// of the seat and down to the boot; at the knee the vertices are shared between the two bones, so the cloth
    /// bends instead of showing two tubes with a gap or a bulge between them. Every edge is turned inwards, so the
    /// cloth has thickness where it ends.
    ///
    /// The jacket is a padded winter one: a body wider than the trousers whose hem hangs over their waistband, a
    /// stand-up collar round the bottom of the head, and no shoulder seam — the sleeves are not here at all. A sleeve
    /// is the arm: one thick tube rebuilt every frame along the line the figure solved (<see cref="PuppetArm"/>),
    /// leaving the jacket's shoulder, stepping down onto a knitted cuff at the wrist, with the hand coming out of it.
    /// Only the fingers are turned here.
    ///
    /// The rucksack is the one thing on the body that is not round. The lathe turns a rectangular ring as readily as
    /// a circle once told to (<see cref="PuppetMesh.Station.Corner"/>), so the pack is three boxes with soft corners —
    /// the bag, a lid over it and a pocket on its face — swept the same way as everything else.
    ///
    /// The bones the clothes hang on have a rest pose — legs straight down, arms hanging — and the clothes are drawn
    /// in it; <see cref="BindPoses"/> is that pose written down as matrices, and the joint positions it uses are
    /// <see cref="PuppetFigure"/>'s own constants, so the two cannot disagree.
    ///
    /// Everything is built at run time and cached in statics. Unity can destroy these behind our back on a domain
    /// reload or a scene load, so <see cref="Ensure"/> checks all of them and rebuilds the set as a whole.</summary>
    public static class PuppetSkin
    {
        public static Material Skin;
        public static Mesh Head, Boot;
        /// <summary>Three fingers and a thumb of the right hand, in the hand's own frame — origin at the hand point,
        /// y back up the forearm, x the thin way across the palm, z the wide way. Not a Mesh: <see cref="PuppetArm"/>
        /// copies them into its own tube each frame, mirrored in x for the left hand.</summary>
        public static PuppetMesh Digits;
        /// <summary>The same fingers for the hands held up in front of the eye: bent further and cut thinner. At
        /// arm's length from the body the relaxed curl reads as a hand; half a metre from the lens it reads as
        /// four sausages pointing at the horizon.</summary>
        public static PuppetMesh EyeDigits;
        /// <summary>Jacket with the pack, seat and both trouser legs: one skinned mesh over <see cref="BoneCount"/>
        /// bones in the order below (the two arm bones carry nothing now that the sleeves are the arms).</summary>
        public static Mesh Clothes;

        public const int BoneBody = 0, BoneThighL = 1, BoneShinL = 2, BoneThighR = 3, BoneShinR = 4, BoneArmL = 5, BoneArmR = 6, BoneCount = 7;

        /// <summary>Segments around a piece. The head and torso are what the eye reads, a finger is four pixels.</summary>
        public const int Round = 22, Slim = 14;

        /// <summary>How much finer the weave is on the trousers than on the jacket: same pattern, denser cloth.</summary>
        const float TrouserWeave = 1.5f;

        public static void Ensure()
        {
            if (Skin != null && Head != null && Digits != null && EyeDigits != null && Boot != null && Clothes != null) return;
            Discard();

            Skin = PuppetSkinTexture.Build();
            Head = MakeHead();
            // the arms themselves are PuppetArm's, rebuilt every frame; only their fingers are turned here
            Digits = MakeDigits(1f, 1f);
            EyeDigits = MakeDigits(1.6f, 1f);
            Boot = MakeBoot();
            Clothes = MakeClothes();
        }

        /// <summary>A half-built set is worse than none: whatever survived a reload is destroyed before rebuilding, or
        /// a second run in the editor leaks a four-megabyte atlas every time.</summary>
        static void Discard()
        {
            if (Skin != null)
            {
                PuppetRig.Kill(Skin.mainTexture);
                PuppetRig.Kill(Skin.GetTexture("_DetailAlbedoMap"));
            }
            PuppetRig.Kill(Skin); PuppetRig.Kill(Head); PuppetRig.Kill(Boot); PuppetRig.Kill(Clothes);
            Skin = null; Head = Boot = Clothes = null; Digits = null; EyeDigits = null;
        }

        static Mesh Turn(string name, Vector2[] outline, int sides, Vector2 xz, Rect uv)
        {
            var m = new PuppetMesh();
            m.Revolve(outline, sides, xz, uv);
            return m.ToMesh(name);
        }

        // ─── the clothes ────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The rest pose the clothes are drawn in, inverted, one matrix per bone: legs straight down off the
        /// hips, arms hanging off the shoulders, every bone's own Y running along it and its Z facing the body's
        /// front — exactly the frame <see cref="PuppetFigure.Aim"/> hands the bones at run time, so with the figure
        /// standing still the skinned cloth lands precisely where it was drawn.</summary>
        static Matrix4x4[] BindPoses()
        {
            var hang = PuppetFigure.Aim(Vector3.down, Vector3.forward);
            var rest = new Matrix4x4[BoneCount];
            rest[BoneBody] = Matrix4x4.identity;
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                var hip = Hip(sign);
                rest[s == 0 ? BoneThighL : BoneThighR] = Matrix4x4.TRS(hip, hang, Vector3.one);
                rest[s == 0 ? BoneShinL : BoneShinR] = Matrix4x4.TRS(hip + Vector3.down * PuppetFigure.ThighLen, hang, Vector3.one);
                rest[s == 0 ? BoneArmL : BoneArmR] = Matrix4x4.TRS(Shoulder(sign), hang, Vector3.one);
            }
            var bind = new Matrix4x4[BoneCount];
            for (int i = 0; i < BoneCount; i++) bind[i] = rest[i].inverse;
            return bind;
        }

        static Vector3 Hip(float sign) => new Vector3(sign * PuppetFigure.HipOut, -PuppetFigure.HipSag, 0f);
        static Vector3 Shoulder(float sign) => new Vector3(sign * PuppetFigure.ShoulderOut, PuppetFigure.ShoulderUp, .01f);

        static PuppetMesh.Station At(Vector3 c, Vector3 axis, float r, Vector2 squash, BoneWeight w)
            => new PuppetMesh.Station { Centre = c, Axis = axis, Radius = r, Squash = squash, Weight = w };


        /// <summary>A trouser leg: a narrow ring inside the seat, out through the hip joint, straight down the thigh,
        /// through the knee where the rings are shared between thigh and shin, and down the shin to a hem just above
        /// the boot, turned in so the boot goes into it. It hangs loose — a 20 cm tube — and flares slightly downward,
        /// so the cloth reads as hanging rather than gripping. Narrowest at the hip: the hips stand 0.13 m out
        /// (<see cref="PuppetFigure.HipOut"/>), and the top of the leg has to fit under the seat there.</summary>
        static PuppetMesh.Station[] LegStations(float sign)
        {
            int thigh = sign < 0f ? BoneThighL : BoneThighR, shin = sign < 0f ? BoneShinL : BoneShinR;
            var hip = Hip(sign);
            float knee = hip.y - PuppetFigure.ThighLen, ankle = knee - PuppetFigure.ShinLen, x = hip.x;
            var sq = new Vector2(1f, .94f);
            var start = new Vector3(sign * .05f, .04f, 0f);
            var outward = (hip - start).normalized;
            Vector3 P(float y) => new Vector3(x, y, 0f);
            return new[]
            {
                At(start, outward, .060f, sq, PuppetMesh.Rigid(BoneBody)),
                At(hip, (outward + Vector3.down).normalized, .087f, sq, PuppetMesh.Blend(BoneBody, thigh, .5f)),
                At(P(hip.y - .07f), Vector3.down, .095f, sq, PuppetMesh.Rigid(thigh)),
                At(P(hip.y - .14f), Vector3.down, .100f, sq, PuppetMesh.Rigid(thigh)),
                At(P(knee + .07f), Vector3.down, .098f, sq, PuppetMesh.Rigid(thigh)),
                At(P(knee + .035f), Vector3.down, .096f, sq, PuppetMesh.Blend(thigh, shin, .2f)),
                At(P(knee), Vector3.down, .095f, sq, PuppetMesh.Blend(thigh, shin, .5f)),
                At(P(knee - .035f), Vector3.down, .094f, sq, PuppetMesh.Blend(thigh, shin, .8f)),
                At(P(knee - .07f), Vector3.down, .093f, sq, PuppetMesh.Rigid(shin)),
                At(P(ankle + HemUp), Vector3.down, .090f, sq, PuppetMesh.Rigid(shin)),          // the hem
                At(P(ankle + HemUp), Vector3.down, .066f, sq, PuppetMesh.Rigid(shin)),          // turned in
                At(P(ankle + HemUp + .03f), Vector3.down, .066f, sq, PuppetMesh.Rigid(shin)),   // and up
            };
        }

        /// <summary>The hem stops this far above the ankle, so the trouser never hangs past the boot. The boot's
        /// ankle lump reaches 11 cm above the sole and goes up inside the hem.</summary>
        const float HemUp = .05f;

        /// <summary>The seat of the trousers: a bowl hanging off the hips, from a waistband under the jacket's hem to
        /// a rounded bottom the two legs come out of. Wide enough at the hips to hold both leg tops (0.13 m out and
        /// 0.09 thick: 0.22 to each side), squashed front to back like the trunk it is worn on. Its lower half follows the two thighs equally rather than the body, so
        /// that in a crouch the seat goes forward with the legs instead of being left hanging behind them.</summary>
        static Vector2[] SeatOutline() => PuppetMesh.Spline(new[]
        {
            new Vector2(.185f, .130f),     // waistband, under the jacket's hem
            new Vector2(.196f, .060f),
            new Vector2(.218f, -.010f),
            new Vector2(.238f, -.070f),    // the hips
            new Vector2(.236f, -.115f),
            new Vector2(.205f, -.152f),
            new Vector2(.120f, -.180f),
            new Vector2(0f, -.190f),
        }, 3);

        static BoneWeight SeatWeight(Vector2 p)
        {
            float body = Mathf.Clamp01((p.y + .17f) / .16f);   // all body down to the hips, all legs by the bottom
            return PuppetMesh.Blend(BoneBody, body, BoneThighL, (1f - body) * .5f, BoneThighR, (1f - body) * .5f);
        }

        public static float TrouserHemV
        {
            get { var st = LegStations(1f); return (PuppetMesh.Arc(st, 9) - .03f) / PuppetMesh.Arc(st); }
        }

        /// <summary>And the waistband: the first 3 cm of the seat's outline, which starts at the waist.</summary>
        public static float SeatBeltV
        {
            get
            {
                var o = SeatOutline();
                float total = 0f;
                for (int i = 1; i < o.Length; i++) total += (o[i] - o[i - 1]).magnitude;
                return .03f / Mathf.Max(total, 1e-3f);
            }
        }

        static Mesh MakeClothes()
        {
            var m = new PuppetMesh();
            // The jacket and the pack are one surface: they never move relative to each other. Squashed front to
            // back the way a person is, though less than the trunk inside it — it is padded.
            m.Revolve(PuppetMesh.Spline(JacketOutline(), JacketSpline), Round, new Vector2(1f, .80f), PuppetSkinTexture.Torso);

            // The pack is turned with its face toward +Z — u = 0.5, the middle of the atlas column, is where the
            // straps and the pocket are painted — and then spun round to hang on the back, so that face looks away
            // from the body and the seam of its texture is against the jacket where nothing sees it.
            var pack = new PuppetMesh();
            pack.Sweep(PackStations(), PackSides, PuppetSkinTexture.Pack);
            pack.Sweep(LidStations(), PackSides, PuppetSkinTexture.Lid);
            pack.Sweep(PocketStations(), PackSides, PuppetSkinTexture.Pocket);
            m.Append(pack, Matrix4x4.TRS(new Vector3(0f, 0f, PackZ), Quaternion.Euler(0f, 180f, 0f), Vector3.one));

            for (int s = 0; s < 2; s++)
                m.Sweep(LegStations(s == 0 ? -1f : 1f), Round, PuppetSkinTexture.TrouserLeg, TrouserWeave);
            m.Revolve(SeatOutline(), Round, new Vector2(1f, .74f), PuppetSkinTexture.Seat, SeatWeight, TrouserWeave);
            return m.ToMesh("PuppetClothes", BindPoses());
        }

        // ─── the jacket ─────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The jacket's outline (radius, height) in the body's frame, run from inside its hem, out under the
        /// hem, up the outside and over the shoulders to the collar, and in over the collar's top. It is a padded
        /// jacket, so it is a barrel: 0.255 at the hem hanging 2 cm over the trousers' waistband (which is 0.185),
        /// 0.272 at the chest, rounding over the shoulders — the arms hang from 0.28 out, 0.414 up, and the sleeve's
        /// shoulder is buried in that rounding — into a stand-up collar 0.178 wide that rings the bottom of the head
        /// (the head is 0.15 wide at the collar's top and comes to its point 3 cm under it).</summary>
        static Vector2[] JacketOutline() => new[]
        {
            new Vector2(.200f, .015f),   // inside the hem, up inside the jacket
            new Vector2(.200f, -.020f),  // the hem, turned in
            new Vector2(.255f, -.020f),  // its outer edge
            new Vector2(.265f, .060f),
            new Vector2(.270f, .160f),
            new Vector2(.272f, .260f),
            new Vector2(.268f, .350f),
            new Vector2(.255f, .405f),   // the shoulders
            new Vector2(.225f, .450f),
            new Vector2(.185f, .475f),   // where the collar rises
            new Vector2(.178f, .505f),   // the collar's top
            new Vector2(.150f, .505f),   // turned in
            new Vector2(.150f, .480f),
            new Vector2(0f, .478f),
        };
        const int JacketSpline = 3;
        /// <summary>The outer run of the outline — hem edge to collar top — as indices into the splined outline.</summary>
        const int JacketHemIdx = 2 * JacketSpline, JacketCollarTopIdx = 10 * JacketSpline;

        /// <summary>Where a height on the OUTSIDE of the jacket lands along its texture column, 0…1 of the whole
        /// outline's arc: the hem is at <c>JacketV(-.02)</c>, the collar's foot at <c>JacketV(.475)</c>. The atlas
        /// paints the hem, the zip, the pockets and the collar by this.</summary>
        public static float JacketV(float y)
        {
            var o = PuppetMesh.Spline(JacketOutline(), JacketSpline);
            float total = 0f, at = -1f;
            for (int i = 1; i < o.Length; i++)
            {
                float seg = (o[i] - o[i - 1]).magnitude;
                if (at < 0f && i > JacketHemIdx && i <= JacketCollarTopIdx && y <= o[i].y)
                    at = total + seg * Mathf.InverseLerp(o[i - 1].y, o[i].y, y);
                total += seg;
            }
            if (at < 0f) at = y < o[JacketHemIdx].y ? OutlineArc(o, JacketHemIdx) : OutlineArc(o, JacketCollarTopIdx);
            return total > 1e-6f ? at / total : 0f;
        }

        static float OutlineArc(Vector2[] o, int upTo)
        {
            float t = 0f;
            for (int i = 1; i < o.Length && i <= upTo; i++) t += (o[i] - o[i - 1]).magnitude;
            return t;
        }

        // ─── the pack ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The rucksack, metres: half-width and half-depth of the bag, how soft its corners are, where its
        /// bottom and top sit on the body, and its centre line behind the back. The jacket's back is at −0.22, so the
        /// face of the pack sinks a couple of centimetres into the padding, the way a loaded pack does. It is narrower
        /// than the shoulders (0.26 to each side) and its lid stops under the back of the head.</summary>
        const float PackHalfW = .17f, PackHalfD = .10f, PackCorner = .035f, PackBottom = 0f, PackTop = .42f, PackZ = -.30f;
        const float LidTop = .51f, PocketHalfW = .115f, PocketHalfD = .045f, PocketBottom = .03f, PocketTop = .215f;
        /// <summary>Strap positions on the face, metres off its centre line, and half a strap's width.</summary>
        const float StrapOff = .07f, StrapHalf = .015f;
        /// <summary>Segments round a pack ring: a metre of perimeter with four corners of a few centimetres each
        /// wants twice what a round part does, or the corners come out as chamfers.</summary>
        const int PackSides = 44;

        /// <summary>One rectangular ring of a box, on the body's bone. A zero width makes a pole that closes it.</summary>
        static PuppetMesh.Station Box(float y, float z, float hx, float hz, float corner) => new PuppetMesh.Station
        {
            Centre = new Vector3(0f, y, z), Axis = Vector3.up,
            Radius = hx, Squash = hx > 1e-5f ? new Vector2(1f, hz / hx) : Vector2.one,
            Corner = hx > 1e-5f ? corner : 0f, Weight = PuppetMesh.Rigid(BoneBody),
        };

        /// <summary>The bag: a flat-ish bottom rounded over into straight sides, closed under the lid.</summary>
        static PuppetMesh.Station[] PackStations()
        {
            float w = PackHalfW, d = PackHalfD, c = PackCorner, y0 = PackBottom, y1 = PackTop;
            return new[]
            {
                Box(y0, 0f, 0f, 0f, 0f),
                Box(y0, 0f, w * .80f, d * .70f, c),
                Box(y0 + .012f, 0f, w * .94f, d * .90f, c),
                Box(y0 + .03f, 0f, w, d, c),
                Box(y1 - .03f, 0f, w, d, c),
                Box(y1 - .01f, 0f, w * .96f, d * .92f, c),
                Box(y1, 0f, w * .80f, d * .70f, c),
                Box(y1, 0f, 0f, 0f, 0f),
            };
        }

        /// <summary>The lid: starts inside the top of the bag, comes out a centimetre wider than it and rounds over.</summary>
        static PuppetMesh.Station[] LidStations()
        {
            float w = PackHalfW + .012f, d = PackHalfD + .012f, c = PackCorner + .01f;
            return new[]
            {
                Box(PackTop - .035f, 0f, PackHalfW * .94f, PackHalfD * .92f, c),
                Box(PackTop - .02f, 0f, w, d, c),
                Box(LidTop - .055f, 0f, w, d, c),
                Box(LidTop - .03f, 0f, w * .96f, d * .94f, c),
                Box(LidTop - .01f, 0f, w * .80f, d * .72f, c),
                Box(LidTop, 0f, w * .45f, d * .40f, c),
                Box(LidTop + .003f, 0f, 0f, 0f, 0f),
            };
        }

        /// <summary>The pocket on the face of the bag, sunk a centimetre and a half into it.</summary>
        static PuppetMesh.Station[] PocketStations()
        {
            float w = PocketHalfW, d = PocketHalfD, c = .03f, z = PackHalfD + PocketHalfD - .015f, y0 = PocketBottom, y1 = PocketTop;
            return new[]
            {
                Box(y0, z, 0f, 0f, 0f),
                Box(y0, z, w * .75f, d * .60f, c),
                Box(y0 + .015f, z, w * .96f, d * .90f, c),
                Box(y0 + .035f, z, w, d, c),
                Box(y1 - .03f, z, w, d, c),
                Box(y1 - .012f, z, w * .95f, d * .85f, c),
                Box(y1, z, w * .70f, d * .50f, c),
                Box(y1, z, 0f, 0f, 0f),
            };
        }

        static float Perimeter(float hx, float hz, float c)
        {
            c = Mathf.Min(c, hx, hz);
            return 4f * (hx - c) + 4f * (hz - c) + 2f * Mathf.PI * c;
        }

        /// <summary>What the atlas needs to paint the pack: where a height lands along each piece's texture column
        /// (0…1 of its arc), and how far off u = 0.5 a strap is on each piece. The pieces have different perimeters,
        /// so the same seven centimetres is a different fraction of u on each.</summary>
        public static float PackV(float y) => VAt(PackStations(), y);
        public static float LidV(float y) => VAt(LidStations(), y);
        public static float PocketV(float y) => VAt(PocketStations(), y);
        public static float PackStrapU => StrapOff / Perimeter(PackHalfW, PackHalfD, PackCorner);
        public static float PackStrapHalfU => StrapHalf / Perimeter(PackHalfW, PackHalfD, PackCorner);
        public static float LidStrapU => StrapOff / Perimeter(PackHalfW + .012f, PackHalfD + .012f, PackCorner + .01f);
        public static float LidStrapHalfU => StrapHalf / Perimeter(PackHalfW + .012f, PackHalfD + .012f, PackCorner + .01f);
        public static float PocketStrapU => StrapOff / Perimeter(PocketHalfW, PocketHalfD, .03f);
        public static float PocketStrapHalfU => StrapHalf / Perimeter(PocketHalfW, PocketHalfD, .03f);

        /// <summary>Where a height lands along a chain of stations, 0…1 of the arc <see cref="PuppetMesh.Sweep"/>
        /// lays v out by — so the atlas can paint "a strap from the bottom to the lid" without knowing how much of
        /// the arc the rounding spends. Rings at one height (a pole and the ring it opens into) are stepped over.</summary>
        static float VAt(PuppetMesh.Station[] st, float y)
        {
            float total = PuppetMesh.Arc(st);
            if (total < 1e-6f || y <= st[0].Centre.y) return 0f;
            for (int i = 1; i < st.Length; i++)
            {
                float a = st[i - 1].Centre.y, b = st[i].Centre.y;
                if (b <= a || y > b) continue;
                float lo = PuppetMesh.Arc(st, i - 1), hi = PuppetMesh.Arc(st, i);
                return (lo + (hi - lo) * (y - a) / (b - a)) / total;
            }
            return 1f;
        }

        // ─── the rigid pieces ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The head is a drop, not a ball, and the hat rides with it: both are posed by the same rotation, so
        /// they are one mesh. The hat is a knitted winter one — a cap hugging the head three centimetres out from
        /// it, a fold turned up round the bottom that stands out a little further, and a pompom on top. The fold's
        /// lower edge is at 7 cm above the head's centre, a finger above the brows, so the eyes are never covered.
        ///
        /// The head is the one thing that did not shrink when the climber came down to 1.62 m, and that is the whole
        /// point of the exercise: 40 cm of head on 1.62 m of man is the quarter PEAK's figures carry, where the same
        /// head on the old 1.79 m was barely a fifth. The hat adds 8 cm of it, pompom included.</summary>
        static Mesh MakeHead()
        {
            var m = new PuppetMesh();
            m.Revolve(PuppetMesh.Drop(.46f, .243f, .26f, 22), Round, new Vector2(1f, .97f), PuppetSkinTexture.Head);

            var hat = new PuppetMesh();
            hat.Revolve(PuppetMesh.Spline(HatOutline(), HatSpline), Round, new Vector2(1f, .97f), PuppetSkinTexture.Hat, null, HatWeave);
            m.Append(hat, Matrix4x4.identity);
            var pom = new PuppetMesh();
            pom.Revolve(PuppetMesh.Blob(.085f, .042f, 10), Slim, Vector2.one, PuppetSkinTexture.Pompom, null, HatWeave);
            m.Append(pom, Matrix4x4.Translate(new Vector3(0f, .275f, 0f)));
            return m.ToMesh("PuppetHead");
        }

        /// <summary>The hat's outline (radius, height) in the head's frame, run from inside the head, down and out
        /// round the fold, and up over the crown to the pole. The head under it is 0.205 wide at the fold's edge,
        /// 0.145 at the crown's start and 0.075 at 0.21 up; the hat keeps about three centimetres off it all the way
        /// and the fold stands two more. Points, not a formula, so the fold has a ridge.</summary>
        static Vector2[] HatOutline() => new[]
        {
            new Vector2(.170f, .085f),   // inside the head
            new Vector2(.222f, .073f),   // the lower edge of the fold, just above the brows
            new Vector2(.228f, .100f),
            new Vector2(.220f, .130f),
            new Vector2(.205f, .148f),   // the top of the fold
            new Vector2(.190f, .158f),   // the crown proper
            new Vector2(.145f, .185f),
            new Vector2(.105f, .210f),
            new Vector2(.060f, .232f),
            new Vector2(0f, .245f),
        };
        const int HatSpline = 3;
        /// <summary>A knit is coarser than the jacket's cloth: the weave tile is stretched by the inverse of this.</summary>
        const float HatWeave = .7f;

        /// <summary>Where the fold runs along the hat's texture column, 0…1 of its arc — from its lower edge to the
        /// ridge — so the atlas can rib it.</summary>
        public static float HatFoldV0 => OutlineV(PuppetMesh.Spline(HatOutline(), HatSpline), 1 * HatSpline);
        public static float HatFoldV1 => OutlineV(PuppetMesh.Spline(HatOutline(), HatSpline), 4 * HatSpline);

        /// <summary>Arc along an outline up to a point of it, as a share of the whole — the measure the lathe lays
        /// v out by (a change of radius counts as length too).</summary>
        static float OutlineV(Vector2[] o, int upTo)
        {
            float total = 0f, at = 0f;
            for (int i = 1; i < o.Length; i++)
            {
                total += (o[i] - o[i - 1]).magnitude;
                if (i <= upTo) at = total;
            }
            return total > 1e-6f ? at / total : 0f;
        }

        /// <summary>Three fingers and a thumb, for the right hand, curled the way a hand hangs when nothing is asked
        /// of it. Three and not four: four thin ones read as sticks glued to a cylinder, and a cartoon hand is short,
        /// thick and curled. Each is a bent tube with an open root, and the roots start a centimetre inside the palm
        /// (see <see cref="PuppetArm"/>), so the fingers come out of the hand rather than sit on it. The thumb leaves
        /// the front edge of the palm and curls across it.
        ///
        /// Frame: origin at the hand point, y up the forearm, x across the thin way — the right palm faces −x, toward
        /// the body — and z along the wide way, +z forward. The fingers curl toward −x. The left hand is this
        /// mirrored in x, which puts its thumb in front and its curl toward the body as well.
        ///
        /// <paramref name="curl"/> is how far the fingers are bent, 1 for the loose curl of a hand hanging at the
        /// side and about 2 for one loosely closed: the tips are drawn further round toward the palm and less far
        /// along. <paramref name="thin"/> scales the fingers' thickness.</summary>
        static PuppetMesh MakeDigits(float curl, float thin)
        {
            var m = new PuppetMesh();
            // The fingers start a centimetre inside the palm and leave it straight along the forearm's line, dead
            // square to its end, as thick at the root as the palm is deep (PalmThin × PalmR) and touching each
            // other: the palm divides into fingers instead of having four thinner tubes pushed into its dome at an
            // angle, which is what showed as a seam round every root. The curl only begins past the knuckle.
            const float root = -(PuppetArm.PalmHalf - .010f);
            float bend = curl - 1f;
            float rRoot = PuppetArm.PalmR * PuppetArm.PalmThin * .82f * thin, rTip = rRoot * .80f;
            const float pitch = .048f;
            for (int i = 0; i < 3; i++)
            {
                float z = (i - 1) * pitch;
                // the middle finger is the long one; the outer two a little shorter and turned a touch outward
                float len = .092f - .008f * Mathf.Abs(i - 1);
                float splay = (i - 1) * .006f;
                m.Digit(new Vector3(0f, root, z),
                        new Vector3(-.004f * bend, root - len * .55f, z + splay * .5f),
                        new Vector3(-.026f - .030f * bend, root - len * (1f - .22f * bend), z + splay),
                        rRoot, rTip, 8, 10, PuppetSkinTexture.Hand);
            }
            // The thumb leaves the wide edge of the palm from its middle, not from the corner by the fingers, and
            // starts well inside it: one long easy arc forward and down toward the fingertips, thick at the root
            // and never bent sharper than a finger — a thumb that came out of the top corner as a short hook read
            // as a broken one.
            m.Digit(new Vector3(0f, .010f, .045f),
                    new Vector3(-.006f - .006f * bend, -.040f, .100f),
                    new Vector3(-.030f - .022f * bend, -.078f + .010f * bend, .112f - .012f * bend),
                    rRoot * 1.05f, rTip, 8, 10, PuppetSkinTexture.Hand);
            return m;
        }

        /// <summary>A boot: an ankle lump and a long foot lump that overlap into one shape. Local origin is the point
        /// the leg's IK calls the sole, so the foot's underside sits exactly on the ground the probe found.</summary>
        static Mesh MakeBoot()
        {
            var m = new PuppetMesh();
            var ankle = new PuppetMesh();
            ankle.Revolve(PuppetMesh.Blob(.17f, .072f, 10), Slim, new Vector2(1f, 1.05f), PuppetSkinTexture.Boot);
            m.Append(ankle, Matrix4x4.Translate(new Vector3(0f, .03f, -.012f)));

            var foot = new PuppetMesh();
            foot.Revolve(PuppetMesh.Spline(new[]
            {
                new Vector2(0f, -.055f), new Vector2(.055f, -.05f), new Vector2(.078f, -.02f),
                new Vector2(.08f, .015f), new Vector2(.055f, .04f), new Vector2(0f, .05f)
            }, 4), Slim, new Vector2(.95f, 1.5f), PuppetSkinTexture.Boot);
            m.Append(foot, Matrix4x4.Translate(new Vector3(0f, -.005f, .05f)));
            return m.ToMesh("PuppetBoot");
        }
    }
}
