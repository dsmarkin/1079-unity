using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The climber's parts, turned once and shared by every body in the scene: the meshes and the single
    /// material they are all painted with. This is where the silhouettes actually live — the numbers below are the
    /// figure, and moving one of them changes the character.
    ///
    /// Why several meshes on plain transforms rather than one skinned mesh on a skeleton: the pose is not a bone
    /// hierarchy. <see cref="PuppetFigure"/> solves every joint in world space from the physics capsule, so there is
    /// no chain for a skin to hang off, and a SkinnedMeshRenderer would mean inventing one and maintaining bind poses
    /// for it. The one thing skinning buys — a surface that stays continuous across a bend — is bought two ways:
    /// the legs give both bones of a joint the same hemisphere at that joint (see <see cref="PuppetMesh.Bone"/>),
    /// and the arms are not here at all, because an arm is one tube rebuilt every frame along the line the figure
    /// solved (<see cref="PuppetArm"/>). Only the fingers of it are turned here. Everything shares one material, so
    /// the pieces still batch into very few draw calls.
    ///
    /// Everything is built at run time and cached in statics. Unity can destroy these behind our back on a domain
    /// reload or a scene load, so <see cref="Ensure"/> checks all of them and rebuilds the set as a whole.</summary>
    public static class PuppetSkin
    {
        public static Material Skin;
        /// <summary>Clothing is worn, not painted. On the reference figures a sleeve has thickness and ends in a cuff
        /// the arm comes out of, shorts are a skirt with a hem, socks are a tube with a roll at the top — so each is
        /// its own turned piece, a little wider than the limb inside it, with its own flat colour.</summary>
        public static Material ShirtCloth, ShortsCloth, SockCloth;
        public static Mesh Body, Head, Thigh, Shin, Boot;
        public static Mesh Sleeve, Shorts, ThighLeg, ShinLeg;
        /// <summary>Three fingers and a thumb of the right hand, in the hand's own frame — origin at the hand point,
        /// y back up the forearm, x the thin way across the palm, z the wide way. Not a Mesh: <see cref="PuppetArm"/>
        /// copies them into its own tube each frame, mirrored in x for the left hand.</summary>
        public static PuppetMesh Digits;

        /// <summary>Segments around a piece. The head and torso are what the eye reads, a finger is four pixels.</summary>
        public const int Round = 22, Slim = 14;

        /// <summary>The knee's radius, shared by the two bones that meet there — a thigh ends exactly as thick as a shin
        /// begins, which is what keeps the knee a single smooth surface at any bend.</summary>
        const float Knee = .098f;

        /// <summary>Bone lengths, and they are the same numbers PuppetFigure poses with (ThighLen, ShinLen, UpperArm,
        /// Forearm there). A mesh turned shorter than the bone that carries it gets stretched to fit, which is the one
        /// thing that looks wrong.
        ///
        /// They are short for the body they hang on, and deliberately: the climber was cut down from 1.79 m to 1.62 m
        /// and the whole of it came out of the limbs and a little out of the trunk. Not one radius came down with
        /// them — a shorter man of the same width is stocky, a scaled-down one is just a smaller man, and PEAK's
        /// figures are the first of those.</summary>

        public static void Ensure()
        {
            if (Skin != null && Body != null && Head != null && Thigh != null && Shin != null
             && Digits != null && Boot != null
             && Sleeve != null && Shorts != null && ThighLeg != null && ShinLeg != null) return;
            Discard();

            Skin = PuppetSkinTexture.Build();
            ShirtCloth = Flat(PuppetSkinTexture.ShirtColour);
            ShortsCloth = Flat(PuppetSkinTexture.ShortsColour);
            SockCloth = Flat(PuppetSkinTexture.SockColour);
            Body = MakeBody();
            Head = MakeHead();
            // thicker than they were by a fifth, and not a millimetre longer: the reference figures are stocky, and
            // a thin limb on a short body reads as a spindly child rather than a stubby doll
            Thigh = Turn("PuppetThigh", PuppetMesh.Bone(.352f, .128f, Knee, .05f), Slim + 2, Vector2.one, PuppetSkinTexture.Thigh);
            Shin = Turn("PuppetShin", PuppetMesh.Bone(.334f, Knee, .076f, .02f, openStart: true), Slim + 2, Vector2.one, PuppetSkinTexture.Shin);
            // the arms themselves are PuppetArm's, rebuilt every frame; only their fingers are turned here
            Digits = MakeDigits();
            // worn over the top, each ending in a visible hem
            // Oversized on purpose: a sleeve that follows the arm is a painted-on stripe. This one stands well clear
            // of a 9.4 cm arm (PuppetArm.ShoulderR) and flares at the cuff, so the arm visibly comes OUT of it.
            Sleeve = Turn("PuppetSleeve", PuppetMesh.Bone(.165f, .140f, .152f, .004f, 5, 3), Round, new Vector2(1f, .92f), PuppetSkinTexture.UpperArm);
            Shorts = MakeShorts();
            // Trouser legs, not shorts and socks: loose tubes over the thigh and the shin that reach the boot, each
            // flaring slightly downward so the cloth hangs rather than grips.
            ThighLeg = Turn("PuppetThighLeg", PuppetMesh.Bone(.352f, .150f, .142f, .004f, 5, 3), Round, new Vector2(1f, .94f), PuppetSkinTexture.Thigh);
            ShinLeg = Turn("PuppetShinLeg", PuppetMesh.Bone(.334f, .138f, .128f, .004f, 5, 3), Round, new Vector2(1f, .94f), PuppetSkinTexture.Shin);
            Boot = MakeBoot();
        }

        /// <summary>A half-built set is worse than none: whatever survived a reload is destroyed before rebuilding, or
        /// a second run in the editor leaks a four-megabyte atlas every time.</summary>
        static void Discard()
        {
            if (Skin != null) PuppetRig.Kill(Skin.mainTexture);
            PuppetRig.Kill(Skin); PuppetRig.Kill(Body); PuppetRig.Kill(Head); PuppetRig.Kill(Thigh); PuppetRig.Kill(Shin);
            PuppetRig.Kill(Boot);
            PuppetRig.Kill(Sleeve); PuppetRig.Kill(Shorts); PuppetRig.Kill(ThighLeg); PuppetRig.Kill(ShinLeg);
            PuppetRig.Kill(ShirtCloth); PuppetRig.Kill(ShortsCloth); PuppetRig.Kill(SockCloth);
            Skin = null; Body = Head = Thigh = Shin = Boot = null; Digits = null;
            Sleeve = Shorts = ThighLeg = ShinLeg = null; ShirtCloth = ShortsCloth = SockCloth = null;
        }

        /// <summary>A plain matte colour. Clothing does not need the atlas — it is one flat tone each — and its own
        /// material is cheaper to reason about than finding a patch of the right colour to point its UVs at.</summary>
        static Material Flat(Color c)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", 0f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// <summary>Shorts: a short skirt hanging off the hips with a hem the legs come out of, turned as its own
        /// piece so the edge reads from any angle. Squashed front to back like the trunk it is worn on.</summary>
        static Mesh MakeShorts()
        {
            var m = new PuppetMesh();
            m.Revolve(PuppetMesh.Spline(new[]
            {
                new Vector2(.205f, .12f),      // waistband, just under the shirt
                new Vector2(.234f, .05f),
                new Vector2(.248f, -.02f),
                new Vector2(.250f, -.062f),    // the seat, where the trouser legs take over
                new Vector2(.228f, -.076f),
                new Vector2(.205f, -.066f),
            }, 4), Round, new Vector2(1f, .74f), PuppetSkinTexture.Torso);
            return m.ToMesh("PuppetShorts");
        }

        static Mesh Turn(string name, Vector2[] outline, int sides, Vector2 xz, Rect uv)
        {
            var m = new PuppetMesh();
            m.Revolve(outline, sides, xz, uv);
            return m.ToMesh(name);
        }

        /// <summary>Hips, chest and pack used to be three primitives stacked on each other. They are posed by the same
        /// rotation and never move apart, so one hand-drawn silhouette turns them into a single soft body, squashed
        /// front to back the way a person is, with the pack the only lump still added on top.
        ///
        /// Every height below is the old trunk's, shortened by a sixteenth, and every radius is untouched: the trunk
        /// lost 4 cm along with the rest of the man and not a millimetre across. The clothes survive that without
        /// being redrawn, because the texture's bands are measured in arc length along this very outline — squash it
        /// and the waistband stays on the waist.</summary>
        static Mesh MakeBody()
        {
            var m = new PuppetMesh();
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
            return m.ToMesh("PuppetBody");
        }

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
        /// mirrored in x, which puts its thumb in front and its curl toward the body as well.</summary>
        static PuppetMesh MakeDigits()
        {
            var m = new PuppetMesh();
            const float root = -(PuppetArm.PalmHalf + .005f);   // half a centimetre past the end of the palm proper
            for (int i = 0; i < 3; i++)
            {
                float z = (i - 1) * .044f;
                m.Digit(new Vector3(0f, root, z), new Vector3(-.005f, root - .045f, z * 1.12f),
                        new Vector3(-.030f, root - .068f, z * 1.22f), .029f, .025f, 6, 10, PuppetSkinTexture.Hand);
            }
            m.Digit(new Vector3(0f, -.020f, .030f), new Vector3(-.010f, -.045f, .075f),
                    new Vector3(-.032f, -.060f, .095f), .027f, .023f, 6, 10, PuppetSkinTexture.Hand);
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
