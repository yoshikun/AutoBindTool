using UnityEngine;

public abstract class AutoBindView : MonoBehaviour
{
    protected void Awake()
    {
        InitView();
        InitEvent();
    }

    protected virtual void InitView()
    {
        BindComponents(gameObject);
    }

    protected virtual void InitEvent()
    {
    }

    protected virtual void BindComponents(GameObject go)
    {
    }
}
