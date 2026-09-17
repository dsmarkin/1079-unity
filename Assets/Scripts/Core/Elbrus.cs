using System;
using System.Collections.Generic;

namespace Height1079.Core
{
    /// <summary>Geography of the second playable area: 12 288 × 12 288 m of the southern slope of Elbrus,
    /// from the Azau meadow (2 350 m) to the West summit (5 642 m). 1 unit = 1 metre.
    /// Frame: x = east, z = north, y = metres above sea level — the same convention as <see cref="WorldData"/>.
    /// Projection: local ellipsoidal equirectangular about the origin (WGS84); scale error &lt; 1 m across the area.
    /// Every ropeway tower, station, hut and path point below is an OpenStreetMap position (ODbL), see docs/ELBRUS.md.</summary>
    public static class Elbrus
    {
        public const double OriginLat = 43.30910, OriginLon = 42.45857;
        /// <summary>Height grid: 2049² nodes 6 m apart (see Tools/terrain/elbrus.py).</summary>
        public const int Resolution = 2049;
        public const float GridStep = 6f;
        public static readonly float Half = (Resolution - 1) * GridStep / 2f; // 6144 m
        public static readonly float Size = Half * 2;
        /// <summary>Height range of elbrus/height_2049.r16 (must match Tools/terrain/elbrus.py output).</summary>
        public const float HeightMin = 2107f, HeightMax = 5647f;

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

        public static bool Inside(float x, float z, float margin = 0) => Math.Abs(x) < Half - margin && Math.Abs(z) < Half - margin;

        public static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        // ── places ────────────────────────────────────────────────────────────────────────────────────────

        public enum Kind { Station, Hut, Landmark, Route }

        public sealed class Poi
        {
            public string Id = "", Label = "", Note = "";
            public Kind Kind;
            public double Lat, Lon;
            public float Ele;
            public float X, Z;
        }

        static Poi Row(string id, Kind kind, string label, double lat, double lon, float ele, string note)
        {
            var (x, z) = Project(lat, lon);
            return new Poi { Id = id, Kind = kind, Label = label, Lat = lat, Lon = lon, Ele = ele, X = x, Z = z, Note = note };
        }

        /// <summary>Source keys → URLs (also listed in docs/ELBRUS.md).</summary>
        public static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>
        {
            ["osm"] = "https://www.openstreetmap.org/ (ODbL) — станции, опоры, приюты, тропы",
            ["terrarium"] = "https://registry.opendata.aws/terrain-tiles/ — рельеф (SRTM/ASTER, ~30 м)",
            ["resort"] = "https://resort-elbrus.ru/ — очереди канатных дорог, высоты станций",
        };

        public static readonly IReadOnlyList<Poi> Pois = new List<Poi>
        {
            Row("azau", Kind.Station, "Поляна Азау · 2350", 43.266242, 42.477855, 2350, "Нижняя станция. Слева — павильон маятниковой «Эльбрус-1» (1969), справа — гондольная очередь (Poma, 2006)."),
            Row("krugozor", Kind.Station, "Старый Кругозор · 3000", 43.274188, 42.461509, 2970, "Первая пересадка. Отсюда виден Баксан и Донгуз-Орун. Гондольная станция стоит в 60 м западнее маятниковой."),
            Row("mir", Kind.Station, "Мир · 3500", 43.289537, 42.460352, 3455, "Вторая пересадка, кафе и музей обороны Приэльбрусья. Дальше вверх — гондола «Эльбрус-3» или старая одноместная кресельная."),
            Row("garabashi", Kind.Station, "Гара-Баши · 3847", 43.304232, 42.460208, 3847, "Верхняя станция канатной дороги — самая высокая в Европе. Ниже неё по склону стоят бочки и приюты."),
            Row("barrels", Kind.Hut, "Бочки · 3710", 43.298937, 42.464064, 3710, "Вагончики-бочки, базовый лагерь с 1980-х: по 6 мест, дизель, кухня."),
            Row("redfox", Kind.Hut, "Хижина RedFox · 3720", 43.299606, 42.462993, 3720, "Приют рядом с бочками."),
            Row("leaprus", Kind.Hut, "LeapRus · 3912", 43.309686, 42.457207, 3912, "Капсульный отель-бивуак."),
            Row("garabashiHut", Kind.Hut, "Приют Гарабаши", 43.306051, 42.460111, 3870, "Приют над станцией."),
            Row("priut11", Kind.Hut, "Приют 11 (Дизель-хат) · 4050", 43.313898, 42.459270, 4050, "Новый приют на месте сгоревшего в 1998 году «Приюта одиннадцати». Рядом — «Приют 88», «Мария», «Орлиное гнездо»."),
            Row("priut88", Kind.Hut, "Приют 88 · 4100", 43.314764, 42.459111, 4100, "Соседний приют на той же морене."),
            Row("pastukhov", Kind.Landmark, "Скалы Пастухова · 4650", 43.331069, 42.458679, 4650, "Выходы лавы на южном склоне, названы по А. В. Пастухову (1890). Обычная точка разворота ратрака."),
            Row("shelf", Kind.Route, "Косая полка · ~5100", 43.343307, 42.451724, 5100, "Длинный траверс от скал к седловине; самое ветреное место маршрута."),
            Row("saddle", Kind.Landmark, "Седловина · 5416", 43.350413, 42.447307, 5416, "Между вершинами; рядом аварийная хижина RedFox 5300."),
            Row("westSummit", Kind.Landmark, "Западная вершина · 5642", 43.352410, 42.437843, 5642, "Высшая точка Эльбруса, Кавказа и Европы."),
            Row("eastSummit", Kind.Landmark, "Восточная вершина · 5621", 43.346794, 42.453904, 5621, "Вторая вершина, кратер сохранился лучше."),
        };

        public static Poi Get(string id)
        {
            foreach (var p in Pois) if (p.Id == id) return p;
            throw new KeyNotFoundException(id);
        }

        public static readonly Poi Azau = Get("azau"), Krugozor = Get("krugozor"), Mir = Get("mir"),
            Garabashi = Get("garabashi"), Barrels = Get("barrels"), Priut = Get("priut11"),
            Pastukhov = Get("pastukhov"), Saddle = Get("saddle"), WestSummit = Get("westSummit");

        // ── ropeways ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>lat/lon of every tower of a line, bottom station first, top station last (OSM way geometry).</summary>
        static (float x, float z)[] Line(params double[] latLon)
        {
            var pts = new (float x, float z)[latLon.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = Project(latLon[2 * i], latLon[2 * i + 1]);
            return pts;
        }

        /// <summary>The six ropeways of the southern slope, as they run today. Speeds and ride times are the operator's figures;
        /// tower positions come from OSM. Both the 1969 jig-back and the modern gondola work in the game.</summary>
        public static readonly RopewaySpec[] Ropeways =
        {
            // the 1969 jig-back carries long spans between few lattice towers — exactly the OSM way, nothing added
            new RopewaySpec("pendulum1", "«Эльбрус-1» · маятниковая", RopewayKind.Pendulum, "azau", "krugozor", 3.0f, 20, 0, 45f,
                Line(43.265767, 42.479297, 43.266796, 42.476974, 43.270276, 42.469898, 43.273232, 42.463599, 43.274188, 42.461509)),

            new RopewaySpec("pendulum2", "«Эльбрус-2» · маятниковая", RopewayKind.Pendulum, "krugozor", "mir", 3.0f, 20, 0, 45f,
                Line(43.274602, 42.461224, 43.275337, 42.461266, 43.280067, 42.461190, 43.289058, 42.460724)),

            new RopewaySpec("gondola1", "«Эльбрус-1» · гондольная", RopewayKind.Gondola, "azau", "krugozor", 5f, 8, 110f, 0f,
                Line(43.266242, 42.477855, 43.267397, 42.475441, 43.267774, 42.474538, 43.268626, 42.472688,
                     43.269124, 42.471512, 43.269888, 42.469742, 43.271011, 42.467257, 43.271176, 42.466928,
                     43.272635, 42.463984, 43.273159, 42.462659, 43.273787, 42.461148, 43.273979, 42.460739)),

            new RopewaySpec("gondola2", "«Эльбрус-2» · гондольная", RopewayKind.Gondola, "krugozor", "mir", 5f, 8, 110f, 0f,
                Line(43.274333, 42.460462, 43.274929, 42.460433, 43.275431, 42.460428, 43.277853, 42.460434,
                     43.280059, 42.460428, 43.281450, 42.460426, 43.283966, 42.460435, 43.287099, 42.460422,
                     43.289169, 42.460336, 43.289537, 42.460352)),

            new RopewaySpec("gondola3", "«Эльбрус-3» · гондольная", RopewayKind.Gondola, "mir", "garabashi", 5f, 8, 110f, 0f,
                Line(43.289890, 42.460640, 43.290636, 42.460680, 43.291533, 42.460650, 43.293793, 42.460567,
                     43.295464, 42.460511, 43.295870, 42.460497, 43.298065, 42.460425, 43.298838, 42.460417,
                     43.299781, 42.460347, 43.302957, 42.460269, 43.303220, 42.460261, 43.303970, 42.460231,
                     43.304232, 42.460208)),

            new RopewaySpec("chair", "Мир — Гарабаши · кресельная", RopewayKind.Chair, "mir", "garabashiChair", 2.5f, 1, 30f, 0f,
                Line(43.289402, 42.461240, 43.290399, 42.461562, 43.291302, 42.461853, 43.292764, 42.462319,
                     43.295449, 42.463165, 43.297445, 42.463794, 43.298255, 42.463943)),
        };

        /// <summary>Platform anchor of every ropeway terminal (the chair lift has its own upper terminal near the barrels).</summary>
        public static (float x, float z) Terminal(string id)
        {
            if (id == "garabashiChair") { var p = Project(43.298255, 42.463943); return p; }
            var poi = Get(id);
            return (poi.X, poi.Z);
        }

        // ── routes ────────────────────────────────────────────────────────────────────────────────────────

        static (float x, float z)[] Path(params double[] latLon) => Line(latLon);

        /// <summary>Foot route Гара-Баши → Приют → скалы Пастухова → косая полка → седловина → взлёт → плато → Западная вершина.
        /// Geometry from the OSM ways «Южная тропа», «Косая полка», «взлёт» and «Тропа зомби»; it is the line the wands follow.</summary>
        public static readonly (float x, float z)[] SummitRoute = Path(
            43.30423, 42.46021, 43.30441, 42.46033, 43.30540, 42.46089, 43.30611, 42.46083, 43.31039, 42.46088,
            43.31153, 42.46065, 43.31220, 42.45995, 43.31321, 42.45987, 43.31398, 42.46013, 43.31596, 42.46049,
            43.31689, 42.46083, 43.31898, 42.46051, 43.32188, 42.45940, 43.32497, 42.45907, 43.32930, 42.45832,
            43.33246, 42.45813, 43.33536, 42.45754, 43.33835, 42.45638, 43.33899, 42.45567, 43.33977, 42.45496,
            43.34069, 42.45370, 43.34153, 42.45263, 43.34236, 42.45148, 43.34260, 42.45043, 43.34297, 42.44978,
            43.34343, 42.44938, 43.34407, 42.44892, 43.34688, 42.44773, 43.34752, 42.44730, 43.34827, 42.44707,
            43.34974, 42.44698, 43.35041, 42.44731, 43.35100, 42.44760, 43.35041, 42.44731, 43.34913, 42.44438,
            43.34946, 42.44398, 43.34991, 42.44368, 43.35030, 42.44345, 43.35074, 42.44335, 43.35137, 42.44331,
            43.35171, 42.44310, 43.35201, 42.44275, 43.35223, 42.44226, 43.35245, 42.44146, 43.35266, 42.44018,
            43.35280, 42.43939, 43.35282, 42.43902, 43.35277, 42.43865, 43.35267, 42.43837, 43.35241, 42.43784);

        /// <summary>Named stages of the ascent, as the wands and the HUD call them (arc-length fraction along SummitRoute).</summary>
        public static readonly (string label, float ele)[] RouteStages =
        {
            ("Гара-Баши", 3847), ("Приют 11", 4050), ("Скалы Пастухова", 4650), ("Косая полка", 5100),
            ("Седловина", 5416), ("Взлёт", 5550), ("Западная вершина", 5642),
        };

        /// <summary>Snow-cat road: the barrels, the huts, and up the same slope to about 5 100 m under the shelf.
        /// The snow-cats turn round at Pastukhov rocks or, on a good morning, at the foot of the косая полка.</summary>
        public static readonly (float x, float z)[] RatrakRoute = Path(
            43.29894, 42.46406, 43.29961, 42.46299, 43.30040, 42.46150, 43.30160, 42.46060, 43.30423, 42.46021,
            43.30980, 42.46090, 43.31220, 42.45995, 43.31596, 42.46049, 43.31898, 42.46051, 43.32497, 42.45907,
            43.32930, 42.45832, 43.33536, 42.45754, 43.33835, 42.45638, 43.33930, 42.45530);

        /// <summary>Where a visitor starts: the square in front of the Azau terminals.</summary>
        public static readonly (float x, float z) Start = (Azau.X + 24f, Azau.Z - 30f);

        public const float GoalRadius = 30f;
        public static bool AtSummit(float x, float z) => Distance(x, z, WestSummit.X, WestSummit.Z) < GoalRadius;

        /// <summary>Arc-length of a polyline in metres.</summary>
        public static float Length((float x, float z)[] path)
        {
            float s = 0;
            for (int i = 1; i < path.Length; i++) s += Distance(path[i - 1].x, path[i - 1].z, path[i].x, path[i].z);
            return s;
        }

        /// <summary>Point at arc-length <paramref name="s"/> along a polyline (clamped).</summary>
        public static (float x, float z) PointAt((float x, float z)[] path, float s)
        {
            if (s <= 0) return path[0];
            for (int i = 1; i < path.Length; i++)
            {
                float d = Distance(path[i - 1].x, path[i - 1].z, path[i].x, path[i].z);
                if (s <= d || i == path.Length - 1)
                {
                    float t = d < 1e-4f ? 0 : Math.Min(1f, s / d);
                    return (path[i - 1].x + (path[i].x - path[i - 1].x) * t, path[i - 1].z + (path[i].z - path[i - 1].z) * t);
                }
                s -= d;
            }
            return path[path.Length - 1];
        }
    }
}
