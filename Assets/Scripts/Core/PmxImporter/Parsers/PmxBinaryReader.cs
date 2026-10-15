using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MMDPlayerForVR.PmxImporter.Parsers
{
    public class PmxBinaryReader : IDisposable
    {
        private readonly BinaryReader _reader;
        public Encoding TextEncoding { get; private set; }

        public int VertexIndexSize { get; private set; }
        public int TextureIndexSize { get; private set; }
        public int MaterialIndexSize { get; private set; }
        public int BoneIndexSize { get; private set; }
        public int MorphIndexSize { get; private set; }
        public int RigidBodyIndexSize { get; private set; }

        public PmxBinaryReader(Stream stream)
        {
            _reader = new BinaryReader(stream);
            TextEncoding = Encoding.UTF8; // default
        }

        public void SetupHeader(byte[] globals)
        {
            if (globals.Length < 8)
            {
                throw new Exception("Invalid PMX globals length.");
            }

            TextEncoding = globals[0] == 0 ? Encoding.Unicode : Encoding.UTF8;

            // Append sizes
            VertexIndexSize = globals[2];
            TextureIndexSize = globals[3];
            MaterialIndexSize = globals[4];
            BoneIndexSize = globals[5];
            MorphIndexSize = globals[6];
            RigidBodyIndexSize = globals[7];
        }

        public byte ReadByte() => _reader.ReadByte();
        public sbyte ReadSByte() => _reader.ReadSByte();
        public ushort ReadUInt16() => _reader.ReadUInt16();
        public short ReadInt16() => _reader.ReadInt16();
        public uint ReadUInt32() => _reader.ReadUInt32();
        public int ReadInt32() => _reader.ReadInt32();
        public float ReadSingle() => _reader.ReadSingle();
        public char[] ReadChars(int count) => _reader.ReadChars(count);
        public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

        public string ReadTextBuf()
        {
            int byteLen = _reader.ReadInt32();
            if (byteLen == 0) return string.Empty;
            byte[] bytes = _reader.ReadBytes(byteLen);
            return TextEncoding.GetString(bytes);
        }

        // Signed index: -1 means "none" (often used for Bone, RigidBody)
        public int ReadSignedIndex(int byteSize)
        {
            switch (byteSize)
            {
                case 1:
                    sbyte v1 = _reader.ReadSByte();
                    return v1 == -1 ? -1 : v1; // Note: PMX specifies 0xFF as -1 here
                case 2:
                    short v2 = _reader.ReadInt16();
                    return v2;
                case 4:
                    return _reader.ReadInt32();
                default:
                    throw new FormatException($"Invalid index size: {byteSize}");
            }
        }

        // Unsigned index: used for Vertex, Texture, Material, Morph
        public int ReadUnsignedIndex(int byteSize)
        {
            switch (byteSize)
            {
                case 1:
                    byte v1 = _reader.ReadByte();
                    return v1 == 0xFF ? -1 : v1;
                case 2:
                    ushort v2 = _reader.ReadUInt16();
                    return v2 == 0xFFFF ? -1 : v2;
                case 4:
                    int v4 = _reader.ReadInt32();
                    return v4; // Usually -1 is represented by -1 in Int32
                default:
                    throw new FormatException($"Invalid index size: {byteSize}");
            }
        }

        public Vector3 ReadVector3() => new Vector3(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
        public Vector4 ReadVector4() => new Vector4(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
        public Vector2 ReadVector2() => new Vector2(_reader.ReadSingle(), _reader.ReadSingle());

        public Color ReadColorRGBA() => new Color(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle());
        public Color ReadColorRGB() => new Color(_reader.ReadSingle(), _reader.ReadSingle(), _reader.ReadSingle(), 1f);

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
