using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class DeadcordRuntime
{
    #region Input and moderation commands

    private void SubmitCommandInput()
    {
        if (_commandInput == null || _showingModeration)
            return;

        string input = _commandInput.text;
        if (string.IsNullOrWhiteSpace(input))
            return;

        _commandInput.SetTextWithoutNotify(string.Empty);
        ProcessPlayerInput(input.Trim());

        if (_commandInput != null && _commandInput.gameObject.activeInHierarchy)
        {
            _commandInput.ActivateInputField();
            _commandInput.Select();
        }
    }

    private void ProcessPlayerInput(string input)
    {
        if (!input.StartsWith("/", StringComparison.Ordinal))
        {
            AddMessage("Вы", input, "player");
            return;
        }

        string[] parts = input.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            AddMessage("Вы", input, "player", "💀   -1");
            AddSystemMessage("Формат команды: /ban @Ник или /mute @Ник");
            ApplyReputationChange(false);
            RefreshStatusUI();
            return;
        }

        string action = parts[0].Trim().ToLowerInvariant();
        if (action != "/ban" && action != "/mute" && action != "/unmute")
        {
            AddMessage("Вы", input, "player");
            AddSystemMessage("Неизвестная команда. Доступны /ban, /mute и /unmute.");
            return;
        }

        string targetName = parts[1].Trim().TrimStart('@');
        if (!_usersByName.TryGetValue(targetName, out UserData target))
        {
            AddMessage("Вы", input, "player", "💀   -1");
            AddSystemMessage("Пользователь не найден: " + targetName);
            ApplyReputationChange(false);
            RefreshStatusUI();
            return;
        }

        if (action == "/unmute")
        {
            if (!target.banned && target.mutedUntil > _shiftElapsed)
            {
                target.mutedUntil = 0f;
                AddMessage("Вы", input, "player", "🐸   +1");
                AddSystemMessage(target.username + " снова может писать. Это дополнительная команда прототипа.");
            }
            else
            {
                AddMessage("Вы", input, "player", "💀   -1");
                AddSystemMessage("Этот пользователь сейчас не замучен.");
                ApplyReputationChange(false);
            }
            RefreshStatusUI();
            return;
        }

        bool correctIncident = _activeIncident != null
            && !_activeIncident.resolved
            && _activeIncident.target == target
            && string.Equals(_activeIncident.requiredAction, action.Substring(1), StringComparison.OrdinalIgnoreCase);

        if (correctIncident)
        {
            ApplyActionToUser(target, action.Substring(1));
            _activeIncident.resolved = true;
            _activeIncident = null;
            AddMessage("Вы", input, "player", "🐸   +1");
            ApplyReputationChange(true);
            AddSystemMessage("Верное решение. Нарушение обработано.");
            RefreshTypingIndicator();
        }
        else
        {
            AddMessage("Вы", input, "player", "💀   -1");
            if (_activeIncident == null)
                AddSystemMessage("Сейчас нет активного нарушения для этой команды.");
            else if (_activeIncident.target != target)
                AddSystemMessage("Неверная цель. Проверьте ник активного нарушителя в #общий.");
            else
                AddSystemMessage("Неверное действие. Для этого нарушения требуется /" + _activeIncident.requiredAction + ".");
            ApplyReputationChange(false);
        }

        RefreshStatusUI();
    }

    private void ApplyActionToUser(UserData user, string action)
    {
        if (user == null) return;
        if (string.Equals(action, "ban", StringComparison.OrdinalIgnoreCase))
        {
            user.banned = true;
            user.mutedUntil = 0f;
        }
        else if (string.Equals(action, "mute", StringComparison.OrdinalIgnoreCase))
        {
            user.mutedUntil = _shiftElapsed + 20f;
        }
    }

    private void ApplyReputationChange(bool success)
    {
        if (GameManager.Instance != null)
        {
            if (success) GameManager.Instance.OnSuccess();
            else GameManager.Instance.OnError();
        }

        float reputation = CurrentReputation;
        if (!_shiftEnded && reputation <= 0f)
        {
            _shiftEnded = true;
            AddSystemMessage("Репутация обнулилась. Смена провалена: вас уволили.");
        }
        else if (!_shiftEnded && reputation >= 100f)
        {
            _shiftEnded = true;
            AddSystemMessage("Репутация достигла максимума. Вы получили повышение!");
        }
    }

    private float CurrentReputation
    {
        get { return GameManager.Instance != null ? GameManager.Instance.reputation : 50f; }
    }

    #endregion
}
