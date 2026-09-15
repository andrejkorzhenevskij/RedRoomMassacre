#if UNITY_EDITOR
namespace AyuoDev.CharCrafter
{
    using UnityEngine;
    using UnityEditor;
    using UnityEngine.Rendering;

    public class PipelineMaterialConverter : EditorWindow
    {
        [MenuItem("Window/CharCrafter/Pipeline Material Converter")]
        public static void ShowWindow()
        {
            GetWindow(typeof(PipelineMaterialConverter));
        }

        private void OnGUI()
        {
            GUILayout.Label("Render Pipeline Material Converter", EditorStyles.boldLabel);
            if (GUILayout.Button("Convert All Materials In Project"))
            {
                ConvertAllMaterials();
            }
        }

        private static void ConvertAllMaterials()
        {
            string assetFolder = "Assets/CharCrafter - Moduler Low-Poly Character Creation/";
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { assetFolder });
            int convertedCount = 0;

            string pipeline = GetRenderPipeline();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    bool converted = ConvertMaterial(mat, pipeline);
                    if (converted)
                    {
                        EditorUtility.SetDirty(mat);
                        convertedCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Pipeline conversion completed. {convertedCount} materials updated.");
        }

        private static bool ConvertMaterial(Material mat, string pipeline)
        {
            // Backup common values
            Color baseColor = Color.white;
            float metallic = 0f;
            float smoothness = 0.5f;
            bool isTransparent = false;

            // Fetch base color from URP/Built-in
            if (mat.HasProperty("_BaseColor"))
                baseColor = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color"))
                baseColor = mat.GetColor("_Color");

            if (mat.HasProperty("_Metallic"))
                metallic = mat.GetFloat("_Metallic");

            if (mat.HasProperty("_Smoothness"))
                smoothness = mat.GetFloat("_Smoothness");

            // Detect transparency
            if (mat.HasProperty("_Surface"))
                isTransparent = mat.GetFloat("_Surface") == 1f;
            else if (mat.renderQueue > 2500)
                isTransparent = true;

            // Set correct target shader
            Shader targetShader = pipeline switch
            {
                "Built-in" => Shader.Find("Standard"),
                "URP" => Shader.Find("Universal Render Pipeline/Lit"),
                "HDRP" => Shader.Find("HDRP/Lit"),
                _ => null
            };

            if (targetShader == null)
            {
                Debug.LogWarning($"Unsupported pipeline: {pipeline}");
                return false;
            }

            if (mat.shader == targetShader)
                return false; // Already converted

            mat.shader = targetShader;

            // Reapply color clear conflicting textures
            if (pipeline == "HDRP")
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", baseColor);

                if (mat.HasProperty("_BaseColorMap"))
                    mat.SetTexture("_BaseColorMap", null); // Remove map so color shows
            }
            else if (pipeline == "URP")
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", baseColor);

                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", null); // Remove texture for color-only
            }
            else if (pipeline == "Built-in")
            {
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", baseColor);

                if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", null);
            }

            // Apply other properties
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);

            // Handle transparency settings
            if (pipeline == "Built-in")
            {
                mat.SetFloat("_Mode", isTransparent ? 3f : 0f);
                mat.renderQueue = isTransparent ? 3000 : 2000;
            }
            else if (pipeline == "URP")
            {
                mat.SetFloat("_Surface", isTransparent ? 1f : 0f);
                mat.SetFloat("_Blend", 0f);
                mat.renderQueue = isTransparent ? 3000 : 2000;
            }
            else if (pipeline == "HDRP")
            {
                mat.SetFloat("_SurfaceType", isTransparent ? 1f : 0f);
                mat.renderQueue = isTransparent ? 3000 : 2000;
            }

            Debug.Log($"Converted: {mat.name} {pipeline} pipeline ({(isTransparent ? "Transparent" : "Opaque")})");
            return true;
        }

        private static string GetRenderPipeline()
        {
            var asset = GraphicsSettings.defaultRenderPipeline;

            if (asset == null)
                return "Built-in";

            string rpAsset = asset.GetType().ToString();

            if (rpAsset.Contains("UniversalRenderPipelineAsset"))
                return "URP";
            if (rpAsset.Contains("HDRenderPipelineAsset"))
                return "HDRP";

            return "Unknown";
        }
    }
}
#endif
