Shader "AIFG/Skill Shield"
{
    Properties
    {
        _Color ("Shield Color", Color) = (0.2, 0.75, 1, 0.22)
        _RimColor ("Rim Color", Color) = (0.65, 0.95, 1, 0.75)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            fixed4 _RimColor;
            float _RimPower;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 viewDir : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos.xyz - worldPos;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float rim = pow(1.0 - saturate(dot(normalize(i.normal), normalize(i.viewDir))), _RimPower);
                fixed4 color = _Color + _RimColor * rim;
                color.a = saturate(_Color.a + _RimColor.a * rim);
                return color;
            }
            ENDCG
        }
    }
}
