using UnityEngine;
using XUUI;

public class Login : MonoBehaviour
{
    Context context = null;
    static Context registerContext = null;

    void Start()
    {
        context = new Context(@"
            return {
                data = {
                    username = '',
                    password = '',
                    message = 'Please login'
                },
                commands = {
                    login = function(data)
                        if data.username == 'admin' and data.password == '123456' then
                            data.message = 'Login successful!'
                        else
                            data.message = 'Invalid username or password!'
                            CS.Login.LoadRegisterPanel()
                        end
                    end,
                },
            }
        ");

        context.Attach(gameObject);
    }

    void Update()
    {
        if (registerContext != null && GameObject.Find("RegisterPanel") == null)
        {
            registerContext.Dispose();
            registerContext = null;
        }
    }

    void OnDestroy()
    {
        if (registerContext != null)
        {
            registerContext.Dispose();
            registerContext = null;
        }
        context.Dispose();
    }

    public static void LoadRegisterPanel()
    {
        if (registerContext != null || GameObject.Find("RegisterPanel") != null)
            return;

        var prefab = Resources.Load<GameObject>("RegisterPanel");
        if (prefab == null)
        {
            Debug.LogError("Failed to load RegisterPanel prefab!");
            return;
        }
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("Canvas not found!");
            return;
        }
        var go = Instantiate(prefab, canvas.transform, false);
        go.name = "RegisterPanel";

        registerContext = new Context(@"
            return require('register')
        ");
        registerContext.Attach(go);
    }

    public static void CloseRegisterPanel()
    {
        var go = GameObject.Find("RegisterPanel");
        if (go != null)
            Destroy(go);
    }
}
