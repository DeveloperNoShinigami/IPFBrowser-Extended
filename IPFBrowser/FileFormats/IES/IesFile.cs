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

// IES parsing based on IesEdit by exectails (https://github.com/exectails/IesEdit)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace IPFBrowser.FileFormats.IES
{
	/// <summary>
	/// Represents an IES file - a binary format used by Tree of Savior for storing game data.
	/// </summary>
	public class IesFile : IDisposable
	{
		private const int HeaderNameLength = 0x40;
		private const int ColumnSize = 136;
		private const int SizesPos = (2 * HeaderNameLength + 2 * sizeof(short));
		private const int XorKey = 1;
		private const string DefaultString = "None";

		private Stream _stream;
		private BinaryReader _reader;

		public List<IesColumn> Columns { get; private set; }
		public IesHeader Header { get; private set; }
		public List<IesRow> Rows { get; private set; }

		/// <summary>
		/// Creates a new empty IES file instance.
		/// </summary>
		public IesFile()
		{
			this.Header = new IesHeader();
			this.Columns = new List<IesColumn>();
			this.Rows = new List<IesRow>();
		}

		public IesFile(Stream stream)
		{
			_stream = stream;
			_reader = new BinaryReader(_stream);

			this.ReadHeader();
			this.ReadColumns();
			this.ReadRows();
		}

		public IesFile(byte[] content)
			: this(new MemoryStream(content))
		{
		}

		public void Dispose()
		{
			if (_reader != null)
				_reader.Close();
		}

		/// <summary>
		/// Loads IES data from an XML string.
		/// </summary>
		public static IesFile LoadFromXmlString(string xml)
		{
			var result = new IesFile();
			var doc = XDocument.Parse(xml);
			result.LoadFromXmlDoc(doc);
			return result;
		}

		#region String Reading Helpers

		/// <summary>
		/// XORs buffer in place for decryption/encryption.
		/// </summary>
		private static void XorBuffer(byte[] buffer)
		{
			for (var i = 0; i < buffer.Length; ++i)
			{
				if (buffer[i] == 0)
					break;
				buffer[i] = (byte)(buffer[i] ^ XorKey);
			}
		}

		/// <summary>
		/// Reads a ushort-length-prefixed, XORed UTF8 string.
		/// </summary>
		private string ReadXoredLpString()
		{
			var length = _reader.ReadUInt16();
			return ReadXoredFixedString(length);
		}

		/// <summary>
		/// Reads an XORed UTF8 string with given length.
		/// </summary>
		private string ReadXoredFixedString(int length)
		{
			if (length <= 0)
				return "";

			var buffer = _reader.ReadBytes(length);
			XorBuffer(buffer);
			var index = Array.IndexOf(buffer, (byte)0);

			if (index != -1)
				length = index;

			return Encoding.UTF8.GetString(buffer, 0, length);
		}

		/// <summary>
		/// Reads a fixed-length UTF8 string (non-XORed).
		/// </summary>
		private string ReadFixedString(int length)
		{
			if (length <= 0)
				return "";

			var buffer = _reader.ReadBytes(length);
			var index = Array.IndexOf(buffer, (byte)0);

			if (index != -1)
				length = index;

			return Encoding.UTF8.GetString(buffer, 0, length);
		}

		#endregion

		/// <summary>
		/// Reads the IES file header.
		/// </summary>
		private void ReadHeader()
		{
			this.Header = new IesHeader();
			this.Header.IdSpace = ReadFixedString(HeaderNameLength);
			this.Header.KeySpace = ReadFixedString(HeaderNameLength);
			this.Header.Version = _reader.ReadUInt16();
			_reader.ReadUInt16(); // padding
			this.Header.InfoSize = _reader.ReadUInt32();
			this.Header.DataSize = _reader.ReadUInt32();
			this.Header.TotalSize = _reader.ReadUInt32();
			this.Header.UseClassId = (_reader.ReadByte() != 0);
			_reader.ReadByte(); // padding
			this.Header.RowCount = _reader.ReadUInt16();
			this.Header.ColumnCount = _reader.ReadUInt16();
			this.Header.NumberColumnCount = _reader.ReadUInt16();
			this.Header.StringColumnCount = _reader.ReadUInt16();
			_reader.ReadUInt16(); // padding
		}

		/// <summary>
		/// Reads the column definitions.
		/// </summary>
		private void ReadColumns()
		{
			this.Columns = new List<IesColumn>();
			for (int i = 0; i < this.Header.ColumnCount; i++)
			{
				var item = new IesColumn();
				item.Column = ReadXoredFixedString(HeaderNameLength);
				item.Name = ReadXoredFixedString(HeaderNameLength);
				item.Type = (ColumnType)_reader.ReadUInt16();
				item.Access = (PropertyAccess)_reader.ReadUInt16();
				item.Sync = _reader.ReadUInt16();
				item.DeclarationIndex = _reader.ReadUInt16();

				// Handle duplicate names
				var originalName = item.Name;
				for (int j = 1; this.Columns.Exists(a => a.Name == item.Name); ++j)
					item.Name = originalName + "_" + j;

				this.Columns.Add(item);
			}
		}

		/// <summary>
		/// Reads the row data.
		/// </summary>
		private void ReadRows()
		{
			this.Rows = new List<IesRow>();
			for (int i = 0; i < this.Header.RowCount; ++i)
			{
				var row = new IesRow();

				row.ClassId = _reader.ReadInt32();
				row.ClassName = ReadXoredLpString();

				// Read all number values first
				var numbers = new List<float>();
				for (var j = 0; j < this.Header.NumberColumnCount; ++j)
				{
					var value = _reader.ReadSingle();
					numbers.Add(value);
				}

				// Read all string values
				var strings = new List<string>();
				for (var j = 0; j < this.Header.StringColumnCount; ++j)
				{
					var value = ReadXoredLpString();
					strings.Add(value);
				}

				// Read UseScr bools for strings
				var bools = new List<bool>();
				for (var j = 0; j < this.Header.StringColumnCount; ++j)
				{
					var value = _reader.ReadByte() != 0;
					bools.Add(value);
				}

				// Map values to columns
				for (var j = 0; j < this.Header.ColumnCount; ++j)
				{
					var column = this.Columns[j];
					var key = column.Name;

					if (column.IsNumber)
					{
						row.Add(key, numbers[column.DeclarationIndex]);
					}
					else
					{
						row.Add(key, strings[column.DeclarationIndex]);
						row.UseScr.Add(key, bools[column.DeclarationIndex]);
					}
				}

				this.Rows.Add(row);
			}
		}

		#region XML Export (for editing support)

		/// <summary>
		/// Returns the IES data as XML string for viewing/editing.
		/// </summary>
		public string GetXml()
		{
			var sb = new StringBuilder();
			sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
			sb.AppendLine("<!-- Generated by IPFBrowser -->");
			sb.AppendLine($"<idspace id=\"{Header.IdSpace}\" keyid=\"{Header.KeySpace}\">");
			sb.AppendLine("  <Category>");

			foreach (var row in this.Rows)
			{
				sb.Append("    <Class");
				
				var fields = row.OrderBy(a => this.Columns.FirstOrDefault(b => b.Name == a.Key)?.DeclarationIndex ?? 10000)
								.ThenBy(a => this.Columns.FirstOrDefault(b => b.Name == a.Key)?.Type ?? (ColumnType)10000);

				foreach (var field in fields)
				{
					if (field.Value is float floatValue)
					{
						if (Math.Abs(floatValue) >= 0.01f)
						{
							string formattedValue = floatValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
							if (formattedValue.EndsWith(".00"))
								formattedValue = formattedValue.Substring(0, formattedValue.Length - 3);
							sb.Append($" {field.Key}=\"{formattedValue}\"");
						}
						else
						{
							sb.Append($" {field.Key}=\"0\"");
						}
					}
					else
					{
						var stringValue = field.Value?.ToString() ?? "";
						sb.Append($" {field.Key}=\"{System.Security.SecurityElement.Escape(stringValue.Length > 0 ? stringValue : "None")}\"");
					}
				}
				sb.AppendLine(" />");
			}

			sb.AppendLine("  </Category>");
			sb.AppendLine("</idspace>");

			return sb.ToString();
		}

		#endregion

		#region XML Import (for editing support)

		/// <summary>
		/// Loads IES data from an XML document.
		/// </summary>
		private void LoadFromXmlDoc(XDocument doc)
		{
			var root = doc.Root;
			if (root.Name != "idspace")
				throw new ArgumentException("Invalid IES XML: root element must be 'idspace'");

			// Load header info
			this.Header.IdSpace = root.Attribute("id")?.Value ?? "";
			this.Header.KeySpace = root.Attribute("keyid")?.Value ?? root.Attribute("keyspace")?.Value ?? "";

			// First pass: determine column types from all data
			var columnTypes = new Dictionary<string, ColumnType>();
			foreach (var element in root.Descendants("Class"))
			{
				foreach (var attr in element.Attributes())
				{
					var propertyName = attr.Name.LocalName;
					var type = ColumnType.String;

					if (propertyName.StartsWith("CP_"))
					{
						type = ColumnType.Calculated;
					}
					else if (IsValueNumeric(attr.Value))
					{
						type = ColumnType.Number;
					}

					if (columnTypes.TryGetValue(propertyName, out var existingType))
					{
						// Switch types that vary between rows to strings
						if (existingType != type && existingType == ColumnType.Number)
							columnTypes[propertyName] = ColumnType.String;
					}
					else
					{
						columnTypes[propertyName] = type;
					}
				}
			}

			// Build columns from first row attributes
			this.Columns.Clear();
			foreach (var element in root.Descendants("Class"))
			{
				foreach (var attr in element.Attributes())
				{
					var propertyName = attr.Name.LocalName;
					if (this.Columns.Any(a => a.Name == propertyName))
						continue;

					var simpleName = propertyName;
					var access = PropertyAccess.SP;
					var sync = false;

					if (simpleName.StartsWith("EP_"))
					{
						access = PropertyAccess.EP;
						simpleName = simpleName.Substring(3);
					}
					else if (simpleName.StartsWith("CP_"))
					{
						access = PropertyAccess.CP;
						simpleName = simpleName.Substring(3);
					}
					else if (simpleName.StartsWith("VP_"))
					{
						access = PropertyAccess.VP;
						simpleName = simpleName.Substring(3);
					}
					else if (simpleName.StartsWith("CT_"))
					{
						access = PropertyAccess.CT;
						simpleName = simpleName.Substring(3);
					}

					var ntIndex = simpleName.IndexOf("_NT");
					if (ntIndex != -1)
					{
						sync = true;
						simpleName = simpleName.Substring(0, ntIndex);
					}

					if (!columnTypes.TryGetValue(propertyName, out var type))
						type = ColumnType.String;

					var column = new IesColumn();
					column.Name = propertyName;
					column.Column = simpleName;
					column.Type = type;
					column.Access = access;
					column.Sync = sync ? 1 : 0;

					if (column.IsNumber)
						column.DeclarationIndex = this.Columns.Count(a => a.IsNumber);
					else
						column.DeclarationIndex = this.Columns.Count(a => !a.IsNumber);

					if (propertyName == "ClassID")
						this.Header.UseClassId = true;

					this.Columns.Add(column);
				}
			}

			// Load rows
			this.Rows.Clear();
			foreach (var element in root.Descendants("Class"))
			{
				var row = new IesRow();

				foreach (var column in this.Columns)
				{
					var key = column.Name;
					var attr = element.Attribute(key);

					if (attr == null)
					{
						if (column.IsNumber)
						{
							row.Add(key, 0f);
						}
						else
						{
							row.Add(key, "");
							row.UseScr.Add(key, false);
						}
					}
					else
					{
						if (column.IsNumber)
						{
							row.Add(key, float.Parse(attr.Value, CultureInfo.InvariantCulture));
						}
						else
						{
							var value = attr.Value != DefaultString ? attr.Value : "";
							row.Add(key, value);
							row.UseScr.Add(key, value.Contains("SCR_") || value.Contains("SCP"));
						}
					}

					if (key == "ClassID" && attr != null)
						row.ClassId = int.Parse(attr.Value);
					else if (key == "ClassName" && attr != null)
						row.ClassName = attr.Value;
				}

				this.Rows.Add(row);
			}

			// Update header counts
			this.Header.RowCount = this.Rows.Count;
			this.Header.ColumnCount = this.Columns.Count;
			this.Header.NumberColumnCount = this.Columns.Count(a => a.IsNumber);
			this.Header.StringColumnCount = this.Header.ColumnCount - this.Header.NumberColumnCount;
		}

		/// <summary>
		/// Returns true if the given string is a numeric value.
		/// </summary>
		private static bool IsValueNumeric(string value)
		{
			if (string.IsNullOrEmpty(value))
				return false;

			if (value.StartsWith("-"))
				return true;

			return value.All(a => a == ' ' || a == '.' || (a >= '0' && a <= '9'));
		}

		#endregion

		#region Binary IES Saving

		/// <summary>
		/// Saves the IES data to a binary IES format byte array.
		/// </summary>
		public byte[] SaveToBytes()
		{
			using (var ms = new MemoryStream())
			{
				SaveToStream(ms);
				return ms.ToArray();
			}
		}

		/// <summary>
		/// Saves the IES data to a stream in binary IES format.
		/// </summary>
		public void SaveToStream(Stream stream)
		{
			var columns = this.Columns.ToList();
			var sortedColumns = columns.OrderBy(a => a.IsNumber ? 0 : 1).ThenBy(a => a.DeclarationIndex);
			var rows = this.Rows;

			var rowCount = rows.Count;
			var colCount = columns.Count;
			var numberColCount = columns.Count(a => a.IsNumber);
			var stringColCount = colCount - numberColCount;

			using (var bw = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				// Write header
				WriteFixedString(bw, this.Header.IdSpace, HeaderNameLength);
				WriteFixedString(bw, this.Header.KeySpace ?? "", HeaderNameLength);
				bw.Write((ushort)this.Header.Version);
				bw.Write((ushort)0); // padding
				bw.Write((uint)this.Header.InfoSize);
				bw.Write((uint)this.Header.DataSize);
				bw.Write((uint)this.Header.TotalSize);
				bw.Write(this.Header.UseClassId ? (byte)1 : (byte)0);
				bw.Write((byte)0); // padding
				bw.Write((ushort)rowCount);
				bw.Write((ushort)colCount);
				bw.Write((ushort)numberColCount);
				bw.Write((ushort)stringColCount);
				bw.Write((ushort)0); // padding

				// Write columns
				foreach (var column in columns)
				{
					WriteXoredFixedString(bw, column.Column, HeaderNameLength);
					WriteXoredFixedString(bw, column.Name, HeaderNameLength);
					bw.Write((ushort)column.Type);
					bw.Write((ushort)column.Access);
					bw.Write((ushort)column.Sync);
					bw.Write((ushort)column.DeclarationIndex);
				}

				// Write rows
				var rowsStart = bw.BaseStream.Position;
				foreach (var row in rows)
				{
					bw.Write(row.ClassId);
					WriteXoredLpString(bw, row.ClassName ?? "");

					// Write values in sorted order (numbers first, then strings)
					foreach (var column in sortedColumns)
					{
						if (!row.TryGetValue(column.Name, out var value))
						{
							if (column.IsNumber)
								bw.Write(0f);
							else
								bw.Write((ushort)0);
						}
						else
						{
							if (column.IsNumber)
								bw.Write(Convert.ToSingle(value));
							else
								WriteXoredLpString(bw, (string)value);
						}
					}

					// Write UseScr bools for strings
					foreach (var column in sortedColumns.Where(a => !a.IsNumber))
					{
						if (row.UseScr.TryGetValue(column.Name, out var useScr))
							bw.Write(useScr ? (byte)1 : (byte)0);
						else
							bw.Write((byte)0);
					}
				}

				// Update header sizes
				this.Header.InfoSize = (uint)(columns.Count * ColumnSize);
				this.Header.DataSize = (uint)(bw.BaseStream.Position - rowsStart);
				this.Header.TotalSize = (uint)bw.BaseStream.Position;

				// Rewrite sizes in header
				bw.BaseStream.Seek(SizesPos, SeekOrigin.Begin);
				bw.Write((uint)this.Header.InfoSize);
				bw.Write((uint)this.Header.DataSize);
				bw.Write((uint)this.Header.TotalSize);
				bw.BaseStream.Seek(0, SeekOrigin.End);
			}
		}

		#endregion

		#region String Writing Helpers

		/// <summary>
		/// Writes a fixed-length string (non-XORed).
		/// </summary>
		private static void WriteFixedString(BinaryWriter writer, string value, int length)
		{
			var buffer = Encoding.UTF8.GetBytes(value ?? "");
			var writeLength = Math.Min(buffer.Length, length);

			writer.Write(buffer, 0, writeLength);

			for (var i = writeLength; i < length; ++i)
				writer.Write((byte)0);
		}

		/// <summary>
		/// Writes an XORed fixed-length string.
		/// </summary>
		private static void WriteXoredFixedString(BinaryWriter writer, string value, int length)
		{
			var buffer = Encoding.UTF8.GetBytes(value ?? "");
			var writeLength = Math.Min(buffer.Length, length);

			// XOR the buffer
			for (var i = 0; i < writeLength; ++i)
				buffer[i] = (byte)(buffer[i] ^ XorKey);

			writer.Write(buffer, 0, writeLength);

			for (var i = writeLength; i < length; ++i)
				writer.Write((byte)0);
		}

		/// <summary>
		/// Writes a length-prefixed XORed string.
		/// </summary>
		private static void WriteXoredLpString(BinaryWriter writer, string value)
		{
			var buffer = Encoding.UTF8.GetBytes(value ?? "");
			writer.Write((ushort)buffer.Length);

			// XOR the buffer
			for (var i = 0; i < buffer.Length; ++i)
				buffer[i] = (byte)(buffer[i] ^ XorKey);

			writer.Write(buffer);
		}

		#endregion
	}

	/// <summary>
	/// Represents an IES file's header.
	/// </summary>
	public class IesHeader
	{
		public string IdSpace { get; set; }
		public string KeySpace { get; set; } = "";
		public int Version { get; set; } = 1;
		public uint InfoSize { get; set; }
		public uint DataSize { get; set; }
		public uint TotalSize { get; set; }
		public bool UseClassId { get; set; }
		public int ColumnCount { get; set; }
		public int RowCount { get; set; }
		public int NumberColumnCount { get; set; }
		public int StringColumnCount { get; set; }
	}

	/// <summary>
	/// Represents a column in an IES file.
	/// </summary>
	public class IesColumn : IComparable<IesColumn>
	{
		public string Column { get; set; }
		public string Name { get; set; }
		public ColumnType Type { get; set; }
		public PropertyAccess Access { get; set; } = PropertyAccess.SP;
		public int Sync { get; set; }
		public int DeclarationIndex { get; set; }

		public bool IsNumber { get { return (this.Type == ColumnType.Number); } }

		public int CompareTo(IesColumn other)
		{
			// Compare declaration if the two columns are the same type
			if (this.Type == other.Type || (!this.IsNumber && !other.IsNumber))
				return this.DeclarationIndex.CompareTo(other.DeclarationIndex);

			// Otherwise compare the type (numbers first)
			if (this.Type < other.Type)
				return -1;

			return 1;
		}

		public override string ToString()
		{
			return this.Column + "/" + this.Name + " : " + this.Type;
		}
	}

	/// <summary>
	/// Describes a column's data type.
	/// </summary>
	public enum ColumnType : ushort
	{
		Number = 0,
		String = 1,
		Calculated = 2
	}

	/// <summary>
	/// Property access type.
	/// </summary>
	public enum PropertyAccess : byte
	{
		EP,
		CP,
		VP,
		SP,
		CT,
	}

	/// <summary>
	/// Represents a row of data in an IES file.
	/// </summary>
	public class IesRow : Dictionary<string, object>
	{
		public int ClassId { get; set; }
		public string ClassName { get; set; }
		public Dictionary<string, bool> UseScr { get; } = new Dictionary<string, bool>();

		public float GetFloat(string name)
		{
			if (!ContainsKey(name))
				throw new ArgumentException("Unknown field: " + name);

			if (this[name] is float floatValue) return floatValue;
			if (this[name] is uint uintValue) return (float)uintValue;

			throw new ArgumentException(name + " is not numeric");
		}

		public uint GetUInt(string name)
		{
			return (uint)GetInt(name);
		}

		public int GetInt(string name)
		{
			if (!ContainsKey(name))
				throw new ArgumentException("Unknown field: " + name);

			if (this[name] is float floatValue) return (int)floatValue;
			if (this[name] is uint uintValue) return (int)uintValue;

			throw new ArgumentException(name + " is not numeric");
		}

		public string GetString(string name)
		{
			if (!ContainsKey(name))
				throw new ArgumentException("Unknown field: " + name);

			if (this[name] is string stringValue) return stringValue;

			throw new ArgumentException(name + " is not a string");
		}
	}
}
