using UnityEngine;
using UnityEngine.UIElements;

namespace StoreManager
{
    /// <summary>
    /// The UI Toolkit interface ("retail till" style). It is being moved over from the IMGUI Hud screen by
    /// screen: this draws the floor HUD, world labels and the start screen; the Hud still draws the office
    /// computer and build mode, and keeps the input handling and state (selection, alerts, notices).
    /// Not compiled in the headless test (Tools/Headless skips UI/Toolkit).
    /// </summary>
    public class StoreUI : MonoBehaviour
    {
        public static StoreUI I;

        UIDocument doc;
        VisualElement root;
        FloorHud floor;
        StartScreen start;
        WorldLabels labels;
        bool built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            new GameObject("Store UI").AddComponent<StoreUI>();
        }

        void Awake()
        {
            I = this;
            Hud.Toolkit = true;
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "Store UI";
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/StoreTheme");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 1f;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = ps;
        }

        void OnDestroy()
        {
            if (I == this) { I = null; Hud.Toolkit = false; Hud.PointerOverToolkit = false; }
        }

        void Build()
        {
            root = doc.rootVisualElement;
            if (root == null) return;
            var sheet = Resources.Load<StyleSheet>("UI/Store");
            if (sheet != null) root.styleSheets.Add(sheet);
            root.AddToClassList("root");
            root.pickingMode = PickingMode.Ignore;

            labels = new WorldLabels(root);
            floor = new FloorHud(root);
            start = new StartScreen(root);
            built = true;
        }

        void Update()
        {
            var store = Store.I;
            if (store == null || store.Avatar == null || Hud.I == null) return;
            if (!built) { Build(); if (!built) return; }

            var hud = Hud.I;
            bool atPc = store.Avatar.AtDesk && hud.OnPC;
            bool startScreen = atPc && !store.StartChosen;

            start.Refresh(store, startScreen);
            floor.Refresh(store, hud, !atPc);
            labels.Refresh(store, hud, !atPc);
            hud.AlertsHeight = floor.AlertsBottom - 48f;

            // Tell the game when the mouse is over the UI, so clicks don't also select things in the world.
            var panel = root.panel;
            bool over = false;
            if (panel != null)
            {
                Vector2 m = GameInput.MousePosition;
                var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(m.x, Screen.height - m.y));
                var picked = panel.Pick(p);
                over = picked != null;
            }
            Hud.PointerOverToolkit = over;
        }
    }
}
