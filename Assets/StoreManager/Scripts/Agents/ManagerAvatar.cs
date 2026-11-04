using UnityEngine;

namespace StoreManager
{
    /// <summary>The player: walks the floor, talks to staff, deals with complaints, sits at the office desk.</summary>
    public class ManagerAvatar : Agent
    {
        public bool AtDesk;
        public const float TalkRange = 3f;
        public const float ComplaintRange = 2.5f;

        protected override bool StaffAccess { get { return true; } }
        public override float MoveSpeed { get { return 4.2f; } }

        public bool InOffice { get { return Store.I.Grid.ZoneAt(Tile) == Zone.Office; } }

        protected override void Update()
        {
            var store = Store.I;
            var cam = store.Cam;

            if (GameInput.Down(GameKey.Tab))
            {
                if (AtDesk) StandUp();
                else if (InOffice) SitDown();
                else store.Message("Your desk is in the office (back left).");
            }

            // Scaled time so pausing stops you, but capped at 2x so you stay controllable at 8x.
            float dt = Mathf.Min(Time.deltaTime, Time.unscaledDeltaTime * 2f);

            if (!AtDesk)
            {
                Vector3 move = Vector3.zero;
                if (cam != null)
                {
                    if (GameInput.Held(GameKey.W)) move += cam.FlatForward;
                    if (GameInput.Held(GameKey.S)) move -= cam.FlatForward;
                    if (GameInput.Held(GameKey.D)) move += cam.FlatRight;
                    if (GameInput.Held(GameKey.A)) move -= cam.FlatRight;
                }
                if (move.sqrMagnitude > 0.01f)
                {
                    Stop();
                    Vector3 step = move.normalized * MoveSpeed * dt;
                    TryMove(step);
                    Face(move);
                }
                else
                {
                    FollowPath(dt);
                }
            }

            // Walking up to a complaining customer deals with it.
            if (!AtDesk)
            {
                var list = store.Complaints;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var c = list[i];
                    if (c != null && c.Complaining && DistanceTo(c.transform.position) < ComplaintRange)
                    {
                        c.Resolve(this, 30f);
                        store.Message("You calmed down " + c.DisplayName + ".");
                    }
                }
            }
        }

        void TryMove(Vector3 step)
        {
            var grid = Store.I.Grid;
            Vector3 p = transform.position;
            if (Free(grid, p + step)) { transform.position = p + step; return; }
            var sx = new Vector3(step.x, 0f, 0f);
            if (Free(grid, p + sx)) { transform.position = p + sx; return; }
            var sz = new Vector3(0f, 0f, step.z);
            if (Free(grid, p + sz)) transform.position = p + sz;
        }

        static bool Free(StoreGrid grid, Vector3 p)
        {
            const float r = 0.25f;
            return grid.WalkableAt(p, true)
                && grid.WalkableAt(p + new Vector3(r, 0f, r), true)
                && grid.WalkableAt(p + new Vector3(-r, 0f, r), true)
                && grid.WalkableAt(p + new Vector3(r, 0f, -r), true)
                && grid.WalkableAt(p + new Vector3(-r, 0f, -r), true);
        }

        public void SitDown()
        {
            Stop();
            AtDesk = true;
            Teleport(Store.I.DeskSeat);
            Face(Vector3.forward);
            if (Hud.I != null) Hud.I.OpenPC();
        }

        public void StandUp()
        {
            AtDesk = false;
            if (Hud.I != null) Hud.I.LeaveDesk();
        }

        public void WalkTo(Vector2Int tile)
        {
            if (AtDesk) return;
            GoTo(tile);
        }
    }
}
