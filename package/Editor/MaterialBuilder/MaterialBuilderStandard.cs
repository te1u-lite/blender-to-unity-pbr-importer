using UnityEngine;
using UnityEditor;
using System.IO;

namespace BlenderToUnityPBRImporter.Editor
{
    /// <summary>
    /// Standard シェーダーを利用してマテリアルを生成するビルダー。
    /// フォルダ生成、既存マテリアルの再利用などの処理を統一。
    /// </summary>
    public class MaterialBuilderStandard : MaterialBuilder
    {
        /// <summary>
        /// Standard シェーダーを取得する。
        /// </summary>
        protected override Shader GetShader()
        {
            Shader shader = Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[ERROR][MaterialBuilderStandard] Standard シェーダーが見つかりません。（URP/HDRP では使用不可）");
            }

            return shader;
        }

        /// <summary>
        /// 指定パスへ新規マテリアルを生成する。
        /// 既に存在する場合は再利用する。
        /// </summary>
        public override Material CreateMaterial(string materialPath, string baseName)
        {
            // 入力チェック
            if (string.IsNullOrEmpty(materialPath))
            {
                Debug.LogError("[ERROR][MaterialBuilderStandard] materialPath が null または空です。");
                return null;
            }
            baseName = string.IsNullOrEmpty(baseName) ? "Unnamed" : baseName;

            // シェーダー取得
            var shader = GetShader();
            if (shader == null)
                return LogErrorAndNull("シェーダーが取得できなかったため中断します。");

            // フォルダの存在を保証 (なければ作成)
            if (!EnsureFolder(materialPath))
                return LogErrorAndNull($"フォルダ作成に失敗しました: {materialPath}");

            string matName = RemoveInvalidChars(BuildMaterialName(baseName));
            string assetPath = Path.Combine(materialPath, matName + ".mat").Replace("\\", "/");

            // 既存マテリアルが存在する場合はそれを返す
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                Debug.LogWarning($"[WARN][MaterialBuilderStandard] 既存マテリアルを再利用します: {assetPath}");
                return existing;
            }

            // 新規作成
            var mat = new Material(shader) { hideFlags = HideFlags.None };
            AssetDatabase.CreateAsset(mat, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[INFO][MaterialBuilderStandard] マテリアル生成: {assetPath}");
            return mat;
        }

        /// <summary>
        /// エラーログを出力し null を返すヘルパー。
        /// </summary>
        private Material LogErrorAndNull(string msg)
        {
            Debug.LogError("[ERROR][MaterialBuilderStandard] " + msg);
            return null;
        }

        /// <summary>
        /// 指定パスのフォルダを再帰的に生成する。
        /// </summary>
        private bool EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            path = path.Replace("\\", "/");

            // 既にある場合は成功
            if (AssetDatabase.IsValidFolder(path))
            {
                return true;
            }

            // 親フォルダを先に作成
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folderName = Path.GetFileName(path);

            if (!EnsureFolder(parent)) return false;

            AssetDatabase.CreateFolder(parent, folderName);
            return true;
        }

        /// <summary>
        /// ファイル名に使えない文字を置換する。
        /// </summary>
        private string RemoveInvalidChars(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "_");
            return name;
        }
    }
}