using System.IO;
using UnityEditor;
using UnityEngine;
using Height1079.Core;

namespace Height1079.EditorTools.World
{
    /// <summary>Simple models of the things that go into a rucksack (Core.ItemId): tins, paper packs, cloth bags, a hatchet, a flask, a pot,
    /// mittens, an armful of firewood. One prefab per item in Resources/World/Prefabs/Cargo/&lt;ItemId&gt;; pivot = bottom centre,
    /// about the size of the real thing. Placeholders until the food and gear are worked out.</summary>
    public static class CargoFactory
    {
        const string PrefabDir = WorldPaths.Generated + "/Prefabs/Cargo";
        const string MeshDir = WorldPaths.Generated + "/Meshes/Cargo";

        static Material tin, label, paper, cloth, wood, steel, felt, wool, lard, choc, candle;

        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir); Directory.CreateDirectory(MeshDir);
            tin = Materials.Get("CargoTin", new Color(.62f, .62f, .6f), smoothness: .55f);
            label = Materials.Get("CargoLabel", new Color(.55f, .18f, .14f), smoothness: .2f);
            paper = Materials.Get("CargoPaper", new Color(.74f, .68f, .55f), smoothness: .05f);
            cloth = Materials.Get("CargoCloth", new Color(.6f, .55f, .44f), smoothness: .03f);
            wood = Materials.Get("CargoWood", new Color(.55f, .42f, .3f), smoothness: .08f);
            steel = Materials.Get("CargoSteel", new Color(.25f, .25f, .26f), smoothness: .5f);
            felt = Materials.Get("CargoMitten", new Color(.22f, .2f, .19f), smoothness: .02f);
            wool = Materials.Get("CargoWool", new Color(.7f, .66f, .6f), smoothness: .02f);
            lard = Materials.Get("CargoLard", new Color(.9f, .86f, .8f), smoothness: .15f);
            choc = Materials.Get("CargoChocolate", new Color(.22f, .3f, .5f), smoothness: .25f);
            candle = Materials.Get("CargoCandle", new Color(.93f, .92f, .86f), smoothness: .2f);
            for (int i = 1; i < Items.Count; i++) Make((ItemId)i);
        }

        static void Make(ItemId id)
        {
            var root = new GameObject(id.ToString());
            var a = new MeshBuilder(1); var b = new MeshBuilder(1);
            Material ma = tin, mb = label;
            var rnd = new System.Random((int)id * 31);
            switch (id)
            {
                case ItemId.Stew:
                    a.Tube(0, Vector3.zero, new Vector3(0, .115f, 0), .05f, .05f, 16, 1f, 0, true);
                    b.Tube(0, new Vector3(0, .02f, 0), new Vector3(0, .095f, 0), .0505f, .0505f, 16);
                    break;
                case ItemId.CondensedMilk:
                    a.Tube(0, Vector3.zero, new Vector3(0, .08f, 0), .038f, .038f, 16, 1f, 0, true);
                    b.Tube(0, new Vector3(0, .012f, 0), new Vector3(0, .068f, 0), .0385f, .0385f, 16);
                    mb = choc;
                    break;
                case ItemId.Batteries:
                    for (int k = 0; k < 2; k++) a.Tube(0, new Vector3(k * .036f - .018f, 0, 0), new Vector3(k * .036f - .018f, .06f, 0), .017f, .017f, 12, 1f, 0, true);
                    b.Box(0, new Vector3(0, .03f, 0), new Vector3(.075f, .04f, .036f), Quaternion.identity);
                    mb = paper;
                    break;
                case ItemId.Rusks:
                case ItemId.Sugar:
                case ItemId.Oatmeal:
                    // cloth bag, tied at the neck
                    var size = id == ItemId.Rusks ? new Vector3(.12f, .14f, .09f) : new Vector3(.09f, .1f, .07f);
                    Surf.Lump(a, 0, new Vector3(0, size.y * .9f, 0), size, (int)id * 7, .18f, null, 0f, true);
                    b.Tube(0, new Vector3(0, size.y * 1.8f, 0), new Vector3(0, size.y * 2.05f, 0), .02f, .03f, 8);
                    ma = cloth; mb = id == ItemId.Sugar ? paper : cloth;
                    break;
                case ItemId.Lard:
                    Surf.Lump(a, 0, new Vector3(0, .035f, 0), new Vector3(.08f, .035f, .06f), 5, .12f, null, 0f, true);
                    b.Box(0, new Vector3(0, .02f, 0), new Vector3(.17f, .04f, .02f), Quaternion.identity);
                    ma = lard; mb = paper;
                    break;
                case ItemId.Chocolate:
                    a.Box(0, new Vector3(0, .006f, 0), new Vector3(.08f, .012f, .16f), Quaternion.identity);
                    b.Box(0, new Vector3(0, .0065f, 0), new Vector3(.081f, .0125f, .06f), Quaternion.identity);
                    ma = choc; mb = paper;
                    break;
                case ItemId.Matches:
                    a.Box(0, new Vector3(0, .008f, 0), new Vector3(.05f, .016f, .035f), Quaternion.identity);
                    b.Box(0, new Vector3(0, .0082f, 0), new Vector3(.051f, .0164f, .02f), Quaternion.identity);
                    ma = paper; mb = label;
                    break;
                case ItemId.Candles:
                    for (int k = 0; k < 5; k++) a.Tube(0, new Vector3((k - 2) * .022f, .011f, -.09f), new Vector3((k - 2) * .022f, .011f, .09f), .01f, .01f, 8, 1f, 0, true);
                    b.Box(0, new Vector3(0, .012f, 0), new Vector3(.12f, .026f, .04f), Quaternion.identity);
                    ma = candle; mb = paper;
                    break;
                case ItemId.Hatchet:
                    a.Tube(0, new Vector3(0, .02f, -.2f), new Vector3(0, .02f, .18f), .016f, .014f, 8, 1f, 0, true);
                    b.Box(0, new Vector3(0, .02f, .17f), new Vector3(.02f, .04f, .07f), Quaternion.identity);
                    b.Box(0, new Vector3(.055f, .02f, .17f), new Vector3(.09f, .03f, .09f), Quaternion.Euler(0, 0, 0));
                    ma = wood; mb = steel;
                    break;
                case ItemId.Flask:
                    a.Tube(0, Vector3.zero, new Vector3(0, .17f, 0), .045f, .045f, 16, 1f, 0, false);
                    a.Tube(0, new Vector3(0, .17f, 0), new Vector3(0, .2f, 0), .045f, .015f, 16);
                    b.Tube(0, new Vector3(0, .2f, 0), new Vector3(0, .225f, 0), .016f, .016f, 10, 1f, 0, true);
                    ma = Materials.Get("CargoFlask", new Color(.72f, .72f, .68f), smoothness: .7f); mb = steel;
                    break;
                case ItemId.Pot:
                    a.Tube(0, Vector3.zero, new Vector3(0, .15f, 0), .09f, .095f, 20);
                    a.Tube(0, new Vector3(0, .002f, 0), Vector3.zero, .09f, .09f, 20, 1f, 0, true);
                    var arc = new Vector3[9];
                    for (int k = 0; k < 9; k++) { float t = k / 8f * Mathf.PI; arc[k] = new Vector3(Mathf.Cos(t) * .095f, .15f + Mathf.Sin(t) * .08f, 0); }
                    for (int k = 0; k < 8; k++) b.Tube(0, arc[k], arc[k + 1], .003f, .003f, 5);
                    ma = Materials.Get("CargoSootyPot", new Color(.18f, .17f, .16f), smoothness: .25f); mb = steel;
                    break;
                case ItemId.Mittens:
                    for (int k = 0; k < 2; k++)
                    {
                        Surf.Lump(a, 0, new Vector3(k * .1f - .05f, .02f, 0), new Vector3(.045f, .02f, .1f), 20 + k, .15f, null, 0f, true);
                        Surf.Lump(b, 0, new Vector3(k * .1f - .05f + (k == 0 ? -.035f : .035f), .02f, .02f), new Vector3(.015f, .015f, .035f), 30 + k, .1f, null, 0f, true);
                    }
                    ma = felt; mb = felt;
                    break;
                case ItemId.Socks:
                    Surf.Lump(a, 0, new Vector3(0, .025f, 0), new Vector3(.05f, .025f, .1f), 40, .2f, null, 0f, true);
                    Surf.Lump(b, 0, new Vector3(.02f, .04f, .05f), new Vector3(.03f, .02f, .04f), 41, .2f, null, 0f, true);
                    ma = wool; mb = wool;
                    break;
                case ItemId.Axe:
                    // felling axe: a long curved helve and a broad head
                    a.Tube(0, new Vector3(0, .03f, -.34f), new Vector3(0, .03f, .3f), .019f, .016f, 8, 1f, 0, true);
                    b.Box(0, new Vector3(0, .03f, .3f), new Vector3(.022f, .05f, .09f), Quaternion.identity);
                    b.Box(0, new Vector3(.075f, .03f, .3f), new Vector3(.13f, .035f, .12f), Quaternion.identity);
                    b.Box(0, new Vector3(.14f, .03f, .3f), new Vector3(.012f, .026f, .14f), Quaternion.identity);
                    ma = wood; mb = steel;
                    break;
                case ItemId.Saw:
                    // two-handed saw: a long toothed blade with a wooden grip at each end
                    b.Box(0, new Vector3(0, .12f, 0), new Vector3(.004f, .16f, 1.1f), Quaternion.identity);
                    for (int k = 0; k < 44; k++)
                    {
                        float z = -.54f + k * .025f;
                        b.Box(0, new Vector3(0, .038f, z), new Vector3(.004f, .022f, .012f), Quaternion.Euler(20, 0, 0));
                    }
                    foreach (int side in new[] { -1, 1 })
                        a.Tube(0, new Vector3(0, .2f, side * .6f), new Vector3(0, .04f, side * .62f), .018f, .018f, 8, 1f, 0, true);
                    ma = wood; mb = steel;
                    break;
                case ItemId.Branch:
                    // a dry branch with a few side twigs
                    a.Tube(0, new Vector3(0, .06f, -.75f), new Vector3(.05f, .06f, .75f), .035f, .022f, 7, 1f, 0, true);
                    for (int k = 0; k < 4; k++)
                    {
                        float z = -.5f + k * .35f, dir = k % 2 == 0 ? 1f : -1f;
                        b.Tube(0, new Vector3(.02f, .06f, z), new Vector3(.02f + dir * .22f, .12f + .05f * k, z + .18f), .012f, .004f, 5, 1f, 0, true);
                    }
                    ma = Materials.Bark; mb = Materials.Bark;
                    break;
                case ItemId.Firewood:
                    for (int k = 0; k < 6; k++)
                    {
                        float x = (k % 3 - 1) * .09f + (float)rnd.NextDouble() * .02f, y = .04f + (k / 3) * .075f;
                        a.Tube(0, new Vector3(x, y, -.3f), new Vector3(x + (float)rnd.NextDouble() * .03f, y, .3f), .04f, .036f, 8, 1f, 0, true);
                        a.Tube(0, new Vector3(x, y, -.3f), new Vector3(x, y, -.301f), .04f, .04f, 8, 1f, 0, true);
                    }
                    ma = Materials.Bark; mb = wood;
                    break;
            }
            if (a.VertexCount > 0) Part(root.transform, "A", a, ma);
            if (b.VertexCount > 0) Part(root.transform, "B", b, mb);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{id}.prefab");
            Object.DestroyImmediate(root);
        }

        static void Part(Transform parent, string name, MeshBuilder mb, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            string mname = $"{parent.name}_{name}";
            var mesh = mb.ToMesh(mname);
            string path = $"{MeshDir}/{mname}.asset";
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
