using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        static readonly Plane ManorGround = new Plane(Vector3.up, Vector3.zero);
        static readonly Color Meadow = new Color(.46f, .60f, .34f);
        static readonly Color Track = new Color(.76f, .65f, .44f);
        static readonly Color Timber = new Color(.46f, .30f, .19f);
        static readonly Color Survey = new Color(.96f, .86f, .63f);
        static readonly Color[] Clothes = {
            new Color(.28f, .44f, .51f), new Color(.68f, .37f, .23f),
            new Color(.34f, .50f, .34f), new Color(.72f, .53f, .28f)
        };

        Manor manor;
        int manorSelection = -1, manorPace = 1;
        bool manorDirty;
        float manorTime, manorScreenShift;
        readonly string[] builtKinds = new string[10];
        readonly int[] builtLevels = new int[10], homes = new int[10];
        int homeCount;
        MeshSet surveyBlock, hat, parcel;
        WalkRoute[] walks;

        struct CameraBookmark
        {
            public int width, height, mode;
            public Vector3 focus;
            public float yaw, pitch, distance, shift;
        }
        CameraBookmark arenaCamera, villageCamera;

        CameraBookmark Bookmark()
        {
            return new CameraBookmark {width = Screen.width, height = Screen.height, mode = CameraMode,
                focus = target, yaw = yaw, pitch = pitch, distance = distance, shift = manorScreenShift};
        }

        bool Restore(CameraBookmark saved)
        {
            if (saved.width != Screen.width || saved.height != Screen.height || saved.distance <= 0) return false;
            CameraMode = saved.mode;
            target = saved.focus;
            yaw = saved.yaw;
            pitch = saved.pitch;
            distance = saved.distance;
            manorScreenShift = saved.shift;
            return true;
        }

        public void SetManor(Manor state, int selected, int pace = 1)
        {
            bool changingMode = (manor == null) != (state == null);
            if (changingMode)
            {
                if (manor == null) arenaCamera = Bookmark();
                else villageCamera = Bookmark();
                cachedMap = null;
                cachedQuality = -1;
                Marker = null;
            }
            manor = state;
            manorSelection = selected;
            manorPace = Mathf.Clamp(pace, 0, 2);
            if (changingMode)
            {
                if (!Restore(manor == null ? arenaCamera : villageCamera)) ResetCamera();
                UpdateShadowDistance();
            }
            if (manor == null) return;
            manorDirty |= changingMode;
            for (int i = 0; i < manor.slots.Length && !manorDirty; i++)
                manorDirty = manor.slots[i].kind != builtKinds[i] || manor.slots[i].level != builtLevels[i];
        }

        void UpdateShadowDistance()
        {
            float reach = QualityLevel == 0 ? 22 : QualityLevel == 1 ? 42 : 65;
            QualitySettings.shadowDistance = manor == null ? reach : Mathf.Max(reach, distance + 20);
            QualitySettings.shadows = manor == null && QualityLevel == 0 ? ShadowQuality.HardOnly : ShadowQuality.All;
        }

        void FrameManor()
        {
            bool compact = Viewport.Compact;
            ViewCamera.fieldOfView = compact ? 65 : 42;
            CameraMode = 0;
            target = Vector3.up * .25f;
            yaw = compact ? -6 : -12;
            pitch = compact ? 53 : 46;
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float virtualHeight = (compact ? 390 : 1280) / aspect;
            float top = compact ? 148 : 120, bottom = compact ? 215 : 155;
            float tangent = Mathf.Tan(ViewCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            manorScreenShift = (top - bottom) / virtualHeight * tangent;
            float tanX = tangent * aspect * .94f;
            float tanY = tangent * Mathf.Clamp((virtualHeight - top - bottom) / virtualHeight, .28f, .98f) * .95f;
            var toView = Quaternion.Inverse(Quaternion.Euler(pitch, yaw, 0));
            distance = 20;
            // Include the near cliff lip, far treetops and hall roof in the available HUD window.
            for (int xi = -1; xi <= 1; xi += 2)
                for (int zi = -1; zi <= 1; zi += 2)
                {
                    FitPoint(new Vector3(13 * xi, -1.65f, 9 * zi), toView, tanX, tanY);
                    FitPoint(new Vector3(12 * xi, zi > 0 ? 3.1f : .45f, 8.4f * zi), toView, tanX, tanY);
                }
            FitPoint(Vector3.up * 4.25f, toView, tanX, tanY);
            distance = Mathf.Clamp(distance, 20, 90);
            UpdateShadowDistance();
        }

        void FitPoint(Vector3 point, Quaternion toView, float tanX, float tanY)
        {
            var p = toView * (point - target);
            distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(p.x) / tanX, Mathf.Abs(p.y) / tanY) - p.z);
        }

        static Vector3 Plot(int index) => new Vector3(Manor.SlotX(index), 0, Manor.SlotZ(index));
        const float LaneZ = 2.85f, CrossX = 3.25f;

        static Vector3 LaneMouth(int plot)
        {
            if (plot == 10) return new Vector3(-12, 0, -LaneZ);
            var p = Plot(plot);
            p.z = p.z > 2 ? LaneZ : -LaneZ;
            return p;
        }

        static Vector3 Door(int plot)
        {
            if (plot == 10) return LaneMouth(plot);
            var p = Plot(plot);
            p.z += Mathf.Sign(LaneMouth(plot).z - p.z) * (plot == 0 ? 2.04f : 1.72f);
            return p;
        }

        static float FacadeYaw(int index) => Door(index).z > Plot(index).z ? 180 : 0;

        void Still(MeshSet mesh, Vector3 p, Quaternion rotation, Vector3 size, Color tint, float wind = 0)
        {
            (mesh.still ?? Buffer(mesh, still, 1)).Add(Pose(p, rotation, size), tint, wind);
        }

        void Tile(Vector3 p, Vector3 size, Color tint, float angle = 0)
        {
            Still(surveyBlock, p, Quaternion.Euler(0, angle, 0), size, tint);
        }

        void Lane(Vector3 from, Vector3 to, float width)
        {
            Vector3 span = to - from;
            if (span.sqrMagnitude < .001f) return;
            Still(surveyBlock, (from + to) * .5f + Vector3.up * .011f, Quaternion.LookRotation(span),
                new Vector3(width, .018f, span.magnitude + .18f), Track);
        }

        void Fence(Vector3 at, float angle, int spans)
        {
            var rotation = Quaternion.Euler(0, angle, 0);
            for (int i = 0; i <= spans; i++)
            {
                Vector3 post = at + rotation * (Vector3.right * (1.3f * i));
                Still(surveyBlock, post + Vector3.up * .32f, rotation, new Vector3(.10f, .64f, .10f), Timber);
                if (i == spans) break;
                for (int j = 0; j < 2; j++)
                    Still(surveyBlock, post + rotation * new Vector3(.65f, .24f + .25f * j, 0),
                        rotation, new Vector3(1.36f, .072f, .07f), new Color(.61f, .43f, .25f));
            }
        }

        void MeadowBorder()
        {
            var tree = Load("tree");
            var rock = Load("rock");
            var grass = Load("grass");
            var flowers = Load("flowers");
            // The shelterbelt stands behind the plots, with a gap at the western gate.
            for (int i = 0; i < 7; i++)
            {
                float x = i < 4 ? -11.8f + i * 1.48f : 11.5f + (i - 5) * .45f;
                float z = i < 4 ? 7.85f + Mathf.Sin(i * 2.1f) * .35f : 7.8f - (i - 4) * 2.3f;
                float scale = .70f + (i * 7 % 5) * .08f;
                Still(tree, new Vector3(x, 0, z), Quaternion.Euler(0, i * 67, 0), Vector3.one * scale, White, .011f);
            }
            int sprigs = QualityLevel == 0 ? 42 : QualityLevel == 1 ? 92 : 145;
            for (int i = 0; i < sprigs; i++)
            {
                // Irrational spacing gives the same meadow on load, independent of battle randomness.
                float along = Mathf.Repeat(.62f + i * .618034f, 1), inset = Mathf.Repeat(.3f + i * .414214f, 1);
                bool side = i % 3 == 0;
                float sign = (i & 1) == 0 ? 1 : -1;
                Vector3 p = side ? new Vector3(sign * (11.4f + inset), .005f, (along - .5f) * 16.5f)
                                 : new Vector3((along - .5f) * 25, .005f, sign * (7.9f + inset * .75f));
                if (side && p.x < 0 && Mathf.Abs(p.z + LaneZ) < .55f) continue;
                float size = .5f + .45f * inset;
                Still(i % 5 == 0 ? flowers : grass, p, Quaternion.Euler(0, i * 137.5f, 0), Vector3.one * size, White, .026f);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = -11.7f + i * 2.12f;
                float z = i % 3 == 1 ? 8.8f : -8.9f;
                Still(rock, new Vector3(x, -.26f, z), Quaternion.Euler(0, i * 37, 0),
                    new Vector3(.68f + i % 3 * .23f, .53f, .73f), White);
            }
            Fence(new Vector3(-12f, 0, -7.6f), -90, 2);
            Fence(new Vector3(-12f, 0, -1.5f), -90, 2);
            Fence(new Vector3(7.8f, 0, -8.1f), 2, 3);
            Fence(new Vector3(-4.4f, 0, 8.25f), 0, 6);
        }

        void DrawPlotGeometry(int i)
        {
            var p = Plot(i);
            var plot = manor.slots[i];
            builtKinds[i] = plot.kind;
            builtLevels[i] = plot.level;
            var facing = Quaternion.Euler(0, FacadeYaw(i), 0);
            if (string.IsNullOrEmpty(plot.kind))
            {
                Tile(p + Vector3.up * .019f, new Vector3(3.40f, .026f, 2.48f), new Color(.61f,.65f,.40f));
                // Four survey pegs and a cream cross identify buildable land from either camera angle.
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var peg = p + new Vector3(1.65f*x,.115f,1.20f*z);
                        Tile(peg, new Vector3(.12f,.23f,.12f), Timber);
                        Tile(peg + Vector3.up*.13f, new Vector3(.145f,.048f,.145f), Survey);
                    }
                Tile(p + Vector3.up*.054f, new Vector3(.78f,.04f,.19f), Survey);
                Tile(p + Vector3.up*.055f, new Vector3(.19f,.04f,.78f), Survey);
                return;
            }

            float footprint = i == 0 ? 1.19f : 1;
            Tile(p + Vector3.up*.018f, new Vector3(3.85f*footprint,.04f,2.97f*footprint),
                i == 0 ? new Color(.68f,.67f,.55f) : new Color(.67f,.58f,.41f));
            float rise = 1 + (plot.level - 1) * .035f;
            Still(Load(plot.kind), p + Vector3.up*.042f, facing, new Vector3(1,rise,1), White);
            // The dark stake with one to three bright inlays sits on the camera-facing verge.
            var sign = p + new Vector3(1.48f*footprint,.21f,-1.40f*footprint);
            Tile(sign, new Vector3(.76f,.42f,.18f), Timber);
            for (int rank = 0; rank < plot.level; rank++)
            {
                float x = (rank - (plot.level-1)*.5f)*.21f;
                Still(dot, sign+new Vector3(x,.205f,-.012f), Quaternion.identity,
                    new Vector3(.16f,2f,.16f), Survey);
                Tile(sign+new Vector3(x,.02f,-.102f), new Vector3(.105f,.24f,.025f), Survey);
            }
            if (plot.kind == "cottage") homes[homeCount++] = i;
        }

        void RebuildManor()
        {
            foreach (var b in still) b.Clear();
            foreach (var b in grid) b.Clear();
            surveyBlock = Load("platform");
            hat = Load("civilian_hat");
            parcel = Load("civilian_pack");
            Tile(new Vector3(0,-.18f,0),new Vector3(26,.36f,18),Meadow);
            Tile(new Vector3(0,-.56f,0),new Vector3(25.9f,.58f,17.9f),new Color(.54f,.44f,.30f));
            Tile(new Vector3(0,-1.16f,0),new Vector3(25.4f,.88f,17.5f),new Color(.47f,.48f,.40f));
            Still(Load("cliff"),new Vector3(0,-1.93f,0),Quaternion.identity,new Vector3(24,1.7f,16.5f),White);
            MeadowBorder();
            Lane(new Vector3(-12.2f,0,-LaneZ),new Vector3(9,0,-LaneZ),.78f);
            Lane(new Vector3(-8.8f,0,LaneZ),new Vector3(9,0,LaneZ),.68f);
            for (int side = -1; side <= 1; side += 2)
                Lane(new Vector3(side*CrossX,0,-LaneZ),new Vector3(side*CrossX,0,LaneZ),.57f);
            homeCount = 0;
            for (int i = 0; i < manor.slots.Length; i++)
            {
                Lane(Door(i), LaneMouth(i), i == 0 ? .93f : .50f);
                DrawPlotGeometry(i);
            }
            if (walks == null) BuildRoutes();
            sky.SetColor("_Horizon",new Color(.72f,.79f,.70f));
            sky.SetColor("_Zenith",new Color(.25f,.44f,.51f));
            sky.SetColor("_Nadir",new Color(.30f,.39f,.34f));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.72f,.79f,.70f);
            RenderSettings.fogDensity = .0060f;
            material.SetColor("_SkyLight",new Color(.64f,.69f,.66f));
            material.SetColor("_GroundLight",new Color(.29f,.30f,.23f));
            cachedQuality = QualityLevel;
            manorDirty = false;
            StaticRebuilds++;
        }

        sealed class WalkRoute
        {
            public readonly Vector3[] origins = new Vector3[5], directions = new Vector3[5];
            public readonly float[] lengths = new float[5];
            public int count;
            public float total;
            public void Segment(Vector3 from, Vector3 to)
            {
                Vector3 span = to - from;
                float len = span.magnitude;
                if (len < .001f) return;
                origins[count] = from;
                directions[count] = span / len;
                lengths[count++] = len;
                total += len;
            }
            public void Sample(float travel, out Vector3 p, out Vector3 dir)
            {
                int i = 0;
                while (i < count - 1 && travel > lengths[i]) travel -= lengths[i++];
                dir = directions[i];
                p = origins[i] + dir * Mathf.Min(lengths[i], travel);
            }
        }

        void BuildRoutes()
        {
            // Each route follows the actual lanes and stops at doorsteps. Index 10 is the western gate.
            walks = new WalkRoute[11 * 10];
            for (int home = 0; home <= 10; home++)
                for (int dest = 0; dest < 10; dest++)
                {
                    if (home == dest) continue;
                    var path = walks[home * 10 + dest] = new WalkRoute();
                    Vector3 a = LaneMouth(home), b = LaneMouth(dest);
                    path.Segment(Door(home), a);
                    if (a.z != b.z)
                    {
                        float cross = a.x + b.x < 0 ? -CrossX : CrossX;
                        var near = new Vector3(cross, 0, a.z);
                        var far = new Vector3(cross, 0, b.z);
                        path.Segment(a, near);
                        path.Segment(near, far);
                        a = far;
                    }
                    path.Segment(a, b);
                    path.Segment(b, Door(dest));
                }
        }

        void DrawResident(int slot, int worker)
        {
            int id = slot * 5 + worker;
            int home = homeCount > 0 && (slot == 0 || (id & 1) != 0)
                ? homes[id % homeCount] : slot == 0 ? 10 : 0;
            var route = walks[home * 10 + slot];
            if (route == null || route.count == 0) return;
            const float pause = .72f;
            float trip = route.total + pause;
            float travel = Mathf.Repeat(manorTime * (1.18f + .08f * (id%4)) + id*2.16f, 2*trip);
            bool returning = travel >= trip;
            float legTime = returning ? travel-trip : travel;
            float at = Mathf.Min(route.total, legTime);
            route.Sample(returning ? route.total-at : at, out var p, out var dir);
            if (returning) dir = -dir;
            p += new Vector3(dir.z,0,-dir.x)*((worker%3)-1)*.10f;
            float step = legTime >= route.total ? 0 : Mathf.Sin(at*5.1f);
            var tint = Clothes[id%Clothes.Length];
            var root = Pose(p + Vector3.up*(.70f+.021f*Mathf.Abs(step)),Quaternion.LookRotation(dir),Vector3.one*.80f);
            Add(torso,root,tint);
            Add(head,root*Matrix4x4.Translate(new Vector3(0,.49f,.01f)),White);
            Add(hat,root*Matrix4x4.Translate(new Vector3(0,.65f,.01f)),White);
            if (returning) Add(parcel,root*Matrix4x4.Translate(new Vector3(0,-.07f,-.25f)),White);
            for (int side = -1; side <= 1; side += 2)
            {
                float swing = step*side*29;
                Add(leg,root*Pose(new Vector3(side*.135f,-.51f,0),Quaternion.Euler(swing,0,0),Vector3.one),White);
                var shoulder = Pose(new Vector3(side*.30f,.25f,0),Quaternion.Euler(-swing*.80f,side==1?180:0,0),Vector3.one*.92f);
                Add(armLeft,root*shoulder*Matrix4x4.Translate(new Vector3(.36f,-.27f,0)),tint);
            }
        }

        void DrawManor()
        {
            if (manorDirty || cachedQuality != QualityLevel) RebuildManor();
            foreach (var b in still) Draw(b,true);
            manorTime += Mathf.Min(Time.unscaledDeltaTime,.2f)*manorPace;
            for (int i = 0; i < manor.slots.Length; i++)
                for (int w = 0; w < manor.slots[i].workers; w++) DrawResident(i,w);
            if ((uint)manorSelection < manor.slots.Length)
            {
                Vector3 p = Plot(manorSelection) + Vector3.up*.073f;
                float radius = manorSelection == 0 ? 2.65f : 2.16f;
                Add(ring,Pose(p,Quaternion.identity,new Vector3(radius,1,radius*.86f)),Gold);
            }
            foreach (var b in moving) Draw(b,false);
        }
    }
}
