__author__ = 'ZhouLin'

import sys
from fbx import *
import FbxCommon
from PIL import Image
import bisect
import os


def walk_through(node, intent="  "):
    """
    walk through all child nodes and print information
    :param node: root node
    :param intent:
    """
    assert isinstance(node, FbxNode)
    print intent + node.GetName() + "'" + node.GetTypeName() + "'"
    cnt = node.GetChildCount()

    intent = intent + "  "
    for i in range(0, cnt):
        walk_through(node.GetChild(i), intent)

def pixel_query(mesh, start, dir, triangle_groups, group_width, group_height):
    """
    cast a ray to the mesh and query all intersections

    :param mesh: the triangle mesh
    :param start: ray origin
    :param dir: ray direction
    :param triangle_groups: triangle groups for speed up the process
    :param group_width:
    :param group_height:
    :return: list of intersection points along the ray
    """
    results = []
    verts = mesh.GetControlPoints()
    x = int(start[1] / group_width)
    y = int(start[2] / group_height)
    triangles = triangle_groups[x][y]
    if len(triangles) == 0:
        return results
    polygoncount = mesh.GetPolygonCount()
    for i in (triangles):
        triangleVerts = [None, None, None]
        triangleVerts[0] = verts[i[0]]
        triangleVerts[1] = verts[i[1]]
        triangleVerts[2] = verts[i[2]]
        r = ray_query(triangleVerts, start, dir)
        if r >= 0:
            pos = bisect.bisect_left(results, r)
            results.insert(pos, r)
    if len(results) == 0:
        return results

    if len(results) == 0 and start[1] > 0.5 and start[2] > 0.5:
        return results
    # remove results that too close
    i = 0
    while i < len(results) - 1:
        if abs(results[i] - results[i + 1]) < 0.001:
            del results[i + 1]
        else:
            i = i + 1
    return results



def ray_query(verts, start, dir):
    """
    ray vs triangle intersection
    :param verts: 3 vertices that makes a triangle
    :param start: ray origin
    :param dir: ray dir
    :return: intersection point along the ray
    """
    p1 = verts[0]
    p2 = verts[1]
    p3 = verts[2]

    e1 = p2 - p1
    e2 = p3 - p1

    s1 = FbxVector4.CrossProduct(dir, e2)
    divisor = FbxVector4.DotProduct(s1, e1)
    if divisor == 0.0:
        return -1
    invDivisor = 1.0 / divisor

    d = start - p1
    b1 = FbxVector4.DotProduct(d, s1) * invDivisor
    if (b1 < 0 or b1 > 1):
        return -1
    s2 = FbxVector4.CrossProduct(d, e1)
    b2 = FbxVector4.DotProduct(dir, s2) * invDivisor
    if (b2 < 0. or b1 + b2 > 1.):
        return -1
    t = FbxVector4.DotProduct(e2, s2) * invDivisor
    return t


def get_AABB(verts, extent=0):
    """
    :rtype: tuple
    """
    x = [v[0] for v in verts]
    y = [v[1] for v in verts]
    z = [v[2] for v in verts]
    max_point = [max(x), max(y), max(z)]
    min_point = [min(x), min(y), min(z)]
    min_point = map(lambda x: x - extent, min_point)
    max_point = map(lambda x: x + extent, max_point)
    return min_point, max_point


# put triangles into groups by its aabb to speed up ray casting
def group_triangles(mesh, group_row, group_col, group_width, group_height, max_point):
    triangle_groups = [[[] for i in range(group_col)] for j in range(group_row)]

    print "group size = ", group_width, group_height
    verts = mesh.GetControlPoints()
    polygoncount = mesh.GetPolygonCount()

    for i in range(polygoncount):
        p1 = mesh.GetPolygonVertex(i, 0)
        p2 = mesh.GetPolygonVertex(i, 1)
        p3 = mesh.GetPolygonVertex(i, 2)
        tverts = [verts[p1], verts[p2], verts[p3]]

        p_min, p_max = get_AABB(tverts)
        # project triangle into y,z plane
        startx = int(p_min[1] / group_width)
        starty = int(p_min[2] / group_height)
        endx = int(p_max[1] / group_width)
        endy = int(p_max[2] / group_height)
        for x in range(startx, endx + 1):
            for y in range(starty, endy + 1):
                triangle_groups[x][y].append((p1, p2, p3))

    return triangle_groups


def generate_map(mesh_node, save_path):
    width = 32
    height = 32

    mesh = mesh_node.GetMesh()
    # transform all vertices , so they are all larger then 0,0,0
    verts = mesh.GetControlPoints()
    AABBMin, AABBMax = get_AABB(verts,
                                0.1)  # get an AABB that slightly bigger than the actual one, to avoid some conner case
    for i in range(len(verts)):
        newv = FbxVector4(verts[i][0] - AABBMin[0], verts[i][1] - AABBMin[1], verts[i][2] - AABBMin[2], 0)
        mesh.SetControlPointAt(newv, i)
    verts = mesh.GetControlPoints()
    # update bounding box as well
    MaxPoint = map(lambda x, y: x - y, AABBMax, AABBMin)

    wstep = (MaxPoint[1]) / width
    hstep = (MaxPoint[2]) / height
    hscale = 1 / (MaxPoint[0])

    triangle_groups = group_triangles(mesh, width, height, wstep, hstep, MaxPoint)

    img = Image.new("RGBA", (width, height), (255, 255, 255, 255))
    pixels = img.load()
    progress_report = 0
    for y in range(height):
        for x in range(width):
            start = FbxVector4(0, x * wstep, y * hstep, 0)
            dir = FbxVector4(1, 0, 0, 0)
            r = pixel_query(mesh, start, dir, triangle_groups, wstep, hstep)
            color = [255, 255, 255, 255]

            for c in range(len(r)):
                if c >= 4:
                    print "too many intersections for ray: ", start[0], start[1], start[2]
                    break
                color[c] = int(r[c] * hscale * 255)
            pixels[x, y] = (color[0], color[1], color[2], color[3])
            if (x + y * width) / float(width * height) > progress_report:
                print "\r{0:.0f}%".format(progress_report * 100)
                progress_report = progress_report + 0.01

    fp = open(save_path, "wb")
    img.save(fp)
    print "image saved to " + save_path
    img.show()


file_path = sys.argv[1]
save_path = os.path.splitext(os.path.abspath(file_path))[0] + ".tga"

sdk_manager, scene = FbxCommon.InitializeSdkObjects()
converter = FbxCommon.FbxGeometryConverter(sdk_manager)
FbxCommon.LoadScene(sdk_manager, scene, file_path)
converter.Triangulate(scene, False)

print "=== scene graph start ========"
root = scene.GetRootNode()
walk_through(root, "  ")
print "=== scene graph end   ========"
mesh = root.GetChild(0)
attr_type = mesh.GetNodeAttribute().GetAttributeType()
if attr_type == FbxCommon.FbxNodeAttribute.eMesh:
    generate_map(mesh, save_path)
