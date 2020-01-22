using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using ProceduralToolkit;
using GrassShader.Util;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace GrassShader {

    [ExecuteInEditMode]
    public class GrassMeshGPU : MonoBehaviour {

        [SerializeField] bool debug;

        [SerializeField] private ComputeShader shader;
        [SerializeField] private Mesh baseMesh;
        [SerializeField] private Material material;

        [SerializeField] private float randWidth = 0.01f;
        [SerializeField] private float randHeight = 0.01f;
        [SerializeField] private float randLeanDist = 0.1f;
        [SerializeField] private float maxWidth = 0.01f;
        [SerializeField] private float maxHeight = 0.1f;
        [SerializeField] private float leanDist = 0.5f;

        // LOD control
        [SerializeField] [Range(0, 5)] private int maxSplits = 1;
        [SerializeField] [Range(0, 50f)] private float minDist = 5;
        [SerializeField] [Range(0, 100f)] private float maxDist = 20;

        public Mesh Grass { get; private set; }

        private ComputeBuffer buffer;
        private ComputeBuffer outputBuffer;
        private ComputeBuffer normalBuffer;
        
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

            // expend the number normalBufferof triangles
            var triangles = baseMesh.Triangles();

            var dist = Vector3.Distance(cam.transform.position, transform.position);
            var lod = (1f - ((Mathf.Clamp(dist, minDist, maxDist)) - minDist) / (maxDist - minDist));
            var numSplits = lod * maxSplits;
            var height = lod * maxHeight;

#if DEBUG
            Debug.Log(dist + " : " + numSplits);
            var startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
#endif

            // Kernel One
            var eye = cam.transform.forward;
            for (int i = 0; i < numSplits; i++) {
                var tmpTriangles = new ConcurrentStack< (Vector3 a, Vector3 b, Vector3 c)>();

                Parallel.ForEach(triangles, tri => {
                    if (Vector3.Dot(tri.Normal(), eye) < 0) {
                        var (middle, oposite, first, second) = tri.MiddleLargestSide();
                        tmpTriangles.Push((oposite, first, middle));
                        tmpTriangles.Push((oposite, middle, second));
                    }
                });

                triangles = tmpTriangles.ToArray();
            }

#if DEBUG
            var endTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Debug.Log("Kernel One (CPU) : " + (endTime - startTime));

            startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
#endif

            // Kernel Two
            buffer = new ComputeBuffer(triangles.Length, 36);
            buffer.SetData(ToGPUTriangle(triangles));

            outputBuffer = new ComputeBuffer(triangles.Length * 3, 12);
            normalBuffer = new ComputeBuffer(triangles.Length * 3, 12);

            int kernel = shader.FindKernel("CSMain");
            shader.SetVector("position", transform.position);
            shader.SetFloat("randWidth", randWidth);
            shader.SetFloat("randHeight", randHeight);
            shader.SetFloat("randLeanDist", randLeanDist);
            shader.SetFloat("maxWidth", maxWidth);
            shader.SetFloat("maxHeight", maxHeight);
            shader.SetFloat("leanDist", leanDist);
            shader.SetBuffer(kernel, "dataBuffer", buffer);
            shader.SetBuffer(kernel, "outputBuffer", outputBuffer);
            shader.SetBuffer(kernel, "normalBuffer", normalBuffer);
            shader.Dispatch(kernel, triangles.Length, 1, 1);

#if DEBUG
            endTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            Debug.Log("Kernel Two (GPU) : " + (endTime - startTime));
#endif

            //var array = new Vector3[triangles.Length * 3];
            //outputBuffer.GetData(array);
            //Debug.Log(string.Join(",", array));
            //Debug.Log(string.Join(",", triangles));

            //Grass = draft.ToMesh();
        }

        private void Update() {
            var array = new Vector3[outputBuffer.count];
            outputBuffer.GetData(array);
            var normalArray = new Vector3[normalBuffer.count];
            normalBuffer.GetData(normalArray);

#if DEBUG
            if (debug) {
                //var array = new Vector3[outputBuffer.count];
                //outputBuffer.GetData(array);

                //array = array.Select(v => Vector3.Scale(transform.rotation * v, transform.localScale) + transform.position).ToArray();

                for (int i = 0; i < array.Length; i += 3) {
                    Debug.DrawLine(array[i], array[i + 2]);
                    Debug.DrawLine(array[i + 2], array[i + 1]);
                    Debug.DrawLine(array[i + 1], array[i]);
                }
            }
#endif

            Mesh mesh = new Mesh();
            mesh.vertices = array;
            Debug.Log(array.Length);
            mesh.triangles = Enumerable.Range(0, array.Length).ToArray();
            mesh.normals = normalArray;
            //Graphics.DrawMesh(mesh, Vector3.zero, Quaternion.identity, material, 0);

            material.SetPass(0);
            material.SetBuffer("buffer", outputBuffer);
            material.SetBuffer("normalBuffer", normalBuffer);
            Graphics.DrawProcedural(
                material,
                new Bounds(transform.position, transform.lossyScale * 5),
                MeshTopology.Triangles, outputBuffer.count, 1,
                null, null,
                ShadowCastingMode.TwoSided, true, gameObject.layer
            );
        }

        private void OnDestroy() {
            buffer?.Dispose();
            outputBuffer?.Dispose();
        }

        Triangle[] ToGPUTriangle((Vector3 a, Vector3 b, Vector3 c)[] tris) {
            return tris.Select(tri => new Triangle(tri)).ToArray();
        }

        struct Triangle {
            Vector3 a;
            Vector3 b;
            Vector3 c;

            public Triangle(Vector3 a, Vector3 b, Vector3 c) {
                this.a = a;
                this.b = b;
                this.c = c;
            }

            public Triangle((Vector3 a, Vector3 b, Vector3 c) tri) {
                a = tri.a;
                b = tri.b;
                c = tri.c;
            }
        }
    }
}