using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace BlenderToUnityPBRImporter.Editor
{
    public class TextureAssignmentWindow : EditorWindow
    {
        private GameObject fbxObject;
        private string textureFolder = "";
        private TextureFinder.TextureSearchResult searchResult;
        private TextureAssigner.TextureAssignmentData assignmentData;

        private int albedoIndex = -1;
        private int normalIndex = -1;
        private int metallicIndex = -1;
        private int roughnessIndex = -1;

        [MenuItem("tools/Texture Assignment")]
        public static void ShowWindow()
        {
            GetWindow<TextureAssignmentWindow>("Texture Assignment");
        }

        private void OnGUI()
        {
            fbxObject = (GameObject)EditorGUILayout.ObjectField(
                "FBX Object",
                fbxObject,
                typeof(GameObject),
                false
            );

            EditorGUILayout.LabelField("手動テクスチャ割り当て", EditorStyles.boldLabel);

            if (GUILayout.Button("検索 (TextureFinder 使用)"))
            {
                DoSearch();
            }

            EditorGUILayout.Space();

            if (assignmentData != null)
            {
                DrawTextureSelectionUI();
            }

            GUILayout.Space(10);

            if (assignmentData != null && GUILayout.Button("選択結果を Material に適用"))
            {
                ApplyAssignments();
            }


        }

        private void DoSearch()
        {
            if (fbxObject == null)
            {
                Debug.LogError("FBX が未設定です。");
                return;
            }

            string fbmFolder = GetFBMFolder(fbxObject);
            if (fbmFolder == null)
            {
                Debug.LogError("FBM フォルダが見つかりません。");
                return;
            }

            searchResult = TextureFinder.FindTextures(fbmFolder);
            
            var assigner = new TextureAssigner();
            assignmentData = assigner.PrepareAssignment(searchResult);

            // Dropdown 初期選択値
            albedoIndex = assignmentData.AllTextures.IndexOf(assignmentData.Albedo);
            normalIndex = assignmentData.AllTextures.IndexOf(assignmentData.Normal);
            metallicIndex = assignmentData.AllTextures.IndexOf(assignmentData.Metallic);
            roughnessIndex = assignmentData.AllTextures.IndexOf(assignmentData.Roughness);
        }

        private void DrawTextureSelectionUI()
        {
            string[] names = assignmentData.AllTextures
            .Select(t => t != null ? t.name : "(null)")
            .ToArray();

            EditorGUILayout.LabelField("手動割り当て", EditorStyles.boldLabel);

            albedoIndex = EditorGUILayout.Popup("Albedo", albedoIndex, names);
            normalIndex = EditorGUILayout.Popup("Normal", normalIndex, names);
            metallicIndex = EditorGUILayout.Popup("Metallic", metallicIndex, names);
            roughnessIndex = EditorGUILayout.Popup("Roughness", roughnessIndex, names);

            // Index → 実体に反映
            assignmentData.Albedo = (albedoIndex >= 0 ? assignmentData.AllTextures[albedoIndex] : null);
            assignmentData.Normal = (normalIndex >= 0 ? assignmentData.AllTextures[normalIndex] : null);
            assignmentData.Metallic = (metallicIndex >= 0 ? assignmentData.AllTextures[metallicIndex] : null);
            assignmentData.Roughness = (roughnessIndex >= 0 ? assignmentData.AllTextures[roughnessIndex] : null);
        }

        private Material CreateMaterialForFBX(GameObject fbxObject)
        {
            if(fbxObject    == null)
            {
                Debug.LogError("FBX オブジェクトが未設定です。");
                return null;
            }

            string fbxPath = AssetDatabase.GetAssetPath(fbxObject);
            string fbxDir = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

            // マテリアル保存フォルダ
            string matFolder = fbxDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(matFolder))
            {
                AssetDatabase.CreateFolder(fbxDir, "Materials");
            }

            string matPath = $"{matFolder}/{fbxName}.mat";

            // 既に存在するならロード
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                return mat;
            }

            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Auto] Material 生成: {matPath}");
            return mat;
        }

        private string GetFBMFolder(GameObject fbxObject)
        {
            string fbxPath = AssetDatabase.GetAssetPath(fbxObject);
            string fbxDir = Path.GetDirectoryName(fbxPath);
            string fbxName = Path.GetFileNameWithoutExtension(fbxPath);

            string fbmFolder = $"{fbxDir}/{fbxName}.fbm";

            if (System.IO.Directory.Exists(fbmFolder))
                return fbmFolder;

            Debug.LogWarning($"[Auto] FBM フォルダが見つかりません: {fbmFolder}");
            return null;
        }

        private Texture2D AutoGenerateMR(TextureAssigner.TextureAssignmentData data)
        {
            string metallicPath = data.Metallic != null ? AssetDatabase.GetAssetPath(data.Metallic) : null;
            string roughnessPath = data.Roughness != null ? AssetDatabase.GetAssetPath(data.Roughness) : null;

            if (metallicPath == null && roughnessPath == null)
                return null;

            return TextureConverter.CreateMetallicRoughnessMap(metallicPath, roughnessPath);
        }

        private void ApplyMRToMaterial(Material mat, Texture2D mrTex)
        {
            if (mrTex == null) return;

            mat.SetTexture("_MetallicGlossMap", mrTex);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }

        private void ApplyAssignments()
        {
            if (fbxObject == null)
            {
                Debug.LogError("FBX が未設定です。");
                return;
            }

            // 1. Material 自動生成 or 既存マテリアル取得
            var mat = CreateMaterialForFBX(fbxObject);
            if (mat == null)
            {
                Debug.LogError("Material の生成に失敗しました。");
                return;
            }

            var assigner = new TextureAssigner();
            assigner.ApplyToMaterial(mat, assignmentData);

            // 2. Metallic + Roughness から MR 自動生成
            var mrTex = AutoGenerateMR(assignmentData);
            if (mrTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", mrTex);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }

            Debug.Log("[TextureAssignmentWindow] テクスチャ割り当て完了");
        }

    }
}