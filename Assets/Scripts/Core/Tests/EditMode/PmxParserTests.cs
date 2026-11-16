using System;
using System.IO;
using MMDPlayerForVR.PmxImporter.Core;
using MMDPlayerForVR.PmxImporter.Parsers;
using NUnit.Framework;

public class PmxParserTests
{
    [TestCase((byte)0)]
    [TestCase((byte)1)]
    public void ParseFromBytes_TextEncoding_DecodesNames(byte textEncoding)
    {
        PmxDocument document = new PmxParser().ParseFromBytes(TestPmxBuilder.CreateMinimal(textEncoding: textEncoding));

        Assert.That(document.Name, Is.EqualTo("テストモデル"));
        Assert.That(document.Bones[1].Name, Is.EqualTo("Child"));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public void ParseFromBytes_IndexSize_DecodesFacesAndParents(int indexSize)
    {
        PmxDocument document = new PmxParser().ParseFromBytes(TestPmxBuilder.CreateMinimal(indexSize: indexSize));

        Assert.That(document.Faces[0].VertexIndices, Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(document.Bones[0].ParentBoneIndex, Is.EqualTo(-1));
        Assert.That(document.Bones[1].ParentBoneIndex, Is.EqualTo(0));
    }

    [TestCase(PmxWeightDeformType.BDEF1)]
    [TestCase(PmxWeightDeformType.BDEF2)]
    [TestCase(PmxWeightDeformType.BDEF4)]
    [TestCase(PmxWeightDeformType.SDEF)]
    [TestCase(PmxWeightDeformType.QDEF)]
    public void ParseFromBytes_DeformType_ReadsWeights(PmxWeightDeformType deformType)
    {
        PmxVertex vertex = new PmxParser()
            .ParseFromBytes(TestPmxBuilder.CreateMinimal(deformType: deformType))
            .Vertices[0];

        Assert.That(vertex.DeformType, Is.EqualTo(deformType));
        Assert.That(vertex.BoneWeights.Length, Is.EqualTo(
            deformType == PmxWeightDeformType.BDEF1 ? 1 :
            deformType == PmxWeightDeformType.BDEF4 || deformType == PmxWeightDeformType.QDEF ? 4 : 2));
        Assert.That(vertex.BoneWeights[0], Is.EqualTo(
            deformType == PmxWeightDeformType.BDEF1 ? 1.0f :
            deformType == PmxWeightDeformType.BDEF2 || deformType == PmxWeightDeformType.SDEF ? 0.25f : 0.1f)
            .Within(1e-4f));

        if (deformType == PmxWeightDeformType.SDEF)
        {
            Assert.That(vertex.SdefC, Is.EqualTo(new UnityEngine.Vector3(1, 2, 3)));
            Assert.That(vertex.SdefR0, Is.EqualTo(new UnityEngine.Vector3(4, 5, 6)));
            Assert.That(vertex.SdefR1, Is.EqualTo(new UnityEngine.Vector3(7, 8, 9)));
        }
    }

    [Test]
    public void ParseFromBytes_BoneFlags_ReadsAppendAndIkData()
    {
        PmxBone bone = new PmxParser()
            .ParseFromBytes(TestPmxBuilder.CreateMinimal())
            .Bones[1];

        Assert.That(bone.ConnectToBoneIndex, Is.EqualTo(0));
        Assert.That(bone.AppendParentBoneIndex, Is.EqualTo(0));
        Assert.That(bone.AppendRatio, Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(bone.Flags.HasFlag(PmxBoneFlags.RotationAppend), Is.True);
        Assert.That(bone.Flags.HasFlag(PmxBoneFlags.MovementAppend), Is.True);
        Assert.That(bone.Ik.TargetBoneIndex, Is.EqualTo(2));
        Assert.That(bone.Ik.LoopCount, Is.EqualTo(8));
        Assert.That(bone.Ik.Links[0].HasAngleLimit, Is.True);
        Assert.That(bone.Ik.Links[0].LowerLimit, Is.EqualTo(new UnityEngine.Vector3(-1, -2, -3)));
    }

    [Test]
    public void ParseFromBytes_InvalidSignature_ThrowsFormatException()
    {
        byte[] bytes = TestPmxBuilder.CreateMinimal();
        bytes[0] = (byte)'X';

        Assert.Throws<FormatException>(() => new PmxParser().ParseFromBytes(bytes));
    }

    [Test]
    public void ParseFromBytes_TruncatedData_ThrowsEndOfStreamException()
    {
        byte[] bytes = TestPmxBuilder.CreateMinimal();
        Array.Resize(ref bytes, bytes.Length - 1);

        Assert.Throws<EndOfStreamException>(() => new PmxParser().ParseFromBytes(bytes));
    }

    [Test]
    public void ParseFromBytes_UnsupportedVersion_ThrowsFormatException()
    {
        byte[] bytes = TestPmxBuilder.CreateMinimal(version: 3.0f);

        Assert.Throws<FormatException>(() => new PmxParser().ParseFromBytes(bytes));
    }
}
