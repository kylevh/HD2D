using UnityEngine;

namespace KVH.Game.Editor
{
    // disc + box-with-circle-hole meshes for round ponds.
    public static class PondGeometry
    {
        public static Mesh BuildDisc(int segments = 32)
        {
            segments = Mathf.Clamp(segments, 8, 64);
            var mesh = new Mesh { name = "PondDisc" };
            var verts = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            // disc fan — CCW from +Y
            for (var i = 0; i < segments; i++)
            {
                var a = (i / (float)segments) * Mathf.PI * 2f;
                var x = Mathf.Cos(a);
                var z = Mathf.Sin(a);
                verts[i + 1] = new Vector3(x, 0f, z);
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);

                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 2 <= segments ? i + 2 : 1;
                tris[i * 3 + 2] = i + 1;
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        // outer square ±outerHalf, circular hole radius holeRadius.
        public static Mesh BuildBoxWithCircleHole(float outerHalf, float holeRadius, int segments = 24)
        {
            segments = Mathf.Clamp(segments, 12, 48);
            holeRadius = Mathf.Min(holeRadius, outerHalf * 0.95f);

            var verts = new System.Collections.Generic.List<Vector3>(segments * 2);
            var uvs = new System.Collections.Generic.List<Vector2>(segments * 2);
            var tris = new System.Collections.Generic.List<int>(segments * 6);

            for (var i = 0; i < segments; i++)
            {
                var a = (i / (float)segments) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var outer = PointOnSquare(dir, outerHalf);
                var inner = dir * holeRadius;
                verts.Add(new Vector3(outer.x, 0f, outer.y));
                verts.Add(new Vector3(inner.x, 0f, inner.y));
                uvs.Add(new Vector2(outer.x / (outerHalf * 2f) + 0.5f, outer.y / (outerHalf * 2f) + 0.5f));
                uvs.Add(new Vector2(inner.x / (outerHalf * 2f) + 0.5f, inner.y / (outerHalf * 2f) + 0.5f));
            }

            for (var i = 0; i < segments; i++)
            {
                var i0 = i * 2;
                var i1 = i0 + 1;
                var j0 = ((i + 1) % segments) * 2;
                var j1 = j0 + 1;
                // winding so RecalculateNormals points +Y
                tris.Add(i0);
                tris.Add(j1);
                tris.Add(j0);
                tris.Add(i0);
                tris.Add(i1);
                tris.Add(j1);
            }

            var mesh = new Mesh { name = "GrassRingCircleHole" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector2 PointOnSquare(Vector2 dir, float half)
        {
            var ax = Mathf.Abs(dir.x);
            var ay = Mathf.Abs(dir.y);
            if (ax < 1e-5f && ay < 1e-5f)
                return new Vector2(half, 0f);
            var t = half / Mathf.Max(ax, ay);
            return dir * t;
        }

        // radius in world units; disc mesh is unit radius then scaled.
        public static GameObject MakeFlatDisc(
            string name,
            Transform parent,
            float radius,
            float y,
            Material mat,
            bool shadows,
            Mesh discMesh = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(radius, 1f, radius);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = discMesh != null ? discMesh : BuildDisc(32);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = shadows;
            return go;
        }

        // thin cylinder disc for basin steps (has volume for depth foam).
        public static GameObject MakeBasinStep(
            string name,
            Transform parent,
            float radius,
            float y,
            float height,
            Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            // default cylinder r=0.5 h=2
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
                mr.sharedMaterial = mat;
            return go;
        }
    }
}
