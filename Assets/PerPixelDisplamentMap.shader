Shader "Unlit/PerPixelDisplacementMapping"
{
	Properties
	{
		_MainTex ("HeightMap", 2D) = "white" {}
		_NormalX ("NormalX", 2D) = "white" {}
		_NormalY ("NormalY", 2D) = "white" {}
		_Diffuse ("Diffuse", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
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
			sampler2D _NormalX;
			sampler2D _NormalY;
			sampler2D _Diffuse;
			v2f vert (appdata v)
			{
				v2f o;
	
				v.normal = float3(0,0,0);
				o.normal = v.normal;
				o.viewOrigin = v.pos*0.5+0.5;
				o.viewVec = ( mul( _World2Object,float4(_WorldSpaceCameraPos,1)) - v.pos);
				
				o.pos = mul(UNITY_MATRIX_MVP, v.pos);
				return o;
			}
			
			float4 getHeight(float3 uvw)  
			{
				return tex2D(_MainTex, uvw.xy);
				return float4(uvw.x,1,1,1);
			}

			float4 search( const float3 start, float3 dir,float len )
			{
				int layer = 4;
				int linearSteps = 20;
				float distPerStep = len/linearSteps;
				float currentDist = distPerStep;
				float4 preDiff = float4(-1,-1,-1,-1);
				float4 diff = preDiff;
				float4 depthFromMap;


				for ( int step = 1; step < linearSteps; step++ )
				{
					float3 v = start + dir * currentDist;
					depthFromMap = getHeight(v);
					diff = v.z - depthFromMap;
					if ( diff.x * diff.y * diff.z * diff.w > 0 )
					{
						currentDist += distPerStep;
					}
				}

				int binarySteps = 10;
				float insideDist = currentDist;
				for( int step = 1; step < binarySteps; step++ )
				{
					distPerStep *=0.5;
					float3 v = start + dir * currentDist;
					depthFromMap = getHeight(v);
					diff = v.z - depthFromMap;
					if ( diff.x * diff.y * diff.z * diff.w > 0 )
					{
						//out side
						currentDist += distPerStep;
					}				
					else
					{
						insideDist = currentDist;
						currentDist -= distPerStep;
					}

				}

				float3 v = start + dir * insideDist;
				float4 z = abs(v.z-getHeight(v));
				//get layer intersected
				int m=0; 
				if (z.y<z.x) 
					m=1;
				if (z.z<z[m]) 
					m=2; 
				if (z.w< z[m]) 
					m=3;
		
				v = start + dir * ( insideDist );
				return float4(v.xy,m,insideDist/len);
			}

			float4 show_layer( int layer )
			{
				if ( layer == 0)
					return float4(1,0,0,0);
				if ( layer == 1)
					return float4(0,1,0,0);
				if ( layer == 2)
					return float4(0,0,1,0);
				if ( layer == 3)
					return float4(0,1,1,0);
				return float4(0,0,0,0);
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

				float3 viewDir = -normalize(i.viewVec);
				float4 h = search(s, viewDir, length(s-i.viewOrigin) );

				int layer = h.z;

 				float4 Nz = tex2D(_NormalX,h.xy);
				float4 Ny = tex2D(_NormalY,h.xy);
				float3 N;
				N.y = -(Nz[layer]-0.5)*2;
				N.x = (Ny[layer]-0.5)*2;		
				N.z = sqrt(max(0,1.0-dot(N.xy,N.xy)));
				if (layer == 0 || layer == 2)
					N.z *= -1;
				if ( h.w > 0.97)
					discard;
				
				float3 light = normalize(float3(1,0,0));
				N = mul(_Object2World,float4(N,1));
				float b = dot(N,light);
				return b* tex2D(_Diffuse, h.xy);
				return float4(b,b,b,0);

				
				
			}
			ENDCG
		}
	}
}
