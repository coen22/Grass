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
    public class GrassMeshGPUv2 : MonoBehaviour {

        [SerializeField] bool debugOne;
        [SerializeField] bool debugTwo;

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

        private (Vector3 a, Vector3 b, Vector3 c)[] triangles;
        private Camera cam;
        private ComputeBuffer buffer;
        private ComputeBuffer extraBuffer;
        private ComputeBuffer outputBuffer;
        private ComputeBuffer normalBuffer;

        void Start() {
            if (baseMesh == null)
                baseMesh = GetComponent<MeshFilter>().sharedMesh;

#if UNITY_EDITOR
            cam = Camera.current;
            if (cam == null || Application.isPlaying)
                cam = Camera.main;
#else
            cam = Camera.main;
#endif
        }

        private void Update() {
#if DEBUG
            var startTime = DateTime.Now.Ticks; // TimeSpan.TicksPerMillisecond;
#endif

            // expend the number normalBufferof triangles
            triangles = baseMesh.Triangles();

            var dist = Vector3.Distance(cam.transform.position, transform.position);
            var lod = (1f - ((Mathf.Clamp(dist, minDist, maxDist)) - minDist) / (maxDist - minDist));
            int numSplits = (int)(lod * maxSplits);
            var height = lod * maxHeight;

            if (buffer != null)
                buffer.Dispose();

            var eye = cam.transform.forward;
            var trans = (transform.position, transform.rotation, transform.lossyScale);
            triangles = triangles.Select(tri => tri.TransformTriangle(trans))
                .Where(tri => Vector3.Dot(tri.Normal(), eye) < 0).ToArray();

#if DEBUG
            var endTime = DateTime.Now.Ticks; // TimeSpan.TicksPerMillisecond;
            Debug.Log("Setup (CPU) : " + (endTime - startTime));
#endif

#if DEBUG
            //Debug.Log(dist + " : " + numSplits);
            startTime = DateTime.Now.Ticks; // TimeSpan.TicksPerMillisecond;
#endif

            // Kernel One
            int totalSize = (int)(triangles.Length * 3 * Mathf.Pow(2, numSplits));
            buffer = new ComputeBuffer(totalSize, 36);
            buffer.SetData(ToGPUTriangle(triangles));
            int splitKernel = shader.FindKernel("CSSplit");
            shader.SetBuffer(splitKernel, "dataBuffer", buffer);

            int size = triangles.Length;
            for (int i = 0; i < numSplits; i++) {
                size = (int)(triangles.Length * 3 * Mathf.Pow(2, i));

//#if DEBUG
//                Debug.Log(gameObject.name + ": " + (i + 1) + "/" + numSplits + " : " + size * 2 + "/" + totalSize);
//#endif

                shader.SetInt("bufferSize", size);
                shader.Dispatch(splitKernel, size, 1, 1);
            }

#if DEBUG
            endTime = DateTime.Now.Ticks; // TimeSpan.TicksPerMillisecond;
            Debug.Log("Kernel One (GPU) : " + (endTime - startTime));

            if (debugOne) {
                var arrayOne = new Vector3[size];
                buffer.GetData(arrayOne);

                // TODO do this on the GPU
                arrayOne = arrayOne.Select(v => Vector3.Scale(transform.rotation * v, transform.localScale) + transform.position).ToArray();

                for (int i = 0; i < arrayOne.Length; i += 3) {
                    Debug.DrawLine(arrayOne[i], arrayOne[i + 2]);
                    Debug.DrawLine(arrayOne[i + 2], arrayOne[i + 1]);
                    Debug.DrawLine(arrayOne[i + 1], arrayOne[i]);
                }

                return;
            }
#endif

#if DEBUG
            startTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
#endif

            // Kernel Two
            if (outputBuffer?.count != buffer.count * 3) {
                outputBuffer?.Dispose();
                normalBuffer?.Dispose();
                outputBuffer = new ComputeBuffer(buffer.count * 3, 12);
                normalBuffer = new ComputeBuffer(buffer.count * 3, 12);
            }

            int kernel = shader.FindKernel("CSMain");
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


            var array = new Vector3[outputBuffer.count];
            outputBuffer.GetData(array);
            var normalArray = new Vector3[normalBuffer.count];
            normalBuffer.GetData(normalArray);

#if DEBUG
            if (debugTwo) {
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
            mesh.triangles = Enumerable.Range(0, array.Length).ToArray();
            mesh.normals = normalArray;
            Graphics.DrawMesh(mesh, Vector3.zero, Quaternion.identity, material, 0);

            //material.SetPass(0);
            //material.SetBuffer("buffer", outputBuffer);
            //material.SetBuffer("normalBuffer", normalBuffer);
            //Graphics.DrawProcedural(
            //    material,
            //    new Bounds(transform.position, transform.lossyScale * 5),
            //    MeshTopology.Triangles, outputBuffer.count, 1,
            //    null, null,
            //    ShadowCastingMode.TwoSided, true, gameObject.layer
            //);
        }

        private void OnDestroy() {
            buffer?.Dispose();
            outputBuffer?.Dispose();
        }

        Triangle[] ToGPUTriangle((Vector3 a, Vector3 b, Vector3 c)[] tris) {
            return tris.Select(tri => new Triangle(tri)).ToArray();
        }

        (Vector3 a, Vector3 b, Vector3 c)[] FromGPUTriangle(Triangle[] tris) {
            return tris.Select(tri => (tri.a, tri.b, tri.c)).ToArray();
        }

        public struct Triangle {
            internal Vector3 a;
            internal Vector3 b;
            internal Vector3 c;

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
