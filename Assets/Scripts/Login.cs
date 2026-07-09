using UnityEngine;
using XUUI;

public class Login : MonoBehaviour
{
    Context context = null;

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
                            CS.XUUI.UI.RegisterPanel.OpenSelf()
                        end
                    end,
                },
            }
        ");

        context.Attach(gameObject);
    }

    void OnDestroy()
    {
        context.Dispose();
    }
}
