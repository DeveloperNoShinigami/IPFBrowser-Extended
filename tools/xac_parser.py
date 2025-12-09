#!/usr/bin/env python3
"""
XAC File Parser - Dumps XAC model data for debugging
Usage: python xac_parser.py <path_to_xac_file>
Output: Creates a .txt file next to the XAC file with parsed data
"""

import struct
import sys
import os

# Chunk types (from XacFile.cs)
CHUNK_MESH = 1
CHUNK_BONE_INFLUENCES = 2
CHUNK_MATERIAL_DEFINITION = 3
CHUNK_PROPERTIES = 5
CHUNK_METADATA = 7
CHUNK_NODE_HIERARCHY = 11
CHUNK_MORPH_TARGETS = 12
CHUNK_MATERIAL_TOTALS = 13

# Global output list
output_lines = []

def log(msg=""):
    """Print and store output"""
    print(msg)
    output_lines.append(msg)

def read_string(f):
    """Read a length-prefixed string"""
    length = struct.unpack('<I', f.read(4))[0]
    if length > 0:
        return f.read(length).decode('utf-8', errors='replace')
    return ""

def read_vec3(f):
    return struct.unpack('<3f', f.read(12))

def read_vec4(f):
    return struct.unpack('<4f', f.read(16))

def read_quat(f):
    return struct.unpack('<4f', f.read(16))

def read_color(f):
    return struct.unpack('<4f', f.read(16))

def parse_xac(filepath):
    log(f"{'='*60}")
    log(f"XAC Parser Output")
    log(f"File: {filepath}")
    log(f"{'='*60}")
    log()
    
    with open(filepath, 'rb') as f:
        file_size = os.path.getsize(filepath)
        
        # Header
        magic = f.read(4)
        log(f"Magic: {magic}")
        
        if magic != b'XAC ':
            log("ERROR: Not a valid XAC file!")
            return
        
        major_ver, minor_ver = struct.unpack('<BB', f.read(2))
        big_endian = struct.unpack('<B', f.read(1))[0]
        multiply_order = struct.unpack('<B', f.read(1))[0]
        
        log(f"Version: {major_ver}.{minor_ver}")
        log(f"Big Endian: {big_endian}")
        log(f"Multiply Order: {multiply_order}")
        log(f"File Size: {file_size} bytes")
        
        # Parse chunks
        chunk_count = 0
        while f.tell() < file_size:
            chunk_start = f.tell()
            
            if f.tell() + 12 > file_size:
                break
                
            chunk_type = struct.unpack('<I', f.read(4))[0]
            chunk_length = struct.unpack('<I', f.read(4))[0]
            chunk_version = struct.unpack('<I', f.read(4))[0]
            
            chunk_data_start = f.tell()
            chunk_end = chunk_data_start + chunk_length
            
            chunk_names = {
                CHUNK_MESH: "MESH",
                CHUNK_BONE_INFLUENCES: "BONE_INFLUENCES",
                CHUNK_MATERIAL_DEFINITION: "MATERIAL_DEFINITION",
                CHUNK_PROPERTIES: "PROPERTIES",
                CHUNK_METADATA: "METADATA",
                CHUNK_NODE_HIERARCHY: "NODE_HIERARCHY",
                CHUNK_MORPH_TARGETS: "MORPH_TARGETS",
                CHUNK_MATERIAL_TOTALS: "MATERIAL_TOTALS"
            }
            chunk_name = chunk_names.get(chunk_type, f"UNKNOWN({chunk_type})")
            
            log()
            log(f"--- Chunk #{chunk_count}: {chunk_name} at offset {chunk_start} ---")
            log(f"    Type={chunk_type}, Length={chunk_length}, Version={chunk_version}")
            
            try:
                if chunk_type == CHUNK_MESH:
                    parse_mesh_chunk(f, chunk_length)
                elif chunk_type == CHUNK_NODE_HIERARCHY:
                    parse_node_chunk(f, chunk_length, chunk_version)
                elif chunk_type == CHUNK_MATERIAL_TOTALS:
                    parse_material_totals(f)
                elif chunk_type == CHUNK_MATERIAL_DEFINITION:
                    parse_material_def(f)
                elif chunk_type == CHUNK_METADATA:
                    parse_metadata(f)
                elif chunk_type == CHUNK_PROPERTIES:
                    parse_properties(f)
                else:
                    log(f"    (Skipping chunk)")
            except Exception as ex:
                log(f"    ERROR parsing chunk: {ex}")
            
            # Seek to end of chunk
            f.seek(chunk_end)
            chunk_count += 1

def parse_mesh_chunk(f, chunk_length):
    log("  [MESH DATA]")
    
    node_index = struct.unpack('<i', f.read(4))[0]
    num_influence_ranges = struct.unpack('<i', f.read(4))[0]
    num_vertices = struct.unpack('<i', f.read(4))[0]
    num_indices = struct.unpack('<i', f.read(4))[0]
    num_submeshes = struct.unpack('<i', f.read(4))[0]
    num_attrib_layers = struct.unpack('<i', f.read(4))[0]
    is_collision_mesh = struct.unpack('<B', f.read(1))[0]
    f.read(3)  # padding
    
    log(f"    Node Index: {node_index}")
    log(f"    Num Vertices: {num_vertices}")
    log(f"    Num Indices: {num_indices}")
    log(f"    Num SubMeshes: {num_submeshes}")
    log(f"    Num Attrib Layers: {num_attrib_layers}")
    log(f"    Is Collision Mesh: {is_collision_mesh}")
    
    # Parse vertex attribute layers
    log(f"    Vertex Attributes:")
    for i in range(num_attrib_layers):
        attrib_type = struct.unpack('<i', f.read(4))[0]
        attrib_size = struct.unpack('<i', f.read(4))[0]
        keep_originals = struct.unpack('<B', f.read(1))[0]
        is_scale_factor = struct.unpack('<B', f.read(1))[0]
        f.read(2)  # padding
        
        type_names = {0: 'Position', 1: 'Normal', 2: 'Tangent', 3: 'UV', 4: 'Color32', 5: 'InfluenceIdx', 6: 'Color128'}
        type_name = type_names.get(attrib_type, f'Unknown({attrib_type})')
        
        data_size = attrib_size * num_vertices
        log(f"      [{i}] {type_name}: {attrib_size} bytes/vertex, total {data_size} bytes")
        
        # Sample data for position and UV
        if attrib_type == 0:  # Position
            samples = []
            for v in range(min(3, num_vertices)):
                pos = struct.unpack('<3f', f.read(12))
                samples.append(f"({pos[0]:.2f},{pos[1]:.2f},{pos[2]:.2f})")
                if attrib_size > 12:
                    f.read(attrib_size - 12)
            log(f"          First 3: {', '.join(samples)}")
            remaining = num_vertices - min(3, num_vertices)
            f.read(remaining * attrib_size)
            
        elif attrib_type == 3:  # UV
            # Read all UVs to get bounds
            all_uvs = []
            for v in range(num_vertices):
                uv = struct.unpack('<2f', f.read(8))
                all_uvs.append(uv)
                if attrib_size > 8:
                    f.read(attrib_size - 8)
            
            min_u = min(uv[0] for uv in all_uvs)
            max_u = max(uv[0] for uv in all_uvs)
            min_v = min(uv[1] for uv in all_uvs)
            max_v = max(uv[1] for uv in all_uvs)
            log(f"          UV Bounds: U=[{min_u:.4f}, {max_u:.4f}], V=[{min_v:.4f}, {max_v:.4f}]")
            log(f"          First 5 UVs: {all_uvs[:5]}")
        else:
            # Skip attribute data
            f.read(data_size)
    
    # Parse submeshes
    log(f"    SubMeshes:")
    for i in range(num_submeshes):
        sub_num_indices = struct.unpack('<i', f.read(4))[0]
        sub_num_vertices = struct.unpack('<i', f.read(4))[0]
        sub_material_id = struct.unpack('<i', f.read(4))[0]
        sub_num_bones = struct.unpack('<i', f.read(4))[0]
        
        log(f"      [{i}] Indices={sub_num_indices}, Vertices={sub_num_vertices}, MaterialId={sub_material_id}, Bones={sub_num_bones}")
        
        # Read first few indices
        indices = []
        for j in range(min(9, sub_num_indices)):
            idx = struct.unpack('<i', f.read(4))[0]
            indices.append(idx)
        log(f"          First indices: {indices}")
        
        # Calculate index bounds
        remaining_indices = sub_num_indices - min(9, sub_num_indices)
        all_indices = indices.copy()
        for j in range(remaining_indices):
            idx = struct.unpack('<i', f.read(4))[0]
            all_indices.append(idx)
        
        log(f"          Index range: [{min(all_indices)}, {max(all_indices)}]")
        
        # Skip bone indices
        f.read(sub_num_bones * 4)

def parse_node_chunk(f, chunk_length, version):
    log("  [NODE HIERARCHY]")
    
    num_nodes = struct.unpack('<i', f.read(4))[0]
    num_root_nodes = struct.unpack('<i', f.read(4))[0]
    
    log(f"    Num Nodes: {num_nodes}")
    log(f"    Num Root Nodes: {num_root_nodes}")
    
    for i in range(num_nodes):
        rotation = read_quat(f)  # 16 bytes
        scale_rotation = read_quat(f)  # 16 bytes
        position = read_vec3(f)  # 12 bytes
        scale = read_vec3(f)  # 12 bytes
        
        # Skip padding and other fields
        f.read(12)  # 3 floats padding
        unknown1 = struct.unpack('<i', f.read(4))[0]
        unknown2 = struct.unpack('<i', f.read(4))[0]
        parent_node_id = struct.unpack('<i', f.read(4))[0]
        num_children = struct.unpack('<i', f.read(4))[0]
        include_bounds = struct.unpack('<i', f.read(4))[0]
        
        # Skip transform matrix (64 bytes) and importance factor (4 bytes)
        f.read(64 + 4)
        
        name = read_string(f)
        
        if i < 5 or parent_node_id == -1:
            log(f"    [{i}] '{name}' Parent={parent_node_id} Pos=({position[0]:.2f},{position[1]:.2f},{position[2]:.2f})")

def parse_material_totals(f):
    log("  [MATERIAL TOTALS]")
    
    num_total = struct.unpack('<i', f.read(4))[0]
    num_standard = struct.unpack('<i', f.read(4))[0]
    num_fx = struct.unpack('<i', f.read(4))[0]
    
    log(f"    Total Materials: {num_total}")
    log(f"    Standard Materials: {num_standard}")
    log(f"    FX Materials: {num_fx}")

def parse_material_def(f):
    log("  [MATERIAL DEFINITION]")
    
    ambient = read_color(f)
    diffuse = read_color(f)
    specular = read_color(f)
    emissive = read_color(f)
    shine = struct.unpack('<f', f.read(4))[0]
    shine_strength = struct.unpack('<f', f.read(4))[0]
    opacity = struct.unpack('<f', f.read(4))[0]
    ior = struct.unpack('<f', f.read(4))[0]
    is_double_sided = struct.unpack('<B', f.read(1))[0]
    is_wireframe = struct.unpack('<B', f.read(1))[0]
    f.read(1)  # padding
    num_layers = struct.unpack('<B', f.read(1))[0]
    
    name = read_string(f)
    
    log(f"    Name: '{name}'")
    log(f"    Diffuse: RGBA({diffuse[0]:.2f},{diffuse[1]:.2f},{diffuse[2]:.2f},{diffuse[3]:.2f})")
    log(f"    Opacity: {opacity}")
    log(f"    Num Layers: {num_layers}")
    
    for i in range(num_layers):
        amount = struct.unpack('<f', f.read(4))[0]
        offset_u = struct.unpack('<f', f.read(4))[0]
        offset_v = struct.unpack('<f', f.read(4))[0]
        tiling_u = struct.unpack('<f', f.read(4))[0]
        tiling_v = struct.unpack('<f', f.read(4))[0]
        rotation = struct.unpack('<f', f.read(4))[0]
        material_id = struct.unpack('<h', f.read(2))[0]
        map_type = struct.unpack('<B', f.read(1))[0]
        f.read(1)  # padding
        texture = read_string(f)
        
        map_types = {2: 'Diffuse', 3: 'Specular', 4: 'Opacity', 5: 'Normal', 6: 'Bump'}
        map_type_name = map_types.get(map_type, f'Type{map_type}')
        
        log(f"      Layer[{i}]: '{texture}'")
        log(f"                MapType={map_type_name}, Tiling=({tiling_u},{tiling_v}), Offset=({offset_u},{offset_v})")

def parse_metadata(f):
    log("  [METADATA]")
    
    reposition_mask = struct.unpack('<I', f.read(4))[0]
    repositioning_node = struct.unpack('<i', f.read(4))[0]
    exporter_major = struct.unpack('<B', f.read(1))[0]
    exporter_minor = struct.unpack('<B', f.read(1))[0]
    f.read(2)  # padding
    retarget_root_offset = struct.unpack('<f', f.read(4))[0]
    
    source_app = read_string(f)
    original_filename = read_string(f)
    export_date = read_string(f)
    actor_name = read_string(f)
    
    log(f"    Source App: '{source_app}'")
    log(f"    Original Filename: '{original_filename}'")
    log(f"    Export Date: '{export_date}'")
    log(f"    Actor Name: '{actor_name}'")

def parse_properties(f):
    log("  [PROPERTIES]")
    
    int_count = struct.unpack('<i', f.read(4))[0]
    float_count = struct.unpack('<i', f.read(4))[0]
    f.read(4)  # padding
    bool_count = struct.unpack('<i', f.read(4))[0]
    f.read(4)  # padding
    string_count = struct.unpack('<i', f.read(4))[0]
    
    name = read_string(f)
    fx_filename = read_string(f)
    shader_name = read_string(f)
    
    log(f"    Name: '{name}'")
    log(f"    FX Filename: '{fx_filename}'")
    log(f"    Shader Name: '{shader_name}'")
    log(f"    Counts: Int={int_count}, Float={float_count}, Bool={bool_count}, String={string_count}")
    
    for i in range(int_count):
        prop_name = read_string(f)
        prop_value = struct.unpack('<i', f.read(4))[0]
        log(f"      Int: '{prop_name}' = {prop_value}")
    
    for i in range(float_count):
        prop_name = read_string(f)
        prop_value = struct.unpack('<f', f.read(4))[0]
        log(f"      Float: '{prop_name}' = {prop_value}")
    
    for i in range(bool_count):
        prop_name = read_string(f)
        prop_value = struct.unpack('<B', f.read(1))[0]
        log(f"      Bool: '{prop_name}' = {prop_value}")
    
    for i in range(string_count):
        prop_name = read_string(f)
        prop_value = read_string(f)
        log(f"      String: '{prop_name}' = '{prop_value}'")

def save_output(filepath):
    """Save output to a text file next to the input file"""
    output_path = os.path.splitext(filepath)[0] + "_parsed.txt"
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write('\n'.join(output_lines))
    print(f"\n\nOutput saved to: {output_path}")
    return output_path

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python xac_parser.py <path_to_xac_file>")
        print("\nYou can also drag and drop an XAC file onto this script.")
        print("Output will be saved to <filename>_parsed.txt")
        input("Press Enter to exit...")
        sys.exit(1)
    
    filepath = sys.argv[1]
    if not os.path.exists(filepath):
        print(f"Error: File not found: {filepath}")
        input("Press Enter to exit...")
        sys.exit(1)
    
    try:
        parse_xac(filepath)
        output_file = save_output(filepath)
        log()
        log("="*60)
        log("PARSING COMPLETE")
        log("="*60)
    except Exception as ex:
        log(f"\nERROR: {ex}")
        import traceback
        log(traceback.format_exc())
        save_output(filepath)
    
    input("\nPress Enter to exit...")
