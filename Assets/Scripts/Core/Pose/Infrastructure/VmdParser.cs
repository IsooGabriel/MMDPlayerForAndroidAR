using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    public class VmdParser : IVmdParser
    {
        public IReadOnlyList<VmdBoneFrame> ParseBoneFrames(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            ReadHeader(br, out int modelNameLen);
            br.ReadBytes(modelNameLen); // モデル名は読み飛ばす

            uint count = br.ReadUInt32();
            var list = new List<VmdBoneFrame>((int)count);

            for (uint i = 0; i < count; i++)
            {
                byte[] nameBytes = br.ReadBytes(15);
                int len = Array.IndexOf(nameBytes, (byte)0);
                if (len < 0) len = 15;

                var f = new VmdBoneFrame
                {
                    name = ShiftJisDecoder.GetString(nameBytes, 0, len),
                    frame = br.ReadUInt32(),
                    pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    rot = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
                };

                br.ReadBytes(64); // 補間パラメータは読み飛ばす
                list.Add(f);
            }

            return list;
        }

        /// <summary>
        /// VMDのIK ON/OFF区画（区画5）をパースする。
        /// 古いVMDで区画5が存在しない場合は空辞書を返す。
        /// IK名が現れない場合はオン（true）として扱う。
        /// </summary>
        public IReadOnlyDictionary<string, bool> ParseIkStates(byte[] data)
        {
            var result = new Dictionary<string, bool>();

            try
            {
                using var ms = new MemoryStream(data);
                using var br = new BinaryReader(ms);

                ReadHeader(br, out int modelNameLen);
                br.ReadBytes(modelNameLen); // モデル名は読み飛ばす

                // 区画1: ボーンフレーム (各111バイト: 15+4+12+16+64)
                uint boneCount = br.ReadUInt32();
                br.ReadBytes((int)(boneCount * 111));

                // 区画2: モーフフレーム (各23バイト: 15+4+4)
                uint morphCount = br.ReadUInt32();
                br.ReadBytes((int)(morphCount * 23));

                // 区画3: カメラフレーム (各61バイト)
                uint cameraCount = br.ReadUInt32();
                br.ReadBytes((int)(cameraCount * 61));

                // 区画4: ライトフレーム (各28バイト)
                uint lightCount = br.ReadUInt32();
                br.ReadBytes((int)(lightCount * 28));

                // 区画4.5: セルフシャドウフレーム (各9バイト)
                uint shadowCount = br.ReadUInt32();
                br.ReadBytes((int)(shadowCount * 9));

                // 区画5: モデル表示・IKオン/オフ
                if (ms.Position >= ms.Length)
                    return result;

                uint ikFrameCount = br.ReadUInt32();

                // 最小フレーム番号を採用するために一時辞書を使用
                // key: IK名, value: (最小フレーム, オン/オフ)
                var best = new Dictionary<string, (uint frame, bool on)>();

                for (uint i = 0; i < ikFrameCount; i++)
                {
                    uint frameNo = br.ReadUInt32();
                    br.ReadByte(); // 表示フラグ
                    uint ikCount = br.ReadUInt32();

                    for (uint j = 0; j < ikCount; j++)
                    {
                        byte[] nameBytes = br.ReadBytes(20);
                        int len = Array.IndexOf(nameBytes, (byte)0);
                        if (len < 0) len = 20;
                        string ikName = ShiftJisDecoder.GetString(nameBytes, 0, len);
                        bool on = br.ReadByte() != 0;

                        if (!best.TryGetValue(ikName, out var cur) || frameNo < cur.frame)
                        {
                            best[ikName] = (frameNo, on);
                        }
                    }
                }

                foreach (var kv in best)
                    result[kv.Key] = kv.Value.on;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IK] VMD IK ON/OFF区画のパースに失敗しました（古いVMDの可能性）: {ex.Message}");
            }

            return result;
        }

        private static void ReadHeader(BinaryReader br, out int modelNameLen)
        {
            byte[] headerBytes = br.ReadBytes(30);
            string header = System.Text.Encoding.ASCII.GetString(headerBytes).TrimEnd('\0');

            if (!header.StartsWith("Vocaloid Motion Data"))
            {
                throw new InvalidDataException("VMDファイルではありません");
            }

            modelNameLen = header.EndsWith("0002") ? 20 : 10;
        }
    }
}
