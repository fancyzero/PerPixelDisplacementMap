using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class TrueImpostorsWindow : EditorWindow
{
	public struct Facet
	{
		public int p1;
		public int p2;
		public int p3;
		public Facet(int pp1, int pp2, int pp3 )
		{
			p1 = pp1;
			p2 = pp2;
			p3 = pp3;
		}
	};
	public Mesh selected_mesh = null;
	public List<Vector3> vertices = new List<Vector3>();
	public List<Facet> 	faces = new List<Facet>();
	public String errors;
	bool log = false;


	// Add menu item named "My Window" to the Window menu
	[MenuItem("TrueImpostors/Map Generator")]
	public static void ShowWindow()
	{
		//Show existing window instance. If one doesn't exist, make one.
		EditorWindow.GetWindow(typeof(TrueImpostorsWindow));
	}


	
	void OnGUI()
	{

		selected_mesh = (Mesh) EditorGUI.ObjectField(new Rect(3, 30, position.width - 6, 20),
		                                             "Select Mesh", selected_mesh, typeof(Mesh),false);
		if (GUI.Button (new Rect (3, 50, position.width - 6, 20), "Generate Maps")) 
		{
			Generate(selected_mesh);
		}
	
	}

	struct RayCastResult
	{
		public float t;
		public Vector2 uv;
		public RayCastResult(float _t, Vector2 _uv)
		{
			t = _t;
			uv = _uv;
		}
	};

	RayCastResult RayQuery(Vector3[] vertices, Vector3 ray_start, Vector3 ray_dir )
	{
		Vector3 p1 = vertices [0];
		Vector3 p2 = vertices [1];
		Vector3 p3 = vertices [2];
		
		Vector3 e1 = p2 - p1;
		Vector3 e2 = p3 - p1;
				
		Vector3 s1 = Vector3.Cross (ray_dir, e2);
		float divisor = Vector3.Dot (s1, e1);
		if (divisor == 0.0f)
			return new RayCastResult(-1, new Vector2(0,0));
		float inv_divisor = 1.0f / divisor;
						
		Vector3 d = ray_start - p1;
		float u = Vector3.Dot(d, s1) * inv_divisor;
			
		if (u < 0 || u > 1)
			return new RayCastResult(-1, new Vector2(0,0));

		Vector3 s2 = Vector3.Cross(d, e1);
		float v = Vector3.Dot (ray_dir, s2) * inv_divisor;
		if (v < 0 || u + v > 1.0f)
			return new RayCastResult(-1, new Vector2(0,0));
		float t = Vector3.Dot(e2, s2) * inv_divisor;
		return new RayCastResult(t, new Vector2(u,v));
	}

	struct QueryResult
	{
		public float t;
		public Vector2 uv;
		public int triangleIndex;
	};
	


	struct TriangleData
	{
		Vector3[] vertices;
		Vector3[] normals;
	}

	Vector3[] GetAABB( Vector3[] vertices)
	{
		if (vertices.Length == 0)
			return null;
		Vector3 max = vertices[0];
		Vector3 min = vertices[0];
		foreach (Vector3 v in vertices) 
		{
			if ( v.x > max.x )
				max.x = v.x;
			if ( v.y > max.y )
				max.y = v.y;
			if ( v.z > max.z )
				max.z = v.z;
			if ( v.x < min.x )
				min.x = v.x;
			if ( v.y < min.y )
				min.y = v.y;
			if ( v.z < min.z )
				min.z = v.z;
		}
		return new Vector3[]{min,max};
	}

	Vector3[] GetTriangleVertsByIndex( int index)
	{	
		if (index > faces.Count)
			return null;
		return new Vector3[]{vertices[faces[index].p1],vertices[faces[index].p2],vertices[faces[index].p3]};

	}

	Vector3[] GetTriangleNormals(Mesh mesh, int triangleIndex )
	{
		return new Vector3[]{mesh.normals [faces [triangleIndex].p1],
			
			mesh.normals [faces [triangleIndex].p2],
			
			mesh.normals [faces [triangleIndex].p3]};
	}

	Vector2[] GetTriangleUVs( Mesh mesh, int triangleIndex)
	{
	 return new Vector2[]{mesh.uv [faces [triangleIndex].p1],
	
			 mesh.uv [faces [triangleIndex].p2],
	
			mesh.uv [faces [triangleIndex].p3]};
	}

	List<int>[,] GroupTriangles( int group_row, int group_col, float group_width, float group_height, Vector3 max_point)
	{
		// put triangles into groups by its aabb to speed up ray casting
		List<int>[,] triangle_groups = new List<int>[group_col, group_row];

		for( int i=0; i < faces.Count; i++ )
		{
			Vector3 p1 = vertices[faces[i].p1];
			Vector3 p2 = vertices[faces[i].p2];
			Vector3 p3 = vertices[faces[i].p3];
			//get aabb
			Vector3[] aabb = GetAABB(new Vector3[]{p1,p2,p3});
			if ( aabb == null )
				continue;
			float extend = 2.0f;
			Vector3 p_min=aabb[0]-new Vector3(extend,extend,extend);
			Vector3 p_max=aabb[1]+new Vector3(extend,extend,extend);
			int startx = (int)(p_min.y / group_width);
			int starty = (int)(p_min.z / group_height);
			int endx = (int)(p_max.y / group_width);
			int endy = (int)(p_max.z / group_height);
			//fill groups
			for (int x = startx; x <= endx; x++ )
				for (int y = starty; y <= endy; y++ )
				{
				if ( x > triangle_groups.GetUpperBound(0) || y > triangle_groups.GetUpperBound(1) ||
				    x < triangle_groups.GetLowerBound(0) || y < triangle_groups.GetLowerBound(1))
						continue;
					if (triangle_groups[x,y] == null )
						triangle_groups[x,y] = new List<int>();
						triangle_groups[x,y].Add(i);
				}


		}
		return triangle_groups;
	}

	List<QueryResult> PixelQuery( List<Vector3> vertices, Vector3 ray_start, Vector3 ray_dir, List<int>[,] triangle_groups, float group_width, float group_height, float hscale)
	{
		List<QueryResult> results = new List<QueryResult>();

		int x = (int)(ray_start.y / group_width);
		int y = (int)(ray_start.z / group_height);
		List<int> valid_triangles = triangle_groups[x,y];
		if (valid_triangles == null  || valid_triangles.Count == 0)
			return results;
		foreach (int i in valid_triangles) 
		{

			Vector3[] triangle_vertices = GetTriangleVertsByIndex(i);
			RayCastResult result = RayQuery(triangle_vertices, ray_start, ray_dir);
			if ( result.t >= 0 )
			{
				if ( log)
					Debug.LogFormat("{0},{1},{2}", triangle_vertices[0].ToString("F4"),triangle_vertices[1].ToString("F4"),triangle_vertices[2].ToString("F4"));
				QueryResult qr = new QueryResult();
				qr.t = result.t;
				qr.uv = result.uv;
				qr.triangleIndex = i;
				results.Add(qr);
			}

		}
		if (results.Count == 0)
			return results;
		results.Sort (delegate (QueryResult a, QueryResult b) {
			if ( a.t < b.t )
				return -1;
			if ( a.t > b.t )
				return 1;
			return 0;

		});

		int j = 0;

		while( j < results.Count-1 )
		{
			if ( Math.Abs(results[j].t - results[j+1].t) < 0.00001f )
				results.RemoveAt(j);
			else
				j++;
		}
		if (results.Count == 1)
			results.Clear ();
		if (results.Count % 2 == 1) 
		{
			errors+="warning , bad result\n";
			results.Add(results[results.Count-1]);
		}


		return results;
	}

	void SaveTextureToFile( Texture2D texture, string filename)
	{
		var bytes=texture.EncodeToPNG();

		var file =  File.Open(Application.dataPath + "/"+filename,FileMode.Create);
		var binary= new BinaryWriter(file);
		binary.Write(bytes);
		file.Close();
	}

	void Generate( Mesh mesh)
	{
		int width = 256;
		int height = 256;

		faces.Clear ();

		vertices.Clear ();
		for ( int i = 0; i < mesh.subMeshCount; i++ )
		{	
			int[] ts = mesh.GetTriangles(i);
			for (int j=0; j < ts.Length/3; j++ )
				faces.Add(new Facet(ts[j*3],ts[j*3+1],ts[j*3+2]));
		}

		vertices.AddRange(mesh.vertices);

		Vector3[] aabb = GetAABB (vertices.ToArray());
		for ( int i = 0 ; i < vertices.Count; i++) 
		{
			vertices[i] -= aabb[0];
		}
		Vector3 max_point = aabb [1] - aabb [0];
		float wstep = (max_point.y) / width;
		float hstep = (max_point.z) / height;
		float hscale = 1 / (max_point.x);
		List<int>[,] tg = GroupTriangles(width, height, wstep, hstep, max_point);

		var displacement_tex = new Texture2D (width, height);
		var disp_pixels = displacement_tex.GetPixels32 ();

		for (int y = 0; y < height; y++) {
			for (int x =0; x < width; x++) {
				log = false;
				if ( x==128 && y ==129 )
					log = true;
				Vector3 start = new Vector3(0, x*wstep, y*hstep);
				Vector3 dir = new Vector3(1,0,0);
				var query_results = PixelQuery(vertices, start, dir, tg, wstep, hstep, hscale ); 
				byte[] color = new byte[4]{255,255,255,255};
				byte[] normal_x = new byte[4];
				byte[] normal_y = new byte[4];

				for ( int c = 0; c < query_results.Count; c++ )
				{
					if ( c >= 4)
						break;
					var r = query_results[c];
					float u=r.uv.x;
					float v=r.uv.y;
					float w = 1-(u+v);
					var normals = GetTriangleNormals(mesh, r.triangleIndex);
					normal_x[c] = (byte)(((w * normals[0].z + u * normals[1].z + v * normals[2].z)+1)/2*255);
					normal_y[c] = (byte)(((w * normals[0].y + u * normals[1].y + v * normals[2].y)+1)/2*255);
					color[c]=(byte)(query_results[c].t * hscale * 255);
				}
				disp_pixels[x + y*width].r = color[0];
				disp_pixels[x + y*width].g = color[1];
				disp_pixels[x + y*width].b = color[2];
				disp_pixels[x + y*width].a = color[3];
			}
		}
		displacement_tex.SetPixels32 (disp_pixels);
		SaveTextureToFile (displacement_tex, "fff.png");

	

	 }
}
