// XSM File Format Parser for EMotionFX Skeletal Motion files
// Used for animation data in Tree of Savior

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IPFBrowser.FileFormats.XSM
{
    /// <summary>
    /// Parser for EMotionFX .xsm (Skeletal Motion) animation files
    /// </summary>
    public class XsmFile
    {
        public XsmHeader Header { get; private set; }
        public XsmMetadata Metadata { get; private set; }
        public List<XsmMotionPart> MotionParts { get; private set; }
        public List<XsmChunk> RawChunks { get; private set; }

        public XsmFile()
        {
            MotionParts = new List<XsmMotionPart>();
            RawChunks = new List<XsmChunk>();
        }

        public static XsmFile Load(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                return Load(br);
            }
        }

        public static XsmFile Load(BinaryReader br)
        {
            var xsm = new XsmFile();

            // Read header - same format as XAC
            xsm.Header = new XsmHeader
            {
                Magic = Encoding.ASCII.GetString(br.ReadBytes(4)),
                MajorVersion = br.ReadByte(),
                MinorVersion = br.ReadByte(),
                IsBigEndian = br.ReadByte() != 0,
                Unused = br.ReadByte()
            };

            if (xsm.Header.Magic != "XSM ")
            {
                throw new InvalidDataException($"Invalid XSM magic: expected 'XSM ', got '{xsm.Header.Magic}'");
            }

            // Read chunks until end of file
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                if (br.BaseStream.Length - br.BaseStream.Position < 12)
                    break;

                var chunkType = (XsmChunkType)br.ReadInt32();
                var chunkLength = br.ReadInt32();
                var chunkVersion = br.ReadInt32();

                var chunk = new XsmChunk
                {
                    Type = chunkType,
                    Length = chunkLength,
                    Version = chunkVersion,
                    DataOffset = br.BaseStream.Position
                };

                chunk.RawData = br.ReadBytes(chunkLength);
                xsm.RawChunks.Add(chunk);

                using (var chunkMs = new MemoryStream(chunk.RawData))
                using (var chunkBr = new BinaryReader(chunkMs))
                {
                    try
                    {
                        switch (chunkType)
                        {
                            case XsmChunkType.Metadata:
                                xsm.Metadata = ParseMetadata(chunkBr, chunkVersion);
                                break;
                            case XsmChunkType.MotionPart:
                                xsm.MotionParts.Add(ParseMotionPart(chunkBr, chunkVersion));
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        chunk.ParseError = ex.Message;
                    }
                }
            }

            return xsm;
        }

        private static XsmMetadata ParseMetadata(BinaryReader br, int version)
        {
            var metadata = new XsmMetadata();

            if (version >= 2)
            {
                metadata.Unused = br.ReadSingle();
                metadata.MaxAcceptableError = br.ReadSingle();
                metadata.FPS = br.ReadSingle();
                metadata.ExporterMajorVersion = br.ReadByte();
                metadata.ExporterMinorVersion = br.ReadByte();
                br.ReadBytes(2); // padding
                metadata.SourceApp = ReadString(br);
                metadata.OriginalFileName = ReadString(br);
                metadata.ExportDate = ReadString(br);
                metadata.MotionName = ReadString(br);
            }

            return metadata;
        }

        private static XsmMotionPart ParseMotionPart(BinaryReader br, int version)
        {
            var part = new XsmMotionPart();

            if (version >= 2)
            {
                part.PosePosition = ReadVector3(br);
                part.PoseRotation = ReadQuaternion(br);
                part.BindPosePosition = ReadVector3(br);
                part.BindPoseRotation = ReadQuaternion(br);
                part.PoseScale = ReadVector3(br);
                part.BindPoseScale = ReadVector3(br);

                part.NumPositionKeys = br.ReadInt32();
                part.NumRotationKeys = br.ReadInt32();
                part.NumScaleKeys = br.ReadInt32();
                part.NumScaleRotationKeys = br.ReadInt32();
                part.MaxError = br.ReadSingle();
                part.Name = ReadString(br);

                // Read position keyframes
                part.PositionKeys = new List<XsmKeyframe>();
                for (int i = 0; i < part.NumPositionKeys; i++)
                {
                    part.PositionKeys.Add(new XsmKeyframe
                    {
                        Position = ReadVector3(br),
                        Time = br.ReadSingle()
                    });
                }

                // Read rotation keyframes
                part.RotationKeys = new List<XsmKeyframe>();
                for (int i = 0; i < part.NumRotationKeys; i++)
                {
                    part.RotationKeys.Add(new XsmKeyframe
                    {
                        Rotation = ReadQuaternion(br),
                        Time = br.ReadSingle()
                    });
                }

                // Read scale keyframes
                part.ScaleKeys = new List<XsmKeyframe>();
                for (int i = 0; i < part.NumScaleKeys; i++)
                {
                    part.ScaleKeys.Add(new XsmKeyframe
                    {
                        Scale = ReadVector3(br),
                        Time = br.ReadSingle()
                    });
                }

                // Read scale rotation keyframes
                part.ScaleRotationKeys = new List<XsmKeyframe>();
                for (int i = 0; i < part.NumScaleRotationKeys; i++)
                {
                    part.ScaleRotationKeys.Add(new XsmKeyframe
                    {
                        Rotation = ReadQuaternion(br),
                        Time = br.ReadSingle()
                    });
                }
            }

            return part;
        }

        #region Helper Methods

        private static string ReadString(BinaryReader br)
        {
            var length = br.ReadInt32();
            if (length <= 0) return string.Empty;
            var bytes = br.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static Vector3 ReadVector3(BinaryReader br)
        {
            return new Vector3
            {
                X = br.ReadSingle(),
                Y = br.ReadSingle(),
                Z = br.ReadSingle()
            };
        }

        private static Quaternion ReadQuaternion(BinaryReader br)
        {
            return new Quaternion
            {
                X = br.ReadSingle(),
                Y = br.ReadSingle(),
                Z = br.ReadSingle(),
                W = br.ReadSingle()
            };
        }

        #endregion
    }

    #region Data Structures

    public enum XsmChunkType
    {
        Metadata = 0,
        MotionPart = 1,
        MorphTarget = 2,
        MorphMotionPart = 3
    }

    public class XsmHeader
    {
        public string Magic { get; set; }
        public byte MajorVersion { get; set; }
        public byte MinorVersion { get; set; }
        public bool IsBigEndian { get; set; }
        public byte Unused { get; set; }
    }

    public class XsmChunk
    {
        public XsmChunkType Type { get; set; }
        public int Length { get; set; }
        public int Version { get; set; }
        public long DataOffset { get; set; }
        public byte[] RawData { get; set; }
        public string ParseError { get; set; }
    }

    public class XsmMetadata
    {
        public float Unused { get; set; }
        public float MaxAcceptableError { get; set; }
        public float FPS { get; set; }
        public byte ExporterMajorVersion { get; set; }
        public byte ExporterMinorVersion { get; set; }
        public string SourceApp { get; set; }
        public string OriginalFileName { get; set; }
        public string ExportDate { get; set; }
        public string MotionName { get; set; }
    }

    public class XsmMotionPart
    {
        public string Name { get; set; }
        public Vector3 PosePosition { get; set; }
        public Quaternion PoseRotation { get; set; }
        public Vector3 BindPosePosition { get; set; }
        public Quaternion BindPoseRotation { get; set; }
        public Vector3 PoseScale { get; set; }
        public Vector3 BindPoseScale { get; set; }
        public int NumPositionKeys { get; set; }
        public int NumRotationKeys { get; set; }
        public int NumScaleKeys { get; set; }
        public int NumScaleRotationKeys { get; set; }
        public float MaxError { get; set; }
        public List<XsmKeyframe> PositionKeys { get; set; }
        public List<XsmKeyframe> RotationKeys { get; set; }
        public List<XsmKeyframe> ScaleKeys { get; set; }
        public List<XsmKeyframe> ScaleRotationKeys { get; set; }
    }

    public class XsmKeyframe
    {
        public float Time { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
    }

    // Reuse math types from XAC
    public struct Vector3
    {
        public float X, Y, Z;
        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }

    public struct Quaternion
    {
        public float X, Y, Z, W;
        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3}, {W:F3})";
    }

    #endregion
}
