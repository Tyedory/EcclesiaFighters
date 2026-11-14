using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

namespace EcclesiaFighters
{
    public class SettingsManager : MonoBehaviour
    {
        [Header("Graphics")]
        public Button[] qualityButtons;
        public Toggle   vsyncToggle;
        public Toggle   fullscreenToggle;

        [Header("Volume")]
        public AudioMixer audioMixer;
        public Slider masterSlider, musicSlider, sfxSlider, voiceSlider;

        void Start()
        {
            int q = PlayerPrefs.GetInt("Quality", 1);
            QualitySettings.SetQualityLevel(q);
            SelectBtn(qualityButtons, q);
            if (vsyncToggle)      vsyncToggle.isOn      = QualitySettings.vSyncCount > 0;
            if (fullscreenToggle) fullscreenToggle.isOn = Screen.fullScreen;
            LoadVol("Master", masterSlider, -6f);
            LoadVol("Music",  musicSlider,  -6f);
            LoadVol("SFX",    sfxSlider,     0f);
            LoadVol("Voice",  voiceSlider,   0f);
        }

        void LoadVol(string k, Slider s, float def)
        {
            if (!s) return;
            float db = PlayerPrefs.GetFloat("Vol_"+k, def);
            s.value = Mathf.Pow(10f, db/20f);
            if (audioMixer) audioMixer.SetFloat("Vol_"+k, db);
        }

        public void SetQuality(int v)  { QualitySettings.SetQualityLevel(v); PlayerPrefs.SetInt("Quality",v); SelectBtn(qualityButtons,v); }
        public void SetVSync(bool v)   { QualitySettings.vSyncCount = v?1:0; }
        public void SetFullscreen(bool v){ Screen.fullScreen = v; }
        public void SetMasterVol(float v){ SetVol("Master",v); }
        public void SetMusicVol(float v) { SetVol("Music",v); }
        public void SetSFXVol(float v)   { SetVol("SFX",v); }
        public void SetVoiceVol(float v) { SetVol("Voice",v); }

        void SetVol(string k, float lin)
        {
            float db = lin > 0.001f ? 20f*Mathf.Log10(lin) : -80f;
            if (audioMixer) audioMixer.SetFloat("Vol_"+k, db);
            PlayerPrefs.SetFloat("Vol_"+k, db);
        }

        void SelectBtn(Button[] btns, int idx)
        {
            if (btns == null) return;
            for (int i = 0; i < btns.Length; i++)
                if (btns[i]) { var img=btns[i].GetComponent<Image>(); if(img) img.color = i==idx?new Color(0.95f,0.73f,0.1f):new Color(0.28f,0.22f,0.12f); }
        }

        public void SaveAndClose() { PlayerPrefs.Save(); gameObject.SetActive(false); }
    }
}
