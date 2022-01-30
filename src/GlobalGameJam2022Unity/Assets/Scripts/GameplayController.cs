using Cinemachine;
using FMODUnity;
using GlobalGameJam2022;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameplayController : MonoBehaviour
{
	[Header("Managers")]
	[SerializeField] private DialogueManager Dialogue;
	[SerializeField] private VillagePlayerEngine Player;

	[Header("Transitions")]
	[SerializeField] private TransitionBase TransitionAfterKilling;
	[SerializeField] private float TransitionAfterKillingSpeed;

	[Space]
	[SerializeField] private TransitionBase TransitionFromDaytime;
	[SerializeField] private float TransitionFromDaytimeSpeed;

	[Space]
	[SerializeField] private TransitionBase TransitionFromNighttime;
	[SerializeField] private float TransitionFromNighttimeSpeed;

	[Header("Scenes")]
	public string VillageEnvironmentDaySceneId = "Village_Environment_Day";
	public string VillageEnvironmentNightSceneId = "Village_Environment_Night";
	public string VillageVillagersSceneId = "Village_Villagers";

	[Header("Dialogue")]
	public CinemachineVirtualCamera DialogueCamera;
	public DialogueTarget DialogueTarget;

	[Space]
	public DialogueTranscript IntroductionSequence;

	[Space]
	public DialogueTranscript FirstBloodlustSequence;
	public DialogueTranscript[] AlternateBloodlustSequences;

	[Space]
	public KillSequenceController KillSequence;

	[Header("Gameplay")]
	public float TimeUntilBloodlustMinimum = 35.0f;
	public float TimeUntilBloodlustMaximum = 100.0f;

	[Space]
	public float TimeUntilSunriseMinimum = 120.0f;
	public float TimeUntilSunriseMaximum = 120.0f;

	private void Awake()
	{
		Dialogue.gameObject.SetActive(true);
		Player.gameObject.SetActive(false);
		KillSequence.gameObject.SetActive(false);
	}

	private void Start()
	{
		StartCoroutine(GameplayLoop());
	}

	private IEnumerator GameplayLoop()
	{
		TransitionFromNighttime.SetTime(1.0f);

		var loadedEnvironmentDay = SceneManager.LoadScene(VillageEnvironmentDaySceneId, new LoadSceneParameters(LoadSceneMode.Additive));
		var loadedEnvironmentNight = SceneManager.LoadScene(VillageEnvironmentNightSceneId, new LoadSceneParameters(LoadSceneMode.Additive));
		var loadedVillagers = SceneManager.LoadScene(VillageVillagersSceneId, new LoadSceneParameters(LoadSceneMode.Additive));

		yield return null;

		// Move the villagers to the day scene.
		foreach (var villager in loadedVillagers.GetRootGameObjects())
		{
			SceneManager.MoveGameObjectToScene(villager, loadedEnvironmentDay);
		}

		SetActiveInScene(loadedEnvironmentDay, true);
		SetActiveInScene(loadedEnvironmentNight, false);

		var aliveCharacters = new List<VillageNPCEngine>();
		aliveCharacters.AddRange(FindObjectsOfType<VillageNPCEngine>(true));

		int dayNumber = 0;
		while (true)
		{
			dayNumber++;

			// # Daytime

			// ## Setup new day
			Player.transform.SetParent(null);
			SceneManager.MoveGameObjectToScene(Player.gameObject, loadedEnvironmentDay);
			SceneManager.MoveGameObjectToScene(gameObject, loadedEnvironmentDay);
			Player.gameObject.SetActive(true);

			SetActiveInScene(loadedEnvironmentDay, true);
			SetActiveInScene(loadedEnvironmentNight, false);

			RuntimeManager.StudioSystem.setParameterByName("Bloodlust", -1);
			RuntimeManager.StudioSystem.setParameterByName("TimeOfDay", 0);

			foreach (float time in new TimedLoop(TransitionFromNighttimeSpeed))
			{
				TransitionFromNighttime.SetTime(1.0f - time);
				yield return null;
			}

			if (dayNumber == 1)
			{
				Player.enabled = false;
				// yield return new WaitForSeconds(2.5f);

				if (IntroductionSequence != null)
				{
					Dialogue.gameObject.SetActive(true);
					yield return StartCoroutine(Dialogue.DialogueRoutine(IntroductionSequence));
				}
			}

			// ## Bountyboard Sequence
			// while (true)
			// {
			// 	yield return null;
			// 
			// 	if (Input.GetMouseButtonDown(0))
			// 	{
			// 		break;
			// 	}
			// }

			Player.enabled = true;


			VillageNPCEngine killedCharacter = null;
			float timeUntilBloodlust = Random.Range(TimeUntilBloodlustMinimum, TimeUntilBloodlustMaximum);

			// ## Daytime Gameplay
			while (true)
			{
				Player.activatedInteractable = null;
				yield return null;

				timeUntilBloodlust -= Time.deltaTime;

				if (Player.activatedInteractable is VillageNPCInteractable npcToTalkTo)
				{
					// Talk to a character
					Player.enabled = false;
					DialogueCamera.gameObject.SetActive(true);
					npcToTalkTo.Engine.ChangeState(npcToTalkTo.Engine.GetComponent<DialogueState>());

					DialogueTarget.SetTargets(new Transform[] { npcToTalkTo.Engine.Character.transform },
						npcToTalkTo.transform.position.x < Player.Character.transform.position.x ? DialogueSide.Left : DialogueSide.Right);

					DialogueCamera.LookAt = npcToTalkTo.Engine.Character.transform;

					yield return StartCoroutine(
						Dialogue.DialogueRoutine(npcToTalkTo.Engine.Character.Personality.DefaultDialogue[
							Random.Range(0, npcToTalkTo.Engine.Character.Personality.DefaultDialogue.Length)]));

					npcToTalkTo.Engine.ChangeState(npcToTalkTo.Engine.DefaultState);
					DialogueCamera.gameObject.SetActive(false);
					Player.enabled = true;
				}
				Player.activatedInteractable = null;

				if (timeUntilBloodlust <= 0.0f)
				{
					RuntimeManager.StudioSystem.setParameterByName("Bloodlust", 0);

					Player.enabled = false;

					foreach (var npc in aliveCharacters)
					{
						npc.Effects.PlayExclamation();
					}

					yield return new WaitForSeconds(0.1f);

					if (dayNumber == 1)
					{
						yield return StartCoroutine(
							Dialogue.DialogueRoutine(IntroductionSequence));
					}
					else
					{
						yield return StartCoroutine(
							Dialogue.DialogueRoutine(
								AlternateBloodlustSequences[Random.Range(0, AlternateBloodlustSequences.Length)]));
					}

					Player.enabled = true;
					RuntimeManager.StudioSystem.setParameterByName("Bloodlust", 1);

					foreach (var npc in aliveCharacters)
					{
						var fleeState = npc.GetComponent<FleeState>();
						if (fleeState != null)
						{
							fleeState.RunState(Player.transform);
						}
					}

					Player.activatedInteractable = null;
					Player.IsBloodlusted = true;
					Player.enabled = true;

					// wait until a player has killed a character
					// TODO: Add loose state if no characters are killed
					while (true)
					{
						if (Player.activatedInteractable is VillageNPCInteractable interactableNpc)
						{
							killedCharacter = interactableNpc.Engine;
							break;
						}
						yield return null;
					}
					break;
				}
			}

			Player.enabled = false;
			Player.IsBloodlusted = false;
			KillSequence.CharacterGraphic.sprite = killedCharacter.Character.Personality.DialogueGraphic;
			KillSequence.gameObject.SetActive(true);
			yield return new WaitForSeconds(5.0f);
			KillSequence.gameObject.SetActive(false);

			RuntimeManager.StudioSystem.setParameterByName("Bloodlust", 2);

			foreach (float time in new TimedLoop(TransitionAfterKillingSpeed))
			{
				TransitionAfterKilling.SetTime(time);
				yield return null;
			}

			TransitionAfterKilling.SetTime(0.0f);
			TransitionFromDaytime.SetTime(1.0f);

			aliveCharacters.Remove(killedCharacter);
			killedCharacter.gameObject.SetActive(false);

			RuntimeManager.StudioSystem.setParameterByName("Bloodlust", -1);

			// # Nighttime
			Player.transform.SetParent(null);
			SceneManager.MoveGameObjectToScene(Player.gameObject, loadedEnvironmentNight);
			SceneManager.MoveGameObjectToScene(gameObject, loadedEnvironmentNight);
			Player.gameObject.SetActive(true);

			SetActiveInScene(loadedEnvironmentDay, false);
			SetActiveInScene(loadedEnvironmentNight, true);

			RuntimeManager.StudioSystem.setParameterByName("TimeOfDay", 1);

			foreach (float time in new TimedLoop(TransitionFromDaytimeSpeed))
			{
				TransitionFromDaytime.SetTime(1.0f - time);
				yield return null;
			}

			// ## Nighttime Introduction
			if (Player.activatedInteractable != null)
			{
				while (true)
				{
					if (Input.GetMouseButtonDown(0))
					{
						break;
					}
					yield return null;
				}
			}

			Player.enabled = true;

			// ## Nighttime Gameplay
			yield return new WaitForSeconds(Random.Range(TimeUntilSunriseMinimum, TimeUntilSunriseMaximum));
			// while (true)
			// {
			// 	yield return null;
			// }

			foreach (float time in new TimedLoop(TransitionFromNighttimeSpeed))
			{
				TransitionFromNighttime.SetTime(time);
				yield return null;
			}
		}
	}

	private void SetActiveInScene(Scene scene, bool state)
	{
		var rootObjects = scene.GetRootGameObjects();
		for (int i = 0; i < rootObjects.Length; i++)
		{
			var rootObject = rootObjects[i];

			rootObject.SetActive(state);
		}
	}
}
