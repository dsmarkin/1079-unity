using UnityEngine;

namespace Height1079.Puppet
{
    /// <summary>The climber's parts, turned once and shared by every body in the scene: nine meshes and the single
    /// material they are all painted with. This is where the silhouettes actually live — the numbers below are the
    /// figure, and moving one of them changes the character.
    ///
    /// Why several meshes on plain transforms rather than one skinned mesh on a skeleton: the pose is not a bone
    /// hierarchy. <see cref="PuppetFigure"/> solves every joint in world space from the physics capsule, so there is
    /// no chain for a skin to hang off, and a SkinnedMeshRenderer would mean inventing one and maintaining bind poses
    /// for it. The one thing skinning buys — a surface that stays continuous across a bend — is bought here instead by
    /// giving both bones of a joint the same hemisphere at that joint (see <see cref="PuppetMesh.Bone"/>). Everything
    /// shares one material, so the fourteen pieces still batch into very few draw calls.
    ///
    /// Everything is built at run time and cached in statics. Unity can destroy these behind our back on a domain
    /// reload or a scene load, so <see cref="Ensure"/> checks all of them and rebuilds the set as a whole.</summary>
    public static class PuppetSkin
    {
        public static Material Skin;
        public static Mesh Body, Head, Thigh, Shin, UpperArm, Forearm, HandL, HandR, Boot;

        /// <summary>Segments around a piece. The head and torso are what the eye reads, a finger is four pixels.</summary>
        const int Round = 22, Slim = 14;

        /// <summary>Joint radii, shared by the two bones that meet there — a thigh ends exactly as thick as a shin
        /// begins, which is what keeps the knee a single smooth surface at any bend.</summary>
        const float Knee = .082f, Elbow = .062f;

        /// <summary>Bone lengths, and they are the same numbers PuppetFigure poses with. A mesh turned shorter than
        /// the bone that carries it gets stretched to fit, which is the one thing that looks wrong.</summary>

        public static void Ensure()
        {
            if (Skin != null && Body != null && Head != null && Thigh != null && Shin != null
             && UpperArm != null && Forearm != null && HandL != null && HandR != null && Boot != null) return;
            Discard();

            Skin = PuppetSkinTexture.Build();
            Body = MakeBody();
            Head = MakeHead();
            Thigh = Turn("PuppetThigh", PuppetMesh.Bone(.44f, .105f, Knee, .05f), Slim + 2, Vector2.one, PuppetSkinTexture.Thigh);
            Shin = Turn("PuppetShin", PuppetMesh.Bone(.42f, Knee, .062f, .02f, openStart: true), Slim + 2, Vector2.one, PuppetSkinTexture.Shin);
            UpperArm = Turn("PuppetUpperArm", PuppetMesh.Bone(.30f, .078f, Elbow, .04f), Slim, Vector2.one, PuppetSkinTexture.UpperArm);
            Forearm = Turn("PuppetForearm", PuppetMesh.Bone(.28f, Elbow, .052f, .02f, openStart: true), Slim, Vector2.one, PuppetSkinTexture.Forearm);
            HandR = MakeHand(false);
            HandL = MakeHand(true);
            Boot = MakeBoot();
        }

        /// <summary>A half-built set is worse than none: whatever survived a reload is destroyed before rebuilding, or
        /// a second run in the editor leaks a four-megabyte atlas every time.</summary>
        static void Discard()
        {
            if (Skin != null) PuppetRig.Kill(Skin.mainTexture);
            PuppetRig.Kill(Skin); PuppetRig.Kill(Body); PuppetRig.Kill(Head); PuppetRig.Kill(Thigh); PuppetRig.Kill(Shin);
            PuppetRig.Kill(UpperArm); PuppetRig.Kill(Forearm); PuppetRig.Kill(HandL); PuppetRig.Kill(HandR); PuppetRig.Kill(Boot);
            Skin = null; Body = Head = Thigh = Shin = UpperArm = Forearm = HandL = HandR = Boot = null;
        }

        static Mesh Turn(string name, Vector2[] outline, int sides, Vector2 xz, Rect uv)
        {
            var m = new PuppetMesh();
            m.Revolve(outline, sides, xz, uv);
            return m.ToMesh(name);
        }

        /// <summary>Hips, chest and pack used to be three primitives stacked on each other. They are posed by the same
        /// rotation and never move apart, so one hand-drawn silhouette turns them into a single soft body, squashed
        /// front to back the way a person is, with the pack the only lump still added on top.</summary>
        static Mesh MakeBody()
        {
            var m = new PuppetMesh();
            m.Revolve(PuppetMesh.Spline(new[]
            {
                new Vector2(0f, -.19f), new Vector2(.105f, -.175f), new Vector2(.155f, -.135f), new Vector2(.175f, -.06f),
                new Vector2(.17f, .02f),                                                    // waist
                new Vector2(.18f, .10f), new Vector2(.205f, .20f), new Vector2(.23f, .31f),
                new Vector2(.243f, .40f),                                                   // shoulders, where the arms hang
                new Vector2(.235f, .465f), new Vector2(.18f, .52f), new Vector2(.09f, .552f), new Vector2(0f, .56f)
            }, 4), Round, new Vector2(1f, .72f), PuppetSkinTexture.Torso);

            var pack = new PuppetMesh();
            pack.Revolve(PuppetMesh.Blob(.46f, .19f, 12), Slim + 4, new Vector2(1f, .62f), PuppetSkinTexture.Pack);
            m.Append(pack, Matrix4x4.Translate(new Vector3(0f, .28f, -.245f)));
            return m.ToMesh("PuppetBody");
        }

        /// <summary>The head is a drop, not a ball, and the hat rides with it: both are posed by the same rotation, so
        /// they are one mesh. The brim is a ring wide enough to read as straw from any side.</summary>
        static Mesh MakeHead()
        {
            var m = new PuppetMesh();
            m.Revolve(PuppetMesh.Drop(.40f, .21f, .26f, 22), Round, new Vector2(1f, .97f), PuppetSkinTexture.Head);

            var crown = new PuppetMesh();
            crown.Revolve(PuppetMesh.Blob(.22f, .17f, 12), Round, Vector2.one, PuppetSkinTexture.Hat);
            m.Append(crown, Matrix4x4.Translate(new Vector3(0f, .215f, 0f)));   // its widest ring sits above the brim,
            var brim = new PuppetMesh();                                        // so nothing flares out underneath
            brim.Revolve(PuppetMesh.Ring(.215f, .105f, .17f, 12), Round, Vector2.one, PuppetSkinTexture.Hat);
            m.Append(brim, Matrix4x4.Translate(new Vector3(0f, .155f, 0f)));    // 20 cm above the eyes: nothing is hidden
            return m.ToMesh("PuppetHead");
        }

        /// <summary>A hand, not a ball: a palm flattened front to back, four fingers hanging off the bottom edge and a
        /// thumb swung inwards. The whole thing is built once pointing one way and mirrored for the other side, which
        /// also flips the winding so the surface does not end up inside out.</summary>
        static Mesh MakeHand(bool mirror)
        {
            var m = new PuppetMesh();
            m.Revolve(PuppetMesh.Spline(new[]
            {
                new Vector2(0f, -.075f), new Vector2(.05f, -.068f), new Vector2(.075f, -.03f),
                new Vector2(.08f, .02f), new Vector2(.06f, .06f), new Vector2(0f, .075f)
            }, 4), Slim, new Vector2(1f, .52f), PuppetSkinTexture.Hand);

            var finger = new PuppetMesh();
            finger.Revolve(PuppetMesh.Bone(.045f, .022f, .019f, 0f, 3, 3), 9, new Vector2(1f, .85f), PuppetSkinTexture.Hand);
            for (int i = 0; i < 4; i++)
            {
                float x = -.048f + i * .032f;
                m.Append(finger, Matrix4x4.TRS(new Vector3(x, -.055f, .004f),
                    Quaternion.FromToRotation(Vector3.up, new Vector3(x * 3f, -1f, .12f).normalized), Vector3.one));
            }
            m.Append(finger, Matrix4x4.TRS(new Vector3(-.062f, -.012f, .02f),
                Quaternion.FromToRotation(Vector3.up, new Vector3(-.45f, -.80f, .40f).normalized), Vector3.one));

            if (!mirror) return m.ToMesh("PuppetHandR");
            var flipped = new PuppetMesh();
            flipped.Append(m, Matrix4x4.Scale(new Vector3(-1f, 1f, 1f)));
            return flipped.ToMesh("PuppetHandL");
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
