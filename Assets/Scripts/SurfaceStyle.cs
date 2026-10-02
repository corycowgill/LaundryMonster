using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Applies a generated texture and a sensible smoothness to whatever renderer it
    /// sits on. Done at runtime so no texture ever has to be serialised into the scene
    /// or survive the build-time asset stripper.
    /// </summary>
    public class SurfaceStyle : MonoBehaviour
    {
        public enum Style { Floor, Wall, Metal, Weave, Wood, Plain }

        public Style Kind = Style.Plain;
        public Vector2 Tiling = Vector2.one;
        public float Smoothness = 0.2f;
        public float Metallic = 0f;

        void Start()
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;

            var mat = new Material(rend.sharedMaterial);

            Texture2D tex = null;
            switch (Kind)
            {
                case Style.Floor: tex = ProceduralTex.FloorTile(); break;
                case Style.Wall:  tex = ProceduralTex.Wall();      break;
                case Style.Metal: tex = ProceduralTex.Brushed();   break;
                case Style.Weave: tex = ProceduralTex.Weave();     break;
            }

            if (tex != null)
            {
                // mainTexture maps to _BaseMap on URP/Lit.
                mat.mainTexture = tex;
                mat.mainTextureScale = Tiling;
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Metallic);

            rend.sharedMaterial = mat;
        }
    }
}
