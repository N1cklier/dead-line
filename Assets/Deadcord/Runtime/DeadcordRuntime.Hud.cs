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
        GameObject satietyIcon = _playerHunger != null ? _playerHunger.satietyIcon : null;

        if (hideForDeadcord)
        {
            if (!_worldHudStateCaptured)
            {
                _playerHungerSliderWasActive = playerSlider != null && playerSlider.gameObject.activeSelf;
                _meatIconWasActive = meatIcon != null && meatIcon.activeSelf;
                _worldHudStateCaptured = true;
                _satietyIconWasActive = satietyIcon != null && satietyIcon.activeSelf;
            }

            if (playerSlider != null && playerSlider.gameObject.activeSelf)
                playerSlider.gameObject.SetActive(false);
            if (meatIcon != null && meatIcon.activeSelf)
                meatIcon.SetActive(false);
            if (satietyIcon != null && satietyIcon.activeSelf)
                satietyIcon.SetActive(false);
            return;
        }

        if (_worldHudStateCaptured)
        {
            if (playerSlider != null) playerSlider.gameObject.SetActive(_playerHungerSliderWasActive);
            if (meatIcon != null) meatIcon.SetActive(_meatIconWasActive);
            if (satietyIcon != null) satietyIcon.SetActive(_satietyIconWasActive);
        }

        _worldHudStateCaptured = false;
    }

    #endregion
}
