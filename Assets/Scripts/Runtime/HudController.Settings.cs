using UnityEngine;
using UnityEngine.UI;

namespace Height1079.Runtime
{
    /// <summary>The settings sheet: the three knobs of <see cref="Quality"/> and nothing else.
    ///
    /// It opens from the menu, before a night is started, and on <b>F10</b> in the middle of one, because the only
    /// honest way to choose a setting is to stand where the game is slow and watch the number change. The line under
    /// the buttons says what the game is actually rendering and at what frame time — on a Retina display «картинка
    /// 100 %» is seven million pixels, and that is worth seeing in figures.</summary>
    public sealed partial class HudController
    {
        RectTransform settingsCard;
        Text settingsNow;
        Button[] levelButtons, scaleButtons;
        Button vsyncButton;
        float fpsSmooth = 60f;

        /// <summary>Is the sheet up? It is drawn over both the menu and the night.</summary>
        public bool SettingsShown => settingsCard != null && settingsCard.gameObject.activeSelf;

        void BuildSettings()
        {
            settingsCard = Centered("Settings", canvas.transform, new Vector2(460, 330));
            PanelImage(settingsCard, new Color(.05f, .11f, .15f, .97f));
            Label("Title", settingsCard, new Vector2(28, -24), new Vector2(400, 28), 22, Ink).text = "Настройки";
            Label("Hint", settingsCard, new Vector2(28, -52), new Vector2(404, 18), 12, new Color(.67f, .76f, .8f)).text
                = "F10 — открыть и закрыть, в меню и в игре";

            Label("QLabel", settingsCard, new Vector2(28, -84), new Vector2(404, 18), 12, new Color(.67f, .76f, .8f)).text
                = "Качество: тени, трава, деревья";
            levelButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var lvl = (Quality.Level)i;
                levelButtons[i] = ButtonUi("Q" + i, settingsCard, new Vector2(28 + i * 138, -106), new Vector2(130, 34),
                    Quality.LevelTitle(lvl), PlaceOff, Ink, () => { Quality.Set(lvl); RefreshSettings(); });
                levelButtons[i].GetComponentInChildren<Text>().fontSize = 14;
            }

            Label("SLabel", settingsCard, new Vector2(28, -152), new Vector2(404, 18), 12, new Color(.67f, .76f, .8f)).text
                = "Картинка: доля разрешения экрана";
            scaleButtons = new Button[Quality.Scales.Length];
            for (int i = 0; i < Quality.Scales.Length; i++)
            {
                float s = Quality.Scales[i];
                scaleButtons[i] = ButtonUi("S" + i, settingsCard, new Vector2(28 + i * 103, -174), new Vector2(95, 34),
                    Mathf.RoundToInt(s * 100f) + " %", PlaceOff, Ink, () => { Quality.SetScale(s); RefreshSettings(); });
                scaleButtons[i].GetComponentInChildren<Text>().fontSize = 14;
            }

            vsyncButton = ButtonUi("VSync", settingsCard, new Vector2(28, -228), new Vector2(404, 34),
                "Вертикальная синхронизация", PlaceOff, Ink, () => { Quality.SetVSync(!Quality.VSync); RefreshSettings(); });
            vsyncButton.GetComponentInChildren<Text>().fontSize = 14;

            settingsNow = Label("Now", settingsCard, new Vector2(28, -270), new Vector2(290, 40), 12, Amber);
            ButtonUi("Close", settingsCard, new Vector2(332, -282), new Vector2(100, 28), "ЗАКРЫТЬ",
                new Color(.2f, .29f, .34f), Ink, () => ShowSettings(false)).GetComponentInChildren<Text>().fontSize = 13;
            settingsCard.gameObject.SetActive(false);
        }

        public void ShowSettings(bool on)
        {
            if (settingsCard == null) return;
            settingsCard.gameObject.SetActive(on);
            if (on) RefreshSettings();
        }

        void RefreshSettings()
        {
            if (settingsCard == null) return;
            for (int i = 0; i < levelButtons.Length; i++)
                levelButtons[i].GetComponent<Image>().color = (int)Quality.Current == i ? PlaceOn : PlaceOff;
            for (int i = 0; i < scaleButtons.Length; i++)
                scaleButtons[i].GetComponent<Image>().color = Mathf.Approximately(Quality.Scales[i], Quality.Scale) ? PlaceOn : PlaceOff;
            vsyncButton.GetComponent<Image>().color = Quality.VSync ? PlaceOn : PlaceOff;
            vsyncButton.GetComponentInChildren<Text>().text = Quality.VSync
                ? "Вертикальная синхронизация: ВКЛ" : "Вертикальная синхронизация: ВЫКЛ";
            UpdateSettingsLine();
        }

        /// <summary>Only while the sheet is up, and only four times a second: a number that flickers cannot be read.</summary>
        void TickSettings()
        {
            // the window may have become full screen since the last frame; that is the display we scale from
            Quality.NoticeScreen();
            if (Controls.Settings) ShowSettings(!SettingsShown);
            if (!SettingsShown) return;
            fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime), .1f);
            if (Time.unscaledTime < settingsNext) return;
            settingsNext = Time.unscaledTime + .25f;
            UpdateSettingsLine();
        }
        float settingsNext;

        void UpdateSettingsLine()
        {
            if (settingsNow == null) return;
            var r = Quality.Rendered;
            var n = Quality.Native;
            float mpix = r.x * r.y / 1e6f;
            settingsNow.text = $"Рисуем {r.x}×{r.y} из {n.x}×{n.y}, {mpix:0.0} Мпикс на кадр\n"
                + $"{fpsSmooth:0} fps · {1000f / Mathf.Max(1f, fpsSmooth):0.0} мс"
                + (Quality.VSync ? " · потолок держит синхронизация" : "");
        }
    }
}
