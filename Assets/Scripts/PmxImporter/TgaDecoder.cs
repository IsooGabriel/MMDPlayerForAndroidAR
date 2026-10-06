using System;

public readonly struct DecodedImage
{
    public readonly int Width, Height;
    public readonly byte[] Rgba32; // Unity座標系(最初の行が下端)
    public DecodedImage(int w, int h, byte[] px) { Width = w; Height = h; Rgba32 = px; }
}

public static class TgaDecoder
{
    public static DecodedImage Decode(byte[] d)
    {
        int idLen = d[0], cmType = d[1], type = d[2];
        int cmLen = d[5] | d[6] << 8, cmBits = d[7];
        int w = d[12] | d[13] << 8, h = d[14] | d[15] << 8;
        int bpp = d[16], desc = d[17];

        bool rle = type == 10;
        if ((type != 2 && type != 10) || (bpp != 24 && bpp != 32))
            throw new NotSupportedException($"TGA type={type}, bpp={bpp}");

        int bytesPP = bpp / 8;
        int pos = 18 + idLen + (cmType == 1 ? cmLen * ((cmBits + 7) / 8) : 0);
        bool topLeft = (desc & 0x20) != 0;
        int total = w * h;
        var dst = new byte[total * 4];

        int i = 0;
        while (i < total)
        {
            bool run = false;
            int count = total; // 非RLEは全体を1パケット扱い
            if (rle)
            {
                int hd = d[pos++];
                run = (hd & 0x80) != 0;
                count = (hd & 0x7F) + 1;
            }
            count = Math.Min(count, total - i);

            byte b = 0, g = 0, r = 0, a = 255;
            if (run) { b = d[pos]; g = d[pos + 1]; r = d[pos + 2]; if (bytesPP == 4) a = d[pos + 3]; pos += bytesPP; }

            for (int k = 0; k < count; k++, i++)
            {
                if (!run) { b = d[pos]; g = d[pos + 1]; r = d[pos + 2]; if (bytesPP == 4) a = d[pos + 3]; pos += bytesPP; }
                int row = i / w, col = i % w;
                int o = ((topLeft ? h - 1 - row : row) * w + col) * 4;
                dst[o] = r; dst[o + 1] = g; dst[o + 2] = b; dst[o + 3] = a;
            }
        }
        return new DecodedImage(w, h, dst);
    }
}