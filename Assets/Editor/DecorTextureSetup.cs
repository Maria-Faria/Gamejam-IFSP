using System.IO;
using UnityEditor;
using UnityEngine;

public static class DecorTextureSetup
{
  private const string DecorFolder = "Assets/Decor";
  private const string MonsterFolder = "Assets/Sprites/Monster";

  [MenuItem("Tools/Decor/Extrair Texturas e Materiais")]
  public static void Run()
  {
    SetupFolder(DecorFolder, "DECOR");
  }

  [MenuItem("Tools/Monstros/Extrair Texturas e Materiais")]
  public static void RunMonsters()
  {
    SetupFolder(MonsterFolder, "MONSTRO");
  }

  [InitializeOnLoadMethod]
  private static void SetupMonstersOnce()
  {
    if (!AssetDatabase.IsValidFolder(MonsterFolder) || AssetDatabase.IsValidFolder($"{MonsterFolder}/Materials")) return;
    EditorApplication.delayCall += RunMonsters;
  }

  private static void SetupFolder(string folder, string logTag)
  {
    foreach (string modelPath in Directory.GetFiles(folder, "*.fbx"))
    {
      string path = modelPath.Replace('\\', '/');
      string modelName = Path.GetFileNameWithoutExtension(path);
      string textureFolder = $"{folder}/Textures/{modelName}";
      Directory.CreateDirectory(textureFolder);

      ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
      importer.importCameras = false;
      importer.importLights = false;
      importer.ExtractTextures(textureFolder);
      AssetDatabase.Refresh();

      Texture2D baseMap = null;
      Texture2D normalMap = null;
      foreach (string texturePath in Directory.GetFiles(textureFolder, "*.png"))
      {
        string assetPath = texturePath.Replace('\\', '/');
        string textureName = Path.GetFileNameWithoutExtension(assetPath);
        if (textureName.EndsWith("_normal"))
        {
          TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(assetPath);
          if (textureImporter.textureType != TextureImporterType.NormalMap)
          {
            textureImporter.textureType = TextureImporterType.NormalMap;
            textureImporter.SaveAndReimport();
          }
          normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
        else if (!textureName.EndsWith("_roughness") && !textureName.EndsWith("_metallic"))
        {
          baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
      }

      string materialPath = $"{folder}/Materials/{modelName}.mat";
      Directory.CreateDirectory($"{folder}/Materials");
      Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
      if (material == null)
      {
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        AssetDatabase.CreateAsset(material, materialPath);
      }
      material.SetColor("_BaseColor", baseMap != null ? Color.white : FallbackColor(modelName));
      material.SetFloat("_Smoothness", 0.3f);
      if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
      if (normalMap != null)
      {
        material.SetTexture("_BumpMap", normalMap);
        material.EnableKeyword("_NORMALMAP");
      }
      EditorUtility.SetDirty(material);

      foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
      {
        if (subAsset is Material embedded)
        {
          importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
        }
      }
      importer.SaveAndReimport();
      Debug.Log($"{logTag} {modelName}: base={(baseMap != null ? baseMap.name : "none")} normal={(normalMap != null ? normalMap.name : "none")}");
    }

    AssetDatabase.SaveAssets();
  }

  private static Color FallbackColor(string modelName)
  {
    switch (modelName)
    {
      case "crate": return new Color(0.55f, 0.38f, 0.22f);
      case "gun": return new Color(0.35f, 0.37f, 0.4f);
      default: return new Color(0.6f, 0.6f, 0.6f);
    }
  }
}
