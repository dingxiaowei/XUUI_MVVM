namespace XUUI.UI
{
    public class RegisterPanel : UIPanel
    {
        public static void OpenSelf()
        {
            UIManager.Instance.OpenPanel<RegisterPanel>();
        }

        public static void CloseSelf()
        {
            UIManager.Instance.ClosePanel<RegisterPanel>();
        }
    }
}
