// URP, single-pass-instanced VR safe.
// Additive, transparent rim-glow that spreads outward from the touch point.
Shader "Custom/TouchGlow"
{
    Properties
    {
        [HDR] _InnerColor ("Inner Color (center)", Color) = (1, 0.1, 0.05, 1)
        [HDR] _OuterColor ("Outer Color (rim)", Color) = (1, 0.85, 0.2, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _Intensity ("Intensity", Range(0, 5)) = 1.5
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _BaseFill ("Base Fill (inner glow)", Range(0, 1)) = 0.25
        _Amount ("Amount (0-1)", Range(0, 1)) = 0
        _TouchPoint ("Touch Point (World)", Vector) = (0, 0, 0, 0)
        _Radius ("Spread Radius", Float) = 0
        _EdgeSoftness ("Spread Edge Softness", Float) = 0.4
        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Glow"
            Blend SrcAlpha One   // additive: brightens what's behind it
            ZWrite Off
            ZTest LEqual
            Offset -1, -1   // pull the overlay slightly toward the camera, avoids z-fighting
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InnerColor;
                half4 _OuterColor;
                half _Opacity;
                half _Intensity;
                half _FresnelPower;
                half _BaseFill;
                half _Amount;
                float4 _TouchPoint;
                float _Radius;
                float _EdgeSoftness;
                float _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Rim glow + a little inner fill so flat faces still glow
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDir)), _FresnelPower);
                half glow = saturate(fresnel + _BaseFill);

                // Spread outward from where the hand touched
                float dist = distance(IN.positionWS, _TouchPoint.xyz);
                half spread = 1.0h - smoothstep(_Radius - _EdgeSoftness, _Radius, dist);

                // Gentle breathing pulse
                half pulse = 1.0h + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                half a = glow * spread * _Amount * pulse * _Opacity;
                // Red where the surface faces you (inside), yellow toward the silhouette (outside)
                half3 col = lerp(_InnerColor.rgb, _OuterColor.rgb, smoothstep(0.0h, 1.0h, fresnel));
                return half4(col * _Intensity, saturate(a));
            }
            ENDHLSL
        }
    }
}
