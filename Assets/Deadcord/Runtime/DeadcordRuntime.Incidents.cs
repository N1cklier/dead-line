using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class DeadcordRuntime
{
    #region NPCs and violations

    private void GenerateInitialUsers(int count)
    {
        for (int i = 0; i < count; i++)
            GetOrCreateUser(GenerateUsername());
    }

    private string GenerateUsername()
    {
        for (int attempt = 0; attempt < 150; attempt++)
        {
            string first = Pick(_nameParts.first, "Player");
            string second = Pick(_nameParts.second, "User");
            string third = Pick(_nameParts.third, UnityEngine.Random.Range(10, 99).ToString());
            string candidate = first + second + third;
            if (_usedNames.Add(candidate))
                return candidate;
        }

        string fallback;
        do { fallback = "Player_" + UnityEngine.Random.Range(1000, 999999); }
        while (!_usedNames.Add(fallback));
        return fallback;
    }

    private string Pick(string[] values, string fallback)
    {
        if (values == null || values.Length == 0) return fallback;
        return values[UnityEngine.Random.Range(0, values.Length)];
    }

    private UserData GetOrCreateUser(string username)
    {
        string safeName = string.IsNullOrWhiteSpace(username) ? GenerateUsername() : username.Trim().TrimStart('@');
        if (_usersByName.TryGetValue(safeName, out UserData existing))
            return existing;

        UserData user = new UserData
        {
            username = safeName,
            colorIndex = UnityEngine.Random.Range(0, _avatarColors.Length),
            banned = false,
            mutedUntil = 0f
        };
        _users.Add(user);
        _usersByName[safeName] = user;
        _usedNames.Add(safeName);
        return user;
    }

    private UserData PickActiveUser()
    {
        List<UserData> candidates = new List<UserData>();
        for (int i = 0; i < _users.Count; i++)
        {
            UserData user = _users[i];
            if (!user.banned && user.mutedUntil <= _shiftElapsed)
                candidates.Add(user);
        }

        if (candidates.Count == 0)
            return GetOrCreateUser(GenerateUsername());
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void SpawnNpcMessageIfDue()
    {
        if (_shiftElapsed < _nextNpcMessageAt)
            return;

        _nextNpcMessageAt = _shiftElapsed + UnityEngine.Random.Range(npcMessageIntervalMin,
            Mathf.Max(npcMessageIntervalMin + 0.1f, npcMessageIntervalMax)) * npcMessageIntervalMultiplier;
        UserData speaker = PickActiveUser();

        if (_shiftElapsed >= 20f && _activeIncidents.Count == 0 && UnityEngine.Random.value < violationChance)
        {
            List<ViolationDefinition> available = GetAvailableViolations();
            if (available.Count > 0)
            {
                ViolationDefinition selected = available[UnityEngine.Random.Range(0, available.Count)];
                string violationText = Pick(selected.messages, "Нарушение правил");
                AddMessage(speaker.username, violationText, "npc");
                StartIncident(speaker, selected, violationText, false);
                return;
            }
        }

        AddMessage(speaker.username, Pick(_normalMessages.messages, "Привет, чат!"), "npc");
    }

    private List<ViolationDefinition> GetAvailableViolations()
    {
        List<ViolationDefinition> result = new List<ViolationDefinition>();
        if (_violations == null || _violations.incidents == null) return result;

        foreach (ViolationDefinition definition in _violations.incidents)
        {
            if (definition != null && definition.minWave <= CurrentWaveNumber && IsCategoryUnlocked(definition.category))
                result.Add(definition);
        }
        return result;
    }

    private ViolationDefinition FindViolationByCategory(string category)
    {
        if (_violations == null || _violations.incidents == null) return null;
        foreach (ViolationDefinition definition in _violations.incidents)
        {
            if (definition != null && string.Equals(definition.category, category, StringComparison.OrdinalIgnoreCase))
                return definition;
        }
        return null;
    }

    private bool IsCategoryUnlocked(string category)
    {
        if (string.IsNullOrWhiteSpace(category) || _waveRules == null || _waveRules.waves == null || _waveRules.waves.Length == 0)
            return false;
        WaveDefinition wave = _waveRules.waves[Mathf.Clamp(_waveIndex, 0, _waveRules.waves.Length - 1)];
        if (wave.unlockedCategories == null) return false;
        foreach (string unlocked in wave.unlockedCategories)
        {
            if (string.Equals(unlocked, category, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void StartIncident(UserData target, ViolationDefinition definition, string sourceText, bool forceStory = false)
    {
        if (target == null || definition == null || target.banned)
            return;

        if (!forceStory && (_activeIncidents.Count > 0 || !IsCategoryUnlocked(definition.category)))
            return;

        float delay = helperDeadlineBaseSeconds + UnityEngine.Random.Range(-helperDeadlineVarianceSeconds, helperDeadlineVarianceSeconds);
        delay = Mathf.Max(1f, delay);
        _activeIncidents.Add(new IncidentData
        {
            id = ++_incidentSequence,
            target = target,
            category = definition.category,
            requiredAction = string.IsNullOrWhiteSpace(definition.requiredAction) ? "ban" : definition.requiredAction.ToLowerInvariant(),
            resolved = false,
            helperTyping = false,
            deadlineAt = _shiftElapsed + delay,
            helperSendAt = -1f,
            originalText = sourceText
        });
        RefreshTypingIndicator();
    }

    private void UpdateIncidentTimer()
    {
        if (_activeIncidents.Count == 0)
        {
            RefreshTypingIndicator();
            return;
        }

        bool stateChanged = false;
        for (int i = _activeIncidents.Count - 1; i >= 0; i--)
        {
            IncidentData incident = _activeIncidents[i];
            if (incident == null || incident.resolved)
            {
                _activeIncidents.RemoveAt(i);
                stateChanged = true;
                continue;
            }

            if (!incident.helperTyping && _shiftElapsed >= incident.deadlineAt - helperTypingSeconds)
            {
                incident.helperTyping = true;
                incident.helperSendAt = incident.deadlineAt;
                stateChanged = true;
            }

            if (incident.helperTyping && _shiftElapsed >= incident.helperSendAt)
            {
                HelperResolveIncident(incident);
                stateChanged = true;
            }
        }

        if (stateChanged)
            RefreshTypingIndicator();
    }

    private void HelperResolveIncident(IncidentData incident)
    {
        if (incident == null || incident.resolved)
            return;

        string command = "/" + incident.requiredAction + " @" + incident.target.username;
        AddMessage(helperUsername, command, "helper", "💀   +1");
        ApplyActionToUser(incident.target, incident.requiredAction);
        incident.resolved = true;
        _activeIncidents.Remove(incident);
        ApplyReputationChange(false);
        RefreshTypingIndicator();
        AddSystemMessage("Помощник перехватил нарушение. Репутация снижена.");
        RefreshStatusUI();
    }

    #endregion
}
