using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The climber's parts, turned once and shared by every body in the scene: the rigid pieces (head, arms,
    /// hands, boots), the clothes, and the single material they are all painted with. This is where the silhouettes
    /// actually live — the numbers below are the figure, and moving one of them changes the character.
    ///
    /// The clothes are one skinned mesh, the way every game does them. Torso and pack, two sleeves, the seat of the
    /// trousers and two trouser legs are one continuous surface whose vertices are bound to the bones
    /// <see cref="PuppetFigure"/> poses every frame — the body, the thighs, the shins and the upper arms. A sleeve is
    /// swept out of the inside of the torso, over the shoulder and down the arm; a trouser leg out of the seat and
    /// down to the boot; at each joint the vertices are shared between the two bones, so the cloth bends at the knee
    /// and the shoulder instead of showing two tubes with a gap or a bulge between them. Every sleeve and hem is
    /// turned inwards at the end, so the cloth has an edge with thickness and the arm visibly comes OUT of it.
    ///
    /// The skin parts stay rigid on plain transforms (head, boots); the arms are not here at all, because an arm is
    /// one tube rebuilt every frame along the line the figure solved (<see cref="PuppetArm"/>) — only its fingers
    /// are turned here.
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
        /// <summary>Torso with the pack, both sleeves, seat and both trouser legs: one skinned mesh over
        /// <see cref="BoneCount"/> bones in the order below.</summary>
        public static Mesh Clothes;

        public const int BoneBody = 0, BoneThighL = 1, BoneShinL = 2, BoneThighR = 3, BoneShinR = 4, BoneArmL = 5, BoneArmR = 6, BoneCount = 7;

        /// <summary>Segments around a piece. The head and torso are what the eye reads, a finger is four pixels.</summary>
        public const int Round = 22, Slim = 14;

        /// <summary>How much finer the weave is on the trousers than on the shirt: same pattern, denser cloth.</summary>
        const float TrouserWeave = 1.5f;

        public static void Ensure()
        {
            if (Skin != null && Head != null && Digits != null && EyeDigits != null && Boot != null && Clothes != null) return;
            Discard();

            Skin = PuppetSkinTexture.Build();
            Head = MakeHead();
            // the arms themselves are PuppetArm's, rebuilt every frame; only their fingers are turned here
            Digits = MakeDigits(1f, 1f);
            EyeDigits = MakeDigits(1.9f, .78f);
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

        /// <summary>A short sleeve. It starts as a narrow ring inside the torso, widens as it comes out over the
        /// shoulder, and runs down the upper arm to a cuff that is turned in and back up, so the edge has thickness.
        /// The rings inside the body belong to the body; the one on the shoulder joint is shared with the arm; the
        /// rest are the arm's. Oversized on purpose: a sleeve that follows the arm is a painted-on stripe. This one
        /// stands well clear of a 9.4 cm arm (<see cref="PuppetArm.ShoulderR"/>) and flares at the cuff, so the arm
        /// visibly comes OUT of it.</summary>
        static PuppetMesh.Station[] SleeveStations(float sign)
        {
            int arm = sign < 0f ? BoneArmL : BoneArmR;
            var sh = Shoulder(sign);
            var sq = new Vector2(1f, .92f);
            var outward = new Vector3(sign, -.16f, 0f).normalized;          // from inside the chest out to the shoulder
            var turn = new Vector3(sign * .35f, -1f, 0f).normalized;        // rounding the shoulder into "down"
            var body = PuppetMesh.Rigid(BoneBody);
            return new[]
            {
                At(new Vector3(sign * .09f, .445f, .01f), outward, .070f, sq, body),
                // wide enough over the shoulder to clear the arm's own joint ball (radius 0.094 on the shoulder
                // point): a ring here 1 cm smaller let the skin show through the top of the sleeve
                At(new Vector3(sign * .19f, .440f, .01f), outward, .118f, sq, body),
                At(sh, turn, .135f, sq, PuppetMesh.Blend(BoneBody, arm, .5f)),
                At(sh + Vector3.down * .06f, Vector3.down, .135f, sq, PuppetMesh.Rigid(arm)),
                At(sh + Vector3.down * SleeveDrop, Vector3.down, .142f, sq, PuppetMesh.Rigid(arm)),     // the cuff
                At(sh + Vector3.down * SleeveDrop, Vector3.down, .095f, sq, PuppetMesh.Rigid(arm)),     // turned in
                At(sh + Vector3.down * (SleeveDrop - .03f), Vector3.down, .095f, sq, PuppetMesh.Rigid(arm)),   // and up
            };
        }

        /// <summary>How far down the upper arm the sleeve reaches, metres. The arm is 0.26 long.</summary>
        const float SleeveDrop = .165f;

        /// <summary>A trouser leg: a narrow ring inside the seat, out through the hip joint, straight down the thigh,
        /// through the knee where the rings are shared between thigh and shin, and down the shin to a hem just above
        /// the boot, turned in so the boot goes into it. It hangs loose — a 12 cm tube — and flares slightly downward,
        /// so the cloth reads as hanging rather than gripping. Narrowest at the hip: the hips stand 0.16 m out
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
                At(start, outward, .070f, sq, PuppetMesh.Rigid(BoneBody)),
                At(hip, (outward + Vector3.down).normalized, .102f, sq, PuppetMesh.Blend(BoneBody, thigh, .5f)),
                At(P(hip.y - .07f), Vector3.down, .112f, sq, PuppetMesh.Rigid(thigh)),
                At(P(hip.y - .14f), Vector3.down, .118f, sq, PuppetMesh.Rigid(thigh)),
                At(P(knee + .07f), Vector3.down, .115f, sq, PuppetMesh.Rigid(thigh)),
                At(P(knee + .035f), Vector3.down, .113f, sq, PuppetMesh.Blend(thigh, shin, .2f)),
                At(P(knee), Vector3.down, .112f, sq, PuppetMesh.Blend(thigh, shin, .5f)),
                At(P(knee - .035f), Vector3.down, .111f, sq, PuppetMesh.Blend(thigh, shin, .8f)),
                At(P(knee - .07f), Vector3.down, .110f, sq, PuppetMesh.Rigid(shin)),
                At(P(ankle + HemUp), Vector3.down, .106f, sq, PuppetMesh.Rigid(shin)),          // the hem
                At(P(ankle + HemUp), Vector3.down, .078f, sq, PuppetMesh.Rigid(shin)),          // turned in
                At(P(ankle + HemUp + .03f), Vector3.down, .078f, sq, PuppetMesh.Rigid(shin)),   // and up
            };
        }

        /// <summary>The hem stops this far above the ankle, so the trouser never hangs past the boot. The boot's
        /// ankle lump reaches 11 cm above the sole and goes up inside the hem.</summary>
        const float HemUp = .05f;

        /// <summary>The seat of the trousers: a bowl hanging off the hips, from a waistband just under the shirt to a
        /// rounded bottom the two legs come out of. Wide enough at the hips to hold both leg tops (0.16 m out and
        /// 0.10 thick: 0.26 to each side), squashed front to
        /// back like the trunk it is worn on. Its lower half follows the two thighs equally rather than the body, so
        /// that in a crouch the seat goes forward with the legs instead of being left hanging behind them.</summary>
        static Vector2[] SeatOutline() => PuppetMesh.Spline(new[]
        {
            new Vector2(.205f, .130f),     // waistband, just under the shirt
            new Vector2(.218f, .060f),
            new Vector2(.245f, -.010f),
            new Vector2(.270f, -.070f),    // the hips
            new Vector2(.268f, -.115f),
            new Vector2(.230f, -.152f),
            new Vector2(.135f, -.180f),
            new Vector2(0f, -.190f),
        }, 3);

        static BoneWeight SeatWeight(Vector2 p)
        {
            float body = Mathf.Clamp01((p.y + .17f) / .16f);   // all body down to the hips, all legs by the bottom
            return PuppetMesh.Blend(BoneBody, body, BoneThighL, (1f - body) * .5f, BoneThighR, (1f - body) * .5f);
        }

        /// <summary>Where along its own texture column the sleeve's cuff band starts, 0…1 — 2.5 cm of tube above the
        /// edge, then the turned-in part. The texture painter asks, so the band lands on the edge however the sleeve
        /// is re-proportioned.</summary>
        public static float SleeveCuffV
        {
            get { var st = SleeveStations(1f); return (PuppetMesh.Arc(st, 4) - .025f) / PuppetMesh.Arc(st); }
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
            // hips, chest and pack are one surface: they never move relative to each other. Squashed front to back
            // the way a person is, with the pack the only lump still added on top. The heights below are the old
            // trunk's shortened by a sixteenth, every radius untouched: the trunk lost 4 cm along with the rest of
            // the man and not a millimetre across.
            m.Revolve(PuppetMesh.Spline(new[]
            {
                new Vector2(0f, -.179f), new Vector2(.105f, -.165f), new Vector2(.155f, -.127f), new Vector2(.175f, -.056f),
                new Vector2(.17f, .019f),                                                   // waist
                new Vector2(.18f, .094f), new Vector2(.205f, .188f), new Vector2(.23f, .292f),
                new Vector2(.243f, .376f),                                                  // shoulders, where the arms hang
                new Vector2(.235f, .438f), new Vector2(.18f, .489f), new Vector2(.09f, .519f), new Vector2(0f, .527f)
            }, 4), Round, new Vector2(1f, .72f), PuppetSkinTexture.Torso);

            var pack = new PuppetMesh();
            pack.Revolve(PuppetMesh.Blob(.40f, .185f, 12), Slim + 4, new Vector2(1.05f, .70f), PuppetSkinTexture.Pack);
            m.Append(pack, Matrix4x4.Translate(new Vector3(0f, .2635f, -.205f)));

            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? -1f : 1f;
                m.Sweep(SleeveStations(sign), Round, PuppetSkinTexture.Sleeve);
                m.Sweep(LegStations(sign), Round, PuppetSkinTexture.TrouserLeg, TrouserWeave);
            }
            m.Revolve(SeatOutline(), Round, new Vector2(1f, .74f), PuppetSkinTexture.Seat, SeatWeight, TrouserWeave);
            return m.ToMesh("PuppetClothes", BindPoses());
        }

        // ─── the rigid pieces ───────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The head is a drop, not a ball, and the hat rides with it: both are posed by the same rotation, so
        /// they are one mesh. The brim is a ring wide enough to read as straw from any side.
        ///
        /// The head is the one thing that did not shrink when the climber came down to 1.62 m, and that is the whole
        /// point of the exercise: 40 cm of head on 1.62 m of man is the quarter PEAK's figures carry, where the same
        /// head on the old 1.79 m was barely a fifth. The hat did come down — it used to be a dome hanging in the air
        /// above the crown, which added 12 cm of nothing to the silhouette; seated on the head where a hat belongs it
        /// costs 7.</summary>
        static Mesh MakeHead()
        {
            var m = new PuppetMesh();
            m.Revolve(PuppetMesh.Drop(.46f, .243f, .26f, 22), Round, new Vector2(1f, .97f), PuppetSkinTexture.Head);

            var crown = new PuppetMesh();
            crown.Revolve(PuppetMesh.Blob(.21f, .19f, 12), Round, Vector2.one, PuppetSkinTexture.Hat);
            m.Append(crown, Matrix4x4.Translate(new Vector3(0f, .19f, 0f)));    // its widest ring sits above the brim,
            var brim = new PuppetMesh();                                        // so nothing flares out underneath
            brim.Revolve(PuppetMesh.Ring(.245f, .115f, .19f, 12), Round, Vector2.one, PuppetSkinTexture.Hat);
            m.Append(brim, Matrix4x4.Translate(new Vector3(0f, .138f, 0f)));    // 14 cm above the eyes: nothing is hidden
            return m.ToMesh("PuppetHead");
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
            const float root = -(PuppetArm.PalmHalf + .005f);   // half a centimetre past the end of the palm proper
            float bend = curl - 1f;
            for (int i = 0; i < 3; i++)
            {
                float z = (i - 1) * .044f;
                m.Digit(new Vector3(0f, root, z), new Vector3(-.005f - .012f * bend, root - .045f + .006f * bend, z * 1.12f),
                        new Vector3(-.030f - .032f * bend, root - .068f + .034f * bend, z * 1.22f),
                        .029f * thin, .025f * thin, 6, 10, PuppetSkinTexture.Hand);
            }
            m.Digit(new Vector3(0f, -.020f, .030f), new Vector3(-.010f - .010f * bend, -.045f, .075f - .008f * bend),
                    new Vector3(-.032f - .022f * bend, -.060f + .012f * bend, .095f - .020f * bend),
                    .027f * thin, .023f * thin, 6, 10, PuppetSkinTexture.Hand);
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
