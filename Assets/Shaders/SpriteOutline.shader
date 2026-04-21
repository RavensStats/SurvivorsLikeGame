Shader "Custom/SpriteOutline"
{
    Properties
    {
        _MainTex       ("Sprite Texture", 2D)    = "white" {}
        _Color         ("Tint",           Color) = (1,1,1,1)
        _OutlineColor  ("Outline Color",  Color) = (1,1,1,1)
        _OutlineWidth  ("Outline Width (px)", Float) = 1.5
        _OutlineEnabled("Outline Enabled",   Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // _ST and _TexelSize must live outside the cbuffer for 2D SRP Batcher compatibility
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineEnabled;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4  color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.color       = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color * _Color;

                if (_OutlineEnabled > 0.5 && col.a < 0.1)
                {
                    float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;
                    float n = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,  texel.y)).a
                            + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0, -texel.y)).a
                            + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( texel.x, 0)).a
                            + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-texel.x, 0)).a;
                    if (n > 0.1)
                        return _OutlineColor;
                }

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
