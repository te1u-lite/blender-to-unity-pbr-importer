using UnityEngine;

namespace BlenderToUnityPBRImporter.Editor
{
    public abstract class MaterialBuilder
    {
        /// <summary>
        /// 指定パスに新規マテリアルを生成します。
        /// </summary>
        public abstract Material CreateMaterial(string materialPath, string baseName);

        /// <summary>
        /// シェーダーを返す (実装ごとに異なる)
        /// </summary>
        protected abstract Shader GetShader();

        /// <summary>
        /// マテリアル名を生成
        /// </summary>
        protected virtual string BuildMaterialName(string baseName)
        {
            if (string.IsNullOrEmpty(baseName))
                baseName = "Unnamed";

            return $"{baseName}_mat";
        }
    }
}