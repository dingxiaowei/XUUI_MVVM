using System;
using System.Collections.Generic;
using UnityEngine;
using XLua;

namespace XUUI.UI
{
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("UIManager");
                    _instance = go.AddComponent<UIManager>();
                }
                return _instance;
            }
        }

        [SerializeField] private Transform panelRoot;
        [SerializeField] private bool dontDestroyOnLoad = true;

        public Transform PanelRoot
        {
            get
            {
                if (panelRoot != null)
                    return panelRoot;

                var canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    panelRoot = canvas.transform;
                    return panelRoot;
                }

                var go = new GameObject("UIRoot");
                go.AddComponent<Canvas>();
                go.AddComponent<UnityEngine.UI.CanvasScaler>();
                panelRoot = go.transform;
                return panelRoot;
            }
        }

        private LuaEnv luaEnv;
        private Dictionary<Type, UIPanel> activePanels = new Dictionary<Type, UIPanel>();
        private Dictionary<Type, string> prefabOverrides = new Dictionary<Type, string>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            luaEnv = new LuaEnv();
        }

        public void Register<T>() where T : UIPanel, new()
        {
            // Use default path convention: UI/Panels/{TypeName}
            Type type = typeof(T);
            if (!activePanels.ContainsKey(type))
            {
                activePanels[type] = null; // placeholder, panel not created yet
            }
        }

        public void Register<T>(string prefabPath) where T : UIPanel, new()
        {
            Register<T>();
            prefabOverrides[typeof(T)] = prefabPath;
        }

        public T OpenPanel<T>(params object[] args) where T : UIPanel, new()
        {
            Type type = typeof(T);

            // If already open, return it
            if (activePanels.TryGetValue(type, out var existing) && existing != null)
            {
                if (existing.IsOpen)
                    return (T)existing;

                existing.Open(args);
                return (T)existing;
            }

            // Resolve prefab path
            string prefabPath;
            if (!prefabOverrides.TryGetValue(type, out prefabPath))
            {
                prefabPath = "UI/Panels/" + type.Name;
            }

            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"UIManager: Failed to load prefab at '{prefabPath}' for panel {type.Name}");
                return null;
            }

            var go = Instantiate(prefab, PanelRoot);
            go.name = type.Name;

            var panel = new T();
            panel.InitWithLua(go, luaEnv);
            panel.Open(args);
            activePanels[type] = panel;
            return (T)panel;
        }

        public void ClosePanel<T>(bool destroy = true)
        {
            Type type = typeof(T);
            if (activePanels.TryGetValue(type, out var panel) && panel != null)
            {
                if (destroy)
                {
                    panel.Dispose();
                    activePanels.Remove(type);
                }
                else
                {
                    panel.Close();
                }
            }
        }

        public T GetPanel<T>() where T : UIPanel
        {
            Type type = typeof(T);
            if (activePanels.TryGetValue(type, out var panel) && panel != null)
            {
                return (T)panel;
            }
            return null;
        }

        public bool IsPanelOpen<T>() where T : UIPanel
        {
            var panel = GetPanel<T>();
            return panel != null && panel.IsOpen;
        }

        public void PreloadPanel<T>() where T : UIPanel, new()
        {
            Type type = typeof(T);
            if (activePanels.TryGetValue(type, out var existing) && existing != null)
                return; // Already loaded

            string prefabPath;
            if (!prefabOverrides.TryGetValue(type, out prefabPath))
            {
                prefabPath = "UI/Panels/" + type.Name;
            }

            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"UIManager: Failed to load prefab at '{prefabPath}' for panel {type.Name}");
                return;
            }

            var go = Instantiate(prefab, PanelRoot);
            go.name = type.Name;
            go.SetActive(false);

            var panel = new T();
            panel.InitWithLua(go, luaEnv);
            activePanels[type] = panel;
        }

        private void OnDestroy()
        {
            // Dispose all panels in reverse order
            var panels = new List<UIPanel>(activePanels.Values);
            panels.Reverse();
            foreach (var panel in panels)
            {
                if (panel != null)
                    panel.Dispose();
            }
            activePanels.Clear();

            // XLua GC sequence: collect before disposing LuaEnv
            if (luaEnv != null)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                luaEnv.Dispose();
                luaEnv = null;
            }

            _instance = null;
        }
    }
}
