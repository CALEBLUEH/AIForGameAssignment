Shader "AIFG/Targeting Dimmer Cutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0,0,0.62)
        _HoleFeather ("Hole Feather", Range(0.001,0.2)) = 0.035
        _Hole0 ("Hole 0", Vector) = (0,0,0,0)
        _Hole1 ("Hole 1", Vector) = (0,0,0,0)
        _Hole2 ("Hole 2", Vector) = (0,0,0,0)
        _Hole3 ("Hole 3", Vector) = (0,0,0,0)
        _Hole4 ("Hole 4", Vector) = (0,0,0,0)
        _Hole5 ("Hole 5", Vector) = (0,0,0,0)
        _Hole6 ("Hole 6", Vector) = (0,0,0,0)
        _Hole7 ("Hole 7", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _HoleFeather;
            float4 _Hole0, _Hole1, _Hole2, _Hole3, _Hole4, _Hole5, _Hole6, _Hole7;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float HoleMask(float2 uv, float4 hole)
            {
                if (hole.z <= 0.0 || hole.w <= 0.0) return 1.0;
                float distanceFromCenter = length((uv - hole.xy) / hole.zw);
                return smoothstep(1.0 - _HoleFeather, 1.0 + _HoleFeather, distanceFromCenter);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * i.color;
                float mask = HoleMask(i.uv, _Hole0);
                mask *= HoleMask(i.uv, _Hole1);
                mask *= HoleMask(i.uv, _Hole2);
                mask *= HoleMask(i.uv, _Hole3);
                mask *= HoleMask(i.uv, _Hole4);
                mask *= HoleMask(i.uv, _Hole5);
                mask *= HoleMask(i.uv, _Hole6);
                mask *= HoleMask(i.uv, _Hole7);
                color.a *= mask;
                return color;
            }
            ENDCG
        }
    }
}
