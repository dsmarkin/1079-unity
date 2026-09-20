using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Height1079.Art;
using Height1079.EditorTools.World;

/// <summary>The palette (docs/art/palette.json) and the table of imported models that paints with it. Unity only:
/// the sheet is read through UnityEngine, so this file is excluded from Tools/CoreTests.</summary>
public class PaletteTests
{
    [Test]
    public void SheetLoadsAndEveryNameTheCodeUsesIsOnIt()
    {
        Assert.GreaterOrEqual(Palette.All.Count, 20, "sheet is short");
        var names = new HashSet<string>();
        foreach (var s in Palette.All) Assert.IsTrue(names.Add(s.Name), "duplicate colour name " + s.Name);
        foreach (var n in new[] { Palette.SnowLit, Palette.SnowShade, Palette.NeedlesLit, Palette.NeedlesShade, Palette.Bark, Palette.Birch, Palette.Stone,
                                  Palette.Quilt, Palette.Anorak, Palette.Canvas, Palette.Felt, Palette.Wood, Palette.Metal, Palette.Enamel,
                                  Palette.Red, Palette.FireCore, Palette.FireEdge, Palette.TorchLight, Palette.Black })
            Assert.IsTrue(Palette.Has(n), "no colour «" + n + "» on the sheet");
    }

    [Test]
    public void EveryColourIsItsOwnNearest()
    {
        foreach (var s in Palette.All) Assert.AreEqual(s.Name, Palette.NearestName(s.Colour), s.Hex);
    }

    [Test]
    public void NearestJudgesByEye()
    {
        Assert.AreEqual(Palette.SnowLit, Palette.NearestName(Color.white));
        Assert.AreEqual(Palette.Black, Palette.NearestName(new Color(.1f, .08f, .06f)));
        // a fir green from a model snaps to needles, not to the jacket
        Assert.AreEqual(Palette.NeedlesLit, Palette.NearestName(new Color(.25f, .45f, .33f)));
    }

    [Test]
    public void ImportedTableNamesRealColoursAndFiles()
    {
        foreach (var e in ImportedFactory.Table)
        {
            Assert.IsTrue(File.Exists(e.Path), e.Name + ": no model at " + e.Path);
            Assert.Greater(e.Size, 0f, e.Name);
            foreach (var (match, colour) in e.Colours) Assert.IsTrue(Palette.Has(colour), e.Name + ": «" + colour + "» for " + match);
            if (e.SnowOn != null) foreach (var c in e.SnowOn) Assert.IsTrue(Palette.Has(c), e.Name + ": snow on «" + c + "»");
        }
    }

    /// <summary>Everything the small map can put on itself comes out of the one table, so the table has to hold the
    /// things that map needs by name. These are the ones it asks for and cannot draw itself: the yard loads them by
    /// these exact strings (SandboxHuntYard), and a rename here would go unnoticed until a run had no stove in it.</summary>
    [Test]
    public void ImportedTableHasEverythingTheSmallMapPlaces()
    {
        var names = new HashSet<string>();
        foreach (var e in ImportedFactory.Table) Assert.IsTrue(names.Add(e.Name), "duplicate prefab name " + e.Name);
        foreach (var n in new[] { "Tent", "Bonfire", "WoodLog", "Windfall", "TentRoll", "Backpack", "Axe", "Pot", "Can", "Flashlight", "Bedroll",
                                  "Pine_Snow_1", "Pine_Snow_2", "Pine_Snow_3", "Rock_Snow_1", "Rock_Snow_2", "Rock_Snow_3",
                                  "Birch_Snow_1", "Birch_Snow_2", "DeadTree_Snow_1",
                                  "Stove", "StovePipe", "SnowMound", "Firewood", "Rusks", "Candles", "Skis", "Diary", "PhotoCamera" })
            Assert.IsTrue(names.Contains(n), "площадке нужен префаб «" + n + "», а таблица его не собирает");
    }

    /// <summary>One material for the whole imported set — that is the point of baking the colour into the vertices,
    /// and it is what makes "no paint from outside the palette" checkable on the small map at run time
    /// (SandboxRange.Audit, docs/SANDBOX.md §12). A prefab that came out on a material of its own would be exactly
    /// the patchwork this was all meant to end, so it fails here rather than in a frame.
    ///
    /// The set is only there once the editor has baked it; a checkout that has not generated anything skips.</summary>
    [Test]
    public void EveryImportedPrefabSharesTheOnePaletteMaterial()
    {
        if (!ImportedFactory.IsBuilt) Assert.Ignore("набор ещё не собран (1079 → Rebuild world)");
        var shared = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterials.MaterialPath);
        Assert.IsNotNull(shared, "нет общего материала " + PaletteMaterials.MaterialPath);
        Assert.IsTrue(Palette.IsPalette(shared), "общий материал назван «" + shared.name + "», а проверка карты ищет «" + Palette.MaterialName + "»");
        int checkedPrefabs = 0;
        foreach (var e in ImportedFactory.Table)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ImportedFactory.PrefabDir}/{e.Name}.prefab");
            if (prefab == null) continue;                 // no source model on this machine: the factory warned, move on
            checkedPrefabs++;
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var m in r.sharedMaterials)
                    Assert.AreSame(shared, m, e.Name + ": «" + (m == null ? "пусто" : m.name) + "» вместо общего материала палитры");
        }
        Assert.Greater(checkedPrefabs, 0, "собранных префабов не найдено в " + ImportedFactory.PrefabDir);
    }
}
