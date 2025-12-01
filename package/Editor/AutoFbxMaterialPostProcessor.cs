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
        void OnPostprocessModel(GameObject fbxRoot)
        {
            string fbxPath = assetPath;

            if (!fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return;

            // ★ キューに積むだけで、ここでは絶対に AssetDatabase を触らない！
            DeferredFbxProcessor.Enqueue(() =>
            {
                ProcessFbxAfterImport(fbxPath);
            });
        }

        private void ProcessFbxAfterImport(string fbxPath)
        {
            // 1) Importer 取得＆処理済みチェック
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                return;

            const string ProcessedFlag = "BTU_PBR_IMPORTED";

            if (importer.userData == ProcessedFlag)
            {
                Debug.Log("[AutoFbxMaterialPostProcessor] このFBXはすでに処理済みのためスキップします: " + fbxPath);
                return;
            }

            // 2) パス系
            string dir = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
            string matFolder = $"{dir}/Materials";

            // Materials フォルダ作成（なければ）
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

                // 名前に基づいて外部マテリアルを作成／再利用（StarSparrow → StarSparrow_mat.mat）
                var externalMat = builder.CreateMaterial(matFolder, internalMatName);
                if (externalMat == null)
                    continue;

                // 必要に応じてテクスチャ割り当て
                assigner.ApplyToMaterial(externalMat, data, generateMR: true);

                // Remap キー
                var id = new AssetImporter.SourceAssetIdentifier(
                    typeof(Material),
                    internalMatName
                );

                if (!existingMap.TryGetValue(id, out var mappedObj) || mappedObj != externalMat)
                {
                    importer.AddRemap(id, externalMat);
                    changed = true;
                }
            }

            // 5) 変更があったときだけ再インポート＆処理済みフラグ
            if (changed)
            {
                importer.userData = ProcessedFlag;
                importer.SaveAndReimport();
            }
        }

    }
}