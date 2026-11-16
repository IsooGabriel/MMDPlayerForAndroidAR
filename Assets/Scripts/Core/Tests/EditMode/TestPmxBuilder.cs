using System.IO;
using System.Text;
using MMDPlayerForVR.PmxImporter.Core;

internal static class TestPmxBuilder
{
    public static byte[] CreateMinimal(
        byte textEncoding = 1,
        int indexSize = 1,
        float version = 2.0f,
        PmxWeightDeformType deformType = PmxWeightDeformType.BDEF1)
    {
        Encoding encoding = textEncoding == 0 ? Encoding.Unicode : Encoding.UTF8;
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("PMX "));
                writer.Write(version);
                writer.Write((byte)8);
                writer.Write(new byte[] { textEncoding, 0, (byte)indexSize, (byte)indexSize, (byte)indexSize, (byte)indexSize, (byte)indexSize, (byte)indexSize });

                WriteText(writer, "テストモデル", encoding);
                WriteText(writer, "Test Model", encoding);
                WriteText(writer, "コメント", encoding);
                WriteText(writer, "Comment", encoding);

                writer.Write(3);
                WriteVertex(writer, deformType, indexSize, 0);
                WriteVertex(writer, deformType, indexSize, 1);
                WriteVertex(writer, deformType, indexSize, 2);

                writer.Write(3);
                WriteUnsignedIndex(writer, 0, indexSize);
                WriteUnsignedIndex(writer, 1, indexSize);
                WriteUnsignedIndex(writer, 2, indexSize);

                writer.Write(0); // textures
                writer.Write(1); // materials
                WriteMaterial(writer, encoding, indexSize);

                writer.Write(3); // bones
                WriteBone(writer, encoding, indexSize, "Root", -1, new UnityEngine.Vector3(0, 0, 0), PmxBoneFlags.Rotatable | PmxBoneFlags.Movable);
                WriteBone(writer, encoding, indexSize, "Child", 0, new UnityEngine.Vector3(0, 1, 0),
                    PmxBoneFlags.ConnectByBoneIndex | PmxBoneFlags.Rotatable | PmxBoneFlags.Movable |
                    PmxBoneFlags.RotationAppend | PmxBoneFlags.MovementAppend | PmxBoneFlags.Ik);
                WriteBone(writer, encoding, indexSize, "Extra", 0, new UnityEngine.Vector3(1, 1, 0), PmxBoneFlags.Rotatable | PmxBoneFlags.Movable);

                writer.Write(0); // morphs
                writer.Write(0); // display frames
                writer.Write(0); // rigid bodies
                writer.Write(0); // joints
            }

            return stream.ToArray();
        }
    }

    private static void WriteVertex(BinaryWriter writer, PmxWeightDeformType deformType, int indexSize, int vertexIndex)
    {
        WriteVector3(writer, new UnityEngine.Vector3(vertexIndex, vertexIndex + 1, vertexIndex + 2));
        WriteVector3(writer, new UnityEngine.Vector3(0, 0, 1));
        writer.Write(0.25f);
        writer.Write(0.75f);
        writer.Write((byte)deformType);

        switch (deformType)
        {
            case PmxWeightDeformType.BDEF1:
                WriteSignedIndex(writer, 0, indexSize);
                break;
            case PmxWeightDeformType.BDEF2:
                WriteSignedIndex(writer, 0, indexSize);
                WriteSignedIndex(writer, 1, indexSize);
                writer.Write(0.25f);
                break;
            case PmxWeightDeformType.SDEF:
                WriteSignedIndex(writer, 0, indexSize);
                WriteSignedIndex(writer, 1, indexSize);
                writer.Write(0.25f);
                WriteVector3(writer, new UnityEngine.Vector3(1, 2, 3));
                WriteVector3(writer, new UnityEngine.Vector3(4, 5, 6));
                WriteVector3(writer, new UnityEngine.Vector3(7, 8, 9));
                break;
            default:
                WriteSignedIndex(writer, 0, indexSize);
                WriteSignedIndex(writer, 1, indexSize);
                WriteSignedIndex(writer, 2, indexSize);
                WriteSignedIndex(writer, 0, indexSize);
                writer.Write(0.1f);
                writer.Write(0.2f);
                writer.Write(0.3f);
                writer.Write(0.4f);
                break;
        }

        writer.Write(1.0f);
    }

    private static void WriteMaterial(BinaryWriter writer, Encoding encoding, int indexSize)
    {
        WriteText(writer, "Material", encoding);
        WriteText(writer, "Material", encoding);
        WriteColor(writer, 1, 1, 1, 1);
        WriteColor(writer, 0, 0, 0);
        writer.Write(1.0f);
        WriteColor(writer, 0, 0, 0);
        writer.Write((byte)0);
        WriteColor(writer, 0, 0, 0, 1);
        writer.Write(0.0f);
        WriteSignedIndex(writer, -1, indexSize);
        WriteSignedIndex(writer, -1, indexSize);
        writer.Write((byte)0);
        writer.Write((byte)1);
        writer.Write((byte)0);
        WriteText(writer, string.Empty, encoding);
        writer.Write(3);
    }

    private static void WriteBone(
        BinaryWriter writer,
        Encoding encoding,
        int indexSize,
        string name,
        int parentIndex,
        UnityEngine.Vector3 position,
        PmxBoneFlags flags)
    {
        WriteText(writer, name, encoding);
        WriteText(writer, name, encoding);
        WriteVector3(writer, position);
        WriteSignedIndex(writer, parentIndex, indexSize);
        writer.Write(0);
        writer.Write((ushort)flags);

        if ((flags & PmxBoneFlags.ConnectByBoneIndex) != 0)
        {
            WriteSignedIndex(writer, 0, indexSize);
        }
        else
        {
            WriteVector3(writer, UnityEngine.Vector3.zero);
        }

        if ((flags & (PmxBoneFlags.RotationAppend | PmxBoneFlags.MovementAppend)) != 0)
        {
            WriteSignedIndex(writer, 0, indexSize);
            writer.Write(0.5f);
        }

        if ((flags & PmxBoneFlags.Ik) != 0)
        {
            WriteSignedIndex(writer, 2, indexSize);
            writer.Write(8);
            writer.Write(0.25f);
            writer.Write(1);
            WriteSignedIndex(writer, 0, indexSize);
            writer.Write((byte)1);
            WriteVector3(writer, new UnityEngine.Vector3(-1, -2, -3));
            WriteVector3(writer, new UnityEngine.Vector3(1, 2, 3));
        }
    }

    private static void WriteText(BinaryWriter writer, string value, Encoding encoding)
    {
        byte[] bytes = encoding.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteSignedIndex(BinaryWriter writer, int value, int size)
    {
        switch (size)
        {
            case 1: writer.Write((sbyte)value); break;
            case 2: writer.Write((short)value); break;
            case 4: writer.Write(value); break;
            default: throw new System.ArgumentOutOfRangeException(nameof(size));
        }
    }

    private static void WriteUnsignedIndex(BinaryWriter writer, int value, int size)
    {
        switch (size)
        {
            case 1: writer.Write((byte)value); break;
            case 2: writer.Write((ushort)value); break;
            case 4: writer.Write(value); break;
            default: throw new System.ArgumentOutOfRangeException(nameof(size));
        }
    }

    private static void WriteVector3(BinaryWriter writer, UnityEngine.Vector3 value)
    {
        writer.Write(value.x);
        writer.Write(value.y);
        writer.Write(value.z);
    }

    private static void WriteColor(BinaryWriter writer, float r, float g, float b, float? a = null)
    {
        writer.Write(r);
        writer.Write(g);
        writer.Write(b);
        if (a.HasValue)
        {
            writer.Write(a.Value);
        }
    }
}
