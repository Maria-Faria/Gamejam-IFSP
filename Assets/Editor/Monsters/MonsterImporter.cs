using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Import rules for the monsters (Assets/Monsters/<Folder>/<name>.fbx, made by Tools/make_monsters.py):
// Generic rig with its own avatar, all actions as clips named after the Blender action, looping and
// animation events (MonsterEvent) taken from the <name>.clips.json written next to the FBX. The projectile
// meshes (Assets/Monsters/Projectiles, made by Tools/make_projectiles.py) import as plain static meshes.
// Both import at MonsterScale.Factor: the Blender output is sized against a 1.109 m character, the game's
// player is MonsterScale.PlayerSize tall.
public class MonsterImporter : AssetPostprocessor
{
    public const string Root = "Assets/Monsters/";

    public const string ProjectileDir = Root + "Projectiles/";

    public override uint GetVersion() => 5;

    static bool IsFbx(string path) => path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

    public static bool IsMonster(string path) => path.StartsWith(Root) && !path.StartsWith(ProjectileDir) && IsFbx(path);

    static bool IsProjectile(string path) => path.StartsWith(ProjectileDir) && IsFbx(path);

    public static string MetaPath(string fbxPath) =>
        Path.ChangeExtension(fbxPath, null) + ".clips.json";

    [System.Serializable] public class EventMeta { public float time; public string name; }
    [System.Serializable] public class ClipMeta { public string name; public int frames; public bool loop; public EventMeta[] events; }
    [System.Serializable] public class Meta
    {
        public string name;
        public float size, height, hover, scale;
        public bool textured;
        public ClipMeta[] clips;
    }

    public static Meta LoadMeta(string fbxPath)
    {
        var path = MetaPath(fbxPath);
        return File.Exists(path) ? JsonUtility.FromJson<Meta>(File.ReadAllText(path)) : null;
    }

    void OnPreprocessModel()
    {
        if (IsProjectile(assetPath))
        {
            var mesh = (ModelImporter)assetImporter;
            mesh.globalScale = Roguelike.MonsterScale.Factor;     // sized against this game's player
            mesh.useFileScale = true;
            mesh.importAnimation = false;
            mesh.animationType = ModelImporterAnimationType.None;
            mesh.importCameras = mesh.importLights = mesh.importBlendShapes = false;
            mesh.materialImportMode = ModelImporterMaterialImportMode.None;
            mesh.importNormals = ModelImporterNormals.Import;
            return;
        }
        if (!IsMonster(assetPath))
            return;
        var importer = (ModelImporter)assetImporter;
        importer.globalScale = Roguelike.MonsterScale.Factor; // sized against this game's player (MonsterScale)
        importer.useFileScale = true;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;               // the attacks look up the socket bones
        importer.isReadable = true;                         // smoke particles emit from the skinned mesh
        importer.skinWeights = ModelImporterSkinWeights.Standard;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;   // MonsterSetup assigns the ghost materials
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
    }

    void OnPreprocessAnimation()
    {
        if (!IsMonster(assetPath))
            return;
        context.DependsOnSourceAsset(MetaPath(assetPath));
        var meta = LoadMeta(assetPath);
        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            // Blender names the takes "<armature>|<action>"
            clip.name = clip.takeName.Split('|').Last();
            var m = meta?.clips?.FirstOrDefault(c => c.name == clip.name);
            clip.loopTime = m != null && m.loop;
            clip.loopPose = false;
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = false;
            clip.events = m?.events == null ? new AnimationEvent[0] : m.events.Select(e => new AnimationEvent
            {
                time = Mathf.Clamp01(e.time),
                functionName = "MonsterEvent",
                stringParameter = e.name,
            }).ToArray();
        }
        importer.clipAnimations = clips;
    }
}
