using System.Collections.Generic;
using UnityEngine;
using Height1079.Core;

namespace Height1079.Runtime
{
    /// <summary>Draws what the rucksack lists say: a canvas rucksack on every wearer's back or standing in the snow, loose items lying
    /// on the slope, and whatever a hiker carries in their hands (in the first person it is held in front of the camera).</summary>
    public sealed class PackView : MonoBehaviour
    {
        readonly Dictionary<int, Transform> packs = new Dictionary<int, Transform>();
        readonly Dictionary<int, Transform> loose = new Dictionary<int, Transform>();
        readonly Dictionary<ulong, Transform> hands = new Dictionary<ulong, Transform>();
        readonly Dictionary<ulong, ItemId> handKind = new Dictionary<ulong, ItemId>();
        readonly List<int> gone = new List<int>();
        NightSession subscribed;
        static GameObject rucksack;

        public static PackView Create()
        {
            var go = new GameObject("PackView", typeof(PackView));
            DontDestroyOnLoad(go);
            return go.GetComponent<PackView>();
        }

        static GameObject Rucksack => rucksack != null ? rucksack : rucksack = Resources.Load<GameObject>("World/Prefabs/Items/Rucksack");
        static GameObject Cargo(ItemId id) => Resources.Load<GameObject>("World/Prefabs/Cargo/" + id);

        Transform Spawn(GameObject prefab, string name)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, transform);
            go.name = name;
            return go.transform;
        }

        void LateUpdate()
        {
            var s = NightSession.Instance;
            if (s == null) return;
            if (subscribed != s)
            {
                if (subscribed != null) subscribed.PackAnswer -= Backpacks.Note;
                s.PackAnswer += Backpacks.Note;
                subscribed = s;
            }
            DrawPacks(s);
            DrawLoose(s);
            DrawHands();
        }

        void DrawPacks(NightSession s)
        {
            gone.Clear(); gone.AddRange(packs.Keys);
            for (int i = 0; i < s.PackList.Count; i++)
            {
                var p = s.PackList[i];
                gone.Remove(p.Id);
                if (!packs.TryGetValue(p.Id, out var t) || t == null)
                {
                    t = Spawn(Rucksack, "Pack" + p.Id);
                    if (t == null) continue;
                    packs[p.Id] = t;
                }
                if (p.Wearer == PackNet.NoWearer)
                {
                    if (t.parent != transform) t.SetParent(transform, false);
                    t.SetPositionAndRotation(p.Pos, Quaternion.Euler(0, p.Yaw, 0));
                    t.localScale = Vector3.one;
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    continue;
                }
                var h = HikerController.ByClient(p.Wearer);
                var back = h != null ? h.transform.Find("Visual") : null;
                if (back == null) { if (t.gameObject.activeSelf) t.gameObject.SetActive(false); continue; }
                if (t.parent != back) t.SetParent(back, false);
                // on the back: the straps (+Z of the model) toward the hiker, the body standing on the shoulder blades
                t.localPosition = new Vector3(0, .92f, -.36f);
                t.localRotation = Quaternion.Euler(-8f, 0, 0);
                t.localScale = Vector3.one;
                bool show = back.gameObject.activeSelf;
                if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
            }
            foreach (var id in gone) { if (packs.TryGetValue(id, out var t) && t != null) Destroy(t.gameObject); packs.Remove(id); }
        }

        void DrawLoose(NightSession s)
        {
            gone.Clear(); gone.AddRange(loose.Keys);
            for (int i = 0; i < s.LooseList.Count; i++)
            {
                var l = s.LooseList[i];
                gone.Remove(l.Id);
                if (loose.TryGetValue(l.Id, out var t) && t != null)
                {
                    t.SetPositionAndRotation(l.Pos, Quaternion.Euler(0, l.Yaw, 0));
                    continue;
                }
                t = Spawn(Cargo((ItemId)l.Item.Item), "Loose" + l.Id);
                if (t == null) continue;
                t.SetPositionAndRotation(l.Pos, Quaternion.Euler(0, l.Yaw, 0));
                loose[l.Id] = t;
            }
            foreach (var id in gone) { if (loose.TryGetValue(id, out var t) && t != null) Destroy(t.gameObject); loose.Remove(id); }
        }

        void DrawHands()
        {
            var cam = Camera.main;
            var me = Bootstrap.LocalHiker;
            foreach (var h in HikerController.All)
            {
                if (h == null) continue;
                var id = (ItemId)h.Carried.Value.Item;
                ulong key = h.OwnerClientId;
                if (handKind.TryGetValue(key, out var was) && was != id && hands.TryGetValue(key, out var old))
                {
                    if (old != null) Destroy(old.gameObject);
                    hands.Remove(key);
                }
                handKind[key] = id;
                if (id == ItemId.None) { if (hands.TryGetValue(key, out var t0) && t0 != null) t0.gameObject.SetActive(false); continue; }
                if (!hands.TryGetValue(key, out var t) || t == null)
                {
                    t = Spawn(Cargo(id), "Hand" + key);
                    if (t == null) continue;
                    hands[key] = t;
                }
                bool mine = me != null && h == me;
                bool firstPerson = mine && me.FirstPerson && cam != null && WorldDressing.ViewIndex < 0;
                if (firstPerson)
                {
                    t.SetParent(cam.transform, false);
                    t.localPosition = new Vector3(.24f, -.34f, .62f);
                    t.localRotation = Quaternion.Euler(6f, -18f, 4f);
                    t.localScale = Vector3.one * .6f;
                }
                else
                {
                    var visual = h.transform.Find("Visual");
                    t.SetParent(visual != null ? visual : h.transform, false);
                    t.localPosition = new Vector3(.27f, 1.02f, .26f);
                    t.localRotation = Quaternion.Euler(0, 12f, 0);
                    t.localScale = Vector3.one;
                }
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.PackAnswer -= Backpacks.Note;
            foreach (var t in hands.Values) if (t != null) Destroy(t.gameObject);
        }
    }
}
