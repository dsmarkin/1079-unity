using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>Geography of the playable area: 4096 × 4096 m around Kholat Syakhl, 1 unit = 1 metre.
    /// Frame: x = east, z = north (Unity's left-handed frame with y up — not mirrored), y = metres above sea level.
    /// Projection: local ellipsoidal equirectangular about the origin (WGS84); scale error &lt; 0.1 m across the area.
    /// Every point carries its published source; disagreements are kept as separate points (docs/MAP.md).</summary>
    public static class WorldData
    {
        public const double OriginLat = 61.756, OriginLon = 59.4425;
        /// <summary>Side of the square area, metres.</summary>
        public static readonly float Size = HeightField.Half * 2;
        /// <summary>Height range of height_2049.r16 (must match Tools/terrain/package.py output).</summary>
        public const float HeightMin = 496f, HeightMax = 1096f;

        const double A = 6378137.0, F = 1 / 298.257223563, E2 = F * (2 - F), Deg = Math.PI / 180;
        static readonly double M0 = A * (1 - E2) / Math.Pow(1 - E2 * Math.Pow(Math.Sin(OriginLat * Deg), 2), 1.5);
        static double Nr(double lat) => A / Math.Sqrt(1 - E2 * Math.Pow(Math.Sin(lat * Deg), 2));

        public static (float x, float z) Project(double lat, double lon)
            => ((float)((lon - OriginLon) * Deg * Nr(lat) * Math.Cos(lat * Deg)), (float)((lat - OriginLat) * Deg * M0));

        public static (double lat, double lon) Unproject(float x, float z)
        {
            double lat = OriginLat + z / M0 / Deg;
            return (lat, OriginLon + x / (Nr(lat) * Math.Cos(lat * Deg)) / Deg);
        }

        public enum Kind { Event, Version, Landmark, Derived }

        public sealed class Poi
        {
            public string Id = "", Label = "", Source = "", Note = "";
            public Kind Kind;
            public double Lat, Lon;
            public float X, Z;
        }

        /// <summary>Source keys → URLs (also listed in docs/MAP.md).</summary>
        public static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>
        {
            ["mp1810"] = "https://dyatlovpass.com/tent-location",
            ["table2020"] = "https://dyatlovpass.com/investigation-materials",
            ["kan2012"] = "https://dyatlovpass.com/ravine-alekseenkov-and-kan",
            ["labaz2019"] = "https://dyatlovpass.com/labaz-by-konstantinov",
            ["case1959"] = "https://dyatlovpass.com/1959-search",
            ["arcticdem"] = "https://www.pgc.umn.edu/data/arcticdem/",
        };

        static Poi Row(string id, Kind kind, string label, double lat, double lon, string source, string note)
        {
            var (x, z) = Project(lat, lon);
            return new Poi { Id = id, Kind = kind, Label = label, Lat = lat, Lon = lon, X = x, Z = z, Source = source, Note = note };
        }

        public static double Dms(double d, double m, double s) => d + m / 60 + s / 3600;

        public static readonly IReadOnlyList<Poi> Pois = new List<Poi>
        {
            Row("tent", Kind.Event, "Палатка · МП 18.10", Dms(61, 45, 30.82), Dms(59, 25, 45.97), "mp1810",
                "Место палатки 1–2 февраля 1959. Камеральная привязка по фото поисковиков (группа Константинова, 2012), проверена на местности в 2019–2023 (расхождение ~3 м). Авторы заявляют 1–2 м; Буянов считает реальной точность ±50 м."),
            Row("tentBorzenkov", Kind.Version, "Палатка · версия Борзенкова", 61.759033, 59.429417, "mp1810", "Привязка по фото (В. Борзенков): 61°45,542′ 59°25,765′, 903,7 м."),
            Row("tent2020", Kind.Version, "Палатка · таблица 2020", 61.75962, 59.43045, "table2020", "Таблица расследования 2020 года, точность расстояний ±50 м."),
            Row("kolmogorova", Kind.Event, "Зинаида Колмогорова", 61.76217, 59.44458, "table2020", "Таблица 2020. Протоколы 1959: ~630 м от кедра выше по склону, лицом вниз, головой к палатке."),
            Row("slobodin", Kind.Event, "Рустем Слободин", 61.76266, 59.44727, "table2020", "Таблица 2020. Протоколы 1959: ~480 м от кедра, на линии палатка — кедр."),
            Row("dyatlov", Kind.Event, "Игорь Дятлов", 61.76351, 59.45018, "table2020", "Таблица 2020. Протоколы 1959: ~300 м от кедра, у берёзы, лицом вверх, головой к палатке."),
            Row("cedar", Kind.Event, "Кедр · GPS КАН", Dms(61, 45, 53.20), Dms(59, 27, 17.80), "kan2012", "Кедр у кромки леса. Под ним в ямке — костёр; в 1 м к северу от костра найдены Юрий Дорошенко и Юрий Кривонищенко. Ветки обломаны до 4–5 м."),
            Row("cedar2020", Kind.Version, "Кедр · таблица 2020", 61.76494, 59.45541, "table2020", "Альтернативная привязка кедра."),
            Row("ravine", Kind.Event, "Ручей · место четверых · GPS КАН", Dms(61, 45, 53.93), Dms(59, 27, 14.64), "kan2012", "Людмила Дубинина, Александр Колеватов, Семён Золотарёв, Николай Тибо-Бриньоль — найдены в мае 1959 в русле под 2–2,5 м снега, в нескольких метрах ниже настила. Индивидуальные GPS не установлены."),
            Row("ravine2020", Kind.Version, "Четверо · таблица 2020", 61.76451, 59.45405, "table2020", "Расходится с полевой привязкой КАН."),
            Row("p4", Kind.Landmark, "Камень P4 · GPS КАН", Dms(61, 45, 53.85), Dms(59, 27, 14.47), "kan2012", "Камень в 3 м выше настила по ручью (на юг)."),
            Row("triple", Kind.Landmark, "«Тройное дерево» · КАН", Dms(61, 45, 53.58), Dms(59, 27, 13.75), "kan2012", "Ориентир экспедиции КАН, 645 м по GPS."),
            Row("ficus", Kind.Landmark, "«Фикус» · КАН", Dms(61, 45, 54.59), Dms(59, 27, 15.06), "kan2012", "Ориентир экспедиции КАН."),
            Row("mouth1", Kind.Landmark, "Устье ручья 1 · КАН", Dms(61, 45, 55.66), Dms(59, 27, 13.36), "kan2012", "Точка на ручье ниже по течению, 628 м по GPS."),
            Row("mouth2", Kind.Landmark, "Устье ручья 2 · КАН", Dms(61, 45, 59.40), Dms(59, 27, 15.40), "kan2012", "Точка на ручье ниже по течению, 618 м по GPS."),
            Row("labaz", Kind.Event, "Лабаз · 2019", Dms(61, 44, 48.1), Dms(59, 26, 58.0), "labaz2019",
                "Склад продуктов (≈55 кг, мандолина, запасная обувь), оставленный 1 февраля 1959. Место найдено экспедицией Кунцевича/Константинова (2019) по фото 1959; в 2022 раскопан настил из бересты 1,5 × 1,0 м. Отмечен лыжей с рваной гамашей."),
            Row("summit", Kind.Derived, "Вершина Холатчахль", 61.754457, 59.417964, "arcticdem", "Высшая точка по ArcticDEM (≈1095 м; официально 1096,7 м, на старых картах — «высота 1079»)."),
            Row("saddle", Kind.Derived, "Седловина перевала Дятлова", 61.756323, 59.463175, "arcticdem", "Низшая точка гребня между Холатчахлем и высотой 905 по ArcticDEM (≈792 м)."),
        };

        public static Poi Get(string id)
        {
            foreach (var p in Pois) if (p.Id == id) return p;
            throw new KeyNotFoundException(id);
        }

        public static readonly Poi Tent = Get("tent"), Cedar = Get("cedar"), Ravine = Get("ravine"), P4 = Get("p4"), Labaz = Get("labaz"), Saddle = Get("saddle");

        /// <summary>Night camp of 31 Jan 1959 — the group's last normal bivouac, in the forest of the upper Auspiya valley. On 1 Feb they built the
        /// labaz there and went up the slope (Konstantinov / uralstalker: the labaz marks the camp; diary 31.01: "костёр развели на брёвнах, яму копать
        /// не хотелось", supper in the tent). Exact fire and tent spots were never measured: fire 6 m east / 7 m south of the labaz, tent pad 1 m west /
        /// 6 m south of it on the same gentle bench (DEM ≈ 642 m) — an assumption.</summary>
        public static readonly (float x, float z) Camp = (Labaz.X + 6f, Labaz.Z - 7f);
        public static readonly (float x, float z) CampTentPad = (Labaz.X - 1f, Labaz.Z - 6f);
        public const float CampRadius = 5f, GoalRadius = 12f, ShelterHeight = 735f;

        static (float x, float z) P(string id) { var p = Get(id); return (p.X, p.Z); }

        /// <summary>Stream through the ravine, upstream (south) to downstream (north): the upper point is conditional, the rest are KAN GPS points.</summary>
        public static readonly (float x, float z)[] Creek =
        {
            Project(61.7646, 59.4544), P("p4"), P("ravine"), P("mouth1"), P("mouth2"),
        };

        /// <summary>Floor of branches (настил): 3 m downstream of stone P4 along the stream (KAN description). Its own position was not measured.</summary>
        public static readonly (float x, float z) Den = Along(Creek[1], Creek[2], 3f);

        /// <summary>Footprint line of 1–2 Feb: tent → Kolmogorova → Slobodin → Dyatlov → cedar. The bodies lay "almost on the line"; the trail itself is a reconstruction.</summary>
        public static readonly (float x, float z)[] FootprintLine = { P("tent"), P("kolmogorova"), P("slobodin"), P("dyatlov"), P("cedar") };

        /// <summary>1 Feb ascent labaz → tent: least-cost ski line on the DEM (Tools/terrain/route.py). The group's actual track is unknown; the diary says ~2 km.</summary>
        public static readonly (float x, float z)[] AscentRoute =
        {
            (368, -1036), (348, -1016), (332, -1008), (312, -988), (292, -976), (272, -956), (252, -936), (232, -916), (212, -896), (204, -876),
            (196, -856), (176, -836), (156, -816), (144, -796), (140, -776), (128, -756), (120, -736), (120, -716), (112, -696), (108, -676),
            (100, -656), (92, -636), (88, -616), (84, -596), (80, -576), (60, -556), (40, -536), (40, -516), (36, -496), (16, -476),
            (-4, -456), (-24, -436), (-28, -416), (-48, -396), (-52, -376), (-72, -356), (-84, -336), (-92, -316), (-108, -296), (-128, -276),
            (-148, -256), (-168, -236), (-188, -216), (-208, -196), (-228, -176), (-248, -156), (-268, -136), (-288, -116), (-308, -96), (-328, -76),
            (-348, -56), (-368, -36), (-388, -16), (-408, 4), (-428, 24), (-448, 44), (-468, 64), (-488, 84), (-508, 104), (-528, 124),
            (-548, 144), (-568, 164), (-588, 184), (-608, 204), (-628, 224), (-648, 244), (-668, 264), (-688, 284),
        };

        static (float x, float z) Along((float x, float z) a, (float x, float z) b, float metres)
        {
            float d = Distance(a.x, a.z, b.x, b.z);
            float t = d < 1e-3f ? 0 : Math.Min(1f, metres / d);
            return (a.x + (b.x - a.x) * t, a.z + (b.z - a.z) * t);
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

        /// <summary>Ground height: DEM plus the snow-filled stream incision. The 2 m DEM does not resolve the ravine under the canopy;
        /// the incision (up to 3 m, ~10 m wide) is illustrative, its width and depth are not surveyed.</summary>
        public static float GroundHeight(HeightField dem, float x, float z)
        {
            float d = CreekDistance(x, z);
            return dem.Sample(x, z) - 3f * (float)Math.Exp(-d * d / 30f);
        }

        public static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static bool Inside(float x, float z, float margin = 0) => Math.Abs(x) < HeightField.Half - margin && Math.Abs(z) < HeightField.Half - margin;
        public static bool NearCamp(float x, float z) => Distance(x, z, Camp.x, Camp.z) < CampRadius;
        public static bool AtGoal(float x, float z) => Distance(x, z, Tent.X, Tent.Z) < GoalRadius;
    }
}
