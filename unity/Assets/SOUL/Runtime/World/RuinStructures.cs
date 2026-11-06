using UnityEngine;
using System.Collections.Generic;

namespace Soul
{
    // Author both meshes and elevation data in sim space; Build alone mirrors to Unity.
    public static class RuinStructures
    {
        const int Concrete = 0x8f8b86, Weathered = 0x7d7a75, Dark = 0x6a6762;
        const int Interior = 0x4c4945, Char = 0x252728, Rust = 0x78432c;
        const int Steel = 0x44494b, Green = 0x5e7470, Ochre = 0x8a6a4a;
        const float Storey = 3.2f;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => MeshBuilder.Hex(hex);

        // MeshBuilder's random stream also determines some collision dimensions.
        // Decorative work must never advance that stream (including Box face jitter).
        internal static void Detail(MeshBuilder b, int seed, System.Action draw)
        {
            var layoutRandom = b.rng;
            b.rng = new System.Random(seed);
            try { draw(); }
            finally { b.rng = layoutRandom; }
        }

        // Two-sided sheet metal / flattened rebar, legible at the gameplay camera
        // without spending a capped cylinder on every narrow decorative member.
        static void Strip(MeshBuilder b, Vector3 a, Vector3 end, float width, int hex,
            bool vertical = false, bool topOnly = false)
        {
            Vector3 side=Vector3.Cross(end-a,vertical?Vector3.forward:Vector3.up).normalized*width*.5f;
            b.Quad(a-side,a+side,end+side,end-side,C(hex));
            if(!topOnly) b.Quad(end-side,end+side,a+side,a-side,C(hex));
        }

        // A chipped extrusion with a very narrow intact bearing lip. Notches are
        // limited to 4 cm at the walking plane; deeper spalls sit below that plane.
        static void ChippedSlab(MeshBuilder b, float width, float depth, float top, float thickness)
        {
            float x = width * .5f, z = depth * .5f;
            if(width<1.2f || depth<.85f)
            {
                // Tiny fragments keep the surface rectangle, with a sheared base.
                b.Quad(V(-x,top,-z),V(-x,top,z),V(x,top,z),V(x,top,-z),C(Concrete));
                var corners=new[]{V(-x,top,-z),V(-x,top,z),V(x,top,z),V(x,top,-z)};
                for(int i=0;i<4;i++)
                {
                    Vector3 a=corners[i], c=corners[(i+1)%4];
                    b.Quad(V(a.x*.82f,top-thickness,a.z*.82f),V(c.x*.82f,top-thickness,c.z*.82f),c,a,C(i%2==0?Dark:Weathered));
                }
                return;
            }
            var rim = new[] { V(-x, top, -z), V(-x, top, -.35f*z),
                V(-x+.04f, top, -.25f*z), V(-x, top, .1f*z), V(-x, top, z),
                V(-.3f*x, top, z), V(-.15f*x, top, z-.04f), V(.1f*x, top, z),
                V(x, top, z), V(x, top, .2f*z), V(x-.04f, top, 0), V(x, top, -.3f*z),
                V(x, top, -z), V(.2f*x, top, -z), V(0, top, -z+.04f), V(-.2f*x, top, -z) };
            for (int i = 0; i < rim.Length; i++)
            {
                int j = (i + 1) % rim.Length;
                Vector3 a = rim[i], c = rim[j];
                float inset = i % 4 == 1 ? .78f : .97f;
                Vector3 lo = V(a.x * inset, top - thickness, a.z * inset);
                Vector3 next = V(c.x * inset, top - thickness, c.z * inset);
                b.Tri(V(0, top, 0), a, c, C(i % 4 == 1 ? Weathered : Concrete));
                b.Quad(lo, next, c, a, C(i % 3 == 0 ? Interior : Dark));
            }
            b.Quad(V(-x*.78f,top-thickness,-z*.78f),V(x*.78f,top-thickness,-z*.78f),
                V(x*.78f,top-thickness,z*.78f),V(-x*.78f,top-thickness,z*.78f),C(Interior));
            if (width < 1.2f || depth < .85f) return;
            for (int side = 0; side < 4; side++)
            {
                b.Push(); b.RotateY(side * Mathf.PI * .5f);
                float edge = side % 2 == 0 ? x : z;
                for (int k = 0; k < 2; k++)
                {
                    Vector3 a = V(edge-.12f, top-.12f, k*.14f-.07f);
                    Vector3 bend = a + V(.24f, -.015f, 0);
                    Strip(b,a,bend,.034f,Rust,topOnly:true);
                    Strip(b,bend,bend+V(.12f,-.1f,.05f),.028f,Rust,topOnly:true);
                }
                b.Pop();
            }
        }

        // Independent, angular fragments. The supplied height field is sampled at
        // EVERY corner, so even steep rubble stays within 0.18 m of its route.
        internal static void Fragments(MeshBuilder b, float width, float depth,
            System.Func<float, float, float> surface, int columns, int rows, bool round = false)
        {
            for (int j = 0; j < rows; j++) for (int i = 0; i < columns; i++)
            {
                float x = ((i+.5f+b.Range(-.05f,.05f))/columns-.5f)*width;
                float z = ((j+.5f+b.Range(-.05f,.05f))/rows-.5f)*depth;
                float dx = width/columns*b.Range(.39f,.45f), dz = depth/rows*b.Range(.36f,.45f);
                if (round && (Mathf.Abs(x)+dx)*(Mathf.Abs(x)+dx) +
                    (Mathf.Abs(z)+dz)*(Mathf.Abs(z)+dz) > width*width*.25f) continue;
                float lift = b.Range(.035f, .15f);
                Vector3 P(float u, float v, float h) => V(u, surface(u,v)+h, v);
                Vector3 a = P(x-dx, z-dz*b.Range(.35f,.9f), lift*.4f);
                Vector3 c = P(x+dx*b.Range(.35f,.9f),z-dz,lift);
                Vector3 d = P(x+dx,z+dz*b.Range(.35f,.9f),lift*.65f);
                Vector3 e = P(x-dx*b.Range(.35f,.9f),z+dz,lift*.8f);
                Color color = b.Jitter(b.Chance(.14f) ? 0x79503f : b.Chance(.3f) ? Dark : Concrete);
                b.Quad(a,e,d,c,color);
                var corners = new[] { a,e,d,c };
                for (int k=0;k<4;k++)
                {
                    Vector3 u=corners[k], v=corners[(k+1)%4];
                    b.Quad(P(u.x,u.z,-.06f),P(v.x,v.z,-.06f),v,u,C(Interior));
                }
                if ((i+j*columns)%11 == 0)
                {
                    Vector3 root=P(x,z,.12f), bend=P(x+dx*.7f,z,.17f);
                    b.Beam(root,bend,.017f,.015f,3,C(Rust));
                    b.Beam(bend,P(x+dx*.9f,z+dz*.5f,.1f),.015f,.01f,3,C(Rust));
                }
            }
        }

        // Broken frames and stains on both faces of an otherwise empty window.
        static void WindowDetail(MeshBuilder b, float x, float z, float bottom, float width, int index)
        {
            float left=x-width*.5f, right=x+width*.5f;
            Strip(b,V(left,bottom+.85f,z),V(left,bottom+2.45f,z),.045f,Char,true);
            Strip(b,V(right,bottom+1.25f,z),V(right,bottom+2.45f,z),.045f,Steel,true);
            Vector3 a=V(left,bottom+1.62f,z), c=V(right-.18f,bottom+1.3f,z);
            Strip(b,a,c,.04f,Rust,true);
            for (int side=-1;side<=1;side+=2)
            {
                float face=z+side*.158f;
                void Patch(float px,float y,float w,float h,int color)
                {
                    Vector3 p0=V(px,y,face), p1=V(px+w,y+.035f,face);
                    Vector3 p2=V(px+w*.8f,y+h,face), p3=V(px+.03f,y+h*.92f,face);
                    if(side==1) b.Quad(p0,p1,p2,p3,C(color)); else b.Quad(p3,p2,p1,p0,C(color));
                }
                Patch(left+.08f,bottom+2.51f,.2f,.43f,Char);
                Patch(left+.1f,bottom+.12f,width*.56f,.42f,index%2==0?Green:Ochre);
                Patch(right-.25f,bottom+.03f,.18f,.27f,0x5f6650);
                Patch(right-.45f,bottom+.08f,.07f,.68f,0x565953);
                Vector3 g0=V(left+.03f,bottom+.87f,z+side*.04f);
                Vector3 g1=g0+V(.42f,0,0), g2=g0+V(.08f,.39f,0);
                if(side==1) b.Tri(g0,g1,g2,C(0x263b3e)); else b.Tri(g2,g1,g0,C(0x263b3e));
            }
        }

        // Flattened, collapsed furniture: all pieces are <= 0.22 m above a floor.
        static void RoomClutter(MeshBuilder b, float x, float z, float floor, int kind)
        {
            b.Push(); b.Translate(x,floor,z);
            if(kind%3==0) // overturned table, folded legs
            {
                b.Box(V(0,.07f,0),V(.85f,.12f,.65f),C(Green));
                for(int i=-1;i<=1;i+=2)
                    b.Beam(V(i*.3f,.13f,-.23f),V(i*.15f,.21f,.2f),.03f,.025f,3,C(Rust));
            }
            else if(kind%3==1) // crushed cabinet and separated drawer fronts
            {
                b.Box(V(0,.09f,0),V(.75f,.18f,.55f),C(Interior));
                for(int i=0;i<3;i++) b.Box(V(-.24f+i*.24f,.19f,.03f),V(.19f,.025f,.48f),C(Ochre));
            }
            else // collapsed bed frame, no standing headboard
            {
                for(int side=-1;side<=1;side+=2)
                    b.Box(V(side*.4f,.08f,0),V(.045f,.08f,1.3f),C(Rust));
                for(int i=0;i<5;i++) b.Box(V(0,.08f,(i-2)*.26f),V(.8f,.035f,.035f),C(Steel));
            }
            for(int i=0;i<5;i++)
            {
                float px=b.Range(-.55f,.55f), pz=b.Range(-.55f,.55f);
                b.Quad(V(px,.014f,pz),V(px,.014f,pz+.11f),V(px+.15f,.02f,pz+.1f),V(px+.17f,.02f,pz),C(0xaaa9a0));
            }
            b.Push(); b.Translate(.52f,.045f,.35f); b.Scale(.35f,.06f,.3f);
            b.Rock(Vector3.zero,1,C(Concrete),.15f,1,0); b.Pop();
            b.Pop();
        }

        static void Exterior(MeshBuilder b, float floor, int level)
        {
            // The fire escape is a torn, folded balcony on the OUTSIDE of the
            // rear wall, not a new walkable platform over the circulation strip.
            float z=5.32f;
            b.Box(V(4.5f,floor+.03f,z),V(1.35f,.06f,.44f),C(Rust));
            Strip(b,V(3.85f,floor+.12f,z+.2f),V(5.15f,floor+.22f,z+.23f),.05f,Rust);
            for(int i=0;i<4;i++)
                Strip(b,V(3.9f+i*.4f,floor+.04f,z+.2f),V(3.87f+i*.4f,floor+.21f,z+.22f),.04f,Steel,true);
            // A broken ladder hangs on the facade, clear of slab coverage.
            for(int side=-1;side<=1;side+=2)
                Strip(b,V(4.5f+side*.28f,floor+.3f,5.22f),V(4.5f+side*.28f,floor+2.5f,5.22f),.05f,Rust,true);
            for(int i=0;i<6;i++)
                Strip(b,V(4.22f,floor+.4f+i*.34f,5.22f),V(4.78f,floor+.4f+i*.34f,5.22f),.04f,Steel,true);
            // AC housing is on the exterior; louvres read in silhouette from above.
            b.Box(V(-3.2f,floor+2.3f,-5.34f),V(.8f,.42f,.4f),C(Weathered));
            for(int i=0;i<4;i++) Strip(b,V(-3.52f,floor+2.17f+i*.08f,-5.548f),V(-2.88f,floor+2.17f+i*.08f,-5.548f),.025f,Char,true);
            Vector3 last=V(-6.6f,floor+2.7f,5.23f);
            for(int i=1;i<=4;i++)
            {
                float t=i/4f;
                Vector3 next=V(-6.6f+t*3.1f,floor+2.7f-.55f*4*t*(1-t),5.23f);
                Strip(b,last,next,.024f,Char,true); last=next;
            }
            // Rusted duct at the rear ceiling line, against the existing wall.
            b.Beam(V(-.8f,floor+2.76f,4.78f),V(3.5f,floor+2.76f,4.78f),.085f,.085f,5,C(Rust));
        }

        static void HangingLamp(MeshBuilder b, float x, float z, float floor)
        {
            // Exterior broken fixture; no uncollided post stands on the deck.
            Vector3 root=V(x,floor-.15f,z), bend=root+V(.16f,-1.05f,.28f);
            b.Beam(root,bend,.055f,.045f,4,C(Rust));
            b.Beam(bend,bend+V(.35f,-.3f,.15f),.045f,.035f,4,C(Steel));
            b.Box(bend+V(.35f,-.36f,.15f),V(.52f,.12f,.27f),C(Char));
            b.Beam(root+V(.08f,0,0),bend+V(.35f,-.4f,.15f),.013f,.013f,3,C(Char));
        }

        static void Joint(MeshBuilder b, float x, float z, float width, float top)
        {
            b.Box(V(x,top+.004f,z),V(width,.008f,.045f),C(Char));
            b.Box(V(x,top+.006f,z+.06f),V(width,.009f,.025f),C(Weathered));
        }

        static PropModel Finish(MeshBuilder b, PropModel p, string name, float footprint)
        {
            p.mesh = b.Build(name);
            var bounds = p.mesh.bounds;
            p.footprint = Mathf.Max(footprint, Mathf.Max(
                Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)),
                Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z))));
            p.height = p.mesh.bounds.max.y;
            return p;
        }

        // A slab's supplied height is its TOP, never its centre. Missing rectangles
        // really are holes: no large invisible catch-all surface spans them.
        internal static void Floor(MeshBuilder b, PropModel p, float x, float z,
            float width, float depth, float top, int gate = 0, float thickness = .24f)
        {
            // Preserve the seven random draws of the original jittered Box.
            b.Jitter(Concrete);
            for (int i=0;i<6;i++) b.Range(-.06f,.06f);
            Detail(b, 41, () =>
            {
                b.Push(); b.Translate(x,0,z);
                ChippedSlab(b,width,depth,top,thickness);
                b.Pop();
            });
            p.surfaces.Add(PropSurface.Box(x, z, width * .5f, depth * .5f, top, gate: gate));
        }

        static void Solid(MeshBuilder b, PropModel p, float x, float z, float width,
            float depth, float bottom, float height, int hex = Dark)
        {
            b.Box(V(x, bottom + height * .5f, z), V(width, height, depth), b.Jitter(hex));
            if (width >= .5f && width <= .9f && depth <= 1.1f && height > 2)
                Detail(b, 73, () =>
                {
                    // Surface spall and exposed steel lie inside the existing pier.
                    float face=z-depth*.5f-.003f;
                    b.Quad(V(x-width*.35f,bottom+.4f,face),V(x+width*.3f,bottom+.6f,face),
                        V(x+width*.23f,bottom+1.6f,face),V(x-width*.3f,bottom+1.3f,face),C(Interior));
                    for(int i=-1;i<=1;i+=2)
                        b.Beam(V(x+i*width*.16f,bottom+.55f,face),V(x+i*width*.14f,bottom+1.4f,face),.016f,.013f,3,C(Rust));
                });
            p.colliders.Add(PropCollider.Rect(x, z, width * .5f, depth * .5f).Between(bottom, bottom + height));
        }

        // Sloping extrusion, not a rotated box (whose top would be vertically offset).
        // Local +Z rises, exactly like PropSurface.Ramp. Stairs use the same nosing
        // line; the solver intentionally smooths their <= 0.16 m individual risers.
        internal static void Flight(MeshBuilder b, PropModel p, float x, float z,
            float width, float run, float low, float high, float yaw = 0,
            bool stairs = false, int gate = 0, int hex = Weathered)
        {
            float a = width * .5f, d = run * .5f, t = .22f;
            b.Push(); b.Translate(x, 0, z); b.RotateY(yaw);
            if (stairs)
            {
                int n = Mathf.CeilToInt(Mathf.Abs(high - low) / .16f);
                for (int i = 0; i < n; i++)
                {
                    float y0 = Mathf.Lerp(low, high, i / (float)n);
                    float y1 = Mathf.Lerp(low, high, (i + 1f) / n);
                    b.Box(V(0, (y0 + y1 - t) * .5f, -d + (i + .5f) * run / n),
                        V(width, y1 - y0 + t, run / n), b.Jitter(hex, .035f));
                }
            }
            else
            {
                Vector3 l0 = V(-a, low, -d), r0 = V(a, low, -d);
                Vector3 l1 = V(-a, high, d), r1 = V(a, high, d), down = V(0, -t, 0);
                b.Quad(l0, l1, r1, r0, C(hex));
                b.Quad(r0 + down, r1 + down, l1 + down, l0 + down, C(Interior));
                b.Quad(l0 + down, l1 + down, l1, l0, C(Dark));
                b.Quad(r1 + down, r0 + down, r0, r1, C(Dark));
                b.Quad(r0 + down, l0 + down, l0, r0, C(Dark));
                b.Quad(l1 + down, r1 + down, r1, l1, C(Dark));
            }
            if (!stairs)
                Detail(b, 109, () =>
                {
                    float Y(float px,float pz) => Mathf.Lerp(low,high,(pz+d)/run);
                    if (hex == Green || hex == Ochre)
                    {
                        // Frayed hanging valance stays below its registered awning.
                        for(int i=0;i<7;i++)
                        {
                            float px=-a+(i+.2f)*width/7;
                            b.Tri(V(px,low,-d),V(px+width/9,low,-d),
                                V(px+width/14,low-(i%3==0?.28f:.12f),-d-.06f),C(i%2==0?hex:Interior));
                            b.Beam(V(px,low-.08f,-d),V(px,high-.08f,d),.025f,.025f,3,C(Rust));
                        }
                    }
                    else Fragments(b,width,run,Y,Mathf.Clamp(Mathf.CeilToInt(width/.65f),3,10),
                        Mathf.Clamp(Mathf.CeilToInt(run/.65f),4,12));
                });
            b.Pop();
            p.surfaces.Add(PropSurface.Ramp(x, z, a, d, low, high, yaw, gate));
        }

        // A recessed fine substrate retains the exact paraboloid; discrete masonry
        // above it supplies the silhouette instead of colouring a smooth dome.
        internal static void Mound(MeshBuilder b, PropModel p, float radius, float height,
            int hex, int rings = 10, int segments = 32)
        {
            Vector3 Point(int ring, int sector)
            {
                float r = radius * ring / rings, a = sector * 2 * Mathf.PI / segments;
                float recess = hex == Dark || hex == 0x6f6a64 ? .035f : 0;
                return V(Mathf.Sin(a) * r, height * (1 - r * r / (radius * radius)) - recess, Mathf.Cos(a) * r);
            }
            for (int j = 0; j < segments; j++)
                b.Tri(V(0, height - (hex == Dark || hex == 0x6f6a64 ? .035f : 0), 0), Point(1, j), Point(1, j + 1), b.Jitter(hex, .045f));
            for (int i = 1; i < rings; i++) for (int j = 0; j < segments; j++)
            {
                // Concrete facets are separate broken pieces, interspersed with soot;
                // sand keeps restrained colour changes over the same sampled dome.
                if (hex == Dark)
                {
                    b.Tri(Point(i, j), Point(i + 1, j), Point(i + 1, j + 1),
                        b.Jitter(b.Chance(.12f) ? Char : Concrete, .13f));
                    b.Tri(Point(i, j), Point(i + 1, j + 1), Point(i, j + 1), b.Jitter(Dark, .13f));
                }
                else b.Quad(Point(i, j), Point(i + 1, j), Point(i + 1, j + 1), Point(i, j + 1), b.Jitter(hex, .045f));
            }
            if (hex == Dark || hex == 0x6f6a64)
                Detail(b, 137, () => Fragments(b,radius*2,radius*2,
                    (x,z) => height*(1-(x*x+z*z)/(radius*radius)),13,13,true));
            p.surfaces.Add(PropSurface.Mound(0, 0, radius, height));
        }

        static void Rebar(MeshBuilder b, Vector3 start, Vector3 direction, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 a = start + V(0, 0, (i - (count - 1) * .5f) * .32f);
                Vector3 bend = a + direction * .65f;
                b.Beam(a, bend, .024f, .022f, 4, C(Rust));
                b.Beam(bend, a + direction + V(.08f, -.18f, .06f), .022f, .014f, 4, C(Rust));
            }
        }

        // Horizontal window bays: solid piers/sills, actual empty windows and door.
        // Only solid geometry has collision, including the lintel's own height band.
        static void Facade(MeshBuilder b, PropModel p, float x, float z, float length,
            float bottom, int bays, int door = -1)
        {
            float bay = length / bays, pier = .34f;
            for (int i = 0; i <= bays; i++)
                Solid(b, p, x - length * .5f + i * bay, z, pier, .3f, bottom, 2.96f);
            for (int i = 0; i < bays; i++)
            {
                float cx = x - length * .5f + (i + .5f) * bay;
                if (i != door) Solid(b, p, cx, z, bay - pier, .3f, bottom, .85f, Concrete);
                Solid(b, p, cx, z, bay - pier, .3f, bottom + 2.5f, .46f, Weathered);
                // Preserve the old paint-box random draws; the new two-sided
                // chipped paint/stain sheets replace those twelve-faced decals.
                if (i != door)
                {
                    for(int k=0;k<12;k++) b.Range(-.06f,.06f);
                    Detail(b, i+151, () => WindowDetail(b,cx,z,bottom,bay-pier-.08f,i));
                }
            }
        }

        static void Rail(MeshBuilder b, PropModel p, float x, float z, float length,
            float bottom, bool alongZ)
        {
            // Short surviving runs leave most fractured edges open to drops.
            float w = alongZ ? .1f : length, d = alongZ ? length : .1f;
            for(int i=0;i<6;i++) b.Range(-.06f,.06f); // original Box draws
            Detail(b, 179, () =>
            {
                Vector3 Point(float t,float y) => V(x+(alongZ?0:t),bottom+y,z+(alongZ?t:0));
                b.Beam(Point(-length*.5f,.9f),Point(-length*.12f,.9f),.04f,.04f,4,C(Rust));
                b.Beam(Point(length*.12f,.76f),Point(length*.5f,.9f),.035f,.04f,4,C(Rust));
                b.Beam(Point(-length*.13f,.9f),Point(-length*.05f,.64f),.035f,.018f,3,C(Rust));
            });
            int n = Mathf.CeilToInt(length / 1.5f);
            for (int i = 0; i <= n; i++)
            {
                float t = (i / (float)n - .5f) * length;
                b.Box(V(x + (alongZ ? 0 : t), bottom + .45f, z + (alongZ ? t : 0)), V(.08f, .9f, .08f), C(Steel));
            }
            p.colliders.Add(PropCollider.Rect(x, z, w * .5f, d * .5f).Between(bottom, bottom + .95f));
        }

        static void Wreck(MeshBuilder b, PropModel p, float x, float z, float floor)
        {
            b.Box(V(x, floor + .27f, z), V(1.65f, .42f, 3.65f), C(Char));
            b.Box(V(x, floor + .55f, z + 1.15f), V(1.6f, .12f, 1.2f), C(Rust));
            b.Box(V(x, floor + .47f, z - .1f), V(1.35f, .04f, 1.5f), C(Interior));
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(V(x + side * .76f, floor + .6f, z - .1f), V(.1f, .36f, 1.7f), C(Steel));
                for (int end = -1; end <= 1; end += 2)
                    b.Beam(V(x + side * .73f, floor + .5f, z + end * .9f),
                        V(x + side * .56f, floor + 1.05f, z + end * .55f), .045f, .035f, 4, C(Rust));
            }
            for(int i=0;i<6;i++) b.Range(-.06f,.06f); // original roof Box draws
            Detail(b, 193, () =>
            {
                b.Quad(V(x-.58f,floor+1.1f,z-.6f),V(x-.58f,floor+1.04f,z+.6f),
                    V(x,floor+.81f,z+.5f),V(x,floor+.77f,z-.55f),C(Char));
                b.Quad(V(x,floor+.77f,z-.55f),V(x,floor+.81f,z+.5f),
                    V(x+.58f,floor+1.02f,z+.6f),V(x+.58f,floor+1.1f,z-.6f),C(Rust));
                for(int side=-1;side<=1;side+=2)
                {
                    for(int end=-1;end<=1;end+=2)
                        b.Beam(V(x+side*.72f,floor+.25f,z+end*1.18f),
                            V(x+side*.84f,floor+.25f,z+end*1.18f),.23f,.23f,6,C(Char));
                    b.Box(V(x+side*.825f,floor+.55f,z+.1f),V(.012f,.1f,.5f),C(Green));
                }
                b.Box(V(x,floor+.39f,z+1.83f),V(1.4f,.11f,.065f),C(Steel));
            });
            p.colliders.Add(PropCollider.Rect(x, z, .85f, 1.85f).Between(floor, floor + 1.13f));
        }

        static void Debris(MeshBuilder b, PropModel p, float x, float z, int count)
        {
            // Ankle-high rubble outside the reserved routes; each shard has its own
            // matching ledge rather than a broad invisible blocking circle.
            for (int i = 0; i < count; i++)
            {
                float px = x + b.Range(-1.1f, 1.1f), pz = z + b.Range(-1, 1);
                float h = b.Range(.08f, .27f), w = b.Range(.2f, .65f), d = b.Range(.2f, .7f);
                Floor(b, p, px, pz, w, d, h, thickness: h);
            }
        }

        // Shared with RegionProps' small shell. The left bay is an uncovered stairwell;
        // rear circulation is 2.4 m deep and the front-right corner has collapsed.
        internal static PropModel Building(int seed, int storeys, bool tower = false)
        {
            var b = new MeshBuilder(seed); var p = new PropModel { landmark = tower };
            const float w = 14, d = 10;
            int standing = tower ? 3 : storeys;
            for (int f = 0; f < standing; f++)
            {
                float y = f * Storey;
                Facade(b, p, -2, -d * .5f, 10, y, 4, f == 0 ? 1 : -1);
                Facade(b, p, 0, d * .5f, w, y, 5, f == 0 ? 2 : -1);
                // Exposed RC end frame, columns and beams instead of a solid side.
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int j = 0; j < 3; j++)
                        if (!(side == 1 && j == 0))
                            Solid(b, p, side * 6.8f, -4 + j * 4, .4f, .4f, y, 2.96f);
                    b.Box(V(side * 6.8f, y + 2.78f, 1.5f), V(.42f, .36f, 6.5f), C(Weathered));
                }
                // Two room partitions with a 1.8 m doorway, clear of stair lanes.
                Solid(b, p, 2.3f, -.5f, 2.6f, .18f, y, 2.1f, Interior);
                Solid(b, p, 6.1f, -.5f, 1.4f, .18f, y, 1.3f, Weathered);
                Detail(b, seed ^ (f*7919), () =>
                {
                    RoomClutter(b,2,-3.4f,y,f);
                    Exterior(b,y,f);
                });
                if (f == 0)
                {
                    b.Box(V(2.5f, .006f, 1.1f), V(3.8f, .012f, 2.3f), C(Interior));
                    continue;
                }
                if (f == 1)
                {
                    // Landing connects across the rear; separate right-hand floor
                    // rectangles leave a 2 x 2 m hole and a missing front corner.
                    Floor(b, p, -.1f, 3.6f, 13.4f, 2.4f, y);
                    Floor(b, p, 2, .2f, 2, 4.4f, y);
                    Floor(b, p, 5.3f, 1.6f, 2.6f, 1.6f, y);
                    Floor(b, p, 2, -3.4f, 2, 2.4f, y);
                    Rebar(b, V(3, y - .1f, -2.7f), V(.9f, -.2f, 0), 4);
                }
                else
                {
                    int gate = tower ? 0 : 1;
                    Floor(b, p, 1.6f, -3.6f, 9.6f, 2.4f, y, gate);
                    Floor(b, p, 2, -.3f, 2, 4.2f, y, gate);
                    Floor(b, p, 5.4f, .9f, 2.4f, 1.8f, y, gate);
                }
            }
            Flight(b, p, -5.25f, -.1f, 2, 5, 0, Storey, stairs: true);
            // Explicit lower landing; upper landing is the rear floor strip.
            Floor(b, p, -5.25f, -3.5f, 2, 1.8f, 0, thickness: .18f);
            Rail(b, p, -6.6f, 3.4f, 2, Storey, true);
            if (tower)
            {
                // Second lane returns to the front, with no floor over the flight.
                Flight(b, p, -2.15f, 0, 2, 4.8f, Storey, 2 * Storey, Mathf.PI, true);
                Floor(b, p, -2.15f, -3.6f, 2, 2.4f, 2 * Storey);
            }
            // Roof remnant over one room, 3.2 m above the last floor: upgrade reward.
            float roof = standing * Storey;
            Floor(b, p, 5.15f, -3.4f, 2.9f, 2.4f, roof, 1);
            Rebar(b, V(6.55f, roof - .1f, -3.4f), V(.65f, .5f, 0), 4);
            // A fallen panel leans out of the collapsed corner; its walking face is
            // represented explicitly and it does not conceal the interior route.
            Flight(b, p, 5.25f, -2.1f, 2, 2.2f, 0, 1.05f);
            Debris(b, p, 5.4f, -4.4f, 12);
            b.Box(V(-1.5f, 2.3f, -5.17f), V(2.4f, .4f, .05f), C(Green));
            for (int i = 0; i < 5; i++)
                b.Box(V(-2.25f + i * .36f, 2.3f, -5.205f), V(.14f, .21f, .015f), C(Weathered));
            if (tower) FallenCrown(b, p, storeys - standing);
            Detail(b, seed ^ 211, () =>
            {
                // Curb runs terminate before the entrance and stair approach.
                for(int i=0;i<5;i++) b.Box(V(-6.8f+i*.65f,.065f,-5.45f),V(.6f,.13f,.24f),C(Weathered));
                RoomClutter(b,5.15f,-3.4f,roof,2);
            });
            return Finish(b, p, tower ? "collapsed-tower" : "apartment-ruin", tower ? 14 : 7.8f);
        }

        public static PropModel ApartmentRuin(int seed)
        {
            int storeys = new System.Random(seed).Next(2, 5);
            return Building(seed, storeys);
        }

        public static PropModel ShopRow(int seed)
        {
            var b = new MeshBuilder(seed); var p = new PropModel();
            int shops = b.RangeInt(2, 5);
            float length = b.Range(18, 24), bay = length / shops;
            const float roof = 3.4f;
            for (int i = 0; i < shops; i++)
            {
                float x = -length * .5f + (i + .5f) * bay;
                Solid(b, p, x, 2.8f, bay, .3f, 0, 3.16f);
                Solid(b, p, x - bay * .5f, 0, .3f, 5.6f, 0, 3.16f, Interior);
                // Shopfront doors are broad gaps, with one broken-off end pier.
                Solid(b, p, x - bay * .5f + .3f, -2.8f, .55f, .4f, 0, 2.8f);
                Solid(b, p, x, -2.8f, bay - .5f, .35f, 2.65f, .5f);
                // Continuous rear roof path; separate front fragment leaves a hole.
                Floor(b, p, x, 1.25f, bay, 3.1f, roof);
                Floor(b, p, x - bay * .32f, -1.55f, bay * .36f, 2.5f, roof);
                b.Box(V(x, 2.95f, -2.98f), V(bay * .7f, .38f, .025f), C(i % 2 == 0 ? Ochre : Green));
                for (int j = 0; j < 4; j++)
                    b.Box(V(x - .6f + j * .4f, 2.95f, -2.997f), V(.18f, .19f, .004f), C(Weathered));
                // Torn awning is another exactly registered, gated high ledge.
                Flight(b, p, x, -3.55f, bay * .65f, 1.1f, 2.2f, 2.4f, gate: 1, hex: i % 2 == 0 ? Green : Ochre);
                Rebar(b, V(x + .1f, roof - .1f, -.3f), V(.6f, -.3f, -.5f), 3);
                Debris(b, p, x + .4f, -2, 5);
                Detail(b, seed ^ (i*3571), () =>
                {
                    // Flush to the lintel: never overhang the awning route (z <= -3).
                    // Broad sun-faded sign, missing panels and broken glyph strokes.
                    b.Box(V(x,3.02f,-2.985f),V(bay*.78f,.57f,.02f),C(i%2==0?Green:Ochre));
                    for(int j=0;j<6;j++)
                    {
                        float sx=x-bay*.29f+j*bay*.105f;
                        b.Box(V(sx,3.03f,-2.999f),V(.055f,.32f,.001f),C(0xb0aea0));
                        if(j!=3) b.Box(V(sx+.09f,3.17f,-2.999f),V(.18f,.045f,.001f),C(0xb0aea0));
                        if(j%2==0) b.Box(V(sx+.08f,3.01f,-2.999f),V(.14f,.04f,.001f),C(0xb0aea0));
                    }
                    // Rolled shutter is under the lintel, leaving the doorway open.
                    b.Beam(V(x-bay*.3f,2.58f,-2.9f),V(x+bay*.3f,2.58f,-2.9f),.12f,.12f,6,C(Rust));
                    for(int j=0;j<3;j++)
                        b.Box(V(x,2.52f-j*.07f,-3.01f),V(bay*.6f,.025f,.025f),C(Steel));
                    RoomClutter(b,x,0,0,i);
                    RoomClutter(b,x,1.35f,roof,i+1);
                    b.Beam(V(x-bay*.4f,2.98f,2.55f),V(x+bay*.4f,2.98f,2.55f),.075f,.075f,5,C(Rust));
                    b.Box(V(x-bay*.42f,.06f,-3.18f),V(.65f,.12f,.25f),C(Weathered));
                    // Peeling paint on the solid rear wall, visible through the roof hole.
                    b.Box(V(x,.8f,2.642f),V(bay*.55f,.32f,.014f),C(Green));
                    b.Box(V(x-bay*.3f,.18f,2.64f),V(.45f,.22f,.018f),C(0x5f6650));
                });
            }
            Solid(b, p, length * .5f, 0, .3f, 5.6f, 0, 3.16f);
            // Outside rear wall: rubble ramp rises 2.4 m in 4.2 m. A 0.8-metre
            // jump mounts the broad roof ledge, then a 0.2 m step reaches the roof.
            float accessX = -length * .5f + 1.5f;
            Flight(b, p, accessX, 6.9f, 2.4f, 4.2f, 0, 2.4f, Mathf.PI);
            for (int side = -1; side <= 1; side += 2) for (int i = 1; i <= 8; i++)
            {
                // Broken masonry flanks the clear 2.4 m walking face.
                float top = i * .3f;
                Floor(b, p, accessX + side * 1.45f, 9 - i * .525f,
                    .5f, .65f, top, thickness: top);
            }
            Floor(b, p, accessX, 4.25f, 2.4f, 1.1f, 2.4f, thickness: 2.4f);
            Floor(b, p, accessX, 3.25f, 2.4f, .9f, 3.2f, thickness: .8f);
            // Distinct inaccessible roof box, away from the access lane.
            Floor(b, p, length * .5f - 2, 1.3f, 2.2f, 2.2f, 5, 1, .35f);
            Solid(b, p, length * .5f - 2, 2.2f, 2.2f, .25f, roof, 1.25f);
            return Finish(b, p, "shop-row", Mathf.Max(length * .5f + .5f, 9.2f));
        }

        public static PropModel Overpass(int seed)
        {
            var b = new MeshBuilder(seed); var p = new PropModel();
            float length = b.Range(30, 38), start = -length * .5f;
            const float deck = 4.5f, width = 8;
            float rampEnd = start + 8, snap = length * .5f - 7;
            Flight(b, p, 0, start + 4, width, 8, 0, deck, hex: Dark);
            Floor(b, p, 0, (rampEnd + snap) * .5f, width, snap - rampEnd, deck, thickness: .42f);
            float stubStart = snap + 3.4f, end = length * .5f;
            Floor(b, p, 0, (stubStart + end) * .5f, width, end - stubStart, deck, 1, .42f);
            // Parallel pairs leave both underpass and carriageway traversable.
            foreach (float z in new[] { rampEnd + 2, snap - 1.5f, end - 1.2f })
            {
                for (int side = -1; side <= 1; side += 2)
                    Solid(b, p, side * 2.6f, z, .85f, 1.1f, 0, deck - .42f);
                b.Box(V(0, deck - .68f, z), V(6.5f, .5f, 1.2f), C(Weathered));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Rail(b, p, side * 3.8f, (rampEnd + snap) * .5f, snap - rampEnd - .8f, deck, true);
                Rail(b, p, side * 3.8f, end - 1.6f, 2, deck, true);
            }
            Wreck(b, p, -2, rampEnd + 3, deck);
            Wreck(b, p, 2, snap - 2.3f, deck);
            // Faded lane dashes stay flush with the visible deck.
            for (float z = rampEnd + 1; z < snap - .6f; z += 2.6f)
                b.Box(V(0, deck + .005f, z), V(.12f, .01f, 1.2f), C(Ochre));
            for (int i = -3; i <= 3; i++)
            {
                b.Beam(V(i, deck - .17f, snap - .2f), V(i + .15f, deck - .3f, snap + .75f), .03f, .02f, 4, C(Rust));
                b.Beam(V(i, deck - .17f, stubStart + .2f), V(i - .15f, deck - .55f, stubStart - .5f), .03f, .02f, 4, C(Rust));
            }
            Debris(b, p, 4.4f, start + 3, 10);
            Detail(b, seed ^ 223, () =>
            {
                Joint(b,0,rampEnd+.3f,7.3f,deck);
                Joint(b,0,snap-.4f,7.3f,deck);
                Joint(b,0,end-1,7.3f,deck);
                HangingLamp(b,-4.15f,rampEnd+1,deck);
                HangingLamp(b,4.15f,end-1.3f,deck);
                for(float z=rampEnd+.8f;z<snap-.4f;z+=1.8f)
                    for(int side=-1;side<=1;side+=2)
                        b.Box(V(side*3.25f,deck+.007f,z),V(.08f,.012f,1.1f),C(0xaaa99a));
            });
            return Finish(b, p, "broken-overpass", length * .5f + .5f);
        }

        public static PropModel ParkingDeck(int seed)
        {
            var b = new MeshBuilder(seed); var p = new PropModel();
            const float top = 3.2f;
            // Ramp occupies x=-7, z=-4..1.4. Its slab opening continues to the
            // entrance, so no upper surface covers the ascending route.
            Flight(b, p, -6.7f, -1.3f, 3, 5.4f, 0, top);
            Floor(b, p, -6.7f, 3.7f, 3, 4.6f, top);
            Floor(b, p, 1.65f, 0, 13.7f, 13, top);
            Floor(b, p, -6.7f, -5.65f, 3, 1.3f, 0);
            foreach (float x in new[] { -8.5f, -.5f, 8.5f })
                foreach (float z in new[] { -6.5f, 0, 6.5f })
                    Solid(b, p, x, z, .55f, .55f, 0, top - .24f);
            for (int side = -1; side <= 1; side += 2)
            {
                Solid(b, p, side * 8.5f, 1.3f, .25f, 9, top, .72f, Concrete);
                Solid(b, p, 2.4f, side * 6.25f, 10.7f, .25f, top, .72f, Concrete);
                // Cross beams make the parking structure legible from the open sides.
                b.Box(V(0, 2.7f, side * 6.5f), V(17.5f, .5f, .5f), C(Dark));
            }
            Wreck(b, p, 5.8f, -2.9f, top);
            Wreck(b, p, 2.2f, 3.4f, top);
            Wreck(b, p, 4.7f, .2f, 0);
            for (int i = 0; i < 4; i++)
                b.Box(V(-1.4f + i * 2.4f, top + .005f, -3.4f), V(.08f, .01f, 4), C(Ochre));
            Floor(b, p, 6.7f, 4.7f, 2.4f, 2, 5, 1);
            Solid(b, p, 7.75f, 4.7f, .25f, 2, top, 1.56f);
            Rebar(b, V(-4.9f, top - .1f, -2.3f), V(-.65f, -.25f, 0), 4);
            Debris(b, p, 7.1f, -6.5f, 8);
            Detail(b, seed ^ 227, () =>
            {
                Joint(b,1.4f,1.3f,12.5f,top);
                Joint(b,1.4f,-5.8f,12.5f,top);
                HangingLamp(b,8.8f,2.4f,top);
                for(int i=0;i<4;i++)
                {
                    b.Box(V(-1.4f+i*2.4f,top+.006f,4.1f),V(.075f,.012f,3.3f),C(0xa5a69b));
                    b.Box(V(-.4f+i*2.4f,top+.007f,-5.3f),V(.7f,.012f,.09f),C(0x9e967b));
                }
                // Rusted railing remnants sit on existing parapet collider bands.
                for(int i=0;i<3;i++)
                    b.Beam(V(.2f+i*2.8f,top+.68f,6.25f),V(2.1f+i*2.8f,top+.68f,6.25f),.03f,.025f,3,C(Rust));
                RoomClutter(b,6.7f,4.7f,5,1);
            });
            return Finish(b, p, "parking-deck", 9.3f);
        }

        public static PropModel StairRuin(int seed)
        {
            var b = new MeshBuilder(seed); var p = new PropModel();
            float top = b.Range(3.2f, 3.8f), run = 5.8f;
            Flight(b, p, -.8f, -.9f, 2.2f, run, 0, top, stairs: true);
            Floor(b, p, -.8f, 3, 2.2f, 2, top);
            Solid(b, p, -1.65f, 3.45f, .4f, .4f, 0, top - .24f);
            Rail(b, p, -1.85f, 3, 1.5f, top, true);
            // Adjacent broken pier cap: 0.9 m above the main landing, jumpable.
            Floor(b, p, 1.3f, 3, 2, 2, top + .9f, thickness: top + .9f);
            Floor(b, p, 1.3f, -.1f, 2, 1.8f, top + 2.5f, 1, .3f);
            Solid(b, p, 2.1f, -.1f, .35f, .6f, 0, top + 2.2f);
            Rebar(b, V(.25f, top - .1f, 3), V(.5f, -.15f, 0), 3);
            Debris(b, p, 1.6f, -2.7f, 8);
            return Finish(b, p, "stair-ruin", 4.7f);
        }

        static void FallenCrown(MeshBuilder b, PropModel p, int floors)
        {
            // Sheared upper half rotates about its broken lower edge. Each sloped
            // floor gets an analytically transformed Ramp, including slab top offset.
            const float lean = .62f, pivotX = 2.5f, pivotY = 9.6f;
            float c = Mathf.Cos(lean), s = Mathf.Sin(lean);
            for (int f = 0; f < floors; f++)
            {
                float y = f * Storey;
                float cx = pivotX + y * s, cy = pivotY + y * c;
                b.Push(); b.Translate(pivotX, pivotY, 0); b.RotateZ(-lean);
                // Open centre and shortened left slab preserve the sheared silhouette.
                b.Jitter(Concrete);
                for(int i=0;i<6;i++) b.Range(-.06f,.06f);
                Detail(b, f+229, () =>
                {
                    b.Push(); b.Translate(1,0,0); ChippedSlab(b,6,7,y,.28f); b.Pop();
                    b.Push(); b.Translate(0,y,0); b.Scale(1,.8f,1);
                    RoomClutter(b,1,-1.8f,0,f); RoomClutter(b,2.5f,1.5f,0,f+1); b.Pop();
                    for(int side=-1;side<=1;side+=2)
                    {
                        for(int j=0;j<3;j++) WindowDetail(b,-1+j*2,side*3.5f,y,1.58f,j);
                        b.Beam(V(-1.7f,y+2.66f,side*3.25f),V(3.7f,y+2.66f,side*3.25f),.06f,.06f,4,C(Rust));
                    }
                });
                for (int side = -1; side <= 1; side += 2)
                {
                    b.Box(V(1, y + 2.8f, side * 3.5f), V(6, .32f, .32f), C(Dark));
                    for (int j = 0; j < 4; j++)
                    {
                        float x = -2 + j * 2;
                        b.Box(V(x, y + 1.45f, side * 3.5f), V(.34f, 2.9f, .34f), C(Weathered));
                        if (j < 3) b.Box(V(x + 1, y + .4f, side * 3.5f), V(1.66f, .8f, .25f), C(Interior));
                    }
                }
                b.Pop();
                // +local Z points towards -world X, the high end of this tilted floor.
                p.surfaces.Add(PropSurface.Ramp(cx + c, 0, 3.5f, 3 * c,
                    cy - 4 * s, cy + 2 * s, -Mathf.PI * .5f, 1));
                // Collision is divided vertically, avoiding a ground-to-sky footprint
                // beneath the leaning crown. Segments follow each inclined pier.
                for (int side = -1; side <= 1; side += 2)
                    for (int j = 0; j < 4; j++) for (int k = 0; k < 4; k++)
                    {
                        float lx = -2 + j * 2, ly = y + k * .725f;
                        float x0 = pivotX + lx * c + ly * s;
                        float y0 = pivotY - lx * s + ly * c;
                        p.colliders.Add(PropCollider.Rect(x0 + .3625f * s, side * 3.5f,
                            .17f * c + .3625f * s, .17f).Between(y0 - .17f * s, y0 + .725f * c + .17f * s));
                    }
                for (int side = -1; side <= 1; side += 2)
                    for (int j = 0; j < 3; j++) for (int k = 0; k < 2; k++)
                    {
                        // Small bounds follow the tilted solid window sills without
                        // filling either the window opening or the ground below.
                        float lx = -2 + j * 2 + .17f + (k + .5f) * .83f;
                        float ly = y + .4f, x0 = pivotX + lx * c + ly * s;
                        float y0 = pivotY - lx * s + ly * c;
                        float halfY = .4f * c + .415f * s;
                        p.colliders.Add(PropCollider.Rect(x0, side * 3.5f,
                            .415f * c + .4f * s, .125f).Between(y0 - halfY, y0 + halfY));
                    }
            }
            Rebar(b, V(1, 9.4f, 1), V(1.2f, .8f, 0), 7);
            // Fallen wall panels and shallow debris fan out from the sheared side.
            Flight(b, p, 9.4f, -2.3f, 3, 4.5f, 0, 2.4f);
            Debris(b, p, 9.3f, 2.5f, 22);
        }

        public static PropModel CollapsedTower(int seed)
        {
            return Building(seed, new System.Random(seed).Next(6, 10), true);
        }
    }
}
