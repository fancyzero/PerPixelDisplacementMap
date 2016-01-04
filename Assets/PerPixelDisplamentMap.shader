Shader "Unlit/PerPixelDisplacementMapping"
{
	Properties
	{
		_MainTex ("HeightMap", 2D) = "white" {}
		_DiffuseTex ("Diffuse", 2D) = "white" {}
		_NormalTex ("Normal", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma enable_d3d11_debug_symbols
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 pos : POSITION;
				float2 tex_coords : TEXCOORD0;
				float3 normal:TEXCOORD1;
			};

			struct v2f
			{
				float3 normal:TEXCOORD1;
				float3 viewOrigin:TEXTCOORD2;
				float3 viewVec : TEXTCOORD3;
				float4 pos : SV_POSITION;
			};


			sampler2D _MainTex;
			sampler2D _DiffuseTex;
			v2f vert (appdata v)
			{
				v2f o;
	
				v.normal = float3(0,0,0);
				o.normal = v.normal;
				o.viewOrigin = v.pos;
				o.viewVec = ( mul( _World2Object,float4(_WorldSpaceCameraPos,1)) - v.pos);
				
				o.pos = mul(UNITY_MATRIX_MVP, v.pos);
				return o;
			}
			
			float getHeight(float3 uvw)  
			{
				return tex2D(_MainTex,uvw.xy).x *0.99;
			}


			float3 search( const float3 start, float3 dir,float len )
			{
				int linearSteps = 2000;
				float distPerStep = len/linearSteps;
				float currentDist = distPerStep;
				float preDist = 0;
				for ( int step = 1; step < linearSteps; step++ )
				{
					float3 v = start + dir * currentDist;
					float4 depthFromMap = tex2D(_MainTex, v.xy);
					float4 diff = v.z - depthFromMap;
					if ( diff.x * diff.y * diff.z * diff.w > 0 )
					{
						//out side
						preDist = currentDist;
						currentDist += distPerStep;
					}
				}
				#if 1
				int binarySteps = 30;
				for( step = 1; step < binarySteps; step++ )
				{
					distPerStep *=0.5;
					float3 v = start + dir * currentDist;
					float depthFromMap = tex2D(_MainTex, v.xy);
					float4 diff = v.z - depthFromMap;
					if ( diff.x * diff.y * diff.z * diff.w > 0 )
					{
						//out side
						currentDist += distPerStep;
					}				
					else
					{
						currentDist -= distPerStep;
					}
				}
				#endif
				float3 v = start + dir * currentDist;
				return float3(currentDist/len,v.x,v.y);
			}

			float3 getRayStart(v2f i)
			{
				float x = i.viewOrigin.x;
				float y = i.viewOrigin.y;
				float z = i.viewOrigin.z;

				float dx = i.viewVec.x;
				float dy = i.viewVec.y;
				float dz = i.viewVec.z;

				float Tx = max((1-x)/dx,(-1-x)/dx);
				float Ty = max((1-y)/dy,(-1-y)/dy);
				float Tz = max((1-z)/dz,(-1-z)/dz);

				float T = min(min(Tx,Ty),Tz);

				return (i.viewOrigin + T*i.viewVec + 1)/2;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float3 s = getRayStart(i);
				float3 h = search(s, -normalize(i.viewVec), length(s-i.viewOrigin) );
				float4 col = float4(0,0,0,0);

				col = float4(h,0);
				if ( h.x > 0.8)
					discard;
				return tex2D(_DiffuseTex, float2(h.yz));
				
				
			}
			ENDCG
		}
	}
}
