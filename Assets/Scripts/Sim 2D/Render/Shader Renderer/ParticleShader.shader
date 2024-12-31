Shader "Instanced/ParticleShader" {
    Properties {
    }

    SubShader {
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass {
            Name "ParticleShader"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
			#include "UnityCG.cginc"

            // SamplerState linear_clamp_sampler;
            StructuredBuffer<float2> _ParticlePositions;
            float4 _ParticleColor;
            float _Scale;
            float _EdgeSoftness;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 normal : TEXCOORD0;
            };

            v2f vert(appdata_base v, uint instanceID : SV_InstanceID) {
                float4 centreWorld = float4(_ParticlePositions[instanceID], 0, 1);
				float3 worldVertPos = centreWorld + mul(unity_ObjectToWorld, v.vertex * _Scale);
				float3 objectVertPos = mul(unity_WorldToObject, float4(worldVertPos.xyz, 1));

                v2f o;
                o.pos = UnityObjectToClipPos(objectVertPos);
                o.normal = v.normal;
                return o;
            }

            float4 frag(v2f i) : SV_Target {
				float sqrDst = dot(i.normal, i.normal);
				float alpha = smoothstep(1, 1 - _EdgeSoftness, sqrt(sqrDst));
				return float4(_ParticleColor.rgb, _ParticleColor.a * alpha);
            }

            ENDHLSL
        }
    }
}
