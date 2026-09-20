using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Height1079.Core;
using Height1079.Sandbox;

/// <summary>The yard's arrangement as data (StreamingAssets/sandbox/yard.json, docs/SANDBOX.md §11): a missing file
/// is the yard the code was written with, a broken file says so in the log and is the same yard, and a good file is
/// obeyed. Unity only — the loader reads through UnityEngine, so this file is excluded from Tools/CoreTests.</summary>
public class YardLayoutTests
{
    readonly List<string> temps = new List<string>();

    string Temp(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), "1079-yard-" + System.Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        temps.Add(path);
        return path;
    }

    [TearDown]
    public void Clean()
    {
        foreach (var p in temps) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
        temps.Clear();
        SandboxYardLayout.Forget();
    }

    static void At(Vector3 want, Vector3 got, string what)
    {
        Assert.AreEqual(want.x, got.x, 1e-3f, what + " x");
        Assert.AreEqual(want.y, got.y, 1e-3f, what + " y");
        Assert.AreEqual(want.z, got.z, 1e-3f, what + " z");
    }

    static void AssertIsBuiltIn(SandboxYardLayout l)
    {
        var code = new SandboxYardLayout();
        Assert.AreEqual(code.size, l.size, 1e-4f, "размер двора");
        Assert.AreEqual(code.corridor, l.corridor, 1e-4f, "ширина прохода");
        Assert.AreEqual(code.seed, l.seed, "зерно");
        Assert.AreEqual(code.cover.Count, l.cover.Count, "укрытий");
        Assert.AreEqual(code.gear.Count, l.gear.Count, "вещей у входа");
        Assert.AreEqual(code.dressing.Count, l.dressing.Count, "деревьев по краям");
        Assert.AreEqual(code.fire.z, l.fire.z, 1e-4f, "костёр");
        Assert.AreEqual(code.tent.z, l.tent.z, 1e-4f, "палатка");
        Assert.AreEqual(code.labaz.x, l.labaz.x, 1e-4f, "лабаз");
    }

    [Test]
    public void BuiltInPlanIsTheYardThatWasInTheCode()
    {
        var code = new SandboxYardLayout();
        Assert.IsNull(code.Fault(), "встроенный двор сам себе негоден");
        Assert.AreEqual(HuntRules.YardSize, code.size, 1e-4f, "двор не того размера, что правила");
        Assert.AreEqual(17, code.cover.Count);
        Assert.AreEqual(9, code.gear.Count);
        Assert.AreEqual(5, code.dressing.Count);
        Assert.AreEqual(1.1f, code.corridor, 1e-4f);
        // the fire at one edge, the tent at the other, the labaz east of the line between them
        Assert.Less(code.fire.z, 0f);
        Assert.Greater(code.tent.z, 0f);
        Assert.Greater(code.labaz.x, 0f);
        foreach (var c in code.cover) Assert.IsTrue(SandboxYardLayout.TryKind(c.kind, out _), c.kind);
    }

    [Test]
    public void MissingFileIsTheBuiltInPlan()
    {
        var l = SandboxYardLayout.ReadFile(Path.Combine(Path.GetTempPath(), "1079-yard-no-such-file.json"));
        AssertIsBuiltIn(l);
        Assert.IsNull(SandboxYardLayout.Source, "источником назван файл, которого нет");
    }

    [Test]
    public void BrokenFileWarnsAndIsTheBuiltInPlan()
    {
        LogAssert.Expect(LogType.Warning, new Regex("по умолчанию из кода"));
        var l = SandboxYardLayout.ReadFile(Temp("{ это не json, а записка "));
        AssertIsBuiltIn(l);
        Assert.IsNull(SandboxYardLayout.Source);
    }

    [Test]
    public void NonsenseNumbersWarnAndAreTheBuiltInPlan()
    {
        LogAssert.Expect(LogType.Warning, new Regex("размер двора"));
        AssertIsBuiltIn(SandboxYardLayout.ReadFile(Temp("{\"size\":0}")));

        LogAssert.Expect(LogType.Warning, new Regex("бывают rock, spruce, windfall"));
        AssertIsBuiltIn(SandboxYardLayout.ReadFile(Temp("{\"cover\":[{\"kind\":\"валун\",\"x\":1,\"z\":1,\"height\":2}]}")));
    }

    [Test]
    public void GoodFileOverridesThePlan()
    {
        string path = Temp("{\"name\":\"тесный двор\",\"size\":40,\"corridor\":1.6,\"seed\":7," +
                           "\"origin\":{\"x\":5,\"z\":-100,\"yaw\":0}," +
                           "\"fire\":{\"x\":1,\"z\":-15,\"yaw\":90}," +
                           "\"tent\":{\"x\":-2,\"z\":15,\"yaw\":180}," +
                           "\"labaz\":{\"x\":12,\"z\":0,\"yaw\":45}," +
                           "\"spawn\":{\"x\":0,\"y\":1,\"z\":2}," +
                           "\"cover\":[{\"kind\":\"spruce\",\"x\":3,\"z\":4,\"height\":9}]," +
                           "\"gear\":[{\"name\":\"Pot\",\"ahead\":2,\"across\":-1,\"yaw\":10}]," +
                           "\"dressing\":[]}");
        var l = SandboxYardLayout.ReadFile(path);
        Assert.AreEqual(path, SandboxYardLayout.Source, "файл прочитан, но не назван источником");
        Assert.AreEqual("тесный двор", l.name);
        Assert.AreEqual(40f, l.size, 1e-4f);
        Assert.AreEqual(1.6f, l.corridor, 1e-4f);
        Assert.AreEqual(7, l.seed);
        Assert.AreEqual(1, l.cover.Count);
        Assert.AreEqual("spruce", l.cover[0].kind);
        Assert.AreEqual(9f, l.cover[0].height, 1e-4f);
        Assert.AreEqual(1, l.gear.Count);
        Assert.AreEqual("Pot", l.gear[0].name);
        Assert.AreEqual(0, l.dressing.Count, "пустой список — это «ничего», а не «как в коде»");
        Assert.AreEqual(45f, l.labaz.yaw, 1e-4f);

        // and the yard answers with the file's places, not the code's
        SandboxYardLayout.Use(l, path);
        At(new Vector3(6f, 0f, -115f), SandboxHuntYard.Fire, "костёр");
        At(new Vector3(3f, 0f, -85f), SandboxHuntYard.Tent, "палатка");
        At(new Vector3(17f, 0f, -100f), SandboxHuntYard.Labaz, "лабаз");
        At(new Vector3(6f, 1f, -113f), SandboxHuntYard.Spawn, "старт");
        Assert.AreEqual(40f, SandboxHuntYard.Size, 1e-4f);
        Assert.AreEqual(1.6f, SandboxHuntYard.Corridor, 1e-4f);
    }

    [Test]
    public void AFileMayNameOneNumberAndKeepTheRest()
    {
        var l = SandboxYardLayout.ReadFile(Temp("{\"corridor\":1.4}"));
        Assert.AreEqual(1.4f, l.corridor, 1e-4f);
        Assert.AreEqual(17, l.cover.Count, "остальное должно остаться как в коде");
        Assert.AreEqual(HuntRules.YardSize, l.size, 1e-4f);
    }

    [Test]
    public void TheShippedFileIsTodaysYard()
    {
        string path = SandboxYardLayout.FilePath;
        Assert.IsTrue(File.Exists(path), "нет " + path + " — плеер останется с двором из кода");
        var shipped = SandboxYardLayout.ReadFile(path);
        Assert.AreEqual(path, SandboxYardLayout.Source, "файл в StreamingAssets не читается");
        AssertIsBuiltIn(shipped);
        var code = new SandboxYardLayout();
        for (int i = 0; i < code.cover.Count; i++)
        {
            Assert.AreEqual(code.cover[i].kind, shipped.cover[i].kind, "укрытие " + i);
            Assert.AreEqual(code.cover[i].x, shipped.cover[i].x, 1e-4f, "укрытие " + i + " x");
            Assert.AreEqual(code.cover[i].z, shipped.cover[i].z, 1e-4f, "укрытие " + i + " z");
            Assert.AreEqual(code.cover[i].height, shipped.cover[i].height, 1e-4f, "укрытие " + i + " высота");
        }
        for (int i = 0; i < code.gear.Count; i++)
        {
            Assert.AreEqual(code.gear[i].name, shipped.gear[i].name, "вещь " + i);
            Assert.AreEqual(code.gear[i].ahead, shipped.gear[i].ahead, 1e-4f, "вещь " + i + " вперёд");
            Assert.AreEqual(code.gear[i].across, shipped.gear[i].across, 1e-4f, "вещь " + i + " вбок");
            Assert.AreEqual(code.gear[i].yaw, shipped.gear[i].yaw, 1e-4f, "вещь " + i + " поворот");
        }
        for (int i = 0; i < code.dressing.Count; i++)
        {
            Assert.AreEqual(code.dressing[i].name, shipped.dressing[i].name, "дерево " + i);
            Assert.AreEqual(code.dressing[i].x, shipped.dressing[i].x, 1e-4f, "дерево " + i + " x");
            Assert.AreEqual(code.dressing[i].height, shipped.dressing[i].height, 1e-4f, "дерево " + i + " высота");
        }
        Assert.AreEqual(code.fire.yaw, shipped.fire.yaw, 1e-4f, "поворот костра");
        Assert.AreEqual(code.tent.yaw, shipped.tent.yaw, 1e-4f, "поворот палатки");
        Assert.AreEqual(code.labaz.yaw, shipped.labaz.yaw, 1e-4f, "поворот лабаза");
        Assert.AreEqual(code.spawn.x, shipped.spawn.x, 1e-4f, "старт x");
        Assert.AreEqual(code.spawn.y, shipped.spawn.y, 1e-4f, "старт y");
        Assert.AreEqual(code.spawn.z, shipped.spawn.z, 1e-4f, "старт z");
        Assert.AreEqual(code.origin.x, shipped.origin.x, 1e-4f, "центр x");
        Assert.AreEqual(code.origin.z, shipped.origin.z, 1e-4f, "центр z");
    }
}
