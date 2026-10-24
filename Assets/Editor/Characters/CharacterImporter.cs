using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Import rules for the playable scientists (Assets/Characters/<Key>/<key>.fbx, made by
// Tools/make_characters.py): Generic rig with its own avatar, every Blender action as a clip named after it,
// looping as the <key>.clips.json next to the FBX says. The pistol (Assets/Characters/Pistol) is a plain
// static mesh. CharacterSetup assigns the materials, so none are imported.
public class CharacterImporter : AssetPostprocessor
{
    public const string Root = "Assets/Characters/";
    public const string PistolDir = Root + "Pistol/";

    public override uint GetVersion() => 1;

    static bool IsFbx(string path) => path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

    public static bool IsCharacter(string path) => path.StartsWith(Root) && !path.StartsWith(PistolDir) && IsFbx(path);

    static bool IsPistol(string path) => path.StartsWith(PistolDir) && IsFbx(path);

    [System.Serializable] public class ClipMeta { public string name; public int frames; public bool loop; }
    [System.Serializable] public class Meta
    {
        public string name, source, hand;
        public float height, width;
        public bool rigged;
        public ClipMeta[] clips;
    }

    public static string MetaPath(string fbxPath) => Path.ChangeExtension(fbxPath, null) + ".clips.json";

    public static Meta LoadMeta(string fbxPath)
    {
        var path = MetaPath(fbxPath);
        return File.Exists(path) ? JsonUtility.FromJson<Meta>(File.ReadAllText(path)) : null;
    }

    void OnPreprocessModel()
    {
        if (IsPistol(assetPath))
        {
            var mesh = (ModelImporter)assetImporter;
            mesh.globalScale = 1f;
            mesh.useFileScale = true;
            mesh.importAnimation = false;
            mesh.animationType = ModelImporterAnimationType.None;
            mesh.importCameras = mesh.importLights = mesh.importBlendShapes = false;
            mesh.materialImportMode = ModelImporterMaterialImportMode.None;
            return;
        }
        if (!IsCharacter(assetPath))
            return;
        var importer = (ModelImporter)assetImporter;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;               // the rig looks up the hand and arm bones
        importer.skinWeights = ModelImporterSkinWeights.Standard;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
    }

    void OnPreprocessAnimation()
    {
        if (!IsCharacter(assetPath))
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
            clip.loopTime = m == null || m.loop;
            clip.loopPose = false;
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = false;
        }
        importer.clipAnimations = clips;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root))
            return;
        var importer = (TextureImporter)assetImporter;
        importer.maxTextureSize = 2048;
        if (assetPath.EndsWith("_normal.png"))
            importer.textureType = TextureImporterType.NormalMap;
        else if (assetPath.EndsWith("_metallicSmoothness.png"))
            importer.sRGBTexture = false;
    }
}
