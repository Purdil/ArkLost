KamenMap Unity import folder

Drag this KamenMap folder into a Unity project's Assets folder.
Unity will import the OBJ models and PNG textures, then the Editor script in Editor/
will create Materials, Prefabs/KamenMap.prefab, Prefabs/KamenMap_AllExtractedMeshes.prefab,
and Scenes/KamenMap.unity.

Prefab notes:
- Prefabs/KamenMap.prefab includes every extracted placeable static mesh at its source pivot.
- Prefabs/KamenMap_ArenaLikelyMeshes.prefab is a smaller filtered set of Kamen/SHA floor,
  bridge, pillar, rock, stone, and tile meshes for quick inspection.

Use Tools > Lost Ark > Rebuild Kamen Map Assets if Unity imported scripts before
the model assets finished importing.

Notes:
- This is a static reconstruction from local Lost Ark UPK packages via UModel.
- LV_RAD_SHADS* level packages are audited and included, but UModel does not export UE3
  Level actor transforms; exact in-game placement requires a separate Level parser.
- Materials are rebuilt from exported UModel texture metadata where available.
- Dynamic effects, level scripting, collisions, lighting probes, and gameplay logic are not recreated.
- Keep the extracted files for personal/local analysis unless you have rights for other use.