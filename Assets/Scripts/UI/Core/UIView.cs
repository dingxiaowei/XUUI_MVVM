using UnityEngine;
using XLua;

namespace XUUI.UI
{
    public class UIView : UIBase
    {
        protected Context context;
        protected LuaEnv luaEnv;
        protected string luaModulePath;

        public void InitWithLua(GameObject go, LuaEnv luaEnv, string luaModulePath)
        {
            this.luaEnv = luaEnv;
            this.luaModulePath = luaModulePath;

            base.Init(go);

            var script = $"return require('{luaModulePath}')";
            context = new Context(script, luaEnv);
            RegisterCSharpModules();
            context.Attach(go);
        }

        protected virtual void RegisterCSharpModules()
        {
        }

        public override void Dispose()
        {
            if (context != null)
            {
                context.Dispose();
                context = null;
            }
            luaEnv = null;
            base.Dispose();
        }
    }
}
