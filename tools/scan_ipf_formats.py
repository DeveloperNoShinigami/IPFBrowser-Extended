#!/usr/bin/env python3
"""
Scan IPF files to discover all file formats/extensions used in TOS game data.
"""

import os
import struct
import zlib
from collections import defaultdict
from pathlib import Path

def read_ipf_file_list(ipf_path):
    """Read file entries from an IPF archive and return list of filenames."""
    files = []
    
    try:
        with open(ipf_path, 'rb') as f:
            # Read footer (located at end of file, 24 bytes = 0x18)
            # Footer format from C# code:
            # ushort FileCount
            # uint fileTableOffset
            # ushort (unknown)
            # uint (unknown) 
            # uint compression (ZIP_MAGIC = PK\x05\x06)
            # uint PatchVersion
            # uint NewVersion
            f.seek(-0x18, 2)  # 24 bytes from end
            footer = f.read(24)
            
            file_count, file_table_offset, _, _, magic, patch_version, new_version = struct.unpack('<HIHI4sII', footer)
            
            # ZIP magic is PK\x05\x06
            if magic != b'PK\x05\x06':
                print(f"  Invalid magic: {magic}")
                return files
            
            # Seek to file table
            f.seek(file_table_offset)
            
            # Read each file entry
            # Format from C# Load():
            # ushort pathLength
            # uint Crc32
            # uint SizeCompressed
            # uint SizeUncompressed
            # uint Offset
            # ushort archiveNameLength
            # char[] archiveName
            # char[] path
            for _ in range(file_count):
                path_length = struct.unpack('<H', f.read(2))[0]
                crc32 = struct.unpack('<I', f.read(4))[0]
                size_comp = struct.unpack('<I', f.read(4))[0]
                size_uncomp = struct.unpack('<I', f.read(4))[0]
                offset = struct.unpack('<I', f.read(4))[0]
                archive_name_length = struct.unpack('<H', f.read(2))[0]
                
                archive_name = f.read(archive_name_length).decode('utf-8', errors='ignore')
                path = f.read(path_length).decode('utf-8', errors='ignore')
                
                full_path = f"{archive_name}/{path}" if archive_name else path
                files.append(full_path)
                
    except Exception as e:
        print(f"  Error reading {ipf_path}: {e}")
        import traceback
        traceback.print_exc()
    
    return files

def scan_directory(data_dir):
    """Scan all IPF files in directory and catalog file extensions."""
    
    extensions = defaultdict(lambda: {"count": 0, "examples": [], "ipfs": set()})
    total_files = 0
    ipf_count = 0
    
    data_path = Path(data_dir)
    ipf_files = list(data_path.glob("*.ipf"))
    
    print(f"Found {len(ipf_files)} IPF files to scan...\n")
    
    for ipf_path in sorted(ipf_files):
        print(f"Scanning: {ipf_path.name}")
        ipf_count += 1
        
        files = read_ipf_file_list(str(ipf_path))
        print(f"  Found {len(files)} files")
        
        for filepath in files:
            total_files += 1
            ext = Path(filepath).suffix.lower()
            if not ext:
                ext = "(no extension)"
            
            extensions[ext]["count"] += 1
            extensions[ext]["ipfs"].add(ipf_path.name)
            
            # Keep up to 3 examples per extension
            if len(extensions[ext]["examples"]) < 3:
                extensions[ext]["examples"].append(filepath)
    
    return extensions, total_files, ipf_count

def main():
    data_dir = r"C:\Users\bluel\Documents\Downloads\TOS_WORKSPACE\TOFCN-Client\TreeOfSaviorCN\data"
    
    print("=" * 80)
    print("TOS IPF File Format Scanner")
    print("=" * 80)
    print(f"Scanning: {data_dir}\n")
    
    extensions, total_files, ipf_count = scan_directory(data_dir)
    
    print("\n" + "=" * 80)
    print("RESULTS")
    print("=" * 80)
    print(f"\nTotal IPF files scanned: {ipf_count}")
    print(f"Total files found: {total_files}")
    print(f"Unique extensions: {len(extensions)}\n")
    
    # Sort by count descending
    sorted_exts = sorted(extensions.items(), key=lambda x: x[1]["count"], reverse=True)
    
    print("-" * 80)
    print(f"{'Extension':<15} {'Count':>10}  {'IPFs':>5}  Examples")
    print("-" * 80)
    
    for ext, data in sorted_exts:
        examples = ", ".join(data["examples"][:2])
        if len(examples) > 50:
            examples = examples[:47] + "..."
        print(f"{ext:<15} {data['count']:>10}  {len(data['ipfs']):>5}  {examples}")
    
    # Group by category
    print("\n" + "=" * 80)
    print("GROUPED BY CATEGORY")
    print("=" * 80)
    
    categories = {
        "3D Models": [".xac", ".xsm", ".gr2", ".fbx", ".obj", ".mesh", ".colmesh"],
        "Animations": [".xsm", ".sani", ".ani", ".anim"],
        "Textures": [".dds", ".tga", ".png", ".jpg", ".bmp", ".tif"],
        "Data Tables": [".ies"],
        "Scripts": [".lua"],
        "XML/Config": [".xml", ".effect", ".skn", ".xsd"],
        "Audio": [".fsb", ".wav", ".mp3", ".ogg"],
        "Navigation": [".pathengine", ".tok", ".navmesh"],
        "Other Binary": [".bin", ".dat"],
    }
    
    for cat_name, cat_exts in categories.items():
        found = [(ext, extensions.get(ext, {"count": 0})["count"]) for ext in cat_exts if ext in extensions]
        if found:
            total = sum(c for _, c in found)
            print(f"\n{cat_name}: {total} files")
            for ext, count in found:
                print(f"  {ext}: {count}")

    # Print all extensions not yet categorized
    all_categorized = set()
    for exts in categories.values():
        all_categorized.update(exts)
    
    uncategorized = [ext for ext in extensions.keys() if ext not in all_categorized]
    if uncategorized:
        print(f"\nUncategorized extensions:")
        for ext in sorted(uncategorized):
            print(f"  {ext}: {extensions[ext]['count']}")

if __name__ == "__main__":
    main()
