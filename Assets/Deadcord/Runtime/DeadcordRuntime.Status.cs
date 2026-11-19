using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class DeadcordRuntime
{
    #region Channels, status and close

    private void SetChannel(bool showModeration)
    {
        if (!showModeration && !_shiftAccepted)
            showModeration = true;

        _showingModeration = showModeration;
        if (_channelHeaderText != null)
            _channelHeaderText.text = showModeration ? "#   модерация" : "#   общий";
        if (_messageScrollObject != null) _messageScrollObject.SetActive(!showModeration);
        if (_typingRowObject != null) _typingRowObject.SetActive(!showModeration && _activeIncident != null && _activeIncident.helperTyping);
        if (_inputRowObject != null) _inputRowObject.SetActive(!showModeration);
        if (_rulesPanelObject != null) _rulesPanelObject.SetActive(showModeration);

        SetButtonColor(_generalButton, !showModeration ? HexColor(COLOR_CHANNEL_ACTIVE) : HexColor(COLOR_CHANNEL_IDLE));
        SetButtonColor(_moderationButton, showModeration ? HexColor(COLOR_CHANNEL_ACTIVE) : HexColor(COLOR_CHANNEL_IDLE));

        if (!showModeration)
            RenderAllMessages();
        else
            RefreshInstructions();

        RefreshTypingIndicator();
    }

    private void SetButtonColor(Button button, Color color)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = color;
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.selectedColor = color;
        button.colors = colors;
    }

    private void RefreshTypingIndicator()
    {
        if (_typingRowObject == null) return;
        bool shouldShow = !_showingModeration && _activeIncident != null && _activeIncident.helperTyping && !_activeIncident.resolved;
        _typingRowObject.SetActive(shouldShow);
        if (_typingText != null)
            _typingText.text = helperUsername + " печатает...";
    }

    private void RefreshStatusUI()
    {
        float rep = CurrentReputation;
        float clampedRep = Mathf.Clamp(rep, 0f, 100f);
        if (_reputationText != null)
            _reputationText.text = Mathf.RoundToInt(rep) + " / 100";
        if (_reputationFill != null)
        {
            RectTransform rect = _reputationFill.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(clampedRep / 100f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _reputationFill.color = clampedRep >= 70f ? HexColor(COLOR_GREEN) : (clampedRep <= 25f ? HexColor(COLOR_RED) : HexColor(COLOR_BLUE));
        }

        if (_headerWaveText != null)
            _headerWaveText.text = "ДЕНЬ 01   •   ВОЛНА " + CurrentWaveNumber + "/" + Mathf.Max(1, _waveRules.waves.Length) + "   •   " + FormatClock();

        if (_shiftStatusText != null)
        {
            if (!_shiftAccepted)
            {
                _shiftStatusText.text = "ОЖИДАНИЕ ПРИНЯТИЯ ПРАВИЛ";
                _shiftStatusText.color = HexColor(COLOR_GOLD);
            }
            else if (rep <= 0f)
            {
                _shiftStatusText.text = "СМЕНА ПРОВАЛЕНА";
                _shiftStatusText.color = HexColor(COLOR_RED);
            }
            else if (rep >= 100f)
            {
                _shiftStatusText.text = "ПОВЫШЕНИЕ";
                _shiftStatusText.color = HexColor(COLOR_GREEN);
            }
            else if (_shiftEnded)
            {
                _shiftStatusText.text = "СМЕНА ЗАВЕРШЕНА";
                _shiftStatusText.color = HexColor(COLOR_MUTED);
            }
            else if (_activeIncident != null)
            {
                _shiftStatusText.text = _activeIncident.helperTyping ? "Помощник печатает" : "Есть активное нарушение";
                _shiftStatusText.color = HexColor(COLOR_GOLD);
            }
            else
            {
                _shiftStatusText.text = "Смена активна";
                _shiftStatusText.color = HexColor(COLOR_GREEN);
            }
        }
    }

    private void LockWorldControls()
    {
        if (_controlsLocked) return;
        _controlsLocked = true;
        _cursorWasVisible = Cursor.visible;
        _cursorLockWas = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        _playerMovements = FindObjectsOfType<PlayerMovement>();
        _playerMovementWasEnabled.Clear();
        if (_playerMovements != null)
        {
            for (int i = 0; i < _playerMovements.Length; i++)
            {
                _playerMovementWasEnabled.Add(_playerMovements[i] != null && _playerMovements[i].enabled);
                if (_playerMovements[i] != null)
                    _playerMovements[i].enabled = false;
            }
        }
    }

    private void RestoreWorldControls()
    {
        if (!_controlsLocked) return;
        _controlsLocked = false;
        if (_playerMovements != null)
        {
            for (int i = 0; i < _playerMovements.Length && i < _playerMovementWasEnabled.Count; i++)
            {
                if (_playerMovements[i] != null)
                    _playerMovements[i].enabled = _playerMovementWasEnabled[i];
            }
        }
        Cursor.visible = _cursorWasVisible;
        Cursor.lockState = _cursorLockWas;
    }

    private void CloseDeadcord()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMode(GameManager.Mode.Reality);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    #endregion
}
