using System;
using System.Collections.Generic;
using UnityEngine;
using ProceduralToolkit;
using GrassShader.Util;

namespace GrassShader {

    [ExecuteInEditMode]
    public class GrassMesh : MonoBehaviour {

        [SerializeField] private Mesh baseMesh;
        [SerializeField] private Material material;

        [SerializeField] private float randWidth = 0.01f;
        [SerializeField] private float randHeight = 0.01f;
        [SerializeField] private float maxWidth = 0.01f;
        [SerializeField] private float maxHeight = 0.1f;
        [SerializeField] private float randLeanDist = 0.1f;
        [SerializeField] private float leanDist = 0.5f;

        // LOD control
        [SerializeField] [Range(0, 5)] private int maxSplits = 1;
        [SerializeField] [Range(0, 50f)] private float minDist = 5;
        [SerializeField] [Range(0, 100f)] private float maxDist = 20;

        public Mesh Grass { get; private set; }

        void Start() {
            if (baseMesh == null)
                baseMesh = GetComponent<MeshFilter>().sharedMesh;

#if UNITY_EDITOR
            var cam = Camera.current;
            if (cam == null || Application.isPlaying)
                cam = Camera.main;
#else
            var cam = Camera.main;
#endif

            var draft = new MeshDraft();

            var rand = new System.Random(0);

            // expend the number of triangles
            var triangles = baseMesh.Triangles();

            var dist = Vector3.Distance(cam.transform.position, transform.position);
            var lod = (1f - ((Mathf.Clamp(dist, minDist, maxDist)) - minDist) / (maxDist - minDist));
            var numSplits = lod * maxSplits;
            var height = lod * maxHeight;

#if DEBUG
            Debug.Log(dist + " : " + numSplits);
#endif

            for (int i = 0; i < numSplits; i++) {

                var tmpTriangles = new List<(Vector3 a, Vector3 b, Vector3 c)>();

                foreach (var tri in triangles) {
                    var (middle, oposite, first, second) = tri.MiddleLargestSide();
                    tmpTriangles.Add((oposite, first, middle));
                    tmpTriangles.Add((oposite, middle, second));
                }

                triangles = tmpTriangles.ToArray();
            }

            foreach (var tri in triangles) {
                var middle = tri.Middle(((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble()));
                var normal = tri.Normal();
                var sideDir = (tri.Item1 - middle).normalized;
                var leanDir = MeshExension.Normal(sideDir, normal);

                var rot = Quaternion.RotateTowards(Quaternion.LookRotation(sideDir, normal), Quaternion.LookRotation(sideDir, normal), (float)rand.NextDouble());

                sideDir = rot * sideDir;
                leanDir = rot * leanDir;

                var a = middle + sideDir * (maxWidth + ((float)rand.NextDouble() - 0.5f) * randWidth);
                var b = middle - sideDir * (maxWidth + ((float)rand.NextDouble() - 0.5f) * randWidth);
                var c = middle + (normal + leanDir * (leanDist + ((float)rand.NextDouble() - 0.5f) * randLeanDist)).normalized * (maxHeight + ((float)rand.NextDouble() - 0.5f) * randHeight);

                //normal = MeshExension.Normal(a, b, c);
                //if (Vector3.Dot(normal, cam.transform.forward) < 0)
                //    normal = -normal;
                //normal = -Camera.main.transform.forward;
                //normal = (Camera.main.transform.position - middle).normalized;

                draft.AddTriangle(a, b, c, -leanDir, -leanDir, normal);
            }

            Grass = draft.ToMesh();
        }

        private void Update() {
            Start();
            //var baseMesh = GetComponent<MeshFilter>().sharedMesh;

            //foreach (var tri in baseMesh.Triangles()) {
            //    var middle = MeshExension.Middle(tri.Item1, tri.Item2, tri.Item3);
            //    var normal = MeshExension.Normal(tri.Item1, tri.Item2, tri.Item3);
            //    var a = middle + tri.Item1.normalized * width;
            //    var b = middle - tri.Item1.normalized * width;
            //    var c = middle + normal * height;

            //    Debug.DrawLine(a, b);
            //    Debug.DrawLine(b, c);
            //    Debug.DrawLine(c, a);
            //}

            Graphics.DrawMesh(Grass, transform.position, transform.rotation, material, 0);
        }
    }
}