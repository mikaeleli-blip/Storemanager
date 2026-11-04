using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>Anything that walks around the store on the tile grid.</summary>
    public abstract class Agent : MonoBehaviour
    {
        public string DisplayName = "";
        public float BaseSpeed = 2f;
        public Renderer Body;
        public CharacterRig Rig;

        /// <summary>What the arms are doing when standing still (the rig animates it).</summary>
        public virtual Pose CurrentPose { get { return Pose.Idle; } }

        List<Vector3> path;
        int pathIndex;
        Vector2Int destination;
        bool hasDestination;

        protected virtual bool StaffAccess { get { return false; } }
        public virtual float MoveSpeed { get { return BaseSpeed; } }
        public bool IsMoving { get { return path != null; } }
        public Vector2Int Tile { get { return StoreGrid.TileAt(transform.position); } }
        public Vector2Int Destination { get { return destination; } }

        public float DistanceTo(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        public bool GoTo(Vector2Int tile)
        {
            destination = tile;
            hasDestination = true;
            path = Store.I.Grid.FindPath(Tile, tile, StaffAccess);
            pathIndex = 0;
            if (path == null || path.Count == 0)
            {
                path = null;
                hasDestination = false;
                return false;
            }
            return true;
        }

        public void Stop()
        {
            path = null;
            hasDestination = false;
        }

        /// <summary>Recompute the current path (after the layout changed).</summary>
        public void Repath()
        {
            if (hasDestination && path != null) GoTo(destination);
        }

        public void Teleport(Vector2Int tile)
        {
            Stop();
            transform.position = StoreGrid.World(tile);
        }

        protected IEnumerator MoveTo(Vector2Int tile)
        {
            if (!GoTo(tile))
            {
                // Unreachable or already there. Give callers a frame so loops can't spin.
                yield return null;
                yield break;
            }
            while (path != null) yield return null;
        }

        protected IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        protected virtual void Update()
        {
            FollowPath(Time.deltaTime);
        }

        protected void FollowPath(float dt)
        {
            if (path == null) return;
            float step = MoveSpeed * dt;
            Vector3 pos = transform.position;
            Vector3 lastDir = Vector3.zero;
            while (step > 0f && path != null)
            {
                Vector3 target = path[pathIndex];
                Vector3 to = target - pos;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist <= step)
                {
                    pos = new Vector3(target.x, pos.y, target.z);
                    step -= dist;
                    if (dist > 0.001f) lastDir = to / dist;
                    pathIndex++;
                    if (pathIndex >= path.Count) path = null;
                }
                else
                {
                    lastDir = to / dist;
                    pos += lastDir * step;
                    step = 0f;
                }
            }
            transform.position = pos;
            if (lastDir.sqrMagnitude > 0.01f) Face(lastDir);
        }

        protected void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        /// <summary>
        /// Puts the Blender-made person together (Characters.fbx), or changes their clothes.
        /// Without the models (headless tests) it falls back to the grey-box capsule.
        /// </summary>
        public void Dress(Outfit look, Hold hold)
        {
            if (CharacterRig.Available)
            {
                if (Rig == null) Rig = CharacterRig.Build(this, look, hold);
                else Rig.Rebuild(look, hold);
                return;
            }
            if (Body == null) BuildBody(look.Top, look.Skin, look.Height - 0.15f);
            else Body.sharedMaterial = Mats.Get(look.Top);
        }

        /// <summary>Capsule body, head and a small nose so facing is visible.</summary>
        public void BuildBody(Color body, Color head, float height)
        {
            var b = Mats.Prim(PrimitiveType.Capsule, "Body", transform, Vector3.zero,
                new Vector3(0.55f, height * 0.5f, 0.55f), body, true);
            b.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            Body = b.GetComponent<Renderer>();
            var h = Mats.Prim(PrimitiveType.Sphere, "Head", transform, Vector3.zero,
                new Vector3(0.38f, 0.38f, 0.38f), head, false);
            h.transform.localPosition = new Vector3(0f, height + 0.12f, 0f);
            var n = Mats.Prim(PrimitiveType.Cube, "Nose", transform, Vector3.zero,
                new Vector3(0.12f, 0.08f, 0.14f), head, false);
            n.transform.localPosition = new Vector3(0f, height + 0.1f, 0.2f);
        }
    }
}
