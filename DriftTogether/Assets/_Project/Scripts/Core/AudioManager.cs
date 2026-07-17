using UnityEngine;

namespace DriftTogether.Core
{
    /// <summary>
    /// Central audio hub. Synthesizes all clips once and survives scene loads.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public AudioClip AmbientMusic { get; private set; }
        public AudioClip WaterLoop { get; private set; }
        public AudioClip CampfireLoop { get; private set; }
        public AudioClip PaddleStroke { get; private set; }
        public AudioClip Collision { get; private set; }
        public AudioClip SoftBump { get; private set; }
        public AudioClip MushroomChime { get; private set; }
        public AudioClip CampfireRest { get; private set; }
        public AudioClip Finish { get; private set; }
        public AudioClip Click { get; private set; }

        AudioSource _music;
        AudioSource _water;
        AudioSource _sfx;

        public static AudioManager Ensure()
        {
            if (Instance != null)
                return Instance;
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            GameSettings.ApplyToListener();

            AmbientMusic = AudioSynth.CreateAmbientMusic();
            WaterLoop = AudioSynth.CreateWaterLoop();
            CampfireLoop = AudioSynth.CreateCampfireLoop();
            PaddleStroke = AudioSynth.CreatePaddleStroke();
            Collision = AudioSynth.CreateCollision();
            SoftBump = AudioSynth.CreateSoftBump();
            MushroomChime = AudioSynth.CreateMushroomChime();
            CampfireRest = AudioSynth.CreateCampfireRest();
            Finish = AudioSynth.CreateFinish();
            Click = AudioSynth.CreateClick();

            _music = CreateSource("Music", loop: true, volume: 0.85f);
            _water = CreateSource("Water", loop: true, volume: 0f);
            _sfx = CreateSource("Sfx", loop: false, volume: 1f);

            _music.clip = AmbientMusic;
            _music.Play();
            _water.clip = WaterLoop;
            _water.Play();
        }

        AudioSource CreateSource(string name, bool loop, float volume)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = loop;
            src.volume = volume;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;
            return src;
        }

        /// <summary>0..1 how audible the river bed should be (menu = 0).</summary>
        public void SetWaterPresence(float presence)
        {
            if (_water != null)
                _water.volume = Mathf.Clamp01(presence) * 0.6f;
        }

        public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || _sfx == null)
                return;
            _sfx.pitch = pitch;
            _sfx.PlayOneShot(clip, volume);
        }

        public void PlayClick() => PlaySfx(Click, 0.8f);
    }
}
