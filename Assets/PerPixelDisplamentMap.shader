Shader "Unlit/TrueImpostor"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_HScale ("float",float) = 1
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
				float2 tex_coords : TEXCOORD0;
				float3 normal:TEXCOORD1;
				float3 viewOrigin:TEXTCOORD2;
				float3 viewVec : TEXTCOORD3;
				float4 pos : SV_POSITION;
			};


			sampler2D _MainTex;
			float _HScale;
			float4 _MainTex_ST;
			float3 _dbg_start;
			float3 _dbg_dir;

			
			
			v2f vert (appdata v)
			{
				v2f o;
	
				v.normal = float3(0,0,0);
				o.normal = v.normal;
				o.viewOrigin = v.pos;
				o.viewVec = normalize( mul( _World2Object,float4(_WorldSpaceCameraPos,1)) - v.pos);
				
				o.pos = mul(UNITY_MATRIX_MVP, v.pos);
				o.tex_coords =  TRANSFORM_TEX(v.tex_coords, _MainTex);
				return o;
			}
			
			float getHeight(float2 uv)  
			{
				return tex2D(_MainTex,uv).x ;
			}


			float search( float3 start, float3 dir,float len )
			{
				int linearSteps = 10;
				int bisectionSteps = 8;
				float step = len/linearSteps;
				float dis = step;
				float preDis = 0;
				float3 testingPoint;
				//linear search
				for( int i=0; i < linearSteps; i++ )  
				{
					testingPoint = (start + dis*dir);
					if (  testingPoint.z > getHeight(testingPoint.xy) )
					{
						preDis = dis;
						dis+=step;						
					}
				}
				
				//bisection search
				dis = 0;
				for ( i = 0; i < bisectionSteps; i++ )
				{
					float biDis = preDis + dis;
					float3 testingPoint = (start + biDis*dir);
					step *= 0.5;
					if (  testingPoint.z >  getHeight(testingPoint.xy) )
					{
						dis += step;
					}				
					else
					{
						dis -= step;
					}	
				}

				return getHeight((start + (preDis + dis)*dir).xy);
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
				// sample the texture
				float3 s = getRayStart(i);
				float h = search(s, -i.viewVec, length(s-i.viewOrigin) );
				float4 col = float4(0,1,0,0);
				//if ( h <= 0.000001)
//					discard;
				//else
//					col = float4(h,h,h,0);
				return float4(s,0);
			}
			ENDCG
		}
	}
}
