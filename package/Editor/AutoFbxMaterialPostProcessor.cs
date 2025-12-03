using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace BlenderToUnityPBRImporter.Editor
{
    /// <summary>
    /// Unity のデフォルト External Material 生成を尊重し、
    /// 生成後に上書き編集だけ行う PostProcessor
    /// </summary>
    public class AutoFbxMaterialPostProcessor : AssetPostprocessor
    {

        void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;

            string file = Path.GetFileName(assetPath).ToLower();

            if (TextureFinder.IsNormal(file))
            {
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                }
            }
        }

        void OnPreprocessModel()
        {
            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPostprocessModel(GameObject fbxRoot)
        {
            if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (ModelImporter)assetImporter;

            bool isFirstImport = importer.userData != "InitialRemapDone";

            DeferredFbxProcessor.Enqueue(() =>
            {
                if (isFirstImport)
                {
                    Debug.Log("[Processor] Initial import: " + assetPath);
                    ProcessInitialImport(assetPath);
                }
                else
                {
                    Debug.Log("[Processor] Update import (no reimport): " + assetPath);
                    ProcessUpdateImport(assetPath);
                }
            });
        }

        private void ProcessFbxAfterImport(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            // ★ 無限ループ防止：既に処理済みなら抜ける
            if (importer.userData == "MaterialRemapDone")
            {
                Debug.Log("[AutoFbxMaterialPostProcessor] Skip SaveAndReimport (already processed)");
                return;
            }

            string dir = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
            string matFolder = $"{dir}/Materials";

            if (!AssetDatabase.IsValidFolder(matFolder))
                AssetDatabase.CreateFolder(dir, "Materials");

            string fbmFolder = $"{dir}/{fbxName}.fbm";
            if (!Directory.Exists(fbmFolder))
                return;

            var search = TextureFinder.FindTextures(fbmFolder);
            var assigner = new TextureAssigner();
            var data = assigner.PrepareAssignment(search);

            var builder = new MaterialBuilderStandard();

            var internalAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

            foreach (var internalObj in internalAssets)
            {
                if (internalObj is not Material internalMat)
                    continue;

                string internalMatName = internalMat.name;
                string matPath = $"{matFolder}/{internalMatName}.mat";

                Material externalMat =
                    AssetDatabase.LoadAssetAtPath<Material>(matPath)
                    ?? builder.CreateMaterial(matFolder, internalMatName);

                if (externalMat == null)
                    continue;

                bool dirty = assigner.ApplyToMaterial(
                    externalMat,
                    data,
                    search,
                    TextureAssignmentWindow.MRMode.MetallicAndRoughness
                );

                if (dirty)
                {
                    EditorUtility.SetDirty(externalMat);
                }

                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), internalMatName);
                importer.AddRemap(id, externalMat);
            }

            // ▼▼▼ ★ SaveAndReimport（初回のみ） ▼▼▼
            importer.userData = "MaterialRemapDone";  // フラグを付ける
            Debug.Log("[AutoFbxMaterialPostProcessor] SaveAndReimport triggered");
            importer.SaveAndReimport();
        }

        private void ProcessInitialImport(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            string dir = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
            string matFolder = $"{dir}/Materials";

            if (!AssetDatabase.IsValidFolder(matFolder))
                AssetDatabase.CreateFolder(dir, "Materials");

            string fbmFolder = $"{dir}/{fbxName}.fbm";
            if (!Directory.Exists(fbmFolder)) return;

            var search = TextureFinder.FindTextures(fbmFolder);
            var assigner = new TextureAssigner();
            var data = assigner.PrepareAssignment(search);
            var builder = new MaterialBuilderStandard();

            var internalAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

            foreach (var internalObj in internalAssets)
            {
                if (internalObj is not Material internalMat)
                    continue;

                string internalMatName = internalMat.name;
                string matPath = $"{matFolder}/{internalMatName}.mat";

                // 作成（初回）
                Material externalMat = builder.CreateMaterial(matFolder, internalMatName);

                assigner.ApplyToMaterial(
                    externalMat,
                    data,
                    search,
                    TextureAssignmentWindow.MRMode.MetallicAndRoughness
                );

                EditorUtility.SetDirty(externalMat);

                // ★ Remap を初回に適用
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), internalMatName);
                importer.AddRemap(id, externalMat);
            }

            // ★ 初回は SaveAndReimport が必須
            importer.userData = "InitialRemapDone";
            importer.SaveAndReimport();
        }


        private void ProcessUpdateImport(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            string dir = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

            string fbmFolder = $"{dir}/{fbxName}.fbm";
            if (!Directory.Exists(fbmFolder)) return;

            var search = TextureFinder.FindTextures(fbmFolder);
            var assigner = new TextureAssigner();

            // ★ Remap されている外部マテリアルを全て取得
            var remaps = importer.GetExternalObjectMap();

            foreach (var kv in remaps)
            {
                if (kv.Value is not Material externalMat)
                    continue;

                Debug.Log("[Processor] Updating material: " + externalMat.name);

                var data = assigner.PrepareAssignment(search);

                assigner.ApplyToMaterial(
                    externalMat,
                    data,
                    search,
                    TextureAssignmentWindow.MRMode.ForceMetallicAndRoughness
                );

                EditorUtility.SetDirty(externalMat);
            }

            Debug.Log("[Processor] External materials updated (no SaveAndReimport)");
        }


    }
}
