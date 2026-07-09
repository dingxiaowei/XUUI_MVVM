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
                    --messageData = ''
                },
                computed = {
                    message = function(data)
                        return 'Hello ' .. data.info1.name .. '!'
                        --return data.messageData
                    end
                },
                commands = {
                    click = function(data)
                        print(data.info1.name)
                        --data.messageData='message' -- commands事件只改变数据，UI是由data数据改变驱动消耗
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
