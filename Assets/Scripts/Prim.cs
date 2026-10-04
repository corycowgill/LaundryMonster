using UnityEngine;
using UnityEngine.Rendering;

namespace LaundryMonster
{
    /// <summary>
    /// A primitive mesh object with no collider.
    ///
    /// GameObject.CreatePrimitive always attaches a collider, and every runtime caller
    /// in this game then destroyed it - nothing here uses physics, interaction is by
    /// distance. In the editor that is only wasted work. In the WebGL build it is an
    /// error on every call: engine code stripping removes the physics module because
    /// nothing in the scene needs it, so the collider class does not exist and
    /// CreatePrimitive logs "Can't add component because class 'SphereCollider'
    /// doesn't exist" - twenty-one times on the title screen alone, invisible to every
    /// test because the tests run in the editor, where physics is always present.
    ///
    /// This builds the same thing from the built-in mesh and never asks for a collider.
    /// The built-in meshes are available at runtime (the HUD already fetches its font
    /// the same way), and the material matches what CreatePrimitive would have used.
    /// </summary>
    public static class Prim
    {
        static Material _default;

        public static GameObject Make(PrimitiveType type, string name = null)
        {
            var go = new GameObject(name ?? type.ToString());
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = DefaultMaterial();
            return go;
        }

        public static Mesh Mesh(PrimitiveType type)
        {
            string file = type switch
            {
                PrimitiveType.Sphere => "Sphere.fbx",
                PrimitiveType.Capsule => "Capsule.fbx",
                PrimitiveType.Cylinder => "Cylinder.fbx",
                PrimitiveType.Cube => "Cube.fbx",
                PrimitiveType.Plane => "Plane.fbx",
                _ => "Quad.fbx",
            };
            return Resources.GetBuiltinResource<Mesh>(file);
        }

        /// <summary>
        /// What CreatePrimitive would have assigned: the pipeline's default material.
        /// Every caller in this game overrides it, but a drop-in replacement should not
        /// leave a magenta surprise for the one that forgets.
        /// </summary>
        static Material DefaultMaterial()
        {
            if (_default != null) return _default;
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null) _default = rp.defaultMaterial;
            return _default;
        }
    }
}
