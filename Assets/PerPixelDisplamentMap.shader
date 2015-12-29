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
				o.viewVec = normalize( mul( _World2Object,_WorldSpaceCameraPos) - v.pos);
				
				o.pos = mul(UNITY_MATRIX_MVP, v.pos);
				o.tex_coords =  TRANSFORM_TEX(v.tex_coords, _MainTex);
				return o;
			}
			
			float getHeight(float2 uv)  
			{
				return tex2D(_MainTex,uv).x ;
			}

			
			float linear_search( float3 start, float3 dir,float len )
			{
				int linear_search_steps = 1000;
				float depth_per_step=len/linear_search_steps;
				float depth = 0;
				float pre_height = 1;
				float dis = depth_per_step;
				float hscale = _HScale;
				float collied = 0;
				_dbg_start = start;
				_dbg_dir = dir;
				for( int i=0; i < linear_search_steps; i++ )
				{
					depth = (start + dis*dir).z;
					float2 tex_coords = (start + dis*dir).xy;
					float height = getHeight(tex_coords)*hscale;// + (1-hscale)/2 - 0.5;
					if (  depth < height && !collied )
					{
						collied = 1;
						pre_height = depth;
						
					}
					dis+=depth_per_step;

				}
				return pre_height * collied;
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
				float h = linear_search(s, -i.viewVec, length(s-i.viewOrigin) );
				float4 col = float4(0,1,0,0);
				if ( h <= 0.000001)
					discard;
				else
					col = float4(h,h,h,0);
				return col;
			}
			ENDCG
		}
	}
}
