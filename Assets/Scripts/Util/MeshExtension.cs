using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace GrassShader.Util {

    public static class MeshExension {

        public static (Vector3 a, Vector3 b, Vector3 c)[] Triangles(this Mesh mesh) {
            var tris = new ConcurrentStack<(Vector3, Vector3, Vector3)>();

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            Parallel.For(0, triangles.Length / 3, (i) => {
                tris.Push(vertices.Triangle(triangles, i * 3));
            });

            return tris.ToArray();
        }

        public static (Vector3 a, Vector3 b, Vector3 c) Triangle(this Vector3[] vertices, int[] triangles, int i) {
            i -= i % 3;
            return (vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
        }

        public static Vector3 Middle(this (Vector3 a, Vector3 b, Vector3 c) trangle) {
            return (trangle.a + trangle.b + trangle.c) / 3;
        }

        public static Vector3 Middle(this (Vector3 a, Vector3 b, Vector3 c) trangle, (float a, float b, float c) rand) {
            return (trangle.a * rand.a + trangle.b * rand.b + trangle.c * rand.c) / (rand.a + rand.b + rand.c);
        }

        public static Vector3 Normal(this (Vector3 a, Vector3 b, Vector3 c) trangle) {
            var side1 = trangle.b - trangle.a;
            var side2 = trangle.c - trangle.a;

            return Normal(side1, side2);
        }

        public static Vector3 Normal(Vector3 a, Vector3 b, Vector3 c) {
            var side1 = b - a;
            var side2 = c - a;

            return Normal(side1, side2);
        }


        public static Vector3 Normal(Vector3 side1, Vector3 side2) {
            return Vector3.Cross(side1, side2).normalized;
        }

        public static (Vector3 middle, Vector3 oposite, Vector3 first, Vector3 second) MiddleLargestSide(this (Vector3 a, Vector3 b, Vector3 c) triangle) {
            var mag1 = (triangle.b - triangle.a).sqrMagnitude;
            var mag2 = (triangle.c - triangle.a).sqrMagnitude;
            var mag3 = (triangle.c - triangle.b).sqrMagnitude;

            if (mag1 > mag2 && mag1 > mag3)
                return ((triangle.b + triangle.a) / 2, triangle.c, triangle.a, triangle.b);

            if (mag2 > mag3)
                return ((triangle.c + triangle.a) / 2, triangle.b, triangle.c, triangle.a);

            return ((triangle.c + triangle.b) / 2, triangle.a, triangle.c, triangle.b);
        }

        public static Vector4 ToVector(this Quaternion q) {
            return new Vector4(q.x, q.y, q.z, q.w);
        }

        public static (Vector3 a, Vector3 b, Vector3 c)[] TransformTriangles(this (Vector3 a, Vector3 b, Vector3 c)[] tris, Transform t) {
            for (int i = 0; i < tris.Length; i++) {
                var tri = tris[i];
                tri.a = Vector3.Scale(t.rotation * tri.a, t.lossyScale) + t.position;
                tri.b = Vector3.Scale(t.rotation * tri.a, t.lossyScale) + t.position;
                tri.c = Vector3.Scale(t.rotation * tri.a, t.lossyScale) + t.position;
            }

            return tris;
        }

        public static (Vector3 a, Vector3 b, Vector3 c) TransformTriangle(this (Vector3 a, Vector3 b, Vector3 c) tri, (Vector3 pos, Quaternion rot, Vector3 scale) t) {
            tri.a = Vector3.Scale(t.rot * tri.a, t.scale) + t.pos;
            tri.b = Vector3.Scale(t.rot * tri.b, t.scale) + t.pos;
            tri.c = Vector3.Scale(t.rot * tri.c, t.scale) + t.pos;

            return tri;
        }

        public static GrassMeshGPUv2.Triangle[] TransformTriangles(this GrassMeshGPUv2.Triangle[] tris, Transform t) {
            for (int i = 0; i < tris.Length; i++) {
                var tri = tris[i];
                tri.a = Vector3.Scale(t.rotation * tri.a, t.lossyScale) + t.position;
                tri.b = Vector3.Scale(t.rotation * tri.a, t.lossyScale) + t.position;
                tri.c = Vector3.Scale(t.rotation * tri.a, t.lossyScale) + t.position;
            }

            return tris;
        }
    }
}