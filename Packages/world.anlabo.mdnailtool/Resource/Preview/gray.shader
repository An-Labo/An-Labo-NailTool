﻿Shader "Hidden/world.anlabo.mdnailtool/Gray" {
    Properties {
        _MainTex ("Texture", Any) = "white" {}
    }

    CGINCLUDE
    #pragma vertex vert
    #pragma fragment frag
    #pragma target 2.0

    #include "UnityCG.cginc"

    struct appdata_t {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        float2 clipUV : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    uniform float4 _MainTex_ST;
    uniform float4x4 unity_GUIClipTextureMatrix;

    v2f vert (appdata_t v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.vertex = UnityObjectToClipPos(v.vertex);
        float3 eyePos = UnityObjectToViewPos(v.vertex);
        o.clipUV = mul(unity_GUIClipTextureMatrix, float4(eyePos.xy, 0, 1.0));
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        return o;
    }

    uniform bool _ManualTex2SRGB;
    sampler2D _MainTex;
    sampler2D _GUIClipTexture;

    fixed4 frag (v2f i) : SV_Target
    {
        fixed4 colTex = tex2D(_MainTex, i.texcoord);
        if (_ManualTex2SRGB) {
            colTex.rgb = LinearToGammaSpace(colTex.rgb);
        }
        
        fixed4 col;
        col.rgb = colTex.rgb;
        col.rgb = col.rgb * 0.333;
        col.rgb = col.r + col.g + col.b;
        col.a = tex2D(_GUIClipTexture, i.clipUV).a;
        return col;
    }
    ENDCG

    SubShader {
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass {
            CGPROGRAM
            ENDCG
        }
    }
}