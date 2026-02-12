// FireVisualizer.shader
Shader "Custom/FireVisualizer"
{
    Properties
    {
        _FireTex ("Fire Texture (R=heat)", 2D) = "black" {}
        _Intensity ("Intensity", Range(0, 2)) = 1.2
        _GlowPower ("Glow Power", Range(0, 3)) = 1.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert

            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_FireTex); SAMPLER(sampler_FireTex);
            float _Intensity;
            float _GlowPower;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // Маппинг тепла → цвет пламени
            float3 HeatColor(float t)
            {
                float3 color;
                if (t < 0.3) {
                    color = float3(0, 0, 0) + t * float3(0.8, 0.1, 0.1);
                }
                else if (t < 0.6) {
                    float f = (t - 0.3) / 0.3;
                    color = lerp(float3(0.8, 0.1, 0.1), float3(1.0, 0.5, 0.1), f);
                }
                else if (t < 0.85) {
                    float f = (t - 0.6) / 0.25;
                    color = lerp(float3(1.0, 0.5, 0.1), float3(1.0, 1.0, 0.3), f);
                }
                else {
                    float f = (t - 0.85) / 0.15;
                    color = lerp(float3(1.0, 1.0, 0.3), float3(1.0, 1.0, 1.0), f);
                }
                return color;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Читаем тепло из красного канала
                float heat = SAMPLE_TEXTURE2D(_FireTex, sampler_FireTex, input.uv).r;
                heat *= _Intensity;

                // Минимальная видимость (устраняет полную прозрачность)
                if (heat < 0.01) {
                    return half4(0, 0, 0, 0.05); // 5% прозрачности для контура квада
                }

                // Цвет пламени
                float3 flameColor = HeatColor(heat);

                // Прозрачность и свечение
                float alpha = saturate(heat * 1.5);
                float glow = pow(heat, _GlowPower) * 0.8;

                return half4(flameColor + glow, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
