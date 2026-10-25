using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class DeadcordRuntime
{
    #region Gameplay HUD visibility

    private void SyncGameplayHudVisibility(bool hideForDeadcord)
    {
        if (!hideForDeadcord && !_worldHudStateCaptured)
            return;

        if (_playerHunger == null)
            _playerHunger = FindObjectOfType<PlayerHunger>();

        Slider playerSlider = _playerHunger != null ? _playerHunger.playerSatietySlider : null;
        GameObject meatIcon = _playerHunger != null ? _playerHunger.meatIconInHand : null;

        if (hideForDeadcord)
        {
            if (!_worldHudStateCaptured)
            {
                _playerHungerSliderWasActive = playerSlider != null && playerSlider.gameObject.activeSelf;
                _meatIconWasActive = meatIcon != null && meatIcon.activeSelf;
                _worldHudStateCaptured = true;
            }

            if (playerSlider != null && playerSlider.gameObject.activeSelf)
                playerSlider.gameObject.SetActive(false);
            if (meatIcon != null && meatIcon.activeSelf)
                meatIcon.SetActive(false);
            return;
        }

        if (_worldHudStateCaptured)
        {
            if (playerSlider != null)
                playerSlider.gameObject.SetActive(_playerHungerSliderWasActive);
            if (meatIcon != null)
                meatIcon.SetActive(_meatIconWasActive);
        }

        _worldHudStateCaptured = false;
    }

    #endregion
}
