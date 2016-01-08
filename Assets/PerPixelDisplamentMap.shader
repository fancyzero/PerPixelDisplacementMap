Shader "Unlit/PerPixelDisplacementMapping"
{
	Properties
	{
		_MainTex ("HeightMap", 2D) = "white" {}
		_TangentTex ("Tangent", 2D) = "white" {}
		_BinormalTex ("Binormal", 2D) = "white" {}
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
			sampler2D _TangentTex;
			sampler2D _BinormalTex;
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
			
			float4 getHeight(float3 uvw)  
			{
				return float4(uvw.x,1,1,1);
			}


			float4 search( const float3 start, float3 dir,float len )
			{
				int linearSteps = 5;
				float distPerStep = len/linearSteps;
				float currentDist = distPerStep;
				float preDist = 0;
				for ( int step = 1; step < linearSteps; step++ )
				{
					float3 v = start + dir * currentDist;
					float4 depthFromMap = getHeight(v);
					float4 diff = v.z - depthFromMap;
					if ( diff.x * diff.y * diff.z * diff.w > 0 )
					{
						//out side
						preDist = currentDist;
						currentDist += distPerStep;
					}
				}
								
				float3 v1 = start + dir * preDist;
				float3 v2 = start + dir * currentDist;
				float vos = v1.z;
				float voe = v2.z;
				float vps = getHeight(v1).x;
				float vpe = getHeight(v2).x;
				float l = length(v1.xy - v2.xy);
				float ok = (voe-vos)/l;
				float pk = (vpe-vps)/l;
				float o = vos;
				float p = vps;

				float intersect = (p-o)/ok-pk;


				return float4(float3(intersect,intersect,intersect),currentDist/len);
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

				return (i.viewOrigin + T*i.viewVec );
			}

			float4 frag (v2f i) : SV_Target
			{
				float3 s = getRayStart(i);
				float4 h = search(s, -normalize(i.viewVec), length(s-i.viewOrigin) );

 				float3 T = normalize(ddx(h.xyz));
				float3 B = normalize(ddy(h.xyz));

				if ( h.w > 0.999999)
					discard;
				float3 N = normalize(cross(B,T));
				//return float4(N,0);
				return float4(h);
				
				
			}
			ENDCG
		}
	}
}
