using UnityEngine;

// 마스터 음량을 PlayerPrefs에 저장하고 AudioListener에 적용한다.
public static class SoundSettingsManager
{
    private const string MasterVolumeKey = "Sound_MasterVolume";

    private static bool loaded;
    private static float masterVolume = 1f;

    public static float MasterVolume
    {
        get
        {
            EnsureLoaded();
            return masterVolume;
        }
        set
        {
            EnsureLoaded();
            masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
            PlayerPrefs.Save();
        }
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        AudioListener.volume = masterVolume;
    }
}
