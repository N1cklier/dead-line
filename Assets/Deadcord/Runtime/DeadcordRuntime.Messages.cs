using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class DeadcordRuntime
{
    #region Chat messages and rendering

    private void AddSystemMessage(string text)
    {
        AddMessage("Система", text, "system");
    }

    private void AddMessage(string username, string text, string role, string reaction = "")
    {
        ChatMessage message = new ChatMessage
        {
            username = username,
            text = text,
            role = role,
            reaction = reaction,
            timestamp = FormatClock()
        };
        _messages.Add(message);

        if (_messages.Count > Mathf.RoundToInt(maxStoredMessages))
        {
            _messages.RemoveAt(0);
            if (!_showingModeration)
                RenderAllMessages();
        }
        else if (!_showingModeration)
        {
            RenderMessage(message);
        }

        PlayNewMessageSound();
    }

    private void RenderAllMessages()
    {
        if (_messagesContent == null) return;
        for (int i = _messagesContent.childCount - 1; i >= 0; i--)
            Destroy(_messagesContent.GetChild(i).gameObject);

        for (int i = 0; i < _messages.Count; i++)
            RenderMessage(_messages[i], false);

        ScrollToBottom();
    }

    private void RenderMessage(ChatMessage message, bool scrollAfter = true)
    {
        if (_messagesContent == null || message == null) return;

        GameObject row = new GameObject("Message_" + _messagesContent.childCount, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        row.transform.SetParent(_messagesContent, false);
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.padding = new RectOffset(0, 0, 1, 1);
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        ContentSizeFitter rowFitter = row.GetComponent<ContentSizeFitter>();
        rowFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement rowElement = row.AddComponent<LayoutElement>();
        rowElement.minHeight = 75f;

        UserData user = (message.role == "system" || message.role == "player" || message.role == "helper")
            ? null
            : GetOrCreateUser(message.username);
        GameObject avatar = new GameObject("Avatar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        avatar.transform.SetParent(row.transform, false);
        LayoutElement avatarElement = avatar.GetComponent<LayoutElement>();
        avatarElement.minWidth = avatarElement.minHeight = 75f;
        avatarElement.preferredWidth = avatarElement.preferredHeight = 75f;
        avatarElement.flexibleWidth = avatarElement.flexibleHeight = 0f;
        avatar.GetComponent<RectTransform>().sizeDelta = new Vector2(75f, 75f);
        Image avatarImage = avatar.GetComponent<Image>();
        avatarImage.sprite = _avatarSprite != null ? _avatarSprite : _uiSprite;
        avatarImage.type = Image.Type.Simple;
        avatarImage.color = ColorForMessage(message, user);
        Text initials = CreateText("AvatarInitials", avatar.transform, GetInitials(message.username), 20, HexColor("#FFFFFF"), TextAnchor.MiddleCenter,
            rt => Stretch(rt, 0f, 0f, 0f, 0f), FontStyle.Bold);

        GameObject textColumn = new GameObject("MessageTextColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        textColumn.transform.SetParent(row.transform, false);
        LayoutElement textColumnElement = textColumn.GetComponent<LayoutElement>();
        textColumnElement.flexibleWidth = 1f;
        textColumnElement.minWidth = 60f;
        VerticalLayoutGroup textLayout = textColumn.GetComponent<VerticalLayoutGroup>();
        textLayout.spacing = 2f;
        textLayout.childAlignment = TextAnchor.UpperLeft;
        textLayout.childControlWidth = true;
        textLayout.childControlHeight = true;
        textLayout.childForceExpandWidth = true;
        textLayout.childForceExpandHeight = false;

        string nameLine = message.username + "   " + message.timestamp;
        Text usernameText = CreateText("UsernameAndTime", textColumn.transform, nameLine, 20, ColorForMessage(message, user), TextAnchor.MiddleLeft,
            rt => rt.sizeDelta = new Vector2(0f, 30f), FontStyle.Bold);
        LayoutElement usernameLayout = usernameText.gameObject.AddComponent<LayoutElement>();
        usernameLayout.minHeight = usernameLayout.preferredHeight = 30f;

        Text bodyText = CreateText("MessageBody", textColumn.transform, message.text, 24, HexColor(COLOR_TEXT), TextAnchor.UpperLeft,
            rt => rt.sizeDelta = new Vector2(0f, 0f), FontStyle.Normal);
        bodyText.supportRichText = false;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutElement bodyLayout = bodyText.gameObject.AddComponent<LayoutElement>();
        bodyLayout.flexibleWidth = 1f;
        bodyLayout.minHeight = 18f;

        if (!string.IsNullOrWhiteSpace(message.reaction))
        {
            GameObject reaction = CreatePanel("ReactionChip", textColumn.transform, HexColor("#383A40"));
            LayoutElement reactionElement = reaction.AddComponent<LayoutElement>();
            reactionElement.minHeight = reactionElement.preferredHeight = 28f;
            reactionElement.preferredWidth = 88f;
            Text reactionText = CreateText("ReactionText", reaction.transform, message.reaction, 18, HexColor("#DBDEE1"), TextAnchor.MiddleCenter,
                rt => Stretch(rt, 7f, 1f, 7f, 1f), FontStyle.Bold);
            reactionText.supportRichText = false;
        }

        if (scrollAfter)
            ScrollToBottom();
    }

    private Color ColorForMessage(ChatMessage message, UserData user)
    {
        switch (message.role)
        {
            case "system": return HexColor(COLOR_GOLD);
            case "helper": return HexColor(COLOR_GREEN);
            case "player": return HexColor(COLOR_BLUE);
            case "boss": return HexColor("#F0B232");
            default:
                int index = user != null ? Mathf.Abs(user.colorIndex) % _avatarColors.Length : 0;
                return _avatarColors[index];
        }
    }

    private string GetInitials(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "?";
        string cleaned = username.Replace("@", "").Replace("#", "").Replace("_", "").Replace("-", "").Trim();
        if (cleaned.Length >= 2) return cleaned.Substring(0, 2).ToUpperInvariant();
        return cleaned.ToUpperInvariant();
    }

    private void ScrollToBottom()
    {
        if (_messagesScroll == null) return;
        Canvas.ForceUpdateCanvases();
        _messagesScroll.verticalNormalizedPosition = 0f;
    }

    private string FormatClock()
    {
        int totalSeconds = 9 * 60 + Mathf.FloorToInt(_shiftElapsed);
        int hours = (totalSeconds / 3600) % 24;
        int minutes = (totalSeconds / 60) % 60;
        return hours.ToString("00") + ":" + minutes.ToString("00");
    }

    private void PlayNewMessageSound()
    {
        if (newMessageSound == null)
            return;

        if (messageAudioSource == null)
        {
            messageAudioSource = GetComponent<AudioSource>();
            if (messageAudioSource == null)
                messageAudioSource = gameObject.AddComponent<AudioSource>();
            messageAudioSource.playOnAwake = false;
            messageAudioSource.spatialBlend = 0f;
        }

        messageAudioSource.PlayOneShot(newMessageSound);
    }

    #endregion
}
