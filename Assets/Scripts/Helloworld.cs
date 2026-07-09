using UnityEngine;
using XUUI;

public class Helloworld : MonoBehaviour
{
    Context context = null;

    void Start()
    {
        context = new Context(@"
            return {
                data = {
                    info1 = {
                        name = 'John',
                    },
                },
                computed = {
                    message = function(data)
                        return 'Hello ' .. data.info1.name .. '!'
                    end
                },
                commands = {
                    click = function(data)
                        print(data.info.name)
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
