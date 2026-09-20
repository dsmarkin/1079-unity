using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
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
}
