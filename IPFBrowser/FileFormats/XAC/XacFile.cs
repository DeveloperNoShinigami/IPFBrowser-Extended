// XAC File Format Parser for EMotionFX Actor files
// Based on O3DE Actor format specification and TOS-specific extensions

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IPFBrowser.FileFormats.XAC
{
    /// <summary>
    /// Parser for EMotionFX .xac (Actor) files used in Tree of Savior
    /// </summary>
    public class XacFile
    {
        public XacHeader Header { get; private set; }
        public XacMetadata Metadata { get; private set; }
        public List<XacNode> Nodes { get; private set; }
        public List<XacMaterial> Materials { get; private set; }
        public List<XacMesh> Meshes { get; private set; }
        public List<XacProperties> PropertiesList { get; private set; }  // Multiple PROPERTIES chunks
        public List<XacChunk> RawChunks { get; private set; }
        
        // Backward compatibility - returns first properties or null
        public XacProperties Properties => PropertiesList.Count > 0 ? PropertiesList[0] : null;
        
        /// <summary>
        /// Get all DiffuseTex texture names from all PROPERTIES chunks
        /// </summary>
        public List<string> GetAllDiffuseTextures()
        {
            var textures = new List<string>();
            
            // First try from parsed properties
            foreach (var props in PropertiesList)
            {
                if (props.StringProperties != null && props.StringProperties.TryGetValue("DiffuseTex", out var tex))
                {
                    if (!string.IsNullOrEmpty(tex) && !textures.Contains(tex))
                        textures.Add(tex);
                }
            }
            
            // If no textures found, scan raw chunks for DiffuseTex pattern
            // TOS FX materials store DiffuseTex in a non-standard format
            if (textures.Count == 0)
            {
                foreach (var chunk in RawChunks)
                {
                    if (chunk.RawData == null) continue;
                    
                    // Look for "DiffuseTex" string in raw data
                    var found = ScanForDiffuseTex(chunk.RawData);
                    foreach (var tex in found)
                    {
                        if (!textures.Contains(tex))
                            textures.Add(tex);
                    }
                }
            }
            
            return textures;
        }
        
        /// <summary>
        /// Scan binary data for DiffuseTex property and extract the texture filename
        /// </summary>
        private List<string> ScanForDiffuseTex(byte[] data)
        {
            var results = new List<string>();
            var marker = Encoding.ASCII.GetBytes("DiffuseTex");
            
            for (int i = 0; i <= data.Length - marker.Length - 8; i++)
            {
                bool match = true;
                for (int j = 0; j < marker.Length; j++)
                {
                    if (data[i + j] != marker[j])
                    {
                        match = false;
                        break;
                    }
                }
                
                if (match)
                {
                    // Found "DiffuseTex", now read the string value after it
                    // Format: [DiffuseTex][4 bytes length][string data]
                    int offset = i + marker.Length;
                    if (offset + 4 <= data.Length)
                    {
                        int strLen = BitConverter.ToInt32(data, offset);
                        if (strLen > 0 && strLen < 256 && offset + 4 + strLen <= data.Length)
                        {
                            string texName = Encoding.UTF8.GetString(data, offset + 4, strLen);
                            // Clean up the texture name (remove path, underscores to spaces etc)
                            texName = texName.Replace('_', ' ').Trim();
                            if (!string.IsNullOrEmpty(texName) && !results.Contains(texName))
                            {
                                results.Add(texName);
                            }
                        }
                    }
                }
            }
            
            return results;
        }

        public XacFile()
        {
            Nodes = new List<XacNode>();
            Materials = new List<XacMaterial>();
            Meshes = new List<XacMesh>();
            PropertiesList = new List<XacProperties>();
            RawChunks = new List<XacChunk>();
        }

        public static XacFile Load(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                return Load(br);
            }
        }

        public static XacFile Load(BinaryReader br)
        {
            var xac = new XacFile();

            // Read header
            xac.Header = new XacHeader
            {
                Magic = Encoding.ASCII.GetString(br.ReadBytes(4)),
                MajorVersion = br.ReadByte(),
                MinorVersion = br.ReadByte(),
                IsBigEndian = br.ReadByte() != 0,
                MultiplyOrder = br.ReadByte()
            };

            if (xac.Header.Magic != "XAC ")
            {
                throw new InvalidDataException($"Invalid XAC magic: expected 'XAC ', got '{xac.Header.Magic}'");
            }

            // Read chunks until end of file
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                var chunkStartPos = br.BaseStream.Position;
                
                if (br.BaseStream.Length - br.BaseStream.Position < 12)
                    break;

                var chunkType = (XacChunkType)br.ReadInt32();
                var chunkLength = br.ReadInt32();
                var chunkVersion = br.ReadInt32();

                var chunk = new XacChunk
                {
                    Type = chunkType,
                    Length = chunkLength,
                    Version = chunkVersion,
                    DataOffset = br.BaseStream.Position
                };

                // Store raw chunk data
                chunk.RawData = br.ReadBytes(chunkLength);
                xac.RawChunks.Add(chunk);

                // Parse specific chunk types
                using (var chunkMs = new MemoryStream(chunk.RawData))
                using (var chunkBr = new BinaryReader(chunkMs))
                {
                    try
                    {
                        switch (chunkType)
                        {
                            case XacChunkType.Metadata:
                                xac.Metadata = ParseMetadata(chunkBr, chunkVersion);
                                break;
                            case XacChunkType.NodeHierarchy:
                                xac.Nodes = ParseNodeHierarchy(chunkBr, chunkVersion);
                                break;
                            case XacChunkType.MaterialTotals:
                                // Just counts, materials come in MaterialDefinition chunks
                                break;
                            case XacChunkType.MaterialDefinition:
                                xac.Materials.Add(ParseMaterial(chunkBr, chunkVersion));
                                break;
                            case XacChunkType.Mesh:
                                xac.Meshes.Add(ParseMesh(chunkBr, chunkVersion));
                                break;
                            case XacChunkType.Properties:
                                xac.PropertiesList.Add(ParseProperties(chunkBr));
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Store error but continue parsing other chunks
                        chunk.ParseError = ex.Message;
                    }
                }
            }

            return xac;
        }

        private static XacMetadata ParseMetadata(BinaryReader br, int version)
        {
            var metadata = new XacMetadata
            {
                RepositionMask = br.ReadUInt32(),
                RepositioningNode = br.ReadInt32(),
                ExporterMajorVersion = br.ReadByte(),
                ExporterMinorVersion = br.ReadByte()
            };
            br.ReadBytes(2); // padding
            metadata.RetargetRootOffset = br.ReadSingle();

            metadata.SourceApp = ReadString(br);
            metadata.OriginalFileName = ReadString(br);
            metadata.ExportDate = ReadString(br);
            metadata.ActorName = ReadString(br);

            return metadata;
        }

        private static List<XacNode> ParseNodeHierarchy(BinaryReader br, int version)
        {
            var nodes = new List<XacNode>();
            var numNodes = br.ReadInt32();
            var numRootNodes = br.ReadInt32();

            for (int i = 0; i < numNodes; i++)
            {
                var node = new XacNode
                {
                    Rotation = ReadQuaternion(br),
                    ScaleRotation = ReadQuaternion(br),
                    Position = ReadVector3(br),
                    Scale = ReadVector3(br)
                };

                br.ReadBytes(12); // padding (3 floats)
                node.UnknownIndex1 = br.ReadInt32();
                node.UnknownIndex2 = br.ReadInt32();
                node.ParentNodeId = br.ReadInt32();
                node.NumChildren = br.ReadInt32();
                node.IncludeInBoundsCalc = br.ReadInt32();
                node.Transform = ReadMatrix44(br);
                node.ImportanceFactor = br.ReadSingle();
                node.Name = ReadString(br);

                nodes.Add(node);
            }

            return nodes;
        }

        private static XacMaterial ParseMaterial(BinaryReader br, int version)
        {
            var material = new XacMaterial
            {
                AmbientColor = ReadColor(br),
                DiffuseColor = ReadColor(br),
                SpecularColor = ReadColor(br),
                EmissiveColor = ReadColor(br),
                Shine = br.ReadSingle(),
                ShineStrength = br.ReadSingle(),
                Opacity = br.ReadSingle(),
                IOR = br.ReadSingle(),
                IsDoubleSided = br.ReadByte() != 0,
                IsWireframe = br.ReadByte() != 0
            };
            br.ReadByte(); // padding
            var numLayers = br.ReadByte();

            material.Name = ReadString(br);
            material.Layers = new List<XacMaterialLayer>();

            for (int i = 0; i < numLayers; i++)
            {
                var layer = new XacMaterialLayer
                {
                    Amount = br.ReadSingle(),
                    OffsetU = br.ReadSingle(),
                    OffsetV = br.ReadSingle(),
                    TilingU = br.ReadSingle(),
                    TilingV = br.ReadSingle(),
                    Rotation = br.ReadSingle(),
                    MaterialId = br.ReadInt16(),
                    MapType = br.ReadByte()
                };
                br.ReadByte(); // padding
                layer.Texture = ReadString(br);
                material.Layers.Add(layer);
            }

            return material;
        }

        private static XacMesh ParseMesh(BinaryReader br, int version)
        {
            var mesh = new XacMesh
            {
                NodeIndex = br.ReadInt32(),
                NumInfluenceRanges = br.ReadInt32(),
                NumVertices = br.ReadInt32(),
                NumIndices = br.ReadInt32(),
                NumSubMeshes = br.ReadInt32(),
                NumAttribLayers = br.ReadInt32(),
                IsCollisionMesh = br.ReadByte() != 0
            };
            br.ReadBytes(3); // padding

            // FIRST: Read vertex attribute layers (positions, normals, UVs, etc.)
            mesh.VertexAttributes = new List<XacVertexAttributeLayer>();
            for (int i = 0; i < mesh.NumAttribLayers; i++)
            {
                var attrib = new XacVertexAttributeLayer
                {
                    Type = br.ReadInt32(),        // Usage: 0=position, 1=normal, 2=tangent, 3=uv, 4=color32, 5=influenceIdx, 6=color128
                    AttribSize = br.ReadInt32(),  // Size per element in bytes
                    KeepOriginals = br.ReadByte() != 0,
                    IsScaleFactor = br.ReadByte() != 0
                };
                br.ReadBytes(2); // padding

                // Read attribute data (size = AttribSize * NumVertices)
                var dataSize = attrib.AttribSize * mesh.NumVertices;
                attrib.Data = br.ReadBytes(dataSize);
                mesh.VertexAttributes.Add(attrib);
            }

            // SECOND: Read submeshes - each submesh contains its own indices and bone references
            mesh.SubMeshes = new List<XacSubMesh>();
            var allIndices = new List<int>();
            
            for (int i = 0; i < mesh.NumSubMeshes; i++)
            {
                var subMesh = new XacSubMesh
                {
                    NumIndices = br.ReadInt32(),
                    NumVertices = br.ReadInt32(),
                    MaterialId = br.ReadInt32(),
                    NumBones = br.ReadInt32()
                };
                
                // Read indices for this submesh
                subMesh.Indices = new int[subMesh.NumIndices];
                for (int j = 0; j < subMesh.NumIndices; j++)
                {
                    subMesh.Indices[j] = br.ReadInt32();
                    allIndices.Add(subMesh.Indices[j]);
                }
                
                // Skip bone indices (NumBones * 4 bytes each)
                br.ReadBytes(subMesh.NumBones * 4);
                
                mesh.SubMeshes.Add(subMesh);
            }
            
            // Store all indices combined
            mesh.Indices = allIndices.ToArray();

            return mesh;
        }

        private static XacProperties ParseProperties(BinaryReader br)
        {
            var props = new XacProperties();

            props.IntCount = br.ReadInt32();
            props.FloatCount = br.ReadInt32();
            br.ReadInt32(); // padding
            props.BoolCount = br.ReadInt32();
            br.ReadInt32(); // padding  
            props.StringCount = br.ReadInt32();

            props.Name = ReadString(br);
            props.FxFileName = ReadString(br);
            props.ShaderName = ReadString(br);

            // Note: Property order is NAME first, then VALUE (verified from Python parser)
            props.IntProperties = new Dictionary<string, int>();
            for (int i = 0; i < props.IntCount; i++)
            {
                var name = ReadString(br);
                var value = br.ReadInt32();
                props.IntProperties[name] = value;
            }

            props.FloatProperties = new Dictionary<string, float>();
            for (int i = 0; i < props.FloatCount; i++)
            {
                var name = ReadString(br);
                var value = br.ReadSingle();
                props.FloatProperties[name] = value;
            }

            props.BoolProperties = new Dictionary<string, bool>();
            for (int i = 0; i < props.BoolCount; i++)
            {
                var name = ReadString(br);
                var value = br.ReadByte() != 0;
                props.BoolProperties[name] = value;
            }

            props.StringProperties = new Dictionary<string, string>();
            for (int i = 0; i < props.StringCount; i++)
            {
                // String properties have name first, then value
                var name = ReadString(br);
                var value = ReadString(br);
                props.StringProperties[name] = value;
            }

            return props;
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

        private static Matrix44 ReadMatrix44(BinaryReader br)
        {
            var m = new Matrix44(true);  // Initialize with array
            for (int i = 0; i < 16; i++)
            {
                m.Values[i] = br.ReadSingle();
            }
            return m;
        }

        private static Color4 ReadColor(BinaryReader br)
        {
            return new Color4
            {
                R = br.ReadSingle(),
                G = br.ReadSingle(),
                B = br.ReadSingle(),
                A = br.ReadSingle()
            };
        }

        #endregion
    }

    #region Data Structures

    public enum XacChunkType
    {
        Mesh = 1,
        BoneInfluences = 2,
        MaterialDefinition = 3,
        Properties = 5,
        Metadata = 7,
        NodeHierarchy = 11,
        MorphTargets = 12,
        MaterialTotals = 13
    }

    public class XacHeader
    {
        public string Magic { get; set; }
        public byte MajorVersion { get; set; }
        public byte MinorVersion { get; set; }
        public bool IsBigEndian { get; set; }
        public byte MultiplyOrder { get; set; }
    }

    public class XacChunk
    {
        public XacChunkType Type { get; set; }
        public int Length { get; set; }
        public int Version { get; set; }
        public long DataOffset { get; set; }
        public byte[] RawData { get; set; }
        public string ParseError { get; set; }
    }

    public class XacMetadata
    {
        public uint RepositionMask { get; set; }
        public int RepositioningNode { get; set; }
        public byte ExporterMajorVersion { get; set; }
        public byte ExporterMinorVersion { get; set; }
        public float RetargetRootOffset { get; set; }
        public string SourceApp { get; set; }
        public string OriginalFileName { get; set; }
        public string ExportDate { get; set; }
        public string ActorName { get; set; }
    }

    public class XacNode
    {
        public string Name { get; set; }
        public Quaternion Rotation { get; set; }
        public Quaternion ScaleRotation { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Scale { get; set; }
        public int UnknownIndex1 { get; set; }
        public int UnknownIndex2 { get; set; }
        public int ParentNodeId { get; set; }
        public int NumChildren { get; set; }
        public int IncludeInBoundsCalc { get; set; }
        public Matrix44 Transform { get; set; }
        public float ImportanceFactor { get; set; }
    }

    public class XacMaterial
    {
        public string Name { get; set; }
        public Color4 AmbientColor { get; set; }
        public Color4 DiffuseColor { get; set; }
        public Color4 SpecularColor { get; set; }
        public Color4 EmissiveColor { get; set; }
        public float Shine { get; set; }
        public float ShineStrength { get; set; }
        public float Opacity { get; set; }
        public float IOR { get; set; }
        public bool IsDoubleSided { get; set; }
        public bool IsWireframe { get; set; }
        public List<XacMaterialLayer> Layers { get; set; }
    }

    public class XacMaterialLayer
    {
        public float Amount { get; set; }
        public float OffsetU { get; set; }
        public float OffsetV { get; set; }
        public float TilingU { get; set; }
        public float TilingV { get; set; }
        public float Rotation { get; set; }
        public short MaterialId { get; set; }
        public byte MapType { get; set; }
        public string Texture { get; set; }
    }

    public class XacMesh
    {
        public int NodeIndex { get; set; }
        public int NumInfluenceRanges { get; set; }
        public int NumVertices { get; set; }
        public int NumIndices { get; set; }
        public int NumSubMeshes { get; set; }
        public int NumAttribLayers { get; set; }
        public bool IsCollisionMesh { get; set; }
        public List<XacSubMesh> SubMeshes { get; set; }
        public List<XacVertexAttributeLayer> VertexAttributes { get; set; }
        public int[] Indices { get; set; }
    }

    public class XacSubMesh
    {
        public int NumIndices { get; set; }
        public int NumVertices { get; set; }
        public int MaterialId { get; set; }
        public int NumBones { get; set; }
        public int[] Indices { get; set; }  // Indices for this submesh
        public int VertexOffset { get; set; }  // Offset into the mesh's vertex data
    }

    public class XacVertexAttributeLayer
    {
        public int Type { get; set; }  // 0=positions, 1=normals, 2=tangents, 3=uvs, 4=colors32, etc.
        public int AttribSize { get; set; }
        public bool KeepOriginals { get; set; }
        public bool IsScaleFactor { get; set; }
        public byte[] Data { get; set; }
    }

    public class XacProperties
    {
        public string Name { get; set; }
        public string FxFileName { get; set; }
        public string ShaderName { get; set; }
        public int IntCount { get; set; }
        public int FloatCount { get; set; }
        public int BoolCount { get; set; }
        public int StringCount { get; set; }
        public Dictionary<string, int> IntProperties { get; set; }
        public Dictionary<string, float> FloatProperties { get; set; }
        public Dictionary<string, bool> BoolProperties { get; set; }
        public Dictionary<string, string> StringProperties { get; set; }
    }

    // Math types
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

    public struct Color4
    {
        public float R, G, B, A;
        public override string ToString() => $"RGBA({R:F2}, {G:F2}, {B:F2}, {A:F2})";
    }

    public struct Matrix44
    {
        public float[] Values;
        public Matrix44(bool init = true) { Values = new float[16]; }
    }

    #endregion
}
