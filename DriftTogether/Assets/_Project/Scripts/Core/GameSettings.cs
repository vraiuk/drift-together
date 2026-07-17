using UnityEngine;

namespace DriftTogether.Core
{
    /// <summary>Session-only settings (the brief explicitly allows no persistence).</summary>
    public static class GameSettings
    {
        static float _masterVolume = 0.8f;
        static float _cameraSmoothing = 0.5f;

        public static float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                AudioListener.volume = _masterVolume;
            }
        }

        /// <summary>0 = very smooth/lazy camera, 1 = tight camera.</summary>
        public static float CameraSmoothing
        {
            get => _cameraSmoothing;
            set => _cameraSmoothing = Mathf.Clamp01(value);
        }

        public static void ApplyToListener() => AudioListener.volume = _masterVolume;
    }
}
