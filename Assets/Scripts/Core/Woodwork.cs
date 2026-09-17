using System;

namespace Height1079.Core
{
    /// <summary>Work with wood, held down for a few seconds: cut a dry branch off a tree with the saw or the axe, split the branch into
    /// firewood, feed the fire. The group carried three axes and a saw and cut "frail damp firs" for the 31 Jan fire (diary, labaz protocol);
    /// how long any of it took is of course a game balance, not a document.</summary>
    public enum WorkKind : byte { None = 0, CutBranch = 1, SplitBranch = 2, FeedFire = 3 }

    public static class Woodwork
    {
        /// <summary>How far a tree or a branch can be to work on it.</summary>
        public const float Reach = 2.6f;
        /// <summary>One dry branch gives this many logs.</summary>
        public const byte LogsPerBranch = 3;
        /// <summary>Each log put into the fire buys this much burning time.</summary>
        public const float SecondsPerLog = 45f;

        /// <summary>Seconds of steady work with this tool, cold hands make it slower. 0 = the tool cannot do it.</summary>
        public static float Seconds(WorkKind kind, ToolKind tool, float hands = 100f)
        {
            float basis;
            switch (kind)
            {
                case WorkKind.CutBranch:
                    basis = tool == ToolKind.Saw ? 7f : tool == ToolKind.Axe ? 10f : tool == ToolKind.Hatchet ? 16f : 0f;
                    break;
                case WorkKind.SplitBranch:
                    basis = tool == ToolKind.Axe ? 6f : tool == ToolKind.Hatchet ? 10f : tool == ToolKind.Saw ? 13f : 0f;
                    break;
                case WorkKind.FeedFire:
                    basis = 2f;
                    break;
                default: return 0f;
            }
            if (basis <= 0f) return 0f;
            // stiff hands work slower: up to half as fast again
            return basis * (1f + .5f * (1f - Math.Max(0f, Math.Min(100f, hands)) / 100f));
        }

        public static bool CanDo(WorkKind kind, ToolKind tool) => Seconds(kind, tool) > 0f;

        public static string Title(WorkKind kind, ToolKind tool) => kind switch
        {
            WorkKind.CutBranch => tool == ToolKind.Saw ? "Пилить ветку" : "Рубить ветку",
            WorkKind.SplitBranch => "Разрубить на дрова",
            WorkKind.FeedFire => "Подложить дров",
            _ => "",
        };
    }
}
