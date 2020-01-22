// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Cone/BufferShader" {

    Properties {
      _Color ("Color", Color) = (0.26,0.19,0.16,0.0)
    }

    SubShader
    {
       Pass
       {
            ZTest Always
            ZWrite On

            Fog { Mode off }
            CGPROGRAM
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc" // for _LightColor0
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag

            uniform StructuredBuffer<float3> buffer;
            uniform StructuredBuffer<float3> normalBuffer;

            half4 _Color;
            
             struct appdata
             {
                uint id : SV_VertexID;
                float4 vertex : POSITION;
                float3 normal : NORMAL; 
                float4 texcoord : TEXCOORD0;
                float4 tangent : TANGENT;
             };

            struct v2f
            {
                half3 worldRefl : TEXCOORD0;
                fixed4 diff : COLOR0;
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                float4 pos = float4(buffer[v.id], 1);
                float4 normal = float4(normalBuffer[v.id], 1);

                v2f o;
                o.pos = UnityObjectToClipPos(pos);
                
                half3 worldNormal = UnityObjectToWorldNormal(normal);
                half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
                o.diff = nl * _LightColor0;

                // the only difference from previous shader:
                // in addition to the diffuse lighting from the main light,
                // add illumination from ambient or light probes
                // ShadeSH9 function from UnityCG.cginc evaluates it,
                // using world space normal
                o.diff.rgb += ShadeSH9(half4(worldNormal,1));
                return o;
            }

            float4 frag(v2f IN) : COLOR
            {
                return _Color * IN.diff;
                // return _Color;
            }
            ENDCG
        }
    }
}