Shader "ithappy/WaterURP"
{
    Properties
    {
        [Header(Surface)]
        _MaskSurface ("Mask", 2D) = "black" {}
        _SurfaceOpacity ("Opacity", range(0, 1)) = 1
        _ColorSurface ("Color", color) = (0.9, 0.9, 0.9, 1)

        [Header(Color)]
        _ColorShallow ("Shallow", color) = (0.4, 0.8, 1, 1)
        _ColorDeep ("Deep", color) = (0.0, 0.2, 0.5, 1)
        _DepthMaxDistance ("Depth Distance", float) = 5.0

        [Header(Foam)]
        _FoamTex ("Foam Texture", 2D) = "white" {}
        _FoamSpeed ("Foam Speed", float) = 0.1
        _FoamIntensity ("Foam Intensity", float) = 1.0

        [Header(Normal)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0

        [Header(Flow Control)]
        _FlowDir ("Flow Direction", Vector) = (0, -1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            sampler2D _MaskSurface;
            sampler2D _FoamTex;
            sampler2D _NormalMap;
            float4 _ColorSurface;
            float _SurfaceOpacity;
            float4 _ColorShallow;
            float4 _ColorDeep;
            float _DepthMaxDistance;
            float _FoamSpeed;
            float _FoamIntensity;
            float _NormalStrength;
            float4 _FlowDir;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 flowUV = i.uv;
                flowUV += _Time.y * 0.05 * _FlowDir.xz; // 흐름 방향 반영

                float3 normalTex = UnpackNormal(tex2D(_NormalMap, flowUV));
                normalTex.xy *= _NormalStrength;

                float depthLerp = saturate((i.worldPos.y - _DepthMaxDistance) / -_DepthMaxDistance);
                float4 waterColor = lerp(_ColorShallow, _ColorDeep, depthLerp);

                float foam = tex2D(_FoamTex, flowUV * 2 + _Time.y * _FoamSpeed).r * _FoamIntensity;

                float4 mask = tex2D(_MaskSurface, i.uv);
                float4 finalColor = lerp(waterColor, _ColorSurface, mask.r);

                finalColor.rgb += foam * 0.2;
                finalColor.a = _SurfaceOpacity;
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}