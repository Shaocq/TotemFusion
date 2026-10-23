using UnityEngine;

namespace TotemFusion;

internal class Ticker : MonoBehaviour
{
	private void Update()
	{
		try
		{
			TotemFusionView.TickDragHighlight();
		}
		catch
		{
		}
		try
		{
			if (TotemFusionView.IsOpen && Input.GetKeyDown((KeyCode)27))
			{
				Input.ResetInputAxes();
				TotemFusionView.Close();
			}
		}
		catch
		{
		}
	}
}
