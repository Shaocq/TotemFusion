using System.Collections;
using ItemStatsSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TotemFusion;

public class TotemFusionInstaller : MonoBehaviour
{
	private void Awake()
	{
		Object.DontDestroyOnLoad((Object)(object)((Component)this).gameObject);
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void Start()
	{
		((MonoBehaviour)this).StartCoroutine(SetupSoon());
		((MonoBehaviour)this).StartCoroutine(SetupModSetting());
		((MonoBehaviour)this).StartCoroutine(SetupOverrides());
	}

	private IEnumerator SetupModSetting()
	{
		yield return (object)new WaitForSeconds(2f);
		for (int i = 0; i < 3; i++)
		{
			try
			{
				if (ModSettingBridge.TryBuild())
				{
					break;
				}
			}
			catch
			{
			}
			yield return (object)new WaitForSeconds(3f);
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		((MonoBehaviour)this).StartCoroutine(SetupSoon());
	}

	private IEnumerator SetupSoon()
	{
		yield return (object)new WaitForSeconds(0.5f);
		TotemFusionLogic.InstallTotemOptions();
	}

	private IEnumerator SetupOverrides()
	{
		int[] array = new int[6] { 2, 5, 10, 20, 40, 80 };
		int[] array2 = array;
		foreach (int num in array2)
		{
			yield return (object)new WaitForSeconds((float)num);
			try
			{
				LevelManager instance = LevelManager.Instance;
				object obj;
				if (instance == null)
				{
					obj = null;
				}
				else
				{
					CharacterMainControl mainCharacter = instance.MainCharacter;
					if (mainCharacter == null)
					{
						obj = null;
					}
					else
					{
						Item characterItem = mainCharacter.CharacterItem;
						obj = ((characterItem != null) ? characterItem.Inventory : null);
					}
				}
				Inventory val = (Inventory)obj;
				if ((Object)(object)val != (Object)null && val.Content != null && val.Content.Count > 0)
				{
					TotemFusionLogic.RepairFused();
				}
			}
			catch
			{
			}
		}
	}
}
