using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

class Rasterizer
{
	public class Edge 
	{
		public Vector3 v1;
		public Vector3 v2;
		public Edge( Vector3 pv1, Vector3 pv2)
		{
			v1 = pv1;
			v2 = pv2;
		}
		public Vector3 Vector()
		{
			Vector3 ret = (v2 - v1);
			ret.Normalize();
			return ret;
		}
		public float Slope()
		{
			return (v2 - v1).z / (v2 - v1).y;
		}
		public float Height()
		{
			return Top() - Bottom();
		}
		public float Bottom()
		{
			if (v1.y > v2.y)
				return v2.y;
			else
				return v1.y;
		}
		public float Top()
		{
			if (v1.y < v2.y)
				return v2.y;
			else
				return v1.y;
		}
	};

	public Rasterizer( List<float>[,] _buffer, List<Vector3> _vertices, float _pixel_size)
	{
		vertices = _vertices;
		Buffer = _buffer;
		pixel_size = _pixel_size;
		RasterizeSetup();
	}

	private void RasterizeSetup()
	{
		//todo: skip degenerated triangles
		edges = new Edge[3];
		//find bottom most then left most vertex
		int start = 0;
		for (int i = 0; i < vertices.Count; i++)
		{
			if (vertices[start].z > vertices[i].z)
				start = i;
			else if (vertices[start].z == vertices[i].z && vertices[start].y < vertices[i].y)
			{
				start = i;
			}
		}
		
		int p0 = start;
		int p1 = (start + 1) % 3;
		int p2 = (start + 2) % 3;
		
		float max_y = Mathf.Max (vertices [p0].y, vertices [p1].y, vertices [p2].y);
		float min_y = Mathf.Min (vertices [p0].y, vertices [p1].y, vertices [p2].y);
		
		
		edges[0] = new Edge (vertices [p0], vertices [p1]);
		edges[1] = new Edge (vertices [p1], vertices [p2]);
		edges[2] = new Edge (vertices [p2], vertices [p0]);
		
		//swap if CCW
		if (Vector3.Cross(edges[0].Vector(), edges[2].Vector()).x < 0)
		{
			edges[0] = new Edge(vertices[p0], vertices[p2]);
			edges[1] = new Edge(vertices[p2], vertices[p1]);
			edges[2] = new Edge(vertices[p1], vertices[p0]);
		}
		
		bottom_edge = -1;
		left_edges = new int[2];
		
		//find a triangle that bottom edge is horizontal
		if (edges[2].v1.y == edges[2].v2.y)
		{
			bottom_edge = 2;
			left_edges[0] = 0;
		}
		
		//if not a bottom horiz triangle, find one or two left edges
		if (bottom_edge == null) 
		{
			if (vertices[1].z >= vertices[2].z  )
			{
				left_edges[0] = 0;
			}
			else
			{
				left_edges[0] = 0;
				left_edges[1] = 1;
			}
		}
	}

	public int[] sweep( int edge_index, float starth, float endh)
	{

	}

	public int[] sweep2( int edge_index, float starth, float endh)
	{
		float m = endh;
		if (endh > edges [edge_index]) 
		{
			m = edges[edge_index].Top();
		}
		float width = edges [edge_index].Slope () * m - starth;
	}

	public void Rasterize()
	{
		start_edge = 0;
		end_edge = 2;
		float start_h=edges[start_edge].Bottom();
		while (true) 
		{
			float next_h = GetNextScanLine ();
			float delta_h = next_h - start_h;
			float middle_h = next_h;
			if ( next_h > edges[start_edge].Top() )
			{
				middle_h = edges[start_edge].Top();
			}
			if ( next_h > edges[end_edge].Top() )
			{
				middle_h = edges[end_edge].Top();
			}

			float left = edges[start_edge].Slope() * delta_h;
			float right = edges[end_edge].Slope() * delta_h;
			//rasterize start edge for current scanline

			//rasterize end edge for current scanline
			//fill scanline between edge
			//next scanline
		}

	}

	int GetNextEdge(int edge_idx)
	{
		if (edge_idx == 1)
			return -1;
		return 1;
	}

	float GetNextScanLine()
	{
		return scanline % pixel_size + pixel_size;
	}

	int bottom_edge;
	int[] left_edges;
	float scanline;
	List<Vector3> vertices;
	List<float>[,] buffer;
	float pixel_size;
	int start_edge;
	int end_edge;
	Edge[] edges;
}

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

	List<Vector3> GetTriangleVertsByIndex( int index)
	{	
		if (index > faces.Count)
			return null;
		return new List<Vector3>{vertices[faces[index].p1],vertices[faces[index].p2],vertices[faces[index].p3]};

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

	void RasterizeTriangle( List<float>[,] buffer, List<Vector3> vertices, float pixel_size )
	{

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
		int map_size = 256;

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
		Vector3 max_point = aabb [1] - aabb [0];
		float aspect_scale = max_point.y / max_point.z;
		float inv_aspect_scale = 1.0f / aspect_scale;

		for ( int i = 0 ; i < vertices.Count; i++) 
		{
			vertices[i] -= aabb[0];
			if (max_point.z > max_point.y)
				vertices[i].Set (vertices[i].x, vertices[i].y, vertices[i].z * aspect_scale ) ;
			else
				vertices[i].Set (vertices[i].x, vertices[i].y* inv_aspect_scale, vertices[i].z  ) ;
		}

		float pixel_size = (max_point.y) / map_size;

		float hscale = 1.0f / (max_point.x);

		var displacement_tex = new Texture2D (map_size, map_size);
		var disp_pixels = displacement_tex.GetPixels32 ();
		List<float>[,] buffer = new List<float>[map_size,map_size];
		for ( int i = 0; i < faces.Count; i++ )
		{
			RasterizeTriangle( buffer, GetTriangleVertsByIndex(i), pixel_size);
		}
		for ( int x = 0; x < map_size; x++ )
			for ( int y = 0; y < map_size; y++ )
		{
			if ( buffer[x,y] != null && buffer[x,y].Count > 0 )
				disp_pixels[x+y*map_size] = new Color32(255,255,255,255);
			else
				disp_pixels[x+y*map_size] = new Color32(0,255,0,255);
		}
		displacement_tex.SetPixels32 (disp_pixels);
		SaveTextureToFile (displacement_tex, "fff.png");

		Debug.Log("Done");

	 }
}
