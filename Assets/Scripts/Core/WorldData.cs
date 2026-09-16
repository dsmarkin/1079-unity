using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>Geography of the prototype. Port of public/world.js: one Terrarium tile, local metric projection, documented points.</summary>
    public static class WorldData
    {
        public const int Zoom = 13, TileX = 5448, TileY = 2296, Resolution = 256;
        /// <summary>Tile width in metres at this latitude; 1 world unit = 1 metre.</summary>
        public static readonly double Size = 40075016.686 * Math.Cos(61.762 * Math.PI / 180) / Math.Pow(2, Zoom);

        public static (float x, float z) Project(double lat, double lon)
        {
            double n = Math.Pow(2, Zoom);
            double x = ((lon + 180) / 360 * n - TileX - .5) * Size;
            double z = ((1 - Math.Log(Math.Tan(lat * Math.PI / 180) + 1 / Math.Cos(lat * Math.PI / 180)) / Math.PI) / 2 * n - TileY - .5) * Size;
            return ((float)x, (float)z);
        }

        public sealed class Poi
        {
            public string Id = "", Label = "", Source = "", Note = "";
            public double Lat, Lon;
            public float X, Z;
        }

        static Poi Row(string id, string label, double lat, double lon, string source, string note)
        {
            var (x, z) = Project(lat, lon);
            return new Poi { Id = id, Label = label, Lat = lat, Lon = lon, X = x, Z = z, Source = source, Note = note };
        }

        static double Dms(double d, double m, double s) => d + m / 60 + s / 3600;

        public static readonly IReadOnlyList<Poi> Pois = new List<Poi>
        {
            Row("tent", "Палатка · версия 2020", 61.75962, 59.43045, "table", "Спорная привязка. В публикации точность расстояний ±50 м."),
            Row("kolmogorova", "Зинаида Колмогорова", 61.76217, 59.44458, "table", "Место обнаружения: современная географическая реконструкция."),
            Row("slobodin", "Рустем Слободин", 61.76266, 59.44727, "table", "Место обнаружения: современная географическая реконструкция."),
            Row("dyatlov", "Игорь Дятлов", 61.76351, 59.45018, "table", "Место обнаружения: современная географическая реконструкция."),
            Row("cedar2020", "Кедр · версия 2020", 61.76494, 59.45541, "table", "Юрий Дорошенко и Юрий Кривонищенко. Альтернативная привязка."),
            Row("ravine2020", "Четверо · версия 2020", 61.76451, 59.45405, "table", "Расходится с полевой реконструкцией; не отдельное место гибели."),
            Row("cedar", "Кедр · GPS экспедиции", Dms(61, 45, 53.20), Dms(59, 27, 17.80), "field", "Район обнаружения Юрия Дорошенко и Юрия Кривонищенко. GPS KAN 2012."),
            Row("ravine", "Овраг · GPS экспедиции", Dms(61, 45, 53.93), Dms(59, 27, 14.64), "field", "Район обнаружения Людмилы Дубининой, Александра Колеватова, Семёна Золотарёва и Николая Тибо-Бриньоля. Индивидуальные GPS не установлены."),
            Row("den", "Ориентир P4 у настила", Dms(61, 45, 53.85), Dms(59, 27, 14.47), "field", "Камень P4: по экспедиции в 3 м выше настила по ручью. Положение настила отдельно не измерено."),
        };

        public static Poi Get(string id)
        {
            foreach (var p in Pois) if (p.Id == id) return p;
            throw new KeyNotFoundException(id);
        }

        public static readonly Poi Tent = Get("tent"), Cedar = Get("cedar"), Ravine = Get("ravine"), Den = Get("den");

        /// <summary>Fictional camp for the survival route; not a historical fire-site coordinate.</summary>
        public static readonly (float x, float z) Camp = (Cedar.X - 16f, Cedar.Z + 12f);
        public const float CampRadius = 5f, GoalRadius = 12f, ShelterHeight = 735f;

        /// <summary>Illustrative stream polyline used to carve the ravine incision.</summary>
        public static readonly (float x, float z)[] Creek =
        {
            Project(61.7646, 59.4544), (Den.X, Den.Z), (Ravine.X, Ravine.Z),
            Project(Dms(61, 45, 55.66), Dms(59, 27, 13.36)), Project(Dms(61, 45, 59.40), Dms(59, 27, 15.40)),
        };

        /// <summary>Bilinear sample of a 256×256 height grid at world coordinates; pixel centres, clamped edges.</summary>
        public static float Sample(float[] data, float x, float z)
        {
            double px = Math.Max(0, Math.Min(255, (x / Size + .5) * 256 - .5));
            double py = Math.Max(0, Math.Min(255, (z / Size + .5) * 256 - .5));
            int a = (int)Math.Floor(px), b = (int)Math.Floor(py), c = Math.Min(255, a + 1), d = Math.Min(255, b + 1);
            double u = px - a, v = py - b;
            return (float)((data[b * 256 + a] * (1 - u) + data[b * 256 + c] * u) * (1 - v) + (data[d * 256 + a] * (1 - u) + data[d * 256 + c] * u) * v);
        }

        public static float CreekDistance(float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 1; i < Creek.Length; i++)
            {
                var (ax, az) = Creek[i - 1];
                var (bx, bz) = Creek[i];
                float dx = bx - ax, dz = bz - az;
                float t = Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz)));
                float ex = x - ax - t * dx, ez = z - az - t * dz;
                best = Math.Min(best, (float)Math.Sqrt(ex * ex + ez * ez));
            }
            return best;
        }

        /// <summary>Ground height with the illustrative ravine incision (width/depth are not surveyed).</summary>
        public static float GroundHeight(float[] dem, float x, float z)
        {
            float d = CreekDistance(x, z);
            return Sample(dem, x, z) - 4f * (float)Math.Exp(-d * d / 55f);
        }

        public static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static bool NearCamp(float x, float z) => Distance(x, z, Camp.x, Camp.z) < CampRadius;
        public static bool AtGoal(float x, float z) => Distance(x, z, Tent.X, Tent.Z) < GoalRadius;
    }
}
