Shader "AIFG/Skill Video Wipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Video", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Range(0,1)) = 0
        _Exiting ("Exiting", Range(0,1)) = 0
        _Feather ("Edge Feather", Range(0.001,0.1)) = 0.018
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

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            float _Progress, _Exiting, _Feather;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * i.color;
                float enteringMask = smoothstep(1.0 - _Progress - _Feather,
                    1.0 - _Progress + _Feather, i.uv.x);
                float exitingMask = smoothstep(_Progress - _Feather,
                    _Progress + _Feather, i.uv.x);
                color.a *= lerp(enteringMask, exitingMask, step(0.5, _Exiting));
                return color;
            }
            ENDCG
        }
    }
}
