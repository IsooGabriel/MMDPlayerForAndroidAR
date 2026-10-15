using System;
using UnityEngine;

namespace MMDPlayerForVR.PmxImporter.Core
{
    public enum PmxWeightDeformType { BDEF1 = 0, BDEF2 = 1, BDEF4 = 2, SDEF = 3, QDEF = 4 }

    public class PmxVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 Uv;
        public Vector4[] AdditionalUv;
        public PmxWeightDeformType DeformType;

        public int[] BoneIndices;
        public float[] BoneWeights;

        // SDEF
        public Vector3 SdefC, SdefR0, SdefR1;

        public float EdgeScale;
    }

    public class PmxFace
    {
        public int[] VertexIndices = new int[3];
    }

    public class PmxMaterial
    {
        public string Name;
        public string NameEnglish;
        public Color Diffuse;
        public Color Specular;
        public float SpecularPower;
        public Color Ambient;
        public byte DrawFlags;
        public Color EdgeColor;
        public float EdgeSize;
        public int TextureIndex;
        public int SphereTextureIndex;
        public byte SphereMode;
        public bool UseSharedToon;
        public int ToonIndex;
        public string Memo;
        public int SurfaceCount;
    }

    [Flags]
    public enum PmxBoneFlags : ushort
    {
        ConnectByBoneIndex = 0x0001,
        Rotatable          = 0x0002,
        Movable            = 0x0004,
        Visible            = 0x0008,
        Enabled            = 0x0010,
        Ik                 = 0x0020,
        LocalAppend        = 0x0080,
        RotationAppend     = 0x0100,
        MovementAppend     = 0x0200,
        FixedAxis          = 0x0400,
        LocalAxis          = 0x0800,
        PostPhysics        = 0x1000,
        ExternalParent     = 0x2000,
    }

    public class PmxBone
    {
        public string Name;
        public string NameEnglish;
        public Vector3 Position;
        public int ParentBoneIndex;
        public int DeformLayer;
        public PmxBoneFlags Flags;

        public int ConnectToBoneIndex;
        public Vector3 ConnectOffset;

        public int AppendParentBoneIndex;
        public float AppendRatio;

        public Vector3 FixedAxis;
        public Vector3 LocalAxisX, LocalAxisZ;
        public int ExternalParentKey;
        public PmxIkData Ik;
    }

    public class PmxIkData
    {
        public int TargetBoneIndex;
        public int LoopCount;
        public float LimitAngleRadians;
        public PmxIkLink[] Links;
    }

    public class PmxIkLink
    {
        public int BoneIndex;
        public bool HasAngleLimit;
        public Vector3 LowerLimit, UpperLimit;
    }

    public class PmxMorph
    {
        public string Name;
        public string NameEnglish;
        public byte Panel;
        public byte MorphType;
        public object[] Offsets; // Will hold morph-specific offset data structures
    }

    public class PmxRigidBody
    {
        public string Name;
        public string NameEnglish;
        public int RelatedBoneIndex;
        public byte CollisionGroup;
        public ushort CollisionMask;
        public byte Shape; // 0:Sphere 1:Box 2:Capsule
        public Vector3 Size;
        public Vector3 Position;
        public Vector3 RotationEuler;
        public float Mass;
        public float LinearDamping;
        public float AngularDamping;
        public float Restitution;
        public float Friction;
        public byte PhysicsMode; // 0:FollowBone 1:Physics 2:Physics+Bone
    }

    public class PmxJoint
    {
        public string Name;
        public string NameEnglish;
        public byte Type; // 0:Spring6DOF
        public int RigidBodyIndexA;
        public int RigidBodyIndexB;
        public Vector3 Position;
        public Vector3 RotationEuler;
        public Vector3 PositionLimitMin;
        public Vector3 PositionLimitMax;
        public Vector3 RotationLimitMin;
        public Vector3 RotationLimitMax;
        public Vector3 SpringPosition;
        public Vector3 SpringRotation;
    }

    public class PmxDocument
    {
        public string Version;
        public string Name;
        public string NameEnglish;
        public string Comment;
        public string CommentEnglish;

        public PmxVertex[] Vertices;
        public PmxFace[] Faces;
        public string[] Textures;
        public PmxMaterial[] Materials;
        public PmxBone[] Bones;
        public PmxMorph[] Morphs;
        public PmxRigidBody[] RigidBodies;
        public PmxJoint[] Joints;
    }
}
