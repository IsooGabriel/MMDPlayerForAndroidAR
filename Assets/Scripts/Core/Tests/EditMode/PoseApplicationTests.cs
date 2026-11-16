using System.Collections.Generic;
using System.Threading.Tasks;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;
using NUnit.Framework;
using UnityEngine;

public class ApplyInitialPoseUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_MemoryVmd_AppliesEarliestBoneFrameAndSnapsPhysics()
    {
        var frames = new List<VmdBoneFrame>
        {
            new VmdBoneFrame { name = "Bone", frame = 5, pos = new Vector3(5, 0, 0), rot = Quaternion.identity },
            new VmdBoneFrame { name = "Bone", frame = 0, pos = new Vector3(1, 2, 3), rot = Quaternion.identity }
        };
        var parser = new FixedVmdParser(frames);
        var applier = new RecordingPoseApplier();
        var useCase = new ApplyInitialPoseUseCase(new InMemoryReader(), parser);

        await useCase.ExecuteAsync(applier, null, "memory/motion.vmd");

        Assert.That(applier.Pose, Is.Not.Null);
        Assert.That(applier.Pose.bones["Bone"].pos, Is.EqualTo(new Vector3(1, 2, 3)));
        Assert.That(applier.TeleportedBodies, Is.True);
    }

    private sealed class InMemoryReader : IStreamingAssetsReader
    {
        public Task<byte[]> ReadAllBytesAsync(string path)
        {
            return Task.FromResult(new byte[] { 1 });
        }

        public Task<bool> ExistsAsync(string path)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class FixedVmdParser : IVmdParser
    {
        private readonly IReadOnlyList<VmdBoneFrame> _frames;

        public FixedVmdParser(IReadOnlyList<VmdBoneFrame> frames)
        {
            _frames = frames;
        }

        public IReadOnlyList<VmdBoneFrame> ParseBoneFrames(byte[] data)
        {
            return _frames;
        }

        public IReadOnlyDictionary<string, bool> ParseIkStates(byte[] data)
        {
            return new Dictionary<string, bool>();
        }
    }

    private sealed class RecordingPoseApplier : IPoseApplier
    {
        public PoseState Pose { get; private set; }
        public bool TeleportedBodies { get; private set; }

        public void Apply(PoseState pose)
        {
            Pose = pose;
        }

        public void TeleportBodiesForPhysics()
        {
            TeleportedBodies = true;
        }
    }
}

public class TransformPoseApplierTests
{
    private GameObject _boneObject;

    [TearDown]
    public void TearDown()
    {
        if (_boneObject != null)
        {
            Object.DestroyImmediate(_boneObject);
        }
    }

    [Test]
    public void Apply_UnknownBoneEntry_KeepsOtherBoneAndLogsWarning()
    {
        _boneObject = new GameObject("Bone");
        _boneObject.transform.localPosition = new Vector3(1, 2, 3);
        var applier = new MMDPlayerForVR.Pose.Infrastructure.TransformPoseApplier(
            new Dictionary<string, Transform> { ["Bone"] = _boneObject.transform },
            new List<MMDPlayerForVR.PmxImporter.Builders.PmxPhysicsSync>());
        var pose = new PoseState();
        pose.bones["Bone"] = new BonePose { pos = new Vector3(2, 0, -1), rot = Quaternion.identity };
        pose.bones["Missing"] = new BonePose { pos = Vector3.one, rot = Quaternion.identity };
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "[Pose] 対応するボーンなし: Missing");

        Assert.DoesNotThrow(() => applier.Apply(pose));
        Assert.That(_boneObject.transform.localPosition, Is.EqualTo(new Vector3(3, 2, 2)));
    }
}
