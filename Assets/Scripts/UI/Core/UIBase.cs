using UnityEngine;

namespace XUUI.UI
{
    public abstract class UIBase
    {
        public bool IsOpen { get; protected set; }
        public bool IsLoaded { get; protected set; }
        public GameObject GameObject { get; protected set; }
        public Transform Transform { get; protected set; }
        public string Name { get; protected set; }

        public virtual void Init(GameObject go)
        {
            GameObject = go;
            Transform = go.transform;
            Name = go.name;
            IsLoaded = true;
        }

        public virtual void Open(params object[] args)
        {
            if (GameObject != null)
            {
                GameObject.SetActive(true);
            }
            IsOpen = true;
        }

        public virtual void Close()
        {
            if (GameObject != null)
            {
                GameObject.SetActive(false);
            }
            IsOpen = false;
        }

        public virtual void Dispose()
        {
            Close();
            if (GameObject != null)
            {
                Object.Destroy(GameObject);
                GameObject = null;
            }
            Transform = null;
            IsLoaded = false;
            IsOpen = false;
        }
    }
}
