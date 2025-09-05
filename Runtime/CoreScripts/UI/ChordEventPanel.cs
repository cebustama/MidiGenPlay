using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MidiGenPlay.MusicTheory.MusicTheory;

namespace MidiGenPlay.UI
{
    public class ChordEventPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_Text tonalityLabel;         // "Selected Tonality: Ionian Mode"
        [SerializeField] private TMP_Dropdown degreeDropdown;
        [SerializeField] private TMP_Dropdown typeDropdown;      // NEW: Triads / 7ths
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_InputField startInput;      // steps
        [SerializeField] private TMP_InputField lengthInput;     // steps (>=1)
        [SerializeField] private TMP_InputField velocityInput;   // NEW: replaces slider
        [SerializeField] private Button okButton;
        [SerializeField] private Button cancelButton;

        [Header("Behavior")]
        [Tooltip("When the degree changes, choose the diatonic quality for the current tonality.")]
        [SerializeField] private bool autoSelectQuality = true;

        [Tooltip("If true, diatonic suggestion uses seventh qualities; otherwise triads.")]
        [SerializeField] private bool preferSeventhSuggestion = false;

        // current context
        private Tonality tonality = Tonality.Ionian;
        private int maxGridSteps = 1;

        // working values
        private int startStep;
        private int lengthSteps;
        private ScaleDegree degree;
        private ChordQuality quality;
        private int velocity;

        public event Action<int, int, ScaleDegree, ChordQuality, int> Confirmed;
        public event Action Canceled;

        void Awake()
        {
            FillDropdownFromEnum<ScaleDegree>(degreeDropdown);
            FillDropdownFromEnum<ChordQuality>(qualityDropdown);

            // --- Type dropdown (Triads / 7ths) ---
            typeDropdown.ClearOptions();
            typeDropdown.AddOptions(new System.Collections.Generic.List<string> { "Triads", "7ths" });
            typeDropdown.onValueChanged.AddListener(idx =>
            {
                preferSeventhSuggestion = (idx == 1);
                if (!autoSelectQuality) return;
                var d = (ScaleDegree)degreeDropdown.value;
                SetQualityDropdown(GetSuggestedQuality(tonality, d, preferSeventhSuggestion));
            });

            okButton.onClick.AddListener(OnOk);
            cancelButton.onClick.AddListener(OnCancel);

            // Auto quality when degree changes
            degreeDropdown.onValueChanged.AddListener(_ =>
            {
                if (!autoSelectQuality) return;
                var d = (ScaleDegree)degreeDropdown.value;
                var q = GetSuggestedQuality(tonality, d, preferSeventhSuggestion);
                SetQualityDropdown(q);
            });

            // Velocity clamp on end edit
            if (velocityInput != null)
            {
                velocityInput.onEndEdit.AddListener(_ =>
                {
                    velocity = ClampVelocity(velocityInput.text, fallback: 64);
                    velocityInput.SetTextWithoutNotify(velocity.ToString());
                });
            }
        }

        /// <summary>
        /// Open the panel with the current context (including tonality).
        /// </summary>
        public void Show(
            int start, int length,
            ScaleDegree deg, ChordQuality qual, int vel,
            int maxSteps,
            Tonality currentTonality,
            Transform anchor = null)
        {
            gameObject.SetActive(true);

            tonality = currentTonality;
            maxGridSteps = Mathf.Max(1, maxSteps);

            // header label
            if (tonalityLabel != null)
                tonalityLabel.text = $"Selected Tonality: <b>{currentTonality} Mode</b>";

            // bounds & working values
            startStep = Mathf.Clamp(start, 0, maxGridSteps - 1);
            lengthSteps = Mathf.Max(1, Mathf.Min(length, maxGridSteps - startStep));
            degree = deg;
            velocity = Mathf.Clamp(vel, 0, 127);

            // Type dropdown reflects current suggestion mode
            typeDropdown.SetValueWithoutNotify(preferSeventhSuggestion ? 1 : 0);

            // If we're auto-selecting, recompute quality from mode;
            // otherwise keep the incoming quality.
            quality = autoSelectQuality
                ? GetSuggestedQuality(tonality, degree, preferSeventhSuggestion)
                : qual;

            // populate fields
            startInput.text = startStep.ToString();
            lengthInput.text = lengthSteps.ToString();

            degreeDropdown.value = (int)degree;
            degreeDropdown.RefreshShownValue();

            SetQualityDropdown(quality);

            if (velocityInput != null)
                velocityInput.SetTextWithoutNotify(velocity.ToString());

            // position near anchor if provided
            if (anchor != null)
            {
                var rt = transform as RectTransform;
                var a = anchor as RectTransform;
                if (rt != null && a != null)
                    rt.position = a.position;
            }
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnOk()
        {
            int.TryParse(startInput.text, out startStep);
            int.TryParse(lengthInput.text, out lengthSteps);
            lengthSteps = Mathf.Max(1, Mathf.Min(lengthSteps, maxGridSteps - startStep));

            degree = (ScaleDegree)degreeDropdown.value;
            quality = (ChordQuality)qualityDropdown.value;

            velocity = ClampVelocity(velocityInput != null ? velocityInput.text : null, fallback: 64);

            Confirmed?.Invoke(startStep, lengthSteps, degree, quality, velocity);
            Hide();
        }

        private void OnCancel()
        {
            Canceled?.Invoke();
            Hide();
        }

        private static void FillDropdownFromEnum<T>(TMP_Dropdown dd) where T : Enum
        {
            dd.ClearOptions();
            foreach (var name in Enum.GetNames(typeof(T)))
                dd.options.Add(new TMP_Dropdown.OptionData(name));
            dd.RefreshShownValue();
        }

        private void SetQualityDropdown(ChordQuality q)
        {
            qualityDropdown.value = (int)q;
            qualityDropdown.RefreshShownValue();
        }

        private static int ClampVelocity(string text, int fallback)
        {
            if (!int.TryParse(text, out var v)) v = fallback;
            return Mathf.Clamp(v, 0, 127);
        }
    }
}
