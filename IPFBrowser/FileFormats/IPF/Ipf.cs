// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace IPFBrowser.FileFormats.IPF
{
	public class Ipf
	{
		private object _extractLock = new object();
		private Stream _stream;
		private BinaryReader _br;

		private static readonly byte[] ZIP_MAGIC = new byte[] { 0x50, 0x4B, 0x05, 0x06 };
		private const uint ENCODED_START_VERSION = 11035;

		public string FilePath { get; private set; }
		public List<IpfFile> Files { get; private set; }
		public IpfFooter Footer { get; private set; }

		public Ipf(string filePath)
		{
			_stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			_br = new BinaryReader(_stream);

			this.FilePath = filePath;
			this.Load();
		}

		public void Close()
		{
			if (_stream != null)
				_stream.Dispose();

			if (_br != null)
				_br.Dispose();
		}

		public void Load()
		{
			_stream.Position = _stream.Length - 0x18;

			this.Footer = new IpfFooter();
			this.Footer.FileCount = _br.ReadUInt16();
			var fileTableOffset = _br.ReadUInt32();
			_br.ReadUInt16();
			_br.ReadUInt32();
			_br.ReadUInt32(); // compression
			this.Footer.PatchVersion = _br.ReadUInt32();
			this.Footer.NewVersion = _br.ReadUInt32();

			_stream.Position = fileTableOffset;

			this.Files = new List<IpfFile>();
			for (int i = 0; i < this.Footer.FileCount; i++)
			{
				var ipfFile = new IpfFile(this);

				var pathLength = _br.ReadUInt16();
				ipfFile.Crc32 = _br.ReadUInt32();
				ipfFile.SizeCompressed = _br.ReadUInt32();
				ipfFile.SizeUncompressed = _br.ReadUInt32();
				ipfFile.Offset = _br.ReadUInt32();

				var length = _br.ReadUInt16();
				ipfFile.PackFileName = new string(_br.ReadChars(length));

				var path = new string(_br.ReadChars(pathLength));
				ipfFile.Path = path.Replace("\\", "/");
				ipfFile.FullPath = System.IO.Path.Combine(ipfFile.PackFileName, path).Replace("\\", "/");

				this.Files.Add(ipfFile);
			}
		}

		public byte[] ReadData(long offset, int length)
		{
			byte[] data;

			lock (_extractLock)
			{
				_stream.Position = offset;
				data = _br.ReadBytes(length);
			}

			return data;
		}

		/// <summary>
		/// Updates a file within the IPF and saves to a new file path.
		/// </summary>
		public void SaveWithUpdatedFile(string outputPath, string fileToUpdate, byte[] newData)
		{
			var filesToUpdate = new Dictionary<string, byte[]> { { fileToUpdate, newData } };
			SaveWithUpdatedFiles(outputPath, filesToUpdate);
		}

		/// <summary>
		/// Updates multiple files within the IPF and saves to a new file path.
		/// </summary>
		public void SaveWithUpdatedFiles(string outputPath, Dictionary<string, byte[]> updatedFiles)
		{
			SaveWithUpdatedAndNewFiles(outputPath, updatedFiles, null, null);
		}

		/// <summary>
		/// Updates existing files and adds new files, then saves to a new file path.
		/// </summary>
		/// <param name="outputPath">The output file path</param>
		/// <param name="updatedFiles">Dictionary of existing file paths to updated data</param>
		/// <param name="newFiles">Dictionary of new file paths to data (will be added to IPF)</param>
		public void SaveWithUpdatedAndNewFiles(string outputPath, Dictionary<string, byte[]> updatedFiles, Dictionary<string, byte[]> newFiles)
		{
			SaveWithUpdatedAndNewFiles(outputPath, updatedFiles, newFiles, null);
		}

		/// <summary>
		/// Updates existing files, adds new files, and removes deleted files, then saves to a new file path.
		/// </summary>
		/// <param name="outputPath">The output file path</param>
		/// <param name="updatedFiles">Dictionary of existing file paths to updated data</param>
		/// <param name="newFiles">Dictionary of new file paths to data (will be added to IPF)</param>
		/// <param name="deletedFiles">Set of file paths to exclude from the new IPF</param>
		public void SaveWithUpdatedAndNewFiles(string outputPath, Dictionary<string, byte[]> updatedFiles, Dictionary<string, byte[]> newFiles, HashSet<string> deletedFiles)
		{
			using (var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
			using (var bw = new BinaryWriter(outStream))
			{
				var fileEntries = new List<IpfFileEntry>();
				int currentOffset = 0;

				// Write all existing file data (except deleted ones)
				foreach (var ipfFile in this.Files)
				{
					// Skip deleted files
					if (deletedFiles != null && deletedFiles.Contains(ipfFile.FullPath))
						continue;

					byte[] data;
					uint uncompressedSize;
					uint crc32;

					if (updatedFiles != null && updatedFiles.TryGetValue(ipfFile.FullPath, out var newData))
					{
						// Use new data - compress and encrypt it
						uncompressedSize = (uint)newData.Length;
						var compressed = Compress(newData);
						data = Encrypt(compressed);
						crc32 = CalculateCrc32(data);
					}
					else
					{
						// Use existing data (already compressed and encrypted)
						data = ReadData(ipfFile.Offset, (int)ipfFile.SizeCompressed);
						uncompressedSize = ipfFile.SizeUncompressed;
						crc32 = ipfFile.Crc32;
					}

					var entry = new IpfFileEntry
					{
						Path = ipfFile.Path.Replace("/", "\\"),
						PackFileName = ipfFile.PackFileName,
						Offset = (uint)currentOffset,
						SizeCompressed = (uint)data.Length,
						SizeUncompressed = uncompressedSize,
						Crc32 = crc32
					};
					fileEntries.Add(entry);

					bw.Write(data);
					currentOffset += data.Length;
				}

				// Write new files (added/imported)
				if (newFiles != null)
				{
					foreach (var kvp in newFiles)
					{
						var fullPath = kvp.Key;
						var fileData = kvp.Value;

						// Parse path: "packName/path/to/file.ext"
						var separatorIndex = fullPath.IndexOf('/');
						string packFileName, filePath;
						if (separatorIndex > 0)
						{
							packFileName = fullPath.Substring(0, separatorIndex);
							filePath = fullPath.Substring(separatorIndex + 1);
						}
						else
						{
							packFileName = "";
							filePath = fullPath;
						}

						// Compress and encrypt
						var compressed = Compress(fileData);
						var encrypted = Encrypt(compressed);
						var crc32 = CalculateCrc32(encrypted);

						var entry = new IpfFileEntry
						{
							Path = filePath.Replace("/", "\\"),
							PackFileName = packFileName,
							Offset = (uint)currentOffset,
							SizeCompressed = (uint)encrypted.Length,
							SizeUncompressed = (uint)fileData.Length,
							Crc32 = crc32
						};
						fileEntries.Add(entry);

						bw.Write(encrypted);
						currentOffset += encrypted.Length;
					}
				}

				// Write file table
				var fileTableOffset = (uint)currentOffset;
				foreach (var entry in fileEntries)
				{
					var pathBytes = Encoding.UTF8.GetBytes(entry.Path);
					var archiveBytes = Encoding.UTF8.GetBytes(entry.PackFileName);

					bw.Write((ushort)pathBytes.Length);
					bw.Write(entry.Crc32);
					bw.Write(entry.SizeCompressed);
					bw.Write(entry.SizeUncompressed);
					bw.Write(entry.Offset);
					bw.Write((ushort)archiveBytes.Length);
					bw.Write(archiveBytes);
					bw.Write(pathBytes);
				}

				// Write footer
				var footerOffset = (uint)outStream.Position;
				bw.Write((ushort)fileEntries.Count);
				bw.Write(fileTableOffset);
				bw.Write((ushort)0);
				bw.Write(footerOffset);
				bw.Write(ZIP_MAGIC);
				bw.Write(this.Footer.PatchVersion);
				bw.Write(this.Footer.NewVersion);
			}
		}

		/// <summary>
		/// Compresses data using Deflate.
		/// </summary>
		private static byte[] Compress(byte[] data)
		{
			using (var outMs = new MemoryStream())
			{
				using (var deflate = new DeflateStream(outMs, CompressionLevel.Optimal, true))
				{
					deflate.Write(data, 0, data.Length);
				}
				return outMs.ToArray();
			}
		}

		/// <summary>
		/// Encrypts data using Pkware traditional encryption.
		/// </summary>
		private byte[] Encrypt(byte[] data)
		{
			if (this.Footer.NewVersion >= ENCODED_START_VERSION || this.Footer.NewVersion == 0)
			{
				var pkw = new PkwareTraditionalEncryptionData("ofO1a0ueXA? [\xFFs h %?");
				return pkw.Encrypt(data);
			}
			return data;
		}

		/// <summary>
		/// Calculates CRC32 for the data.
		/// </summary>
		private static uint CalculateCrc32(byte[] data)
		{
			uint crc = 0xFFFFFFFF;
			foreach (var b in data)
			{
				crc = (crc >> 8) ^ Crc32Table[(crc ^ b) & 0xFF];
			}
			return crc ^ 0xFFFFFFFF;
		}

		private static readonly uint[] Crc32Table = GenerateCrc32Table();

		private static uint[] GenerateCrc32Table()
		{
			var table = new uint[256];
			for (uint i = 0; i < 256; i++)
			{
				uint crc = i;
				for (int j = 0; j < 8; j++)
				{
					crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
				}
				table[i] = crc;
			}
			return table;
		}

		private class IpfFileEntry
		{
			public string Path { get; set; }
			public string PackFileName { get; set; }
			public uint Offset { get; set; }
			public uint SizeCompressed { get; set; }
			public uint SizeUncompressed { get; set; }
			public uint Crc32 { get; set; }
		}
	}

	public class IpfFile
	{
		private readonly string[] _noCompression = new[] { ".jpg", ".jpg", ".fsb", ".mp3" };

		public Ipf Ipf { get; set; }
		public string PackFileName { get; set; }
		public string Path { get; set; }
		public string FullPath { get; set; }
		public uint Offset { get; set; }
		public uint SizeCompressed { get; set; }
		public uint SizeUncompressed { get; set; }
		public uint Crc32 { get; set; }

		public IpfFile(Ipf ipf)
		{
			this.Ipf = ipf;
		}

		public byte[] GetData()
		{
			var ext = System.IO.Path.GetExtension(this.Path);
			var data = this.Ipf.ReadData(this.Offset, (int)this.SizeCompressed);

			if (_noCompression.Contains(ext.ToLowerInvariant()))
				return data;

			return this.Decompress(data);
		}

		private byte[] Decompress(byte[] data)
		{
			if (this.Ipf.Footer.NewVersion > 11000 || this.Ipf.Footer.NewVersion == 0)
			{
				var pkw = new PkwareTraditionalEncryptionData("ofO1a0ueXA? [\xFFs h %?");
				data = pkw.Decrypt(data, data.Length);
			}

			using (var msOut = new MemoryStream())
			using (var msIn = new MemoryStream(data))
			using (var deflate = new DeflateStream(msIn, CompressionMode.Decompress))
			{
				deflate.CopyTo(msOut);
				return msOut.ToArray();
			}
		}
	}

	public class IpfFooter
	{
		public ushort FileCount { get; set; }
		public uint NewVersion { get; set; }
		public uint PatchVersion { get; set; }
	}
}

