using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Cutting.Data
{
    public struct GeneratedMesh
    {
        public NativeList<float3> vertices;
        public NativeList<float3> normals;
        public NativeList<float2> uvs;
        public readonly NativeList<int>[] triangles;

        public GeneratedMesh(int subMeshCount)
        {
            vertices = new NativeList<float3>(Allocator.Persistent);
            normals = new NativeList<float3>(Allocator.Persistent);
            uvs = new NativeList<float2>(Allocator.Persistent);
            triangles = new NativeList<int>[subMeshCount + 1];
            for (var i = 0; i < subMeshCount + 1; i++)
            {
                triangles[i] = new NativeList<int>(Allocator.Persistent);
            }
        }

        public void Dispose()
        {
            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
            for (var i = 0; i < triangles.Length; i++)
            {
                triangles[i].Dispose();
            }
        }

        public Mesh GetMesh()
        {
            var mesh = new Mesh();

            //set 32bit index format if needed
            if (vertices.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            var meshVertices = new Vector3[vertices.Length];
            var meshNormals = new Vector3[normals.Length];
            var meshUVs = new Vector2[uvs.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                meshVertices[i] = new Vector3(vertices[i].x, vertices[i].y, vertices[i].z);
                meshNormals[i] = new Vector3(normals[i].x, normals[i].y, normals[i].z);
                meshUVs[i] = new Vector2(uvs[i].x, uvs[i].y);
            }

            mesh.vertices = meshVertices;
            mesh.normals = meshNormals;
            mesh.uv = meshUVs;
            mesh.subMeshCount = triangles.Length;
    
            //set triangles
            for (var i = 0; i < triangles.Length; i++)
            {
                mesh.SetTriangles(triangles[i].ToArray(), i);
            }

            return mesh;
        }
    }
}
