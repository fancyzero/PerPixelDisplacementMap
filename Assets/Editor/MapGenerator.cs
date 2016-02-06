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
            return Mathf.Abs((v2 - v1).y);
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
	
    bool InTriangle(float x, float y, List<Vector3> vertices )
    {
        return false;
    }


	void RasterizeTriangle( List<float>[,] buffer, List<Vector3> vertices, float pixel_size )
	{
        //todo: skip degenerated triangles
		Edge[] edges = new Edge[3];
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

        Edge bottom_edge = null;
		Edge[] left_edges = new Edge[2];

        //find a triangle that bottom edge is horizontal
        if (edges[2].v1.y == edges[2].v2.y)
        {
            bottom_edge = edges[2];
            left_edges[0] = edges[0];
        }

        //if not a bottom horiz triangle, find one or two left edges
        if (bottom_edge == null) 
		{
			if (vertices[1].z >= vertices[2].z  )
			{
				left_edges[0] = edges[0];
			}
			else
            {
                left_edges[0] = edges[0];
                left_edges[0] = edges[1];
            }
        }

        //start fill scanline from edge0 to edge2 then edge1
        //until start and end of the scanline mets
        int start_edge = 0;
        int end_edge = 2;
        while(start_edge != end_edge)
        {
            float sk = edges[start_edge].Slope();
            float sh = edges[start_edge].Height();
            float sx = edges[start_edge].v1.z;
            float sy = edges[start_edge].v1.y;

            float ek = edges[end_edge].Slope();
            float eh = edges[end_edge].Height();
            float ex = edges[end_edge].v1.z;
            float ey = edges[end_edge].v1.y;

            Vector2 pus = new Vector2(sx / pixel_size, sy / pixel_size);
            Vector2 pue = new Vector2(ex / pixel_size, ey / pixel_size);

            while (sh > 0 && eh > 0)
            {
                //fill scan line
                while (pus.x < pue.x)
                {
                    //fill pixel
                }
                //move to next line
                pus.x += sk * pixel_size;
                pus.y += pixel_size;
                pue.x += ek * pixel_size;
                pue.y += pixel_size;

            }
        }
	}
	bool pixel_inside_triangle( int x, int y, float pixel_size, List<Vector3> vertices )
	{
		return true;
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
