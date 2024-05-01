using UnityEngine;
using UnityEngine.UI;

namespace Gordian
{

	public class HeroItem : AutoBindView
	{

		/* COMPONENT FIELDS */
		private Button m_Button_Icon;

		protected override void BindComponents(GameObject go)
		{
			var autoBindTool = go.GetComponent<AutoBindTool>();

			m_Button_Icon = autoBindTool.BindComponents<Button>(0);
		}

		/* COMPONENT FIELDS END */
	}
}
