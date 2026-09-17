using System;
using System.IO;

namespace Height1079.Core
{
    /// <summary>Bare-earth height grid of the playable area (ArcticDEM v4.1 2 m mosaic, see docs/MAP.md).
    /// World frame: x = east, z = north, y = metres above sea level (EGM96), origin = <see cref="WorldData.OriginLat"/>/<see cref="WorldData.OriginLon"/>.
    /// Row 0 of the grid is the southern edge, column 0 the western edge; nodes are spaced <see cref="Step"/> metres apart.</summary>
    public sealed class HeightField
    {
        public const int Resolution = 2049;
        public const float Step = 2f;
        public static readonly float Half = (Resolution - 1) * Step / 2f; // 2048 m
        public readonly float[] Heights;
        public readonly float Min, Max;

        public HeightField(float[] heights)
        {
            if (heights.Length != Resolution * Resolution) throw new InvalidDataException($"height grid must be {Resolution}²");
            Heights = heights;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var h in heights) { if (h < min) min = h; if (h > max) max = h; }
            Min = min; Max = max;
        }

        /// <summary>Unity RAW 16-bit little-endian, value 0..65535 mapped to [min, max] metres.</summary>
        public static HeightField FromR16(byte[] raw, float min, float max)
        {
            if (raw == null || raw.Length != Resolution * Resolution * 2) throw new InvalidDataException("height_2049.r16 must hold 2049² uint16 values");
            var h = new float[Resolution * Resolution];
            float scale = (max - min) / 65535f;
            for (int i = 0; i < h.Length; i++) h[i] = min + (raw[2 * i] | raw[2 * i + 1] << 8) * scale;
            return new HeightField(h);
        }

        public float At(int col, int row) => Heights[row * Resolution + col];

        /// <summary>Bilinear sample at world x/z; clamped to the grid edge.</summary>
        public float Sample(float x, float z)
        {
            double fx = Math.Max(0, Math.Min(Resolution - 1, (x + Half) / Step));
            double fz = Math.Max(0, Math.Min(Resolution - 1, (z + Half) / Step));
            int a = Math.Min(Resolution - 2, (int)fx), b = Math.Min(Resolution - 2, (int)fz);
            double u = fx - a, v = fz - b;
            return (float)((At(a, b) * (1 - u) + At(a + 1, b) * u) * (1 - v) + (At(a, b + 1) * (1 - u) + At(a + 1, b + 1) * u) * v);
        }

        /// <summary>Downhill unit vector (x, z) and slope in degrees from central differences over <paramref name="span"/> metres.</summary>
        public (float dx, float dz, float slopeDeg) Fall(float x, float z, float span = 6f)
        {
            float gx = (Sample(x + span, z) - Sample(x - span, z)) / (2 * span);
            float gz = (Sample(x, z + span) - Sample(x, z - span)) / (2 * span);
            float g = (float)Math.Sqrt(gx * gx + gz * gz);
            if (g < 1e-6f) return (0, 0, 0);
            return (-gx / g, -gz / g, (float)(Math.Atan(g) * 180 / Math.PI));
        }
    }

    /// <summary>Tree instances detected in the Meta/WRI 1 m canopy height model (local maxima ≥ 3 m). Species are assigned by rule (docs/MAP.md).</summary>
    public enum TreeSpecies : byte { Spruce = 0, Fir = 1, Birch = 2, SiberianPine = 3 }

    public readonly struct TreeRecord
    {
        public readonly float X, Z, Height; public readonly TreeSpecies Species;
        public TreeRecord(float x, float z, float height, TreeSpecies species) { X = x; Z = z; Height = height; Species = species; }
    }

    public static class Dem
    {
        /// <summary>trees.f32: records of 4 little-endian floats (x east, z north, canopy height m, species).</summary>
        public static TreeRecord[] LoadTrees(byte[] raw)
        {
            if (raw == null || raw.Length % 16 != 0) throw new InvalidDataException("trees.f32 must hold 16-byte records");
            var list = new TreeRecord[raw.Length / 16];
            for (int i = 0; i < list.Length; i++)
            {
                int o = i * 16;
                list[i] = new TreeRecord(BitConverter.ToSingle(raw, o), BitConverter.ToSingle(raw, o + 4), BitConverter.ToSingle(raw, o + 8), (TreeSpecies)(int)BitConverter.ToSingle(raw, o + 12));
            }
            return list;
        }

        /// <summary>8-bit mask grid (row 0 = south), e.g. rock_1025.r8 or canopy_2049.r8, sampled nearest at world x/z.</summary>
        public static byte Mask(byte[] grid, int resolution, float x, float z)
        {
            float step = 2 * HeightField.Half / (resolution - 1);
            int c = Math.Max(0, Math.Min(resolution - 1, (int)Math.Round((x + HeightField.Half) / step)));
            int r = Math.Max(0, Math.Min(resolution - 1, (int)Math.Round((z + HeightField.Half) / step)));
            return grid[r * resolution + c];
        }
    }
}
