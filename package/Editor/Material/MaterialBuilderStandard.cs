using UnityEngine;
using UnityEditor;
using System.IO;

namespace BlenderToUnityPBRImporter.Editor.MaterialBuilder
{
    public class MaterialBuilderStandard : MaterialBuilder
    {
        protected override Shader GetShader()
        {
            Shader shader = Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[MaterialBuilderStandard] Standard シェーダーが見つかりません。"
                    + "（URP/HDRP プロジェクトでは使用不可です）");
            }

            return shader;
        }

        public override Material CreateMaterial(string materialPath, string baseName)
        {
            // 入力チェック
            if (string.IsNullOrEmpty(materialPath))
            {
                Debug.LogError("[MaterialBuilderStandard] materialPath が null です。");
                return null;
            }
            if (string.IsNullOrEmpty(baseName))
            {
                Debug.LogError("[MaterialBuilderStandard] baseName が空のため 'Unnamed' を使用します。");
                baseName = "Unnamed";
            }

            // シェーダー取得
            var shader = GetShader();
            if (shader == null)
            {
                Debug.LogError("[MaterialBuilderStandard] シェーダーが取得できないためマテリアル作成を中断します。");
                return null;
            }

            // フォルダの存在を保証 (なければ作成)
            if (!AssetDatabase.IsValidFolder(materialPath))
            {
                Debug.Log($"[MaterialBuilderStandard] フォルダが無いため作成します: {materialPath}");

                if (!EnsureFolder(materialPath))
                {
                    Debug.LogError($"[MaterialBuilderStandard] フォルダの作成に失敗しました: {materialPath}");
                    return null;
                }
            }

            // マテリアル生成
            var mat = new Material(shader);
            string matName = BuildMaterialName(baseName);

            matName = RemoveInvalidChars(matName);

            string fullPath = Path.Combine(materialPath, matName + ".mat");
            string assetPath = fullPath.Replace("\\", "/");

            // すでに存在する場合 → 上書きしない、既存を返す
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                Debug.LogWarning($"[MaterialBuilderStandard] 既に存在するため再利用します: {assetPath}");
                return existing;
            }

            // 保存
            AssetDatabase.CreateAsset(mat, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[MaterialBuilderStandard] マテリアル作成完了: " + assetPath);

            return mat;
        }

        // フォルダ生成
        bool EnsureFolder(string path)
        {
            path = path.Replace("\\", "/");

            if(AssetDatabase.IsValidFolder(path))
            {
                return true;
            }

            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folderName = Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                if(!EnsureFolder(parent))
                {
                    return false;
                }
            }
            AssetDatabase.CreateFolder(parent, folderName);
            return true;
        }

        // 禁止文字の除去
        private string RemoveInvalidChars(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            foreach (var c in invalids)
            {
                name = name.Replace(c.ToString(), "_");
            }

            return name;
        }
    }
}