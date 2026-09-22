Shader "AIFG/Character Preview Toon"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Tone", Color) = (0.52, 0.58, 0.76, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.52
        _ShadowSoftness ("Shadow Edge Softness", Range(0.001, 0.25)) = 0.035
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.22
        _RimColor ("Rim Color", Color) = (0.55, 0.75, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.12
        [Toggle] _AlphaClip ("Use Texture Transparency", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _AmbientStrength;
            fixed4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half _AlphaClip;
            half _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
                SHADOW_COORDS(3)
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                TRANSFER_SHADOW(output);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, input.uv) * _Color;
                clip(_AlphaClip < 0.5h ? 1.0h : albedo.a - _Cutoff);
                half3 normal = normalize(input.worldNormal);
                half3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
                half3 viewDirection = normalize(UnityWorldSpaceViewDir(input.worldPosition));
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);

                half lightAmount = saturate(dot(normal, lightDirection)) * attenuation;
                half band = smoothstep(
                    _ShadowThreshold - _ShadowSoftness,
                    _ShadowThreshold + _ShadowSoftness,
                    lightAmount);
                half3 tone = lerp(_ShadowColor.rgb, _LightColor0.rgb, band);
                half3 ambient = ShadeSH9(half4(normal, 1)).rgb * _AmbientStrength;
                half rim = pow(saturate(1 - dot(normal, viewDirection)), _RimPower) * _RimStrength;

                return fixed4(albedo.rgb * (tone + ambient) + _RimColor.rgb * rim, albedo.a);
            }
            ENDCG
        }

        Pass
        {
            Name "SHADOWCASTER"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
            };

            v2fShadow vertShadow(appdata_base v)
            {
                v2fShadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 fragShadow(v2fShadow i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
