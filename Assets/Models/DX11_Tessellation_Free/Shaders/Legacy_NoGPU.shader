﻿Shader "Tessellation/Legacy NoGPU" {
        Properties {
            _MainTex ("Base (RGB)", 2D) = "white" {}
            _DispTex ("Disp Texture", 2D) = "gray" {}
            _NormalMap ("Normalmap", 2D) = "bump" {}
            _Displacement ("Displacement", Range(0, 1.0)) = 0.3
            _DispOffset ("Disp Offset", Range(0, 1)) = 0.5
            _Color ("Color", color) = (1,1,1,0)
            _SpecColor ("Spec color", color) = (0.5,0.5,0.5,0.5)
            _SpecPow ("Specularity", Range(0, 1)) = 0.5
            _GlossPow ("Glossiness", Range(0, 1)) = 0.5
        }
        SubShader {
            Tags { "RenderType"="Opaque" }
            LOD 300
            
            CGPROGRAM
            #pragma surface surf BlinnPhong addshadow fullforwardshadows vertex:disp
            #pragma target 3.0

            struct appdata {
                float4 vertex : POSITION;
                float4 tangent : TANGENT;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };

            sampler2D _DispTex;
            uniform float4 _DispTex_ST;
            float _Displacement;
            float _DispOffset;

            void disp (inout appdata v)
            {
                float d = tex2Dlod(_DispTex, float4(v.texcoord.xy * _DispTex_ST.xy + _DispTex_ST.zw,0,0)).r * _Displacement;
                d = d * 0.5 - 0.5 +_DispOffset;
                v.vertex.xyz += v.normal * d;
            }

            struct Input {
                float2 uv_MainTex;
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;
            fixed4 _Color;
            float _SpecPow;
            float _GlossPow;

            void surf (Input IN, inout SurfaceOutput o) {
                half4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
                o.Albedo = c.rgb;
                o.Specular = 0.25 / _SpecPow;
                o.Gloss = _GlossPow;
                o.Normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
            }
            ENDCG
        }
        FallBack "Diffuse"
    }