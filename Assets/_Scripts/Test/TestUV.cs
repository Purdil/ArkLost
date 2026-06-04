using System;
using UnityEngine;

namespace Test
{
    public class TestUV : MonoBehaviour
    {
        private MeshFilter _meshFilter;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
        }

        private void Start()
        {
            Mesh mesh = new Mesh();
            
            Vector3[] vertices = new Vector3[4];
            Vector2[] uv = new Vector2[4];
            int[] triangles = new int[6];
            
            vertices[0] = Vector3.zero;
            vertices[1] = new Vector3(0, 2);
            vertices[2] = new Vector3(2, 2);
            vertices[3] = new Vector3(2, 0);
            
            triangles[0] = 1;
            triangles[1] = 2;
            triangles[2] = 0;
            triangles[3] = 0;
            triangles[4] = 2;
            triangles[5] = 3;
            
            uv[0] = new Vector2(0, 0.5f);
            uv[1] = new Vector2(0, 1f);
            uv[2] = new Vector2(0.5f, 1f);
            uv[3] = new Vector2(0.5f, 0.5f);
            
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            
            _meshFilter.mesh = mesh; //새로만든 메시를 필터에 넣어준다.
        }
    }
}