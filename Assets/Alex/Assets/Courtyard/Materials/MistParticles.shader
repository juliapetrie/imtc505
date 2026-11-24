Shader "Campus/Mist Particles"
{
    // Soft, drifting mist for particle systems in URP. Built for VR (single-pass instanced)
    // and light enough for Quest. CampusProgress tints it through _BaseColor.
    Properties
    {
        [MainTexture] _BaseMap ("Mist Texture", 2D) = "white" {}
        [MainColor]   _BaseColor ("Tint (alpha = opacity)", Color) = (1, 1, 1, 0.5)

        [Toggle] _AlphaFromRed ("Use Red Channel As Mask (for black/white textures)", Float) = 0

        _ScrollSpeed ("Drift Speed (xy)", Vector) = (0.02, 0.01, 0, 0)
        _Wispiness ("Wispiness (second layer)", Range(0, 1)) = 0.6

        _NearFadeStart ("Fade Out Closer Than (m)", Float) = 0.5
        _NearFadeEnd ("Fully Visible Beyond (m)", Float) = 2.5

        _EdgeFade ("Soft Quad Edges", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _AlphaFromRed;
                float4 _ScrollSpeed;
                half   _Wispiness;
                float  _NearFadeStart;
                float  _NearFadeEnd;
                half   _EdgeFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4  color      : COLOR;
                half   fogCoord   : TEXCOORD2;
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
                output.uv = input.uv;
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Two layers of the same texture drifting in different directions.
                float t = _Time.y;
                float2 uv1 = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + t * _ScrollSpeed.xy;
                float2 uv2 = input.uv * _BaseMap_ST.xy * 1.7 + 0.37 - t * _ScrollSpeed.xy * 0.6;

                half4 s1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1);
                half4 s2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv2);

                half m1 = lerp(s1.a, s1.r, _AlphaFromRed);
                half m2 = lerp(s2.a, s2.r, _AlphaFromRed);
                half mask = m1 * lerp(1.0h, m2, _Wispiness);

                // Fade out as the camera gets close, so walking through mist doesn't pop.
                float dist = distance(input.positionWS, GetCameraPositionWS());
                half nearFade = smoothstep(_NearFadeStart, _NearFadeEnd, dist);

                // Round off the quad so its square edges never show.
                float2 c = input.uv * 2.0 - 1.0;
                half edge = smoothstep(0.0, 1.0, saturate(1.0 - dot(c, c)));
                edge = lerp(1.0h, edge, _EdgeFade);

                half4 col = _BaseColor * input.color;
                col.a = saturate(col.a * mask * nearFade * edge);
                col.rgb = MixFog(col.rgb, input.fogCoord);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
