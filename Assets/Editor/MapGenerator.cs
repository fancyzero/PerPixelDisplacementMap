using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;


public class TrueImpostorsWindow : EditorWindow
{

	public RenderTexture dest_texture = null;
	public Mesh selected_mesh = null;
	public Texture2D selected_texture = null;
	public Material selected_mat = null;
	public List<Vector3> vertices = new List<Vector3>();
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

		selected_mat = (Material) EditorGUI.ObjectField(new Rect(3, 190, position.width - 6, 190),
			"Select mat", selected_mat, typeof(Material),false);

		selected_texture = (Texture2D) EditorGUI.ObjectField(new Rect(3, 70, position.width - 6, 70),
			"Select Texture", selected_texture, typeof(Texture2D),false);
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


	void SaveTextureToFile( string filename)
	{
		Texture2D t2d = new Texture2D(256,256, TextureFormat.RGB24, false);
		t2d.ReadPixels (new Rect (0, 0, 256, 256), 0, 0);

		var bytes = t2d.EncodeToPNG ();

		var file =  File.Open(Application.dataPath + "/"+filename,FileMode.Create);
		var binary= new BinaryWriter(file);
		binary.Write(bytes);
		file.Close();
	}


	void Generate( Mesh mesh)
	{


		vertices.AddRange( selected_mesh.vertices);
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

		int map_size = 256;
		dest_texture = RenderTexture.GetTemporary (256, 256);
		dest_texture.DiscardContents ();
		Graphics.SetRenderTarget (dest_texture);
		GL.Clear(true, true,new Color(0,1.0f,0));
		GL.PushMatrix ();
		GL.LoadOrtho ();

		//Graphics.Blit (selected_texture, dest_texture);
		var ms = Matrix4x4.TRS(new Vector3 (0.5f, 0.5f, 0.0f), Quaternion.identity, new Vector3(0.4f,0.4f,0.4f));

		Graphics.DrawMeshNow(selected_mesh, ms);
		SaveTextureToFile ("fff2.png");
		new WaitForEndOfFrame ();
		SaveTextureToFile ("fff.png");
		RenderTexture.ReleaseTemporary(dest_texture);
		GL.PopMatrix ();
		return;

		float pixel_size = (max_point.y) / map_size;

		float hscale = 1.0f / (max_point.x);

		var displacement_tex = new Texture2D (map_size, map_size);
		var disp_pixels = displacement_tex.GetPixels32 ();
		List<float>[,] buffer = new List<float>[map_size,map_size];
		for ( int x = 0; x < map_size; x++ )
			for ( int y = 0; y < map_size; y++ )
		{
			if ( buffer[x,y] != null && buffer[x,y].Count > 0 )
				disp_pixels[x+y*map_size] = new Color32(255,255,255,255);
			else
				disp_pixels[x+y*map_size] = new Color32(0,255,0,255);
		}
		displacement_tex.SetPixels32 (disp_pixels);
		SaveTextureToFile ("fff.png");

		Debug.Log("Done");

	 }
}
