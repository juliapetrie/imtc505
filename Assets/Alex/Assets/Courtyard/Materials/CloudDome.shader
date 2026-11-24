Shader "Campus/Cloud Dome"
{
    // Clouds painted on the inside of a big sphere. No fog (so the clouds stay visible
    // above thick ground fog), draws the inside of the sphere, and projects the texture
    // from the viewer's direction so there is no seam or pinching at the top.
    // CampusProgress changes the tint (_BaseColor) and drifts the clouds via the texture offset.
    Properties
    {
        [MainTexture] _BaseMap ("Cloud Texture (Wrap Mode: Repeat)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Tint (alpha = thickness)", Color) = (1, 1, 1, 0.8)

        [Toggle] _AlphaFromRed ("Use Red Channel As Mask (for black/white textures)", Float) = 0

        _HorizonStart ("Fade In Above Horizon (start)", Range(0, 1)) = 0.02
        _HorizonEnd ("Fade In Above Horizon (end)", Range(0, 1)) = 0.30
        _Flatten ("Stretch Limit Near Horizon", Range(0.02, 0.5)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front   // draw the INSIDE of the sphere

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _AlphaFromRed;
                half   _HorizonStart;
                half   _HorizonEnd;
                half   _Flatten;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Which way the viewer is looking through this pixel.
                float3 dir = normalize(input.positionWS - GetCameraPositionWS());

                // Project the texture onto a flat "sky plane" above the viewer.
                float2 uv = dir.xz / max(dir.y, _Flatten) * _BaseMap_ST.xy + _BaseMap_ST.zw;

                half4 s = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half mask = lerp(s.a, s.r, _AlphaFromRed);

                // Clouds fade out toward the horizon and below it.
                half horizon = smoothstep(_HorizonStart, _HorizonEnd, dir.y);

                half4 col = _BaseColor;
                col.a = saturate(col.a * mask * horizon);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
