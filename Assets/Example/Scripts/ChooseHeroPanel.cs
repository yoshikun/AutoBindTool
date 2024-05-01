using UnityEngine;
using UnityEngine.UI;

namespace Gordian
{

    public class ChooseHeroPanel : AutoBindView
    {

        /* COMPONENT FIELDS */

        private Button m_Button_Test;
        private Button m_Button_Close;

        protected override void BindComponents(GameObject go)
        {
            var autoBindTool = go.GetComponent<AutoBindTool>();

            m_Button_Test = autoBindTool.BindComponents<Button>(0);
            m_Button_Close = autoBindTool.BindComponents<Button>(1);
        }

        /* COMPONENT FIELDS END */

        protected override void InitEvent()
        {
            base.InitEvent();

            m_Button_Close.onClick.AddListener(OnButtonClose);
        }

        private void OnButtonClose()
        {
            Debug.Log("OnButtonClose");
        }
    }
}
