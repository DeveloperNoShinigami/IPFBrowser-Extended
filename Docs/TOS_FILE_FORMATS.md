# Tree of Savior File Formats Reference

This document catalogs all file formats discovered in the Tree of Savior game data archives (IPF files).

**Total IPF files scanned:** 54  
**Total files found:** 175,317  
**Unique extensions:** 64

---

## Table of Contents

1. [3D Models & Meshes](#3d-models--meshes)
2. [Animations](#animations)
3. [Textures & Images](#textures--images)
4. [Maps & World Data](#maps--world-data)
5. [Data Tables](#data-tables)
6. [Scripts & Code](#scripts--code)
7. [UI & Interface](#ui--interface)
8. [Effects & Particles](#effects--particles)
9. [Sprites & 2D Assets](#sprites--2d-assets)
10. [Audio](#audio)
11. [Navigation & Pathfinding](#navigation--pathfinding)
12. [Shaders](#shaders)
13. [Fonts](#fonts)
14. [Miscellaneous](#miscellaneous)
15. [IPF Archive Contents](#ipf-archive-contents)

---

## 3D Models & Meshes

### `.xac` - EMotion FX Actor/Character Model (40,142 files)
**Purpose:** Primary 3D model format for characters, monsters, items, and props. Contains mesh geometry, skeleton hierarchy, and bone weights.

**Found in:**
- `animation.ipf` - Monster/NPC/Item models with animations
- `char_hi.ipf` - High-quality character models
- `bg_hi.ipf`, `bg_hi2.ipf`, `bg_hi3.ipf` - Background/environment models
- `item_hi.ipf` - Item/equipment models
- `effect.ipf` - Effect-related models
- `templatepc.ipf` - Player character templates

**Examples:**
- `animation.ipf/monster/monster_corpseTower_set.xac`
- `char_hi.ipf/PC/common_PC/item/helmet/item_helmet_000.xac`

**Technical Notes:**
- EMotion FX format by Mystic Game Development
- Contains: vertices, normals, UVs, bone weights, materials
- Often paired with `.xsm` animation files

---

### `.xpm` - EMotion FX Morph/Pose Map (27,549 files)
**Purpose:** Morph target data for facial expressions, blend shapes, and character customization. Critical for face/head rendering.

**Found in:**
- `char_hi.ipf` - Character morphs and face data

**Examples:**
- `char_hi.ipf/monster/boss_zesty/boss_zesty_hair_back.xpm`
- `char_hi.ipf/PC/archer_f/face/archer_f_face_000.xpm`

**Technical Notes:**
- Contains vertex position deltas for morph targets
- Used for facial animations and expressions
- **Currently not supported in viewer - causes rendering issues for faces**

**XPM File Format Structure (Reverse Engineered):**
```
File Header (8 bytes):
  char[4]  magic           = "XPM " (0x58 0x50 0x4D 0x20)
  uint8    majorVersion    = 1
  uint8    minorVersion    = 0
  uint8    bigEndian       = 0 (little endian)
  uint8    multiplyOrder   = 1

Chunk Structure (same as XAC):
  Each chunk has a 12-byte header:
    int32  chunkType
    int32  chunkLength     (in bytes, excluding header)
    int32  chunkVersion

Known Chunk Types:
  101 (0x65) = XPM Metadata (version 1)
    - uint32  unknown1      (possibly flags/repositionMask)
    - uint32  unknown2      (possibly node reference)
    - string  sourceApp     (length-prefixed, e.g. "3D Studio Max 13")
    - string  originalFile  (length-prefixed, source .max file path)
    - string  exportDate    (length-prefixed, e.g. "May 25 2011")
    - string  actorName     (length-prefixed, can be empty)
    - uint32  numMorphTargets? (tentative)
    - uint32[3] unknown

  102 (0x66)? = MorphTarget Definitions (needs larger file to confirm)
    - Similar structure to XAC MORPH_TARGETS (type 12):
      - int32  numMorphTargets
      - int32  lodIndex
      - For each morph target:
        - float   rangeMin
        - float   rangeMax
        - int32   lodLevel
        - int32   numDeformations
        - int32   numTransformations
        - int32   phonemeSetBitmask
        - string  name
        - Deformation data (vertex deltas)

  Deformation Data (per node):
    - int32    nodeId
    - float    minValue        (for delta decompression)
    - float    maxValue        (for delta decompression)
    - int32    numVertices
    - For each vertex:
      - uint16[3] positionOffset (XYZ, compressed: real = min + (max-min) * val/65535)
      - uint8[3]  normalOffset   (XYZ, compressed: real = val/127.5 - 1.0)
      - uint8[3]  tangentOffset  (XYZ, compressed: real = val/127.5 - 1.0)
      - int32     vertexIndex    (index into mesh vertex array)
```

**Relationship to XAC:**
- XPM files are companion files to XAC models
- Same base filename pattern: `model.xac` + `model.xpm`
- XPM morph deltas are applied to XAC mesh vertices
- Uses same header format and chunk structure as XAC
- Chunk types use 100+ range (vs XAC's 1-13 range)

---

### `.colmesh` - Collision Mesh (56 files)
**Purpose:** Simplified mesh for physics and collision detection.

**Found in:**
- `bg.ipf` - Map collision data

**Examples:**
- `bg.ipf/barrack.colmesh`
- `bg.ipf/d_cmine_02.colmesh`

---

### `.bgcolmesh` - Background Collision Mesh (1 file)
**Purpose:** Large-scale background collision mesh.

**Found in:**
- `bg.ipf/f_siauliai_1.bgcolmesh`

---

## Animations

### `.xsm` - EMotion FX Skeletal Motion (25,092 files)
**Purpose:** Skeletal animation data for characters and objects.

**Found in:**
- `animation.ipf` - All character/monster animations
- `char_hi.ipf` - Character-specific animations
- `SumAni.ipf` - Summon animations
- `item_hi.ipf` - Item animations

**Examples:**
- `animation.ipf/item/equip/it_w_fan_teststd.xsm`
- `animation.ipf/pc/archer_f/archer_f_bow_atk1.xsm`

**Technical Notes:**
- Contains keyframe data for bones
- Paired with `.xac` model files

---

### `.xsmtime` - Animation Timing Data (19,278 files)
**Purpose:** Timing/event markers for animations (attack frames, sound triggers, etc.)

**Found in:**
- `animation.ipf` - Animation event timing
- `char_hi.ipf` - Character animation timing

**Examples:**
- `animation.ipf/monster/Abogust/Abogust_astd.xsmtime`

---

### `.sani` - Summary Animation (10 files)
**Purpose:** Compiled/summarized animation data.

**Found in:**
- `SumAni.ipf`

**Examples:**
- `SumAni.ipf/mandragora_fail.sani`

---

### `.sklm` - Skeleton Mapping (6 files)
**Purpose:** Skeleton bone mapping/retargeting data.

**Found in:**
- `animation.ipf` - Skeleton test files
- `xml_sklmove.ipf` - Skeleton movement data

---

## Textures & Images

### `.dds` - DirectDraw Surface (21,853 files)
**Purpose:** Primary texture format. GPU-ready compressed textures.

**Found in:**
- `bg_hi.ipf`, `bg_hi2.ipf`, `bg_hi3.ipf` - Environment textures
- `char_texture.ipf` - Character textures (high quality)
- `char_texture_low.ipf` - Character textures (low quality)
- `item_texture.ipf` - Item textures
- `bg_texture.ipf` - Background textures
- `effect.ipf` - Effect textures
- `ui.ipf` - UI textures

**Examples:**
- `bg_hi.ipf/barrack3/barrack4_ani01.dds`
- `char_texture.ipf/PC/archer_f/body/archer_f_body_000_a.dds`

**Technical Notes:**
- Supports DXT1, DXT3, DXT5, BC7 compression
- **Fully supported in IPFBrowser viewer**

---

### `.tga` - Targa Image (1,518 files)
**Purpose:** Uncompressed texture format with alpha support.

**Found in:**
- `bg_hi3.ipf` - High-quality environment textures
- `char_texture.ipf` - Character textures
- `item_texture.ipf` - Item textures
- `ui.ipf` - UI elements

**Examples:**
- `bg_hi3.ipf/field/f_castle_95/f_castle95_ground.tga`

**Technical Notes:**
- **Fully supported in IPFBrowser viewer**

---

### `.png` - Portable Network Graphics (5,055 files)
**Purpose:** Lossless images, typically for UI and effects.

**Found in:**
- `bg.ipf` - Normal maps
- `bg_hi.ipf` - Color grading LUTs
- `ui.ipf` - UI elements
- `sprite.ipf` - Sprite sheets

**Examples:**
- `bg.ipf/normal.png`
- `ui.ipf/icon/item/item_misc_old/icon_item_gem_low_011.png`

---

### `.jpg` / `.jpeg` - JPEG Image (434 files)
**Purpose:** Compressed images for backgrounds and lightmaps.

**Found in:**
- `bg.ipf` - Lightmaps and background images
- `ui.ipf` - UI backgrounds
- `etc.ipf` - Miscellaneous images

**Examples:**
- `bg.ipf/barrack_lightmap.jpg`

---

### `.bmp` - Bitmap Image (58 files)
**Purpose:** Cursor and simple UI graphics.

**Found in:**
- `ui.ipf` - Cursor graphics

**Examples:**
- `ui.ipf/cursor/arrow_diag_left.bmp`

---

## Maps & World Data

### `.3dworld` - 3D World Definition (333 files)
**Purpose:** Main map/level file containing world structure, terrain, and object placements.

**Found in:**
- `bg.ipf` - All game maps

**Examples:**
- `bg.ipf/d_cmine_02.3dworld`
- `bg.ipf/f_rokas_29.3dworld`

**Technical Notes:**
- **Priority for map viewer implementation**
- Contains references to props, terrain, and lighting

---

### `.3dprop` - 3D Props/Objects (327 files)
**Purpose:** Prop placement data for maps - decorative objects, trees, rocks, etc.

**Found in:**
- `bg.ipf`

**Examples:**
- `bg.ipf/d_prison_78.3dprop`

---

### `.3drender` - Render Settings (319 files)
**Purpose:** Map rendering configuration (lighting, fog, atmosphere settings).

**Found in:**
- `bg.ipf`

**Examples:**
- `bg.ipf/d_prison_78.3drender`

---

### `.3deffect` - Map Effects (242 files)
**Purpose:** Particle and visual effects placed in maps.

**Found in:**
- `bg.ipf`

**Examples:**
- `bg.ipf/d_prison_78.3deffect`

---

### `.3dzone` - Zone Definition (1 file)
**Purpose:** High-level zone/area definition.

**Found in:**
- `bg.ipf/hi_entity/barrack.3dzone`

---

### `.imctree` - IMC Tree Model (398 files)
**Purpose:** Tree/vegetation model format (IMC Games proprietary).

**Found in:**
- `bg_hi.ipf`, `bg_hi2.ipf`, `bg_hi3.ipf`

**Examples:**
- `bg_hi.ipf/field/f_siaulei/f_siaul_02tree_01.imctree`

---

### `.lightcell` - Lightmap Cell Data (159 files)
**Purpose:** Pre-baked lighting data for maps.

**Found in:**
- `bg_lightcell.ipf`

**Examples:**
- `bg_lightcell.ipf/barrack.lightcell`

---

## Data Tables

### `.ies` - IMC Engine Spreadsheet (989 files)
**Purpose:** Game data tables - items, skills, monsters, quests, etc.

**Found in:**
- `ies.ipf` - Main game data (189 files)
- `ies_ability.ipf` - Ability/skill data (56 files)
- `ies_client.ipf` - Client-side data (33 files)
- `ies_mongen.ipf` - Monster generation data (711 files)

**Examples:**
- `ies.ipf/account.ies`
- `ies.ipf/item.ies`
- `ies_ability.ipf/ability_archer.ies`

**Technical Notes:**
- **Fully supported in IPFBrowser with editing capability**
- Binary tabular format with string table

---

## Scripts & Code

### `.lua` - Lua Scripts (705 files)
**Purpose:** Game logic, AI, UI scripting.

**Found in:**
- `addon.ipf` - UI addon scripts
- `script_client.ipf` - Client scripts
- `shared.ipf` - Shared game logic
- Various addon IPFs

**Examples:**
- `addon.ipf/abilitylist/abilitylist.lua`
- `script_client.ipf/class/player.lua`

**Technical Notes:**
- **Viewable in IPFBrowser with syntax highlighting**

---

### `.export` - Script API Export (9 files)
**Purpose:** Exported function definitions for Lua scripts.

**Found in:**
- `scriptapi.ipf`

**Examples:**
- `scriptapi.ipf/AiScriptFunc.export`

---

## UI & Interface

### `.xml` - XML Configuration (15,408 files)
**Purpose:** UI layouts, configurations, and data definitions.

**Found in:**
- `addon.ipf` - Addon UI definitions
- `xml.ipf` - Main XML data (780 files)
- `xml_client.ipf` - Client XML
- `xml_minigame.ipf` - Minigame data (293 files)
- `effect.ipf` - Effect definitions

**Examples:**
- `addon.ipf/abilitylist/abilitylist.xml`
- `xml.ipf/monster/monster_etc.xml`

---

### `.skn` - UI Skin Definition (139 files)
**Purpose:** UI skinning and theming data.

**Found in:**
- `addon.ipf`
- `ui.ipf`

**Examples:**
- `addon.ipf/abilitylist/abilitylist.skn`

---

### `.effect` - UI Effect Definition (50 files)
**Purpose:** Visual effects for UI elements.

**Found in:**
- `addon.ipf`
- `ui.ipf`

**Examples:**
- `addon.ipf/abilitylist/abilitylist.effect`

---

## Effects & Particles

### `.psb` - Particle System Binary (2,283 files)
**Purpose:** Compiled particle effect data.

**Found in:**
- `effect.ipf`

**Examples:**
- `effect.ipf/forkparticle/psb/F_arcehr_Spoliation00.psb`

---

### `.fxdb` - Effect Database (8,211 files)
**Purpose:** Shader effect database/configuration.

**Found in:**
- `effect.ipf`
- `shader.ipf`

**Examples:**
- `effect.ipf/shader/effectshader.fxdb`
- `shader.ipf/imcMeshShader/common/DeferredMesh_Toon_AlphaBlend.fxdb`

---

### `.eft` - Effect Definition (33 files)
**Purpose:** Sprite-based effect definitions.

**Found in:**
- `sprite.ipf`

---

## Sprites & 2D Assets

### `.ibp` - IMC Binary Picture (1,606 files)
**Purpose:** Compiled sprite/image data.

**Found in:**
- `sprite.ipf`

**Examples:**
- `sprite.ipf/monster/anchor/deadparts/anchor_anchor01.ibp`

---

### `.sprbin` - Sprite Binary (1,102 files)
**Purpose:** Compiled sprite sheet data.

**Found in:**
- `sprite.ipf`

**Examples:**
- `sprite.ipf/monster/anchor/deadparts/anchor_dead01.sprbin`

---

### `.actbin` - Action Binary (277 files)
**Purpose:** Compiled sprite animation data.

**Found in:**
- `sprite.ipf`

**Examples:**
- `sprite.ipf/monster/anvil/body/body/anvil_ASTD_body.actbin`

---

### `.act` - Action Definition (2 files)
**Purpose:** Sprite action/animation source.

**Found in:**
- `sprite.ipf`

---

### `.spr` - Sprite Definition (9 files)
**Purpose:** Sprite source definition.

**Found in:**
- `sprite.ipf`

---

### `.colmap` - Color Map (22 files)
**Purpose:** Color mapping for sprites.

**Found in:**
- `sprite.ipf`

---

### `.dead` - Death Animation (10 files)
**Purpose:** Death/destruction slice animations.

**Found in:**
- `deadslice.ipf`

**Examples:**
- `deadslice.ipf/aa1.dead`

---

## Audio

### `.fsb` - FMOD Sound Bank (2 files)
**Purpose:** Compiled sound effects and voice data.

**Found in:**
- `sound.ipf`

**Examples:**
- `sound.ipf/SE.fsb`
- `sound.ipf/skilvoice_kor.fsb`

---

### `.fev` / `.fdp` / `.lst` / `.h` - FMOD Project Files
**Purpose:** FMOD audio project and event data.

**Found in:**
- `sound.ipf`

---

### `.mp3` - Audio File (1 file)
**Purpose:** Music/audio track.

**Found in:**
- `sound.ipf/track.mp3`

---

### `.snd` - Sound Reference (18 files)
**Purpose:** Sound effect references for sprites.

**Found in:**
- `sprite.ipf`

---

## Navigation & Pathfinding

### `.pathengine` - PathEngine Navigation Mesh (326 files)
**Purpose:** AI navigation mesh for pathfinding.

**Found in:**
- `bg.ipf`

**Examples:**
- `bg.ipf/barrack.pathengine`
- `bg.ipf/f_castle_95.pathengine`

---

### `.tok` - Token/Pathfinding Data (325 files)
**Purpose:** Pathfinding tokens and waypoints.

**Found in:**
- `bg.ipf`

**Examples:**
- `bg.ipf/barrack.tok`

---

### `.wmove` - World Movement Data (22 files)
**Purpose:** Movement/trigger zone definitions.

**Found in:**
- `binary.ipf`

**Examples:**
- `binary.ipf/aa.wmove`

---

## Shaders

### `.fx` - DirectX Effect/Shader (21 files)
**Purpose:** HLSL shader source code.

**Found in:**
- `decal.ipf`
- `effect.ipf`
- `shader.ipf`

**Examples:**
- `shader.ipf/imcMeshShader/common/Standard.fx`

---

### `.fxh` - Shader Header (3 files)
**Purpose:** Shader include files.

**Found in:**
- `shader.ipf`

---

### `.cam` - Camera Shader (1 file)
**Purpose:** Camera/post-processing effect.

**Found in:**
- `shader.ipf/3DTo2D.cam`

---

## Fonts

### `.ttf` - TrueType Font (2 files)
**Purpose:** Game fonts.

**Found in:**
- `font.ipf`

**Examples:**
- `font.ipf/imcm_book.ttf`
- `font.ipf/imcm_original.ttf`

---

## Miscellaneous

### `.lma` - Lightmap Archive (9 files)
**Purpose:** Compiled lightmap data.

**Found in:**
- `etc.ipf`

---

### `.xsd` - XML Schema (2 files)
**Purpose:** Schema definitions for XML validation.

**Found in:**
- `animation.ipf`
- `effect.ipf`

---

### `.bin` - Binary Data (1 file)
**Purpose:** Generic binary data.

**Found in:**
- `effect.ipf/effectlist.bin`

---

### `.db` - Database (5 files)
**Purpose:** Thumbnail/preview databases.

**Found in:**
- `bg_hi2.ipf`

---

### `.max` - 3ds Max Scene (3 files)
**Purpose:** Original 3ds Max source files (shouldn't be in release).

**Found in:**
- `bg_hi2.ipf`

---

### `.x` - DirectX Mesh (1 file)
**Purpose:** DirectX mesh format (legacy).

**Found in:**
- `effect.ipf/forkparticle/model/sphere0.x`

---

### Files Without Extension (870 files)
**Purpose:** Various data files without extensions.

**Found in:**
- `animation.ipf` - Bone/skeleton data

---

## IPF Archive Contents

### Core Game Data
| IPF File | Files | Description |
|----------|-------|-------------|
| `ies.ipf` | 189 | Main game data tables |
| `ies_ability.ipf` | 56 | Ability/skill definitions |
| `ies_client.ipf` | 33 | Client-specific data |
| `ies_mongen.ipf` | 711 | Monster spawn data |
| `xml.ipf` | 780 | XML configurations |
| `xml_client.ipf` | 9 | Client XML |
| `binary.ipf` | 22 | Binary data files |
| `global.ipf` | 3 | Global settings |

### 3D Assets
| IPF File | Files | Description |
|----------|-------|-------------|
| `animation.ipf` | 56,722 | Models and animations |
| `char_hi.ipf` | 56,783 | High-quality character models |
| `item_hi.ipf` | 644 | Item models |
| `templatepc.ipf` | 11 | PC templates |

### Textures
| IPF File | Files | Description |
|----------|-------|-------------|
| `char_texture.ipf` | 4,418 | Character textures (high) |
| `char_texture_low.ipf` | 2,861 | Character textures (low) |
| `item_texture.ipf` | 1,660 | Item textures |
| `item_texture_low.ipf` | 1,624 | Item textures (low) |
| `bg_texture.ipf` | 948 | Background textures |

### Maps & Environments
| IPF File | Files | Description |
|----------|-------|-------------|
| `bg.ipf` | 2,168 | Map data (world, props, collision) |
| `bg_hi.ipf` | 3,893 | Environment models |
| `bg_hi2.ipf` | 7,020 | Environment models (part 2) |
| `bg_hi3.ipf` | 8,705 | Environment models (part 3) |
| `bg_lightcell.ipf` | 159 | Lightmap data |

### Effects & Particles
| IPF File | Files | Description |
|----------|-------|-------------|
| `effect.ipf` | 6,305 | Visual effects |
| `shader.ipf` | 8,236 | Shaders and materials |
| `decal.ipf` | 16 | Decal effects |
| `deadslice.ipf` | 20 | Death animations |

### Sprites & UI
| IPF File | Files | Description |
|----------|-------|-------------|
| `sprite.ipf` | 3,917 | 2D sprites |
| `ui.ipf` | 5,867 | UI elements |
| `addon.ipf` | 838 | UI addons/scripts |

### Audio
| IPF File | Files | Description |
|----------|-------|-------------|
| `sound.ipf` | 10 | Sound banks and music |
| `font.ipf` | 2 | Game fonts |
| `language.ipf` | 2 | Language data |

---

## Implementation Priority for IPFBrowser

### Currently Supported ✅
**Full Featured:**
- `.ies` - Full viewing and editing
- `.dds` - Texture viewing (DXT1, DXT3, DXT5, BC7/DX10)
- `.tga` - Texture viewing
- `.png`, `.jpg`, `.jpeg`, `.bmp` - Image viewing
- `.xac` - 3D model viewing (basic)
- `.lua` - Syntax highlighted viewing
- `.xml`, `.effect`, `.skn`, `.xsd`, `.sani` - XML/text viewing with syntax highlighting

**Text/Code Viewing:**
- `.fx`, `.fxh` - HLSL shader code with syntax highlighting
- `.h` - C/C++ headers with syntax highlighting
- `.txt`, `.lst` - Plain text viewing
- `.export` - Script API exports

**Hex Preview (structure viewable, not decoded):**
- `.xsm` - Animation data (with parsed info display)
- `.xpm` - Morph/pose data
- `.xsmtime` - Animation timing
- `.sklm` - Skeleton mapping
- `.colmesh`, `.bgcolmesh` - Collision meshes
- `.3dworld`, `.3dprop`, `.3drender`, `.3deffect`, `.3dzone` - World/level data
- `.pathengine`, `.tok`, `.wmove` - Navigation/pathfinding
- `.lightcell`, `.lma` - Lighting data
- `.fxdb`, `.psb`, `.eft` - Effects
- `.ibp`, `.sprbin`, `.actbin`, `.act`, `.spr`, `.colmap`, `.dead` - Sprites
- `.fsb`, `.fev`, `.fdp`, `.mp3`, `.snd` - Audio
- `.imctree` - Vegetation models
- `.bin`, `.db` - Binary data
- `.x`, `.max` - Legacy 3D formats
- `.cam` - Camera effects
- `.ttf` - Font files

### High Priority 🔴
- `.xpm` - Morph data parsing (needed for proper face rendering)
- `.3dworld` - Map viewing
- `.3dprop` - Map prop placement
- `.xsm` - Animation playback on models

### Medium Priority 🟡
- `.pathengine` - Navigation mesh visualization
- `.psb` - Particle effects
- `.lightcell` - Lighting preview
- `.sprbin` / `.actbin` - Sprite viewing

### Low Priority 🟢
- `.fsb` - Audio playback
- `.fxdb` - Shader preview
- `.imctree` - Vegetation rendering

---

## Notes on Face/Head Rendering Issue

The current 3D viewer shows distorted faces because:

1. **`.xpm` files are not being loaded** - These contain morph target data essential for facial geometry
2. **Face models use morph targets** for expressions and customization
3. **Without morph data**, the base mesh shows incorrect vertex positions

### Solution Required:
1. Parse `.xpm` format (EMotion FX morph data)
2. Apply morph deltas to base mesh vertices
3. Support multiple morph targets for expressions

---

*Generated by IPFBrowser-Extended scan_ipf_formats.py*  
*Last updated: December 6, 2025*

