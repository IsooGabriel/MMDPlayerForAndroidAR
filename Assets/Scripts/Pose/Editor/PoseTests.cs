using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using MMDPlayerForVR.Pose.Domain;
using MMDPlayerForVR.Pose.Infrastructure;

namespace MMDPlayerForVR.Pose.Editor.Tests
{
    public class PoseTests
    {
        [Test]
        public void VmdParser_ThrowsOnInvalidHeader()
        {
            byte[] invalidData = new byte[50];
            System.Text.Encoding.ASCII.GetBytes("Invalid Header 0000").CopyTo(invalidData, 0);

            var parser = new VmdParser();
            Assert.Throws<InvalidDataException>(() => parser.ParseBoneFrames(invalidData));
        }

        [Test]
        public void VmdParser_ParsesValidData()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Header (30 bytes)
            byte[] header = System.Text.Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002");
            bw.Write(header);
            bw.Write(new byte[30 - header.Length]);

            // Model name (20 bytes)
            bw.Write(new byte[20]);

            // Count (uint32)
            bw.Write(2u);

            // Frame 1
            byte[] name1 = ShiftJisDecoder.GetEncoding().GetBytes("BoneA");
            byte[] name1Buffer = new byte[15];
            System.Array.Copy(name1, name1Buffer, name1.Length);
            bw.Write(name1Buffer);
            bw.Write(10u); // Frame
            bw.Write(1.0f); bw.Write(2.0f); bw.Write(3.0f); // Pos
            bw.Write(0.0f); bw.Write(0.0f); bw.Write(0.0f); bw.Write(1.0f); // Rot
            bw.Write(new byte[64]); // Interpolation

            // Frame 2
            byte[] name2 = ShiftJisDecoder.GetEncoding().GetBytes("BoneB");
            byte[] name2Buffer = new byte[15];
            System.Array.Copy(name2, name2Buffer, name2.Length);
            bw.Write(name2Buffer);
            bw.Write(0u); // Frame
            bw.Write(4.0f); bw.Write(5.0f); bw.Write(6.0f); // Pos
            bw.Write(0.0f); bw.Write(1.0f); bw.Write(0.0f); bw.Write(0.0f); // Rot
            bw.Write(new byte[64]); // Interpolation

            var parser = new VmdParser();
            var frames = parser.ParseBoneFrames(ms.ToArray());

            Assert.AreEqual(2, frames.Count);
            Assert.AreEqual("BoneA", frames[0].name);
            Assert.AreEqual(10u, frames[0].frame);
            Assert.AreEqual(new Vector3(1, 2, 3), frames[0].pos);
            Assert.AreEqual(new Quaternion(0, 0, 0, 1), frames[0].rot);

            Assert.AreEqual("BoneB", frames[1].name);
            Assert.AreEqual(0u, frames[1].frame);
            Assert.AreEqual(new Vector3(4, 5, 6), frames[1].pos);
        }

        [Test]
        public void PoseState_SelectsMinFrame()
        {
            var frames = new List<VmdBoneFrame>
            {
                new VmdBoneFrame { name = "BoneA", frame = 10, pos = new Vector3(1,1,1) },
                new VmdBoneFrame { name = "BoneA", frame = 0, pos = new Vector3(2,2,2) }, // Best for A
                new VmdBoneFrame { name = "BoneA", frame = 5, pos = new Vector3(3,3,3) },
                new VmdBoneFrame { name = "BoneB", frame = 20, pos = new Vector3(4,4,4) } // Best for B
            };

            var pose = PoseState.FromFirstFrames(frames);

            Assert.AreEqual(2, pose.bones.Count);
            Assert.AreEqual(new Vector3(2,2,2), pose.bones["BoneA"].pos);
            Assert.AreEqual(new Vector3(4,4,4), pose.bones["BoneB"].pos);
        }

        [Test]
        public void MmdCoordinateConverter_MaintainsValues()
        {
            var pos = new Vector3(1, 2, 3);
            var rot = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f);

            var cPos = MmdCoordinateConverter.ToUnityPosition(pos);
            var cRot = MmdCoordinateConverter.ToUnityRotation(rot);

            Assert.AreEqual(pos, cPos);
            Assert.AreEqual(rot, cRot);
        }

        // --- IK関連テスト ---

        /// <summary>VMD IK ON/OFF区画なし（古いVMD）：空辞書を返す</summary>
        [Test]
        public void VmdParser_ParseIkStates_NoIkSection_ReturnsEmpty()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            WriteVmdHeader(bw);
            bw.Write(new byte[20]); // model name

            // ボーンフレーム 0件
            bw.Write(0u);
            // モーフ 0件
            bw.Write(0u);
            // カメラ 0件
            bw.Write(0u);
            // ライト 0件
            bw.Write(0u);
            // セルフシャドウ 0件
            bw.Write(0u);
            // IK区画なし（ここで終わる）

            var parser = new VmdParser();
            var result = parser.ParseIkStates(ms.ToArray());

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>VMD IK ON/OFF区画が0件：空辞書を返す</summary>
        [Test]
        public void VmdParser_ParseIkStates_ZeroEntries_ReturnsEmpty()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            WriteVmdHeader(bw);
            bw.Write(new byte[20]); // model name

            bw.Write(0u); // ボーン
            bw.Write(0u); // モーフ
            bw.Write(0u); // カメラ
            bw.Write(0u); // ライト
            bw.Write(0u); // セルフシャドウ
            bw.Write(0u); // IKフレーム数 0件

            var parser = new VmdParser();
            var result = parser.ParseIkStates(ms.ToArray());

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        /// <summary>VMD IK ON/OFF区画にオフ指定あり：そのIKはfalseになる</summary>
        [Test]
        public void VmdParser_ParseIkStates_OffEntry_ReturnsFalse()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            WriteVmdHeader(bw);
            bw.Write(new byte[20]); // model name

            bw.Write(0u); // ボーン
            bw.Write(0u); // モーフ
            bw.Write(0u); // カメラ
            bw.Write(0u); // ライト
            bw.Write(0u); // セルフシャドウ

            // IKフレーム 1件
            bw.Write(1u);
            bw.Write(0u);   // フレーム番号
            bw.Write((byte)1); // 表示
            bw.Write(2u);   // IK数

            // IK "IkA" ON
            byte[] ikNameA = ShiftJisDecoder.GetEncoding().GetBytes("IkA");
            byte[] bufA = new byte[20];
            System.Array.Copy(ikNameA, bufA, ikNameA.Length);
            bw.Write(bufA);
            bw.Write((byte)1); // ON

            // IK "IkB" OFF
            byte[] ikNameB = ShiftJisDecoder.GetEncoding().GetBytes("IkB");
            byte[] bufB = new byte[20];
            System.Array.Copy(ikNameB, bufB, ikNameB.Length);
            bw.Write(bufB);
            bw.Write((byte)0); // OFF

            var parser = new VmdParser();
            var result = parser.ParseIkStates(ms.ToArray());

            Assert.IsTrue(result["IkA"]);
            Assert.IsFalse(result["IkB"]);
        }

        /// <summary>複数フレームで最小フレーム番号のIK状態を採用する</summary>
        [Test]
        public void VmdParser_ParseIkStates_SelectsMinFrame()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            WriteVmdHeader(bw);
            bw.Write(new byte[20]); // model name

            bw.Write(0u); // ボーン
            bw.Write(0u); // モーフ
            bw.Write(0u); // カメラ
            bw.Write(0u); // ライト
            bw.Write(0u); // セルフシャドウ

            // IKフレーム 2件
            bw.Write(2u);

            // フレーム番号10: IkA=ON
            bw.Write(10u);
            bw.Write((byte)1);
            bw.Write(1u);
            byte[] ikNameA = ShiftJisDecoder.GetEncoding().GetBytes("IkA");
            byte[] bufA = new byte[20];
            System.Array.Copy(ikNameA, bufA, ikNameA.Length);
            bw.Write(bufA);
            bw.Write((byte)1); // ON

            // フレーム番号0: IkA=OFF (こちらが最小フレームなので採用される)
            bw.Write(0u);
            bw.Write((byte)1);
            bw.Write(1u);
            byte[] bufA2 = new byte[20];
            System.Array.Copy(ikNameA, bufA2, ikNameA.Length);
            bw.Write(bufA2);
            bw.Write((byte)0); // OFF

            var parser = new VmdParser();
            var result = parser.ParseIkStates(ms.ToArray());

            Assert.IsFalse(result["IkA"], "最小フレーム(0)のOFF状態が採用されるべき");
        }

        // --- CcdIkSolverテスト（Transformなしで確認できる単純なケース） ---

        /// <summary>IKがオフのとき：Solve()は何も変更しない</summary>
        [Test]
        public void CcdIkSolver_IkOff_NoChange()
        {
            // GameObjectを使ってTransformを構築
            var root = new GameObject("root");
            var bone1 = new GameObject("Bone1");
            var bone2 = new GameObject("Bone2");
            var effector = new GameObject("Effector");
            var ikTarget = new GameObject("IkTarget");

            bone1.transform.SetParent(root.transform, false);
            bone1.transform.localPosition = new Vector3(0, 0, 0);
            bone2.transform.SetParent(bone1.transform, false);
            bone2.transform.localPosition = new Vector3(0, 1, 0);
            effector.transform.SetParent(bone2.transform, false);
            effector.transform.localPosition = new Vector3(0, 1, 0);
            ikTarget.transform.SetParent(root.transform, false);
            ikTarget.transform.localPosition = new Vector3(0, 1.5f, 0.5f); // 到達可能な目標

            var boneMap = new Dictionary<string, Transform>
            {
                { "IkBone", ikTarget.transform },
                { "Effector", effector.transform },
                { "Bone1", bone1.transform },
                { "Bone2", bone2.transform },
            };

            var chain = new IkChain
            {
                IkBoneName = "IkBone",
                TargetBoneName = "Effector",
                LoopCount = 10,
                LimitAngleRadians = Mathf.PI,
                Links = new[]
                {
                    new IkLinkData { BoneName = "Bone2", HasAngleLimit = false },
                    new IkLinkData { BoneName = "Bone1", HasAngleLimit = false },
                }
            };

            Quaternion bone1RotBefore = bone1.transform.localRotation;
            Quaternion bone2RotBefore = bone2.transform.localRotation;

            var solver = new CcdIkSolver(boneMap);
            var ikStates = new Dictionary<string, bool> { { "IkBone", false } }; // IKオフ

            solver.Solve(new[] { chain }, ikStates);

            // IKオフなので回転が変わらないこと
            Assert.AreEqual(bone1RotBefore, bone1.transform.localRotation);
            Assert.AreEqual(bone2RotBefore, bone2.transform.localRotation);

            // クリーンアップ
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(ikTarget);
        }

        /// <summary>エフェクターと目標が一致するとき：回転が変化しない</summary>
        [Test]
        public void CcdIkSolver_EffectorAtTarget_NoChange()
        {
            var root = new GameObject("root");
            var bone1 = new GameObject("Bone1");
            var effector = new GameObject("Effector");
            var ikTarget = new GameObject("IkTarget");

            bone1.transform.SetParent(root.transform, false);
            bone1.transform.localPosition = Vector3.zero;
            effector.transform.SetParent(bone1.transform, false);
            effector.transform.localPosition = new Vector3(0, 1, 0);
            // IKターゲットをエフェクターと同じ位置に
            ikTarget.transform.position = effector.transform.position;

            var boneMap = new Dictionary<string, Transform>
            {
                { "IkBone", ikTarget.transform },
                { "Effector", effector.transform },
                { "Bone1", bone1.transform },
            };

            var chain = new IkChain
            {
                IkBoneName = "IkBone",
                TargetBoneName = "Effector",
                LoopCount = 10,
                LimitAngleRadians = Mathf.PI,
                Links = new[] { new IkLinkData { BoneName = "Bone1", HasAngleLimit = false } }
            };

            Quaternion before = bone1.transform.localRotation;
            var solver = new CcdIkSolver(boneMap);
            solver.Solve(new[] { chain }, new Dictionary<string, bool>());

            // 既に収束しているので大きな回転変化はないこと
            float angleDiff = Quaternion.Angle(before, bone1.transform.localRotation);
            Assert.Less(angleDiff, 1f, "エフェクターが目標にいる場合、回転変化は最小のはず");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(ikTarget);
        }

        /// <summary>到達不能な目標：例外・NaNなし</summary>
        [Test]
        public void CcdIkSolver_UnreachableTarget_NoExceptionNoNaN()
        {
            var root = new GameObject("root");
            var bone1 = new GameObject("Bone1");
            var effector = new GameObject("Effector");
            var ikTarget = new GameObject("IkTarget");

            bone1.transform.SetParent(root.transform, false);
            bone1.transform.localPosition = Vector3.zero;
            effector.transform.SetParent(bone1.transform, false);
            effector.transform.localPosition = new Vector3(0, 1, 0);
            // 到達不能な遠い目標
            ikTarget.transform.localPosition = new Vector3(0, 100, 0);

            var boneMap = new Dictionary<string, Transform>
            {
                { "IkBone", ikTarget.transform },
                { "Effector", effector.transform },
                { "Bone1", bone1.transform },
            };

            var chain = new IkChain
            {
                IkBoneName = "IkBone",
                TargetBoneName = "Effector",
                LoopCount = 5,
                LimitAngleRadians = Mathf.PI,
                Links = new[] { new IkLinkData { BoneName = "Bone1", HasAngleLimit = false } }
            };

            Assert.DoesNotThrow(() =>
            {
                var solver = new CcdIkSolver(boneMap);
                solver.Solve(new[] { chain }, new Dictionary<string, bool>());
            });

            // NaNチェック
            var rot = bone1.transform.localRotation;
            Assert.IsFalse(float.IsNaN(rot.x), "NaN x");
            Assert.IsFalse(float.IsNaN(rot.y), "NaN y");
            Assert.IsFalse(float.IsNaN(rot.z), "NaN z");
            Assert.IsFalse(float.IsNaN(rot.w), "NaN w");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(ikTarget);
        }

        /// <summary>ひざの角度制限：逆方向に曲がらない（下限0°の場合）</summary>
        [Test]
        public void CcdIkSolver_KneeAngleLimit_NoReverse()
        {
            var root = new GameObject("root");
            var thigh = new GameObject("Thigh");
            var knee = new GameObject("Knee");
            var foot = new GameObject("Foot");
            var ikTarget = new GameObject("IkTarget");

            thigh.transform.SetParent(root.transform, false);
            thigh.transform.localPosition = Vector3.zero;
            knee.transform.SetParent(thigh.transform, false);
            knee.transform.localPosition = new Vector3(0, -1, 0);
            foot.transform.SetParent(knee.transform, false);
            foot.transform.localPosition = new Vector3(0, -1, 0);
            // IKターゲットを逆方向（足が逆に曲がる方向）に設定
            ikTarget.transform.position = new Vector3(0, -0.5f, -0.5f);

            var boneMap = new Dictionary<string, Transform>
            {
                { "FootIK", ikTarget.transform },
                { "Foot", foot.transform },
                { "Knee", knee.transform },
                { "Thigh", thigh.transform },
            };

            // ひざのX軸制限：-179.9°〜-0.5°（負方向のみ曲がる）
            float lowerRad = -179.9f * Mathf.Deg2Rad;
            float upperRad = -0.5f * Mathf.Deg2Rad;

            var chain = new IkChain
            {
                IkBoneName = "FootIK",
                TargetBoneName = "Foot",
                LoopCount = 5,
                LimitAngleRadians = Mathf.PI,
                Links = new[]
                {
                    new IkLinkData { BoneName = "Knee", HasAngleLimit = true,
                        LowerLimit = new Vector3(lowerRad, 0, 0),
                        UpperLimit = new Vector3(upperRad, 0, 0) },
                    new IkLinkData { BoneName = "Thigh", HasAngleLimit = false },
                }
            };

            Assert.DoesNotThrow(() =>
            {
                var solver = new CcdIkSolver(boneMap);
                solver.Solve(new[] { chain }, new Dictionary<string, bool>());
            });

            // ひざのX角度は-179.9〜-0.5の範囲内か確認
            float kneeX = knee.transform.localEulerAngles.x;
            if (kneeX > 180f) kneeX -= 360f;
            Assert.GreaterOrEqual(kneeX, -179.9f - 0.1f, "ひざの下限を超えていない");
            Assert.LessOrEqual(kneeX, -0.5f + 0.1f, "ひざの上限を超えていない");

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(ikTarget);
        }

        // ヘルパー
        private static void WriteVmdHeader(BinaryWriter bw)
        {
            byte[] header = System.Text.Encoding.ASCII.GetBytes("Vocaloid Motion Data 0002");
            bw.Write(header);
            bw.Write(new byte[30 - header.Length]);
        }
    }
}
