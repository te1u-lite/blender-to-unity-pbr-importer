using UnityEngine;
using UnityEditor;
using System.IO;

namespace BlenderToUnityPBRImporter.Editor
{
    /// <summary>
    /// FBX インポート時に自動的にマテリアル作成＆テクスチャ割り当てを行う PostProcessor
    /// </summary>
    public class AutoFbxMaterialPostProcessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;

            string file = System.IO.Path.GetFileName(assetPath).ToLower();

            // NormalMap 判定（あなたの設定に合わせる）
            if (TextureFinder.IsNormal(file))
            {
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                }
            }
        }

        void OnPostprocessModel(GameObject fbxRoot)
        {
            var settings = PbrImportSettings.GetOrCreateSettings();

            if (!settings.autoImportEnabled)
                return;

            string fbxPath = assetPath;

            if (!fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return;

            // ★ キューに積むだけで、ここでは絶対に AssetDatabase を触らない！
            DeferredFbxProcessor.Enqueue(() =>
            {
                ProcessFbxAfterImport(fbxPath);
            });
        }

        private void ProcessFbxAfterImport(string fbxPath, bool force = false)
        {
            // 1) Importer 取得＆処理済みチェック
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            importer.materialLocation = ModelImporterMaterialLocation.External;

            const string ProcessedFlag = "BTU_PBR_IMPORTED";

            if (force)
            {
                importer.userData = ""; // リセット
            }

            // 2) パス系
            string dir = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

            string matFolder = $"{dir}/Materials";
            if (!AssetDatabase.IsValidFolder(matFolder))
                AssetDatabase.CreateFolder(dir, "Materials");

            // FBM フォルダ
            string fbmFolder = $"{dir}/{fbxName}.fbm";
            if (!Directory.Exists(fbmFolder))
            {
                Debug.LogWarning("[AutoFbxMaterialPostProcessor] FBM フォルダがないためスキップ: " + fbmFolder);
                return;
            }

            // 3) テクスチャ検索 & 割り当て準備（1回だけ）
            var search = TextureFinder.FindTextures(fbmFolder);
            var assigner = new TextureAssigner();
            var data = assigner.PrepareAssignment(search);

            var settings = PbrImportSettings.GetOrCreateSettings();
            var builder = new MaterialBuilderStandard();

            bool changed = false;
            var existingMap = importer.GetExternalObjectMap();

            // 4) FBX 内部マテリアルごとに外部マテリアルを用意して Remap
            var internalAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

            foreach (var internalObj in internalAssets)
            {
                if (internalObj is not Material internalMat)
                    continue;

                string internalMatName = internalMat.name;

                var externalMat = builder.CreateMaterial(matFolder, internalMatName);
                if (externalMat == null)
                    continue;

                // マテリアル内容の変更を検知
                bool matDirty = assigner.ApplyToMaterial(externalMat, data, TextureAssignmentWindow.MRMode.MetallicAndRoughness);
                if (matDirty)
                {
                    changed = true;
                    EditorUtility.SetDirty(externalMat);
                }

                // ★ Remap（これが無いと絶対に反映されない）
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), internalMatName);

                if (!existingMap.TryGetValue(id, out var mappedObj) || mappedObj != externalMat)
                {
                    importer.AddRemap(id, externalMat);
                    changed = true;
                }
            }

            if (changed)
            {
                importer.userData = ProcessedFlag;
                AssetDatabase.WriteImportSettingsIfDirty(fbxPath);
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}