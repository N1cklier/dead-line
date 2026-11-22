using System;
using System.Collections;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum TutorialState { Intro, NeedFood, NeedDog, Complete }

    [Header("Data (JSON lives under Assets/Resources/TutorialData)")]
    [SerializeField] private string dataFolder = "TutorialData";
    [SerializeField] private string dataFileName = "TutorialDialogue";

    [Serializable]
    private class TutorialDialogueData
    {
        public string[] introLines;
        public string[] outroLines;
        public string[] computerBlockedWhenBoth;
        public string[] computerBlockedOnlyFood;
        public string[] computerBlockedOnlyDog;
    }
    private TutorialDialogueData _data;
    [Header("Костыль, не трогайте")]
    [TextArea]
    public string[] introLines = {
        "Так, я дома. Наконец-то.",
        "Сначала надо поесть самому, а потом взять мясо в холодильнике и покормить собаку.",
        "Пока оба дела не сделаны — за компьютер даже не сажусь."
    };
    [TextArea] public string[] outroLines = { "Ну всё — сам поел, собаку накормил. Пора приступать к смене." };
    [TextArea] public string[] computerBlockedWhenBoth = { "Не сейчас. Сначала поесть и покормить собаку." };
    [TextArea] public string[] computerBlockedOnlyFood = { "Ты ещё не поел сам. Сначала еда." };
    [TextArea] public string[] computerBlockedOnlyDog = { "Собака голодная. Сначала покорми её." };

    public TutorialState State { get; private set; } = TutorialState.Intro;
    public bool HasEaten { get; private set; }
    public bool HasFedDog { get; private set; }
    public bool IntroFinished { get; private set; }
    public bool OutroFinished { get; private set; }

    public static bool IsTutorialComplete =>
        Instance != null && Instance.IntroFinished && Instance.OutroFinished
        && Instance.HasEaten && Instance.HasFedDog;

    public static bool IsDialogueActive =>
        Instance != null && Instance._ui != null && Instance._ui.IsShowing;

    private TutorialDialogueUI _ui;
    private bool _outroStarted;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadDialogueData();
    }

    void LoadDialogueData()
    {
        string path = dataFolder + "/" + dataFileName;
        TextAsset asset = Resources.Load<TextAsset>(path);

        if (asset == null)
        {
            Debug.LogWarning("[Tutorial] Не найден Resources/" + path + ".json — используются строки из инспектора.", this);
            return;
        }

        try
        {
            _data = JsonUtility.FromJson<TutorialDialogueData>(asset.text);
            if (_data == null) return;

            if (NotEmpty(_data.introLines)) introLines = _data.introLines;
            if (NotEmpty(_data.outroLines)) outroLines = _data.outroLines;
            if (NotEmpty(_data.computerBlockedWhenBoth)) computerBlockedWhenBoth = _data.computerBlockedWhenBoth;
            if (NotEmpty(_data.computerBlockedOnlyFood)) computerBlockedOnlyFood = _data.computerBlockedOnlyFood;
            if (NotEmpty(_data.computerBlockedOnlyDog)) computerBlockedOnlyDog = _data.computerBlockedOnlyDog;
        }
        catch (Exception exception)
        {
            Debug.LogError("[Tutorial] Ошибка чтения JSON: " + exception.Message, this);
        }
    }

    static bool NotEmpty(string[] arr) => arr != null && arr.Length > 0;

    void Start()
    {
        _ui = FindObjectOfType<TutorialDialogueUI>();
        if (_ui == null)
        {
            var go = new GameObject("TutorialDialogueUI");
            _ui = go.AddComponent<TutorialDialogueUI>();
        }
        StartCoroutine(StartIntroNextFrame());
    }

    IEnumerator StartIntroNextFrame()
    {
        yield return null;
        ShowIntro();
    }

    void ShowIntro()
    {
        State = TutorialState.Intro;
        _ui.ShowLines(introLines, () =>
        {
            IntroFinished = true;
            UpdateState();
        });
    }

    public void NotifyPlayerAte()
    {
        if (HasEaten) return;
        HasEaten = true;
        UpdateState();
    }

    public void NotifyDogFed()
    {
        if (HasFedDog) return;
        HasFedDog = true;
        UpdateState();
    }

    public void ShowComputerBlockedMessage()
    {
        if (!IntroFinished || IsTutorialComplete) return;
        if (IsDialogueActive) return;

        string[] lines;
        if (!HasEaten && !HasFedDog) lines = computerBlockedWhenBoth;
        else if (!HasEaten) lines = computerBlockedOnlyFood;
        else lines = computerBlockedOnlyDog;

        _ui.ShowLines(lines, null);
    }

    void UpdateState()
    {
        if (!IntroFinished) return;

        if (HasEaten && HasFedDog)
        {
            State = TutorialState.Complete;
            if (!_outroStarted && !IsDialogueActive)
            {
                _outroStarted = true;
                StartCoroutine(ShowOutroDelayed());
            }
        }
        else if (!HasEaten && HasFedDog) State = TutorialState.NeedFood;
        else if (HasEaten && !HasFedDog) State = TutorialState.NeedDog;
        else State = TutorialState.NeedFood;
    }

    IEnumerator ShowOutroDelayed()
    {
        yield return new WaitForSeconds(0.8f);
        if (IsDialogueActive) yield break;
        _ui.ShowLines(outroLines, () =>
        {
            OutroFinished = true;
            State = TutorialState.Complete;
        });
    }
}