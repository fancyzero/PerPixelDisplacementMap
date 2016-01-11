import sys
from fbx import *
import FbxCommon
from PIL import Image
import bisect
import os

__author__ = 'ZhouLin'


def walk_through(node, intent="  "):
    """
    walk through all child nodes and print information
    :param node: root node
    :param intent:
    """
    assert isinstance(node, FbxNode)
    print intent + node.GetName() + "'" + node.GetTypeName() + "'"
    cnt = node.GetChildCount()

    intent += "  "
    for i in range(0, cnt):
        walk_through(node.GetChild(i), intent)


class QueryResult:
    def __init__(self):
        self.t = 0  # intersect point
        self.uv = (0, 0)
        self.triangleIndex = -1


def pixel_query(in_mesh, ray_start, ray_dir, triangle_groups, group_width, group_height, hscale):
    """
    cast a ray to the mesh and query all intersections

    :param in_mesh: the triangle mesh
    :param ray_start: ray origin
    :param ray_dir: ray direction
    :param triangle_groups: triangle groups for speed up the process
    :param group_width:
    :param group_height:
    :return: list of intersection points along the ray
    """
    results = []
    mesh_control_points = in_mesh.GetControlPoints()
    x = int(ray_start[1] / group_width)
    y = int(ray_start[2] / group_height)
    triangles = triangle_groups[x][y]
    if len(triangles) == 0:
        return results
    for i in triangles:
        triangle_vertices = [None, None, None]

        triangle_vertices[0] = mesh_control_points[in_mesh.GetPolygonVertex(i, 0)]
        triangle_vertices[1] = mesh_control_points[in_mesh.GetPolygonVertex(i, 1)]
        triangle_vertices[2] = mesh_control_points[in_mesh.GetPolygonVertex(i, 2)]

        t, uv = ray_query(triangle_vertices, ray_start, ray_dir)
        if t >= 0:
            qr = QueryResult()
            qr.t = t
            qr.uv = uv
            qr.triangleIndex = i
            results.append(qr)

    if len(results) == 0:
        return results

    if len(results) == 0 and ray_start[1] > 0.5 and ray_start[2] > 0.5:
        return results
    # remove results that too close
    results.sort(key=lambda item: item.t)
    i = 0
    if len(results) % 2 == 1:
        print "bad query"


    if len(results) % 2 == 1:
        results.append(results[-1])
        results[-1].t+=0.02
    return results


def ray_query(vertices, ray_start, ray_dir):
    """
    ray vs triangle intersection
    :param vertices: 3 vertices that makes a triangle
    :param ray_start: ray origin
    :param ray_dir: ray dir
    :return: intersection point along the ray, uv
    """
    p1 = vertices[0]
    p2 = vertices[1]
    p3 = vertices[2]

    e1 = p2 - p1
    e2 = p3 - p1

    s1 = FbxVector4.CrossProduct(ray_dir, e2)
    divisor = FbxVector4.DotProduct(s1, e1)
    if divisor == 0.0:
        return -1, (0, 0)
    inv_divisor = 1.0 / divisor

    d = ray_start - p1
    u = FbxVector4.DotProduct(d, s1) * inv_divisor
    if u < 0 or u > 1:
        return -1, (0, 0)
    s2 = FbxVector4.CrossProduct(d, e1)
    v = FbxVector4.DotProduct(ray_dir, s2) * inv_divisor
    if v < 0. or u + v > 1.:
        return -1, (0, 0)
    t = FbxVector4.DotProduct(e2, s2) * inv_divisor

    return t, (u, v)


def get_aabb(vertices, extent=0):
    """
    :param vertices:
    :param extent:
    :return:
    :rtype: tuple
    """
    x = [v[0] for v in vertices]
    y = [v[1] for v in vertices]
    z = [v[2] for v in vertices]
    max_point = [max(x), max(y), max(z)]
    min_point = [min(x), min(y), min(z)]
    min_point = map(lambda x: x - extent, min_point)
    max_point = map(lambda x: x + extent, max_point)
    return min_point, max_point


# put triangles into groups by its aabb to speed up ray casting
def group_triangles(in_mesh, group_row, group_col, group_width, group_height, max_point):
    triangle_groups = [[[] for i in range(group_col)] for j in range(group_row)]

    print "group size = ", group_width, group_height
    verts = in_mesh.GetControlPoints()
    polygoncount = in_mesh.GetPolygonCount()

    for i in range(polygoncount):
        p1 = in_mesh.GetPolygonVertex(i, 0)
        p2 = in_mesh.GetPolygonVertex(i, 1)
        p3 = in_mesh.GetPolygonVertex(i, 2)
        tverts = [verts[p1], verts[p2], verts[p3]]

        p_min, p_max = get_aabb(tverts)
        # project triangle into y,z plane
        startx = int(p_min[1] / group_width)
        starty = int(p_min[2] / group_height)
        endx = int(p_max[1] / group_width)
        endy = int(p_max[2] / group_height)
        for x in range(startx, endx + 1):
            for y in range(starty, endy + 1):
                triangle_groups[x][y].append(i)

    return triangle_groups


def generate_map(mesh_node, save_path):
    width = 256
    height = 256

    mesh = mesh_node.GetMesh()
    verts = mesh.GetControlPoints()
    layer = mesh.GetLayer(0)
    normals = layer.GetNormals().GetDirectArray()

    # transform all vertices , so they are all larger then 0,0,0

    AABBMin, AABBMax = get_aabb(verts,
                                0.05)  # get an AABB that slightly bigger than the actual one, to avoid some conner case
    for i in range(len(verts)):
        newv = FbxVector4(verts[i][0] - AABBMin[0], verts[i][1] - AABBMin[1], verts[i][2] - AABBMin[2], 0)
        mesh.SetControlPointAt(newv, i)
    verts = mesh.GetControlPoints()
    # update bounding box as well
    MaxPoint = map(lambda x, y: x - y, AABBMax, AABBMin)

    wstep = (MaxPoint[1]) / width
    hstep = (MaxPoint[2]) / height
    hscale = 1 / (MaxPoint[0])
    print hscale,MaxPoint
    triangle_groups = group_triangles(mesh, width, height, wstep, hstep, MaxPoint)

    img = Image.new("RGBA", (width, height), (255, 255, 255, 255))
    normal_x_map = Image.new("RGBA", (width, height), (255, 255, 255, 255))
    normal_y_map = Image.new("RGBA", (width, height), (255, 255, 255, 255))
    pixels = img.load()
    normal_x_pixels = normal_x_map.load()
    normal_y_pixels = normal_y_map.load()
    progress_report = 0
    for y in range(height):
        for x in range(width):
            start = FbxVector4(0, x * wstep, y * hstep, 0)
            dir = FbxVector4(1, 0, 0, 0)
            query_results = pixel_query(mesh, start, dir, triangle_groups, wstep, hstep, hscale)
            color = [255, 255, 255, 255]
            normal_x = [0,0,0,0]
            normal_y = [0,0,0,0]

            for c in range(len(query_results)):
                if c >= 4:
                    print "too many (", c , ") intersections for ray: ", start[0], start[1], start[2]
                    break
                r = query_results[c]
                u = r.uv[0]
                v = r.uv[1]
                w = 1 - (u + v)
                uv1 = FbxVector2()
                uv2 = FbxVector2()
                uv3 = FbxVector2()
                mesh.GetPolygonVertexUV(r.triangleIndex, 0, "UVMap", uv1)
                mesh.GetPolygonVertexUV(r.triangleIndex, 1, "UVMap", uv2)
                mesh.GetPolygonVertexUV(r.triangleIndex, 2, "UVMap", uv3)

                '''Normals'''
                n1 = normals[r.triangleIndex * 3]
                n2 = normals[r.triangleIndex * 3 + 1]
                n3 = normals[r.triangleIndex * 3 + 2]

                normal_x[c] = int(((w * n1[0] + u * n2[0] + v * n3[0])+1)/2*255)
                normal_y[c] = int(((w * n1[1] + u * n2[1] + v * n3[1])+1)/2*255)

                color[c] = int(query_results[c].t * hscale * 255)

            pixels[x, y] = (color[0], color[1], color[2], color[3])

            normal_x_pixels[x, y] = (normal_x[0], normal_x[1], normal_x[2], normal_x[3])
            normal_y_pixels[x, y] = (normal_y[0], normal_y[1], normal_y[2], normal_y[3])

            if (x + y * width) / float(width * height) > progress_report:
                print "\r{0:.0f}%".format(progress_report * 100)
                progress_report += 0.01

    fp = open(save_path + "_Displace.bmp", "wb")
    img.save(fp)
    fp.close()
    fp = open(save_path + "_NormalX.bmp", "wb")
    normal_x_map.save(fp)
    fp.close()
    fp = open(save_path + "_NormalY.bmp", "wb")
    normal_y_map.save(fp)
    fp.close()

    print "images saved to " + os.path.split(save_path)[0]
    img.show()


file_path = sys.argv[1]
print file_path
save_path = os.path.splitext(os.path.abspath(file_path))[0]

sdk_manager, scene = FbxCommon.InitializeSdkObjects()
converter = FbxCommon.FbxGeometryConverter(sdk_manager)
FbxCommon.LoadScene(sdk_manager, scene, file_path)
print "Triangulating..."
converter.Triangulate(scene, False)
print "Triangulated"

print "=== scene graph start ========"
root = scene.GetRootNode()
walk_through(root, "  ")
print "=== scene graph end   ========"
mesh = root.GetChild(0)
attr_type = mesh.GetNodeAttribute().GetAttributeType()
if attr_type == FbxCommon.FbxNodeAttribute.eMesh:
    generate_map(mesh, save_path)
