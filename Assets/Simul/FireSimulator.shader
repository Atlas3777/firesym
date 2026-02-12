Shader "Custom/FireSimulator"
{
    Properties
    {
        _MainTex ("Input", 2D) = "black" {}
        _IgnitionPoint ("Ignition Point", Vector) = (0.5, 0.5, 0.1, 0)
        _SpreadSpeed ("Spread Speed", Float) = 0.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _IgnitionPoint; // xy = позиция, z = радиус активации
            float _SpreadSpeed;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Текущее тепло в точке
                float heat = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;

                // Расстояние до источника
                float dist = distance(input.uv, _IgnitionPoint.xy);

                // Активация при близости к источнику
                if (dist < _IgnitionPoint.z) {
                    heat = 1.0;
                }
                // Диффузия: усредняем с 4 соседями
                else {
                    float2 pixelSize = 1.0 / _ScreenParams.xy;
                    float sum = 0;
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(pixelSize.x, 0)).r;
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-pixelSize.x, 0)).r;
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, pixelSize.y)).r;
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, -pixelSize.y)).r;
                    float avg = sum * 0.25;

                    // Смешиваем с диффузией
                    heat = lerp(heat, avg, 0.4);
                }

                // Охлаждение
                heat = max(0, heat - _Time.y * 0.03);

                return float4(heat, 0, 0, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
