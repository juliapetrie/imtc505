Shader "Living Room/Night Sky"
{
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.direction = input.position.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float elevation = saturate(direction.y);
                float3 sky = lerp(float3(0.090, 0.096, 0.185), float3(0.008, 0.013, 0.043),
                    sqrt(elevation));
                sky *= lerp(0.20, 1.0, smoothstep(-0.28, 0.02, direction.y));

                float2 sphere = float2(atan2(direction.x, direction.z) * 0.15915494 + 0.5,
                    asin(clamp(direction.y, -1.0, 1.0)) * 0.31830989 + 0.5);
                float2 grid = sphere * float2(180.0, 90.0);
                float2 cell = floor(grid);
                float random = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
                float2 offset = float2(random, frac(random * 17.31)) * 0.56 + 0.22;
                float distanceToStar = length(frac(grid) - offset);
                float antialias = clamp(fwidth(distanceToStar), 0.015, 0.12);
                float stars = step(0.89, random) * (1.0 - smoothstep(0.035, 0.035 + antialias, distanceToStar));
                sky += stars * smoothstep(0.04, 0.30, elevation) * 0.40;

                float3 moonDirection = normalize(float3(-0.43, 0.51, -1.0));
                float distanceToMoon = length(direction - moonDirection);
                float moon = 1.0 - smoothstep(0.018, 0.020, distanceToMoon);
                float halo = exp(-distanceToMoon * 31.0) * 0.19;
                sky += float3(0.72, 0.81, 1.0) * (moon * 0.75 + halo);
                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
