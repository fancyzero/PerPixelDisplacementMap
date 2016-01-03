__author__ = 'ZhouLin'

from fbx import *
import FbxCommon
from PIL import Image
import bisect



def walkthrough( node, intent ):
    assert isinstance(node, FbxNode)
    print intent + node.GetName() + "'"+ node.GetTypeName() +"'"
    cnt = node.GetChildCount()


    intent = intent + "  "
    for i in range(0, cnt):
        walkthrough(node.GetChild(i), intent)

def pixel_query( mesh, start, dir ):
    verts = mesh.GetControlPoints()

    polygoncount = mesh.GetPolygonCount()
    results = []
    for i in range(polygoncount):
        p1 = mesh.GetPolygonVertex(i,0)
        p2 = mesh.GetPolygonVertex(i,1)
        p3 = mesh.GetPolygonVertex(i,2)
        triangleVerts=[None,None,None]

        triangleVerts[0] = verts[p1]
        triangleVerts[1] = verts[p2]
        triangleVerts[2] = verts[p3]
        r = ray_query(triangleVerts,start, dir)
        if r >=0:
            pos = bisect.bisect_left(results,r)
            results.insert( pos, r)
    if len(results) == 0:
        return results
    #remove results that too close
    i = 0
    while i < len(results)-1:
        if abs(results[i] - results[i+1]) < 0.001:
            del results[i+1]
        else:
            i = i+1
    return results

#cast a ray to the mesh and query all intersections
def ray_query( verts, start, dir ):

    p1 = verts[0]
    p2 = verts[1]
    p3 = verts[2]

    e1 = p2 - p1
    e2 = p3 - p1

    s1 = FbxVector4.CrossProduct( dir, e2 )
    divisor = FbxVector4.DotProduct( s1, e1 )
    if divisor == 0.0:
        return -1
    invDivisor = 1.0 / divisor

    d = start - p1
    b1 = FbxVector4.DotProduct( d, s1) * invDivisor
    if ( b1 < 0 or b1 > 1):
        return -1
    s2 = FbxVector4.CrossProduct(d, e1)
    b2 = FbxVector4.DotProduct( dir, s2) * invDivisor
    if (b2 < 0. or b1 + b2 > 1.):
        return -1
    t = FbxVector4.DotProduct(e2, s2) * invDivisor
    return t

def get_AABB(verts):
    AABBMax = [0,0,0]
    AABBMin = [0,0,0]

    AABBMax[0] = AABBMin[0] = verts[0][0]
    AABBMax[1] = AABBMin[1] = verts[0][1]
    AABBMax[2] = AABBMin[2] = verts[0][2]
    for v in verts:
        if v[0] > AABBMax[0]:
            AABBMax[0] = v[0]
        if v[1] > AABBMax[1]:
            AABBMax[1] = v[1]
        if v[2] > AABBMax[2]:
            AABBMax[2] = v[2]
        if v[0] < AABBMin[0]:
            AABBMin[0] = v[0]
        if v[1] < AABBMin[1]:
            AABBMin[1] = v[1]
        if v[2] < AABBMin[2]:
            AABBMin[2] = v[2]
    return AABBMin, AABBMax
#put triangles into groups by its aabb to speed up ray casting
def group_triangles(mesh, width, height, wstep, hstep):
    triangle_groups = [[ [] for i in range(width)] for j in range(height)]

    verts = mesh.GetControlPoints()

    polygoncount = mesh.GetPolygonCount()

    for i in range(polygoncount):
        p1 = mesh.GetPolygonVertex(i,0)
        p2 = mesh.GetPolygonVertex(i,1)
        p3 = mesh.GetPolygonVertex(i,2)
        tverts=[[p1,p2,p3]]

    AABBMin,AABBMax = get_AABB(tverts)
    #project triangle into y,z plane
    startx = int(AABBMin[1] / wstep)
    starty = int(AABBMin[2] / hstep)
    lengthx = int((AABBMax[1] - AABBMin[1]) / wstep)
    lengthy = int((AABBMax[2] - AABBMin[2]) / hstep)
    for x in range(startx,startx+lengthx):
        for y in range(starty,starty+lengthy):
            triangle_groups[x][y].append((p1,p2,p3))





def generate_map( mesh_node ):
    width = 256
    height = 256

    mesh = mesh_node.GetMesh()
    verts = mesh.GetControlPoints()

    AABBMin, AABBMax = get_AABB(verts)

    wstep = (AABBMax[1]-AABBMin[1])/width
    hstep = (AABBMax[2]-AABBMin[2])/height
    hscale = 1/(AABBMax[0]-AABBMin[0])

    print group_triangles(mesh, width, height, wstep, hstep)


    img = Image.new("RGBA",(width,height),(255,255,255,255))
    pixels = img.load()
    report = 0
    for y in range(height):
        for x in range(width):
            start = FbxVector4( AABBMin[0], AABBMin[1] + x*wstep, AABBMin[2] + y*hstep,0 )
            dir = FbxVector4(1,0,0,0)
            r = pixel_query(mesh, start, dir)
            color=[255,255,255,255]

            for c in range(len(r)):
                if c >=4:
                    print "too many intersections", r
                    break
                color[c] = int(r[c]*hscale*255)
            pixels[x,y]=(color[0],color[1],color[2],color[3])
            if ( x + y * width) / float(width*height) > report:
                print report
                report = report + 0.01

    fp = open("img.bmp","wb")
    img.show()
    img.save(fp)




sdk_manager, scene = FbxCommon.InitializeSdkObjects()
converter = FbxCommon.FbxGeometryConverter(sdk_manager)
FbxCommon.LoadScene(sdk_manager, scene, "/Users/FancyZero/PerPixelDisplacementMapGenerator/Monkey.fbx" )
converter.Triangulate(scene,False)
root = scene.GetRootNode()
mesh = root.GetChild(0)

attr_type = mesh.GetNodeAttribute().GetAttributeType()
if attr_type==FbxCommon.FbxNodeAttribute.eMesh:
    generate_map( mesh )










