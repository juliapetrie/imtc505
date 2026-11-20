Shader "LivingRoom/Speaker Ring"
{
    Properties { _Phase ("Sweep Phase", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fraction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            CBUFFER_START(UnityPerMaterial)
                float _Phase;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fraction = input.uv.x;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float hue = 0.59 + 0.30 * (0.5 + 0.5 * sin((input.fraction - _Phase) * 6.2831853));
                float3 rgb = saturate(abs(frac(hue + float3(0.0, 0.6666667, 0.3333333)) * 6.0 - 3.0) - 1.0);
                return half4(lerp(1.0, rgb, 0.82) * 0.65, 1.0);
            }
            ENDHLSL
        }
    }
}
