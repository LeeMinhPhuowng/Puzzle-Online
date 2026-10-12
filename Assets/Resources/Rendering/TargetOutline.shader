Shader "PuzzleOnline/TargetOutline"
{
    Properties
    {
        _OutlineColor("Colour", Color) = (0.2, 2.5, 3.0, 1)
        _OutlineWidth("Width in pixels", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+2" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            Stencil { Ref 128 ReadMask 128 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            CBUFFER_END
            float4 Vert(float4 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            {
                float4 clip = TransformObjectToHClip(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                float lengthCS = length(normalCS);
                if (lengthCS > 0.0001)
                    clip.xy += normalCS / lengthCS * (_OutlineWidth * 2.0 / _ScaledScreenParams.xy) * clip.w;
                return clip;
            }
            half4 Frag() : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
