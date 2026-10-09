using System;
using UnityEngine;

/// <summary>
/// BMP (非圧縮 / BITFIELDS) を Color32[] と Texture2D に変換するデコーダー。
/// 対応: 1/4/8bit パレット、16bit、24bit、32bit(BI_RGB / BI_BITFIELDS)、上下どちらの向きも可。
/// 非対応: RLE圧縮、JPEG/PNG埋め込み、OS/2 Core ヘッダー。
/// </summary>
public static class BmpLoader
{
    /// <summary>byte[] から Texture2D を生成する。</summary>
    public static Texture2D Load(byte[] data, bool mipChain = true, bool linear = false)
    {
        Color32[] pixels = Decode(data, out int width, out int height);
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain, linear);
        tex.SetPixels32(pixels);
        tex.Apply(mipChain);
        return tex;
    }

    /// <summary>
    /// BMP をデコードして Color32[] を返す。
    /// 並びは Unity の Texture2D と同じ(下の行から上へ)なのでそのまま SetPixels32 できる。
    /// </summary>
    public static Color32[] Decode(byte[] data, out int width, out int height)
    {
        if (data == null || data.Length < 54 || data[0] != 'B' || data[1] != 'M')
            throw new FormatException("BMPファイルではありません");

        int dataOffset = (int)ReadU32(data, 10);
        int headerSize = (int)ReadU32(data, 14);
        if (headerSize < 40)
            throw new NotSupportedException("OS/2 Core ヘッダーのBMPは非対応です");

        width = ReadI32(data, 18);
        int rawHeight = ReadI32(data, 22);
        int bpp = ReadU16(data, 28);
        uint compression = ReadU32(data, 30);
        int clrUsed = (int)ReadU32(data, 46);

        bool bottomUp = rawHeight > 0;
        height = Math.Abs(rawHeight);

        if (width <= 0 || height == 0)
            throw new FormatException("BMPのサイズが不正です");
        if (compression != 0 && compression != 3 && compression != 6)
            throw new NotSupportedException($"圧縮形式 {compression} は非対応です(RLE等)");

        // ---- パレット ----
        Color32[] palette = null;
        if (bpp <= 8)
        {
            int count = clrUsed != 0 ? clrUsed : 1 << bpp;
            palette = new Color32[count];
            int p = 14 + headerSize;
            for (int i = 0; i < count; i++, p += 4)
            {
                if (p + 3 >= data.Length) break;
                palette[i] = new Color32(data[p + 2], data[p + 1], data[p], 255);
            }
        }

        // ---- ビットマスク(16/32bit用) ----
        uint rMask = 0, gMask = 0, bMask = 0, aMask = 0;
        if (bpp == 16 || bpp == 32)
        {
            if (headerSize >= 108)
            {
                rMask = ReadU32(data, 54);
                gMask = ReadU32(data, 58);
                bMask = ReadU32(data, 62);
                aMask = ReadU32(data, 66);
            }
            else if (compression == 3 || compression == 6)
            {
                rMask = ReadU32(data, 54);
                gMask = ReadU32(data, 58);
                bMask = ReadU32(data, 62);
                if (compression == 6) aMask = ReadU32(data, 66);
            }
            else if (bpp == 16)
            {
                rMask = 0x7C00; gMask = 0x03E0; bMask = 0x001F; // X1R5G5B5
            }
            else
            {
                rMask = 0x00FF0000; gMask = 0x0000FF00; bMask = 0x000000FF; aMask = 0xFF000000;
            }
        }

        int stride = ((width * bpp + 31) / 32) * 4;
        long required = (long)dataOffset + (long)stride * height;
        if (required > data.Length)
            throw new FormatException("BMPのデータが不足しています");

        var result = new Color32[width * height];
        bool anyAlpha = false;

        for (int fileRow = 0; fileRow < height; fileRow++)
        {
            int texRow = bottomUp ? fileRow : height - 1 - fileRow;
            int src = dataOffset + fileRow * stride;
            int dst = texRow * width;

            for (int x = 0; x < width; x++)
            {
                Color32 c;
                switch (bpp)
                {
                    case 1:
                        c = palette[(data[src + (x >> 3)] >> (7 - (x & 7))) & 1];
                        break;
                    case 4:
                        {
                            int b = data[src + (x >> 1)];
                            c = palette[(x & 1) == 0 ? b >> 4 : b & 0x0F];
                            break;
                        }
                    case 8:
                        c = palette[data[src + x]];
                        break;
                    case 16:
                        {
                            uint v = ReadU16(data, src + x * 2);
                            c = new Color32(Extract(v, rMask), Extract(v, gMask), Extract(v, bMask),
                                            aMask != 0 ? Extract(v, aMask) : (byte)255);
                            break;
                        }
                    case 24:
                        {
                            int p = src + x * 3;
                            c = new Color32(data[p + 2], data[p + 1], data[p], 255);
                            break;
                        }
                    case 32:
                        {
                            uint v = ReadU32(data, src + x * 4);
                            c = new Color32(Extract(v, rMask), Extract(v, gMask), Extract(v, bMask),
                                            aMask != 0 ? Extract(v, aMask) : (byte)255);
                            break;
                        }
                    default:
                        throw new NotSupportedException($"{bpp}bit BMPは非対応です");
                }

                if (c.a != 0) anyAlpha = true;
                result[dst + x] = c;
            }
        }

        // 32bit BMPはアルファ未使用でも全て0で出力されることが多い。その場合は不透明にする
        if (bpp == 32 && !anyAlpha)
        {
            for (int i = 0; i < result.Length; i++) result[i].a = 255;
        }

        return result;
    }

    // マスクに該当するビットを取り出し、0-255にスケールする
    private static byte Extract(uint value, uint mask)
    {
        if (mask == 0) return 0;
        int shift = 0;
        while (((mask >> shift) & 1) == 0) shift++;
        uint max = mask >> shift;
        uint v = (value & mask) >> shift;
        return (byte)(v * 255u / max);
    }

    private static ushort ReadU16(byte[] d, int o) => (ushort)(d[o] | (d[o + 1] << 8));
    private static uint ReadU32(byte[] d, int o) =>
        (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
    private static int ReadI32(byte[] d, int o) => (int)ReadU32(d, o);
}