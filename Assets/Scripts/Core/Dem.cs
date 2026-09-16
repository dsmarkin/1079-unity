using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Height1079.Core
{
    /// <summary>Decodes the cached Terrarium DEM (public/data/terrain.png) without UnityEngine so it can run on a headless server and in tests.
    /// Minimal PNG reader: 8-bit RGB/RGBA, non-interlaced. Port of server/terrain.js.</summary>
    public static class Dem
    {
        public static (int width, int height, int channels, byte[] pixels) DecodePng(byte[] file)
        {
            if (file.Length < 8 || file[0] != 0x89 || file[1] != (byte)'P' || file[2] != (byte)'N' || file[3] != (byte)'G') throw new InvalidDataException("Not a PNG");
            int offset = 8, width = 0, height = 0, channels = 0;
            var idat = new MemoryStream();
            while (offset + 8 <= file.Length)
            {
                int length = ReadInt(file, offset);
                string type = Encoding.ASCII.GetString(file, offset + 4, 4);
                int data = offset + 8;
                if (type == "IHDR")
                {
                    width = ReadInt(file, data); height = ReadInt(file, data + 4);
                    int bitDepth = file[data + 8], color = file[data + 9], interlace = file[data + 12];
                    channels = color switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 0 };
                    if (bitDepth != 8 || channels == 0 || interlace != 0) throw new InvalidDataException("Unsupported PNG layout");
                }
                else if (type == "IDAT") idat.Write(file, data, length);
                else if (type == "IEND") break;
                offset += 12 + length;
            }
            idat.Position = 2; // skip the zlib header; DeflateStream reads the raw stream
            var raw = new MemoryStream();
            using (var inflate = new DeflateStream(idat, CompressionMode.Decompress)) inflate.CopyTo(raw);
            byte[] r = raw.ToArray();
            int stride = width * channels;
            var pixels = new byte[stride * height];
            for (int y = 0; y < height; y++)
            {
                int filter = r[y * (stride + 1)], src = y * (stride + 1) + 1, dst = y * stride;
                for (int i = 0; i < stride; i++)
                {
                    int x = r[src + i];
                    int a = i >= channels ? pixels[dst + i - channels] : 0;
                    int b = y > 0 ? pixels[dst - stride + i] : 0;
                    int c = y > 0 && i >= channels ? pixels[dst - stride + i - channels] : 0;
                    int v;
                    switch (filter)
                    {
                        case 0: v = x; break;
                        case 1: v = x + a; break;
                        case 2: v = x + b; break;
                        case 3: v = x + ((a + b) >> 1); break;
                        default:
                            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                            v = x + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c); break;
                    }
                    pixels[dst + i] = (byte)(v & 255);
                }
            }
            return (width, height, channels, pixels);
        }

        static int ReadInt(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

        /// <summary>Terrarium encoding: height = R·256 + G + B/256 − 32768, metres.</summary>
        public static float[] LoadHeights(byte[] pngFile)
        {
            var (width, height, channels, pixels) = DecodePng(pngFile);
            if (width != WorldData.Resolution || height != WorldData.Resolution) throw new InvalidDataException("DEM must be 256×256");
            var heights = new float[width * height];
            for (int i = 0; i < heights.Length; i++)
                heights[i] = pixels[i * channels] * 256f + pixels[i * channels + 1] + pixels[i * channels + 2] / 256f - 32768f;
            return heights;
        }
    }
}
