Shader "PuzzleOnline/TargetOutlineMask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            { return TransformObjectToHClip(positionOS.xyz); }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
