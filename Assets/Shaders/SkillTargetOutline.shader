Shader "AIFG/Skill Target Outline"
{
    Properties { _Color ("Outline Color", Color) = (1,1,1,1) _Width ("Screen Width", Range(0.0005,0.02)) = 0.004 }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Cull Front
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _Width;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 projectedNormal = TransformViewToProjection(viewNormal.xy);
                float lengthSquared = max(dot(projectedNormal, projectedNormal), 0.000001);
                o.pos.xy += projectedNormal * rsqrt(lengthSquared) * _Width * o.pos.w;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
