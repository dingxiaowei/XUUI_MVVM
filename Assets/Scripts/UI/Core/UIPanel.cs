using System;
using System.Collections.Generic;
using UnityEngine;
using XLua;

namespace XUUI.UI
{
    public class UIPanel : UIBase
    {
        protected Context context;
        protected LuaEnv luaEnv;
        protected Dictionary<string, UIView> views = new Dictionary<string, UIView>();
        protected Transform viewContainer;

        protected virtual string PrefabPath => "UI/Panels/" + GetType().Name;
        protected virtual string LuaModulePath => "UI.Panels." + GetType().Name;

        public void InitWithLua(GameObject go, LuaEnv luaEnv)
        {
            this.luaEnv = luaEnv;

            base.Init(go);

            // Find view container child, fallback to self transform
            var container = go.transform.Find("Views");
            viewContainer = container != null ? container : go.transform;

            var script = $"return require('{LuaModulePath}')";
            context = new Context(script, luaEnv);
            RegisterCSharpModules();
            context.Attach(go);
        }

        protected virtual void RegisterCSharpModules()
        {
        }

        public T OpenView<T>(params object[] args) where T : UIView, new()
        {
            string viewName = typeof(T).Name;

            // If already open, return it
            if (views.TryGetValue(viewName, out var existing) && existing != null)
            {
                if (existing.IsOpen)
                    return (T)existing;

                existing.Open(args);
                return (T)existing;
            }

            // Load prefab
            string prefabPath = "UI/Views/" + viewName;
            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"UIPanel: Failed to load view prefab at '{prefabPath}'");
                return null;
            }

            // Instantiate under view container
            var go = UnityEngine.Object.Instantiate(prefab, viewContainer);
            go.name = viewName;

            // Create view
            string luaModulePath = "UI.Views." + viewName;
            var view = new T();
            view.InitWithLua(go, luaEnv, luaModulePath);
            view.Open(args);
            views[viewName] = view;
            return view;
        }

        public void CloseView(string viewName)
        {
            if (views.TryGetValue(viewName, out var view) && view != null)
            {
                view.Close();
            }
        }

        public void CloseAllViews()
        {
            foreach (var kv in views)
            {
                if (kv.Value != null && kv.Value.IsOpen)
                {
                    kv.Value.Close();
                }
            }
        }

        public T GetView<T>() where T : UIView
        {
            string viewName = typeof(T).Name;
            if (views.TryGetValue(viewName, out var view) && view != null)
            {
                return (T)view;
            }
            return null;
        }

        public override void Close()
        {
            CloseAllViews();
            base.Close();
        }

        public override void Dispose()
        {
            // Dispose all views first
            foreach (var kv in views)
            {
                if (kv.Value != null)
                {
                    kv.Value.Dispose();
                }
            }
            views.Clear();

            // Dispose context (does NOT destroy shared LuaEnv)
            if (context != null)
            {
                context.Dispose();
                context = null;
            }

            // Unload Lua module from cache so next open gets a fresh state
            if (luaEnv != null)
            {
                var package = luaEnv.Global.GetInPath<LuaTable>("package");
                if (package != null)
                {
                    var loaded = package.Get<LuaTable>("loaded");
                    if (loaded != null)
                    {
                        loaded.Set<string, object>(LuaModulePath, null);
                    }
                }
            }

            luaEnv = null;

            base.Dispose();
        }
    }
}
