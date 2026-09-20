using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Height1079.EditorTools
{
    /// <summary>Turning an optional map on and off. The map itself is a set of assemblies carrying
    /// <c>"defineConstraints": ["!HEIGHT1079_NO_ELBRUS"]</c>; this writes that symbol into the player settings and
    /// waits for the recompile. There is nothing else to it — the assemblies disappear, the location never registers,
    /// the menu stops offering it, and <c>WorldKitBuilder</c> leaves its 136 MB of generated mountain out of the
    /// index and off the disk (docs/ELBRUS.md).
    ///
    /// <b>Two builds made either way cannot play together.</b> The set of <c>NetworkVariable</c>s on the session and
    /// on the player object differs, so the host and the guest would disagree about the shape of every packet.</summary>
    public static class LocationSwitch
    {
        public const string NoElbrus = "HEIGHT1079_NO_ELBRUS";

        [MenuItem("1079/Локация Эльбрус/Включить", priority = 200)]
        public static void EnableElbrus() => Set(false);

        [MenuItem("1079/Локация Эльбрус/Выключить", priority = 201)]
        public static void DisableElbrus() => Set(true);

        /// <summary>A tick beside whichever line is true right now.</summary>
        [MenuItem("1079/Локация Эльбрус/Включить", true)]
        static bool EnableValidate() { Menu.SetChecked("1079/Локация Эльбрус/Включить", !IsOff); return true; }

        [MenuItem("1079/Локация Эльбрус/Выключить", true)]
        static bool DisableValidate() { Menu.SetChecked("1079/Локация Эльбрус/Выключить", IsOff); return true; }

        /// <summary>Whether this project is set to build without the location.</summary>
        public static bool IsOff => Symbols().Contains(NoElbrus);

        /// <summary>Writes the symbol for every build target group the project can build, so a Mac build and a
        /// Windows build made from the same checkout are the same game.</summary>
        public static void Set(bool off)
        {
            if (off == IsOff)
            {
                Debug.Log($"1079: Эльбрус уже {(off ? "выключен" : "включён")}, ничего не меняю.");
                return;
            }
            foreach (var target in Targets())
            {
                var list = Symbols(target).ToList();
                if (off) { if (!list.Contains(NoElbrus)) list.Add(NoElbrus); }
                else list.RemoveAll(s => s == NoElbrus);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            }
            Debug.Log($"1079: локация Эльбрус {(off ? "ВЫКЛЮЧЕНА" : "ВКЛЮЧЕНА")} — {NoElbrus} {(off ? "поставлен" : "снят")}. Идёт перекомпиляция.");
            AssetDatabase.Refresh();
            CompilationPipelineRequest();
        }

        static void CompilationPipelineRequest()
        {
            // in the editor the recompile happens on its own; in batch mode nobody is going to ask for it
            if (!Application.isBatchMode) return;
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        }

        static IEnumerable<NamedBuildTarget> Targets()
        {
            yield return NamedBuildTarget.Standalone;
        }

        static IEnumerable<string> Symbols(NamedBuildTarget? target = null)
        {
            var t = target ?? NamedBuildTarget.Standalone;
            string raw = PlayerSettings.GetScriptingDefineSymbols(t) ?? "";
            return raw.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
