#if !HEIGHT1079_NO_ELBRUS
// Compiled only when the southern slope of Elbrus is in the build (docs/ELBRUS.md). It stays in
// Height1079.Editor because it is built out of the private parts of its own factory, and a partial class
// cannot be split across assemblies; the location's editor assembly calls it from there.
using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    public static partial class TreeFactory
    {
        /// <summary>Summer trees of the Baksan valley: Scots pine (сосна Коха) with its high open crown and a few mountain
        /// birches. No snow on the branches, no snow skirt — on the Elbrus map it is July, and the forest around Azau is green.</summary>
        public static List<Prototype> BuildElbrusLibrary()
        {
            EnsureMaterials();
            var pineNeedles = Materials.Get("NeedlesCaucasusPine", Color.white,
                TextureFactory.NeedleSpray("needles_caucasus_pine", 71, 19f, new Color(.09f, .16f, .1f), new Color(.31f, .42f, .24f), true), null, .08f, true, .4f);
            var pineBark = Materials.PH("BarkCaucasusPine", "pine_bark", new Color(.72f, .5f, .36f), 1f);
            var farPine = Materials.Get("FarCaucasusPine", new Color(.14f, .2f, .13f), smoothness: .02f);
            var list = new List<Prototype>();
            void Add(string name, ConiferSpec s, Material bark, Material leaf, Material far)
                => list.Add(new Prototype { Species = TreeSpecies.SiberianPine, Form = TreeForm.Normal, Height = s.Height,
                    Prefab = SavePrefab(name, Conifer(s, 1), Conifer(s, .4f), FarConifer(s), bark, leaf, far, s.TrunkRadius, s.Height) });

            for (int v = 0; v < 3; v++)
                Add($"ElbPine_{v}", new ConiferSpec
                {
                    Form = Form.SiberianPine, Seed = 700 + v, Height = 14f + 2f * v, MaxBranch = 2.8f + .3f * v, PerWhorl = 6,
                    Whorl = .55f, Droop = 2f, TipLift = 26f, CardWidth = 1f, TrunkRadius = .24f + .03f * v, CrownBase = .35f + .05f * v,
                    SnowAmount = 0f, Buried = 0f, Shoots = 2, Lichen = .12f, Leaders = 1 + v % 2, DeadTwigs = true,
                }, pineBark, pineNeedles, farPine);
            // the last trees at the forest edge: low, wind-shaped, several leaders
            Add("ElbPineFlagged_0", new ConiferSpec
            {
                Form = Form.SiberianPine, Seed = 710, Height = 9f, MaxBranch = 2.4f, PerWhorl = 5, Whorl = .5f, Droop = 4f,
                TipLift = 30f, CardWidth = .9f, TrunkRadius = .19f, CrownBase = .22f, SnowAmount = 0f, Buried = 0f,
                Shoots = 2, Lichen = .3f, Leaders = 3, Flagged = true,
            }, pineBark, pineNeedles, farPine);
            return list;
        }
    }
}
#endif
