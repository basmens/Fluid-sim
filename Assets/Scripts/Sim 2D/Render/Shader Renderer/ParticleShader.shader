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

            static const uint STATIC_COLOR = 0;
            static const uint VELOCITY_COLOR = 1;

            SamplerState linear_clamp_sampler;
            // float4x4 _BillboardMatrix;
            StructuredBuffer<float2> _Positions;
            StructuredBuffer<float2> _Velocities;
            
            Texture2D<float4> _ColoringTexture;
            float _PropertyMin;
            float _PropertyMax;
            uint _ColoringProperty;

            float _Scale;
            float _EdgeSoftness;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 distFromCenter : TEXCOORD0;
                float4 color : COLOR;
            };

            float discernColoringProperty(uint instanceID) {
                switch (_ColoringProperty) {
                    case STATIC_COLOR:
                        return 0.5f;
                    case VELOCITY_COLOR:
                        return (length(_Velocities[instanceID]) - _PropertyMin) / (_PropertyMax - _PropertyMin);
                    default:
                        return 0;
                }
            }

            v2f vert(appdata_base v, uint instanceID : SV_InstanceID) {
                float4 centreWorld = float4(_Positions[instanceID], 0, 1);
				float3 worldVertPos = centreWorld + mul(unity_ObjectToWorld, v.vertex * _Scale);
				// float3 worldVertPos = centreWorld + mul(unity_ObjectToWorld, mul(v.vertex * _Scale, _BillboardMatrix));
				float3 objectVertPos = mul(unity_WorldToObject, float4(worldVertPos.xyz, 1));

                float2 colorTextureCoords = float2(discernColoringProperty(instanceID), 0.5f);
                float4 color = _ColoringTexture.SampleLevel(linear_clamp_sampler, colorTextureCoords, 0);

                v2f o;
                o.pos = UnityObjectToClipPos(objectVertPos);
                o.distFromCenter = v.vertex.xy * 2;
                o.color = color;
                return o;
            }

            float4 frag(v2f i) : SV_Target {
				float sqDst = dot(i.distFromCenter, i.distFromCenter);
				float alpha = smoothstep(1, 1 - _EdgeSoftness, sqDst);
				return float4(i.color.rgb, i.color.a * alpha);
            }

            ENDHLSL
        }
    }
}
