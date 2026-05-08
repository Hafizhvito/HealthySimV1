using System.IO;

using UnityEngine;

namespace Tripolygon.UModelerX.Editor.Importers.GLTF
{
    internal sealed class UMXGltfNodeBuilder
    {
        private readonly UMXGltfBuiltMesh[] builtMeshes;
        private readonly UMXGltfLoadedDocument document;

        public UMXGltfNodeBuilder(UMXGltfLoadedDocument loadedDocument, UMXGltfBuiltMesh[] meshes)
        {
            document = loadedDocument;
            builtMeshes = meshes;
        }

        public GameObject BuildSceneRoot()
        {
            var rootName = Path.GetFileNameWithoutExtension(document.AssetPath);
            var rootObject = new GameObject(string.IsNullOrEmpty(rootName) ? "GLTF_Root" : rootName);

            var sceneIndex = ResolveSceneIndex();
            if (document.Root.scenes == null || sceneIndex < 0 || sceneIndex >= document.Root.scenes.Length)
            {
                return rootObject;
            }

            var scene = document.Root.scenes[sceneIndex];
            if (scene?.nodes == null)
            {
                return rootObject;
            }

            for (var i = 0; i < scene.nodes.Length; ++i)
            {
                var nodeIndex = scene.nodes[i];
                BuildNodeRecursive(nodeIndex, rootObject.transform);
            }

            return rootObject;
        }

        private int ResolveSceneIndex()
        {
            if (document.Root.scene >= 0)
            {
                return document.Root.scene;
            }

            return document.Root.scenes != null && document.Root.scenes.Length > 0 ? 0 : -1;
        }

        private GameObject BuildNodeRecursive(int nodeIndex, Transform parent)
        {
            var node = document.Root.nodes[nodeIndex];
            var nodeName = string.IsNullOrEmpty(node?.name) ? $"GLTF_Node_{nodeIndex}" : node.name;
            var nodeObject = new GameObject(nodeName);
            nodeObject.transform.SetParent(parent, false);

            if (node != null)
            {
                ApplyNodeTransform(nodeObject.transform, node);

                if (node.mesh >= 0)
                {
                    AttachMesh(nodeObject, node.mesh);
                }

                if (node.children != null)
                {
                    for (var i = 0; i < node.children.Length; ++i)
                    {
                        BuildNodeRecursive(node.children[i], nodeObject.transform);
                    }
                }
            }

            return nodeObject;
        }

        /// <summary>
        /// glTF 노드의 변환은 matrix 또는 TRS 둘 중 하나로 표현된다(spec). matrix 가 있으면
        /// TRS 로 분해해 좌표계 변환 함수에 흘려 보낸다 — 이 단계에서 분해하면 이후 처리 경로가
        /// translation/rotation/scale 케이스와 동일해져 좌표계 변환(handedness, axis flip)이 일관된다.
        /// </summary>
        private static void ApplyNodeTransform(Transform target, UMXGltfNode node)
        {
            if (node.matrix != null && node.matrix.Length == 16)
            {
                DecomposeGltfMatrix(node.matrix, out var translation, out var rotation, out var scale);
                target.localPosition = UMXGltfCoordinateUtility.ToUnityTranslation(translation);
                target.localRotation = UMXGltfCoordinateUtility.ToUnityRotation(rotation);
                target.localScale = UMXGltfCoordinateUtility.ToUnityScale(scale);
                return;
            }

            target.localPosition = UMXGltfCoordinateUtility.ToUnityTranslation(node.translation);
            target.localRotation = UMXGltfCoordinateUtility.ToUnityRotation(node.rotation);
            target.localScale = UMXGltfCoordinateUtility.ToUnityScale(node.scale);
        }

        /// <summary>
        /// glTF 의 column-major 4x4 matrix 를 (translation, rotation, scale) 로 분해한다.
        /// 결과는 glTF 좌표계 그대로의 float 배열 — 이후 ToUnity{...} 헬퍼가 좌표계 변환을 담당.
        /// 음의 scale(반사)은 column 0 의 부호로 보존(Matrix4x4 가 음수 스케일을 정상 처리).
        /// </summary>
        private static void DecomposeGltfMatrix(float[] m, out float[] translation, out float[] rotation, out float[] scale)
        {
            // column-major: m[col*4 + row]
            translation = new[] { m[12], m[13], m[14] };

            var col0 = new Vector3(m[0], m[1], m[2]);
            var col1 = new Vector3(m[4], m[5], m[6]);
            var col2 = new Vector3(m[8], m[9], m[10]);

            var sx = col0.magnitude;
            var sy = col1.magnitude;
            var sz = col2.magnitude;

            // 좌수계 반사: determinant 가 음수면 한 축에 음수 스케일을 부여해 회전이 정상 분리되도록.
            var det =
                col0.x * (col1.y * col2.z - col1.z * col2.y) -
                col0.y * (col1.x * col2.z - col1.z * col2.x) +
                col0.z * (col1.x * col2.y - col1.y * col2.x);
            if (det < 0f)
            {
                sx = -sx;
            }

            scale = new[] { sx, sy, sz };

            if (Mathf.Abs(sx) < Mathf.Epsilon || Mathf.Abs(sy) < Mathf.Epsilon || Mathf.Abs(sz) < Mathf.Epsilon)
            {
                rotation = new[] { 0f, 0f, 0f, 1f };
                return;
            }

            // 회전 matrix = scale 로 normalize 한 column 0/1/2.
            var matrix = new Matrix4x4();
            matrix.SetColumn(0, new Vector4(col0.x / sx, col0.y / sx, col0.z / sx, 0f));
            matrix.SetColumn(1, new Vector4(col1.x / sy, col1.y / sy, col1.z / sy, 0f));
            matrix.SetColumn(2, new Vector4(col2.x / sz, col2.y / sz, col2.z / sz, 0f));
            matrix.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));
            var q = matrix.rotation;
            rotation = new[] { q.x, q.y, q.z, q.w };
        }

        private void AttachMesh(GameObject nodeObject, int meshIndex)
        {
            if (meshIndex < 0 || meshIndex >= builtMeshes.Length)
            {
                return;
            }

            var builtMesh = builtMeshes[meshIndex];
            if (builtMesh == null || builtMesh.Primitives == null || builtMesh.Primitives.Count == 0)
            {
                return;
            }

            if (builtMesh.Primitives.Count == 1)
            {
                AttachPrimitive(nodeObject, builtMesh.Primitives[0]);
                return;
            }

            for (var i = 0; i < builtMesh.Primitives.Count; ++i)
            {
                var primitiveObject = new GameObject($"{builtMesh.Name}_Primitive_{i}");
                primitiveObject.transform.SetParent(nodeObject.transform, false);
                AttachPrimitive(primitiveObject, builtMesh.Primitives[i]);
            }
        }

        private static void AttachPrimitive(GameObject target, UMXGltfBuiltPrimitive primitive)
        {
            var meshFilter = target.AddComponent<MeshFilter>();
            var meshRenderer = target.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = primitive.Mesh;
            meshRenderer.sharedMaterial = primitive.Material;
        }
    }
}
