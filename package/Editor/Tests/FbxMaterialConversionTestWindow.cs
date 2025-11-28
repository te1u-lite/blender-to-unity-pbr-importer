using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlenderToUnityPBRImporter.Editor
{
    public class FbxMaterialTestWindow : EditorWindow
    {
        private GameObject fbxObject;
        private string materialFolder = "Assets/TestModels"; // 作成先マテリアルフォルダ

        [MenuItem("Tests/FBX Material Auto-Convert Test")]
        public static void ShowWindow()
        {
            GetWindow<FbxMaterialTestWindow>("FBX Material Test");
        }

        private void OnGUI()
        {
            GUILayout.Label("FBX Material Auto Conversion Test", EditorStyles.boldLabel);

            fbxObject = (GameObject)EditorGUILayout.ObjectField(
                "FBX Object",
                fbxObject,
                typeof(GameObject),
                false
            );

            materialFolder = EditorGUILayout.TextField("Material Output Folder", materialFolder);

            GUI.enabled = fbxObject != null;

            if (GUILayout.Button("Run Conversion"))
            {
                RunConversion();
            }

            GUI.enabled = true;
        }

        private void RunConversion()
        {
            if (fbxObject == null)
            {
                Debug.LogError("[FBX Test] FBX が指定されていません。");
                return;
            }

            // FBX のパス
            string fbxAssetPath = AssetDatabase.GetAssetPath(fbxObject);
            string fbxDir = Path.GetDirectoryName(fbxAssetPath).Replace("\\", "/");

            // .fbm フォルダ推測
            string fbxName = Path.GetFileNameWithoutExtension(fbxAssetPath);
            string fbmFolder = Path.Combine(fbxDir, fbxName + ".fbm").Replace("\\", "/");

            if (!Directory.Exists(fbmFolder))
            {
                Debug.LogError($"[FBX Test] .fbm フォルダが見つかりません: {fbmFolder}");
                return;
            }

            Debug.Log("[FBX Test] FBM Folder: " + fbmFolder);

            // FBX から Renderer 取得
            Renderer renderer = fbxObject.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                Debug.LogError("[FBX Test] Renderer が見つかりません。");
                return;
            }

            // 新規マテリアル作成
            var builder = new MaterialBuilderStandard();
            Material newMat = builder.CreateMaterial(materialFolder, fbxName);

            if (newMat == null)
            {
                Debug.LogError("[FBX Test] マテリアルの作成に失敗しました。");
                return;
            }

            Debug.Log("[FBX Test] Created Material: " + newMat.name);

            // テクスチャ割り当て
            var assigner = new TextureAssigner();
            assigner.AssignTextures(newMat, fbmFolder);

            // FBX のマテリアル差し替え
            renderer.sharedMaterial = newMat;

            Debug.Log("[FBX Test] 処理が完了しました！");
        }
    }
}
