using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Cutting.Data
{
    public struct GeneratedMesh : IDisposable
    {
        public NativeList<float3> vertices;
        public NativeList<float3> normals;
        public NativeList<float2> uvs;
        public readonly List<NativeList<int>> triangles;

        public GeneratedMesh(int subMeshCount)
        {
            vertices = new NativeList<float3>(Allocator.Persistent);
            normals = new NativeList<float3>(Allocator.Persistent);
            uvs = new NativeList<float2>(Allocator.Persistent);
            triangles = new List<NativeList<int>>(subMeshCount + 1);
            for (var i = 0; i < subMeshCount + 1; i++)
            {
                triangles.Add(new NativeList<int>(Allocator.Persistent));
            }
        }

        public void ResizeVertices(int length)
        {
            if (length == 0)
                return;
            
            vertices.Resize(length, NativeArrayOptions.UninitializedMemory);
            normals.Resize(length, NativeArrayOptions.UninitializedMemory);
            uvs.Resize(length, NativeArrayOptions.UninitializedMemory);
        }

        public void ResizeTriangles(int submeshIndex, int length)
        {
            if (length == 0)
                return;
            
            triangles[submeshIndex].Resize(length, NativeArrayOptions.UninitializedMemory);
        }

        public void Dispose()
        {
            vertices.Dispose();
            normals.Dispose();
            uvs.Dispose();
            for (var i = 0; i < triangles.Count; i++)
            {
                triangles[i].Dispose();
            }
        }

        public Mesh GetMesh()
        {
            var mesh = new Mesh();
            
            CheckSubMeshes();

            //set 32bit index format if needed
            if (vertices.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            
            mesh.SetVertices(vertices.AsArray());
            mesh.SetNormals(normals.AsArray());
            mesh.SetUVs(0, uvs.AsArray());
            
            mesh.subMeshCount = triangles.Count;

            //set triangles
            for (var i = 0; i < triangles.Count; i++)
            {
                mesh.SetIndices(triangles[i].AsArray(), MeshTopology.Triangles, i);
            }

            return mesh;
        }

        private void CheckSubMeshes()
        {
            for (var i = 0; i < triangles.Count; i++)
            {
                if (triangles[i].Length == 0)
                {
                    triangles.RemoveAt(i--);
                }
            }
        }
    }
}
