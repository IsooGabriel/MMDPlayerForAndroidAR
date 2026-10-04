using MMDPlayerForVR.PmxImporter.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace MMDPlayerForVR.PmxImporter.Parsers
{
    public class PmxParser
    {
        private int _additionalUvCount;

        public async Task<PmxDocument> ParseAsync(string filePath)
        {
            byte[] fileBytes = await AsyncFileLoader.ReadAllBytesAsync(filePath);
            if (fileBytes == null) throw new FileNotFoundException($"File not found or failed to load: {filePath}");

            return await Task.Run(() => ParseFromBytes(fileBytes));
        }

        public PmxDocument ParseFromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new PmxBinaryReader(stream))
            {
                PmxDocument doc = new PmxDocument();

                ParseHeader(reader, doc);
                ParseVertices(reader, doc);
                ParseFaces(reader, doc);
                ParseTextures(reader, doc);
                ParseMaterials(reader, doc);
                ParseBones(reader, doc);
                ParseMorphs(reader, doc);
                ParseDisplayFrames(reader);
                ParseRigidBodies(reader, doc);
                ParseJoints(reader, doc);

                return doc;
            }
        }

        public PmxDocument Parse(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new PmxBinaryReader(stream))
            {
                PmxDocument doc = new PmxDocument();

                ParseHeader(reader, doc);
                ParseVertices(reader, doc);
                ParseFaces(reader, doc);
                ParseTextures(reader, doc);
                ParseMaterials(reader, doc);
                ParseBones(reader, doc);
                ParseMorphs(reader, doc);
                ParseDisplayFrames(reader);
                ParseRigidBodies(reader, doc);
                ParseJoints(reader, doc);

                return doc;
            }
        }

        private void ParseHeader(PmxBinaryReader reader, PmxDocument doc)
        {
            string signature = new string(reader.ReadChars(4));
            if (signature != "PMX ") throw new FormatException("Invalid PMX signature.");

            doc.Version = reader.ReadSingle().ToString("F1");
            byte globalCount = reader.ReadByte();
            byte[] globals = reader.ReadBytes(globalCount);

            reader.SetupHeader(globals);
            _additionalUvCount = globals[1];

            doc.Name = reader.ReadTextBuf();
            doc.NameEnglish = reader.ReadTextBuf();
            doc.Comment = reader.ReadTextBuf();
            doc.CommentEnglish = reader.ReadTextBuf();
        }

        private void ParseVertices(PmxBinaryReader reader, PmxDocument doc)
        {
            int count = reader.ReadInt32();
            doc.Vertices = new PmxVertex[count];
            for (int i = 0; i < count; i++)
            {
                var v = new PmxVertex();
                v.Position = reader.ReadVector3();
                v.Normal = reader.ReadVector3();
                v.Uv = reader.ReadVector2();

                v.AdditionalUv = new Vector4[_additionalUvCount];
                for (int j = 0; j < _additionalUvCount; j++)
                    v.AdditionalUv[j] = reader.ReadVector4();

                v.DeformType = (PmxWeightDeformType)reader.ReadByte();
                ParseVertexWeights(reader, v);

                v.EdgeScale = reader.ReadSingle();
                doc.Vertices[i] = v;
            }
        }

        private void ParseVertexWeights(PmxBinaryReader reader, PmxVertex v)
        {
            switch (v.DeformType)
            {
                case PmxWeightDeformType.BDEF1:
                    v.BoneIndices = new int[1] { reader.ReadSignedIndex(reader.BoneIndexSize) };
                    v.BoneWeights = new float[1] { 1.0f };
                    break;
                case PmxWeightDeformType.BDEF2:
                    v.BoneIndices = new int[2] { reader.ReadSignedIndex(reader.BoneIndexSize), reader.ReadSignedIndex(reader.BoneIndexSize) };
                    float w0 = reader.ReadSingle();
                    v.BoneWeights = new float[2] { w0, 1.0f - w0 };
                    break;
                case PmxWeightDeformType.BDEF4:
                    v.BoneIndices = new int[4] {
                        reader.ReadSignedIndex(reader.BoneIndexSize), reader.ReadSignedIndex(reader.BoneIndexSize),
                        reader.ReadSignedIndex(reader.BoneIndexSize), reader.ReadSignedIndex(reader.BoneIndexSize)
                    };
                    v.BoneWeights = new float[4] { reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() };
                    break;
                case PmxWeightDeformType.SDEF:
                    v.BoneIndices = new int[2] { reader.ReadSignedIndex(reader.BoneIndexSize), reader.ReadSignedIndex(reader.BoneIndexSize) };
                    float wSdef = reader.ReadSingle();
                    v.BoneWeights = new float[2] { wSdef, 1.0f - wSdef };
                    v.SdefC = reader.ReadVector3();
                    v.SdefR0 = reader.ReadVector3();
                    v.SdefR1 = reader.ReadVector3();
                    break;
                default:
                    // QDEF (PMX 2.1)
                    v.BoneIndices = new int[4] {
                        reader.ReadSignedIndex(reader.BoneIndexSize), reader.ReadSignedIndex(reader.BoneIndexSize),
                        reader.ReadSignedIndex(reader.BoneIndexSize), reader.ReadSignedIndex(reader.BoneIndexSize)
                    };
                    v.BoneWeights = new float[4] { reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() };
                    break;
            }
        }

        private void ParseFaces(PmxBinaryReader reader, PmxDocument doc)
        {
            int indexCount = reader.ReadInt32();
            int faceCount = indexCount / 3;
            doc.Faces = new PmxFace[faceCount];
            for (int i = 0; i < faceCount; i++)
            {
                doc.Faces[i] = new PmxFace
                {
                    VertexIndices = new int[3] {
                        reader.ReadUnsignedIndex(reader.VertexIndexSize),
                        reader.ReadUnsignedIndex(reader.VertexIndexSize),
                        reader.ReadUnsignedIndex(reader.VertexIndexSize)
                    }
                };
            }
        }

        private void ParseTextures(PmxBinaryReader reader, PmxDocument doc)
        {
            int count = reader.ReadInt32();
            doc.Textures = new string[count];
            for (int i = 0; i < count; i++) doc.Textures[i] = reader.ReadTextBuf();
        }

        private void ParseMaterials(PmxBinaryReader reader, PmxDocument doc)
        {
            int count = reader.ReadInt32();
            doc.Materials = new PmxMaterial[count];
            for (int i = 0; i < count; i++)
            {
                var m = new PmxMaterial();
                m.Name = reader.ReadTextBuf();
                m.NameEnglish = reader.ReadTextBuf();
                m.Diffuse = reader.ReadColorRGBA();
                m.Specular = reader.ReadColorRGB();
                m.SpecularPower = reader.ReadSingle();
                m.Ambient = reader.ReadColorRGB();
                m.DrawFlags = reader.ReadByte();
                m.EdgeColor = reader.ReadColorRGBA();
                m.EdgeSize = reader.ReadSingle();
                m.TextureIndex = reader.ReadSignedIndex(reader.TextureIndexSize);
                m.SphereTextureIndex = reader.ReadSignedIndex(reader.TextureIndexSize);
                m.SphereMode = reader.ReadByte();
                m.UseSharedToon = reader.ReadByte() == 1;
                m.ToonIndex = m.UseSharedToon ? reader.ReadByte() : reader.ReadSignedIndex(reader.TextureIndexSize);
                m.Memo = reader.ReadTextBuf();
                m.SurfaceCount = reader.ReadInt32();
                doc.Materials[i] = m;
            }
        }

        private void ParseBones(PmxBinaryReader reader, PmxDocument doc)
        {
            int count = reader.ReadInt32();
            doc.Bones = new PmxBone[count];
            for (int i = 0; i < count; i++)
            {
                var b = new PmxBone();
                b.Name = reader.ReadTextBuf();
                b.NameEnglish = reader.ReadTextBuf();
                b.Position = reader.ReadVector3();
                b.ParentBoneIndex = reader.ReadSignedIndex(reader.BoneIndexSize);
                b.DeformLayer = reader.ReadInt32();
                b.Flags = (PmxBoneFlags)reader.ReadUInt16();

                if (b.Flags.HasFlag(PmxBoneFlags.ConnectByBoneIndex)) b.ConnectToBoneIndex = reader.ReadSignedIndex(reader.BoneIndexSize);
                else b.ConnectOffset = reader.ReadVector3();

                if (b.Flags.HasFlag(PmxBoneFlags.RotationAppend) || b.Flags.HasFlag(PmxBoneFlags.MovementAppend))
                {
                    b.AppendParentBoneIndex = reader.ReadSignedIndex(reader.BoneIndexSize);
                    b.AppendRatio = reader.ReadSingle();
                }

                if (b.Flags.HasFlag(PmxBoneFlags.FixedAxis)) b.FixedAxis = reader.ReadVector3();

                if (b.Flags.HasFlag(PmxBoneFlags.LocalAxis))
                {
                    b.LocalAxisX = reader.ReadVector3();
                    b.LocalAxisZ = reader.ReadVector3();
                }

                if (b.Flags.HasFlag(PmxBoneFlags.ExternalParent)) b.ExternalParentKey = reader.ReadInt32();

                if (b.Flags.HasFlag(PmxBoneFlags.Ik))
                {
                    b.Ik = new PmxIkData();
                    b.Ik.TargetBoneIndex = reader.ReadSignedIndex(reader.BoneIndexSize);
                    b.Ik.LoopCount = reader.ReadInt32();
                    b.Ik.LimitAngleRadians = reader.ReadSingle();
                    int linkCount = reader.ReadInt32();
                    b.Ik.Links = new PmxIkLink[linkCount];
                    for (int j = 0; j < linkCount; j++)
                    {
                        var link = new PmxIkLink();
                        link.BoneIndex = reader.ReadSignedIndex(reader.BoneIndexSize);
                        link.HasAngleLimit = reader.ReadByte() == 1;
                        if (link.HasAngleLimit)
                        {
                            link.LowerLimit = reader.ReadVector3();
                            link.UpperLimit = reader.ReadVector3();
                        }
                        b.Ik.Links[j] = link;
                    }
                }
                doc.Bones[i] = b;
            }
        }

        private void ParseMorphs(PmxBinaryReader reader, PmxDocument doc)
        {
            int count = reader.ReadInt32();
            doc.Morphs = new PmxMorph[count];
            for (int i = 0; i < count; i++)
            {
                var m = new PmxMorph();
                m.Name = reader.ReadTextBuf();
                m.NameEnglish = reader.ReadTextBuf();
                m.Panel = reader.ReadByte();
                m.MorphType = reader.ReadByte();
                int offsetCount = reader.ReadInt32();
                // Skip payload to avoid memory bloat since Morph is out of scope.
                for (int j = 0; j < offsetCount; j++)
                {
                    switch (m.MorphType)
                    {
                        case 0: reader.ReadSignedIndex(reader.MorphIndexSize); reader.ReadSingle(); break; // Group
                        case 1: reader.ReadUnsignedIndex(reader.VertexIndexSize); reader.ReadVector3(); break; // Vertex
                        case 2: reader.ReadSignedIndex(reader.BoneIndexSize); reader.ReadVector3(); reader.ReadVector4(); break; // Bone
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7: reader.ReadUnsignedIndex(reader.VertexIndexSize); reader.ReadVector4(); break; // UV
                        case 8: // Material
                            reader.ReadSignedIndex(reader.MaterialIndexSize);
                            reader.ReadByte(); reader.ReadColorRGBA(); reader.ReadColorRGB(); reader.ReadSingle(); reader.ReadColorRGB();
                            reader.ReadColorRGBA(); reader.ReadSingle(); reader.ReadColorRGBA(); reader.ReadColorRGBA(); reader.ReadColorRGBA();
                            break;
                        case 9: reader.ReadSignedIndex(reader.MorphIndexSize); reader.ReadSingle(); break; // Flip
                        case 10: reader.ReadSignedIndex(reader.RigidBodyIndexSize); reader.ReadByte(); reader.ReadVector3(); reader.ReadVector3(); break; // Impulse
                    }
                }
                doc.Morphs[i] = m;
            }
        }

        private void ParseDisplayFrames(PmxBinaryReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                reader.ReadTextBuf(); // name
                reader.ReadTextBuf(); // english
                reader.ReadByte(); // flag
                int elementCount = reader.ReadInt32();
                for (int j = 0; j < elementCount; j++)
                {
                    byte target = reader.ReadByte();
                    if (target == 0) reader.ReadSignedIndex(reader.BoneIndexSize);
                    else reader.ReadSignedIndex(reader.MorphIndexSize);
                }
            }
        }

        private void ParseRigidBodies(PmxBinaryReader reader, PmxDocument doc)
        {
            int count = reader.ReadInt32();
            doc.RigidBodies = new PmxRigidBody[count];
            for (int i = 0; i < count; i++)
            {
                var rb = new PmxRigidBody();
                rb.Name = reader.ReadTextBuf();
                rb.NameEnglish = reader.ReadTextBuf();
                rb.RelatedBoneIndex = reader.ReadSignedIndex(reader.BoneIndexSize);
                rb.CollisionGroup = reader.ReadByte();
                rb.CollisionMask = reader.ReadUInt16();
                rb.Shape = reader.ReadByte();
                rb.Size = reader.ReadVector3();
                rb.Position = reader.ReadVector3();
                rb.RotationEuler = reader.ReadVector3();
                rb.Mass = reader.ReadSingle();
                rb.LinearDamping = reader.ReadSingle();
                rb.AngularDamping = reader.ReadSingle();
                rb.Restitution = reader.ReadSingle();
                rb.Friction = reader.ReadSingle();
                rb.PhysicsMode = reader.ReadByte();
                doc.RigidBodies[i] = rb;
            }
        }

        private void ParseJoints(PmxBinaryReader reader, PmxDocument doc)
        {
            int count = reader.ReadInt32();
            doc.Joints = new PmxJoint[count];
            for (int i = 0; i < count; i++)
            {
                var j = new PmxJoint();
                j.Name = reader.ReadTextBuf();
                j.NameEnglish = reader.ReadTextBuf();
                j.Type = reader.ReadByte();
                j.RigidBodyIndexA = reader.ReadSignedIndex(reader.RigidBodyIndexSize);
                j.RigidBodyIndexB = reader.ReadSignedIndex(reader.RigidBodyIndexSize);
                j.Position = reader.ReadVector3();
                j.RotationEuler = reader.ReadVector3();
                j.PositionLimitMin = reader.ReadVector3();
                j.PositionLimitMax = reader.ReadVector3();
                j.RotationLimitMin = reader.ReadVector3();
                j.RotationLimitMax = reader.ReadVector3();
                j.SpringPosition = reader.ReadVector3();
                j.SpringRotation = reader.ReadVector3();
                doc.Joints[i] = j;
            }
        }
    }
}
