using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class DeadcordRuntime
{
    #region Shift and waves

    private void BeginShift()
    {
        _shiftStarted = true;
        _shiftAccepted = false;
        _shiftEnded = false;
        _shiftElapsed = 0f;
        _waveIndex = 0;
        _nextStoryIndex = 0;
        _nextNpcMessageAt = float.PositiveInfinity;
        _nextWaveAt = float.PositiveInfinity;
        _activeIncidents.Clear();
        GenerateInitialUsers(10);
        RefreshInstructions();
        RefreshStatusUI();
    }
    private float GetShiftDurationSeconds()
    {
        float duration = 0f;
        for (int i = 0; i < _waveRules.waves.Length; i++)
        {
            WaveDefinition wave = _waveRules.waves[i];

            if (wave != null)
                duration += Mathf.Max(1f, wave.durationSeconds);
        }
        return duration > 0f ? duration : 180f;
    }


    private void CompleteShift()
    {
        if (_shiftEnded)
            return;

        _shiftEnded = true;
        _nextNpcMessageAt = float.PositiveInfinity;
        _nextWaveAt = float.PositiveInfinity;
        _activeIncidents.Clear();

        if (_typingRowObject != null)
            _typingRowObject.SetActive(false);

        if (_inputRowObject != null)
            _inputRowObject.SetActive(false);

        RefreshTypingIndicator();
        RefreshStatusUI();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver(
                "СМЕНА ЗАКОНЧИЛАСЬ!\n"
            );
        }
    }

    private void AcceptRulesReaction()
    {
        if (_shiftAccepted || _shiftEnded)
            return;

        _shiftAccepted = true;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.MarkShiftStarted();
        }
        _shiftElapsed = 0f;
        _waveIndex = 0;
        _nextStoryIndex = 0;
        _nextNpcMessageAt = UnityEngine.Random.Range(npcMessageIntervalMin,
            Mathf.Max(npcMessageIntervalMin + 0.1f, npcMessageIntervalMax)) * npcMessageIntervalMultiplier;
        _nextWaveAt = _waveRules.waves != null && _waveRules.waves.Length > 0
            ? Mathf.Max(1f, _waveRules.waves[0].durationSeconds)
            : 45f;

        if (_acceptRulesButton != null)
            _acceptRulesButton.gameObject.SetActive(false);

        SetChannel(false);
        AddSystemMessage("✅ Вы приняли правила модерации. Смена 01 началась.");
        AddSystemMessage("Все сообщения пользователей идут в #общий. В #модерация находятся только инструкции.");
        if (_waveRules.waves != null && _waveRules.waves.Length > 0)
            AddSystemMessage(_waveRules.waves[0].announcement);

        RefreshInstructions();
        RefreshStatusUI();
    }

    private void AdvanceWaves()
    {
        if (_waveRules.waves == null || _waveRules.waves.Length == 0)
            return;

        while (_waveIndex < _waveRules.waves.Length - 1 && _shiftElapsed >= _nextWaveAt)
        {
            _waveIndex++;
            WaveDefinition wave = _waveRules.waves[_waveIndex];
            _nextWaveAt += Mathf.Max(1f, wave.durationSeconds);
            RefreshInstructions();
            AddSystemMessage(wave.announcement);
            RefreshStatusUI();
        }
    }

    private int CurrentWaveNumber
    {
        get
        {
            if (_waveRules == null || _waveRules.waves == null || _waveRules.waves.Length == 0) return 1;
            return Mathf.Clamp(_waveIndex + 1, 1, _waveRules.waves.Length);
        }
    }

    private void RefreshInstructions()
    {
        if (_rulesText == null || _waveRules == null || _waveRules.waves == null || _waveRules.waves.Length == 0)
            return;

        WaveDefinition wave = _waveRules.waves[Mathf.Clamp(_waveIndex, 0, _waveRules.waves.Length - 1)];
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine("МАНУЛЯТНИК   /   ПАМЯТКА МОДЕРАТОРА");
        builder.AppendLine("ДЕНЬ 01   •   " + wave.title);
        builder.AppendLine();
        builder.AppendLine("ЧТО ДЕЛАТЬ");
        builder.AppendLine();

        if (wave.instructions != null)
        {
            for (int i = 0; i < wave.instructions.Length; i++)
            {
                builder.Append("•   ");
                builder.AppendLine(wave.instructions[i]);
                builder.AppendLine();
            }
        }

        builder.AppendLine("КОМАНДЫ");
        builder.AppendLine("/ban @Ник — забанить нарушителя");
        builder.AppendLine("/mute @Ник — замутить пользователя (в текущих правилах не заменяет требуемый бан)");
        builder.AppendLine();
        builder.AppendLine("Все сообщения пользователей появляются только в #общий.");
        builder.AppendLine("Если не успеть, DeadcordHelper напечатает правильную команду за вас.");
        builder.AppendLine();
        if (!_shiftAccepted)
        {
            builder.AppendLine("ДЛЯ НАЧАЛА СМЕНЫ");
            builder.AppendLine("Нажмите реакцию ✅ «ПРИНЯТЬ ПРАВИЛА» внизу этого окна.");
            builder.AppendLine("Пока реакция не нажата, сообщения и таймеры не запускаются.");
        }
        else
        {
            builder.AppendLine("✅ Правила приняты. Смена идёт; сообщения появляются только в #общий.");
        }
        _rulesText.text = builder.ToString();
        if (_acceptRulesButton != null)
            _acceptRulesButton.gameObject.SetActive(!_shiftAccepted);
    }

    private void PlayStoryEvents()
    {
        if (!_shiftAccepted || _story == null || _story.events == null)
            return;

        while (_nextStoryIndex < _story.events.Length && _story.events[_nextStoryIndex].atSeconds * npcMessageIntervalMultiplier <= _shiftElapsed)
        {
            StoryEvent storyEvent = _story.events[_nextStoryIndex++];
            if (storyEvent == null)
                continue;

            if (!string.IsNullOrWhiteSpace(storyEvent.incidentCategory))
            {
                ViolationDefinition definition = FindViolationByCategory(storyEvent.incidentCategory);
                if (definition == null)
                    continue;

                bool isCustomStoryIncident = !string.IsNullOrWhiteSpace(storyEvent.username) && !string.IsNullOrWhiteSpace(storyEvent.text);
                if (!isCustomStoryIncident && !IsCategoryUnlocked(definition.category))
                    continue;

                string offenderName = !string.IsNullOrWhiteSpace(storyEvent.username)
                    ? storyEvent.username
                    : GenerateUsername();

                UserData offender = GetOrCreateUser(offenderName);

                string violationText = !string.IsNullOrWhiteSpace(storyEvent.text)
                    ? storyEvent.text
                    : Pick(definition.messages, "Нарушение правил");

                AddMessage(offender.username, violationText, "npc");
                StartIncident(offender, definition, violationText, true);
                continue;
            }

            if (string.IsNullOrWhiteSpace(storyEvent.text) || string.IsNullOrWhiteSpace(storyEvent.username))
                continue;

            UserData user = GetOrCreateUser(storyEvent.username);
            AddMessage(user.username, storyEvent.text, GetStoryRole(storyEvent.speakerKey));
        }
    }

    private string GetStoryRole(string speakerKey)
    {
        if (string.Equals(speakerKey, "boss", StringComparison.OrdinalIgnoreCase)) return "boss";
        if (string.Equals(speakerKey, "helper", StringComparison.OrdinalIgnoreCase)) return "helper";
        return "npc";
    }

    #endregion
}
