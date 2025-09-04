using Melanchall.DryWetMidi.MusicTheory;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MidiGenPlay.UI
{
    public class ChordEventPanel : MonoBehaviour
    {
        [Header("Fields")]
        [SerializeField] private TMP_Dropdown degreeDropdown;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_InputField startInput;   // steps
        [SerializeField] private TMP_InputField lengthInput;  // steps (>=1)
        [SerializeField] private Slider velocitySlider;
        [SerializeField] private Button okButton;
        [SerializeField] private Button cancelButton;

        // current working values
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

            okButton.onClick.AddListener(OnOk);
            cancelButton.onClick.AddListener(OnCancel);
        }

        public void Show(int start, int length, ScaleDegree deg, ChordQuality qual, int vel,
                         int maxSteps, Transform anchor = null)
        {
            gameObject.SetActive(true);

            startStep = Mathf.Clamp(start, 0, maxSteps - 1);
            lengthSteps = Mathf.Max(1, Mathf.Min(length, maxSteps - startStep));
            degree = deg;
            quality = qual;
            velocity = Mathf.Clamp(vel, 0, 127);

            startInput.text = startStep.ToString();
            lengthInput.text = lengthSteps.ToString();

            degreeDropdown.value = (int)degree;
            degreeDropdown.RefreshShownValue();
            qualityDropdown.value = (int)quality;
            qualityDropdown.RefreshShownValue();

            velocitySlider.SetValueWithoutNotify(velocity);

            if (anchor != null)
            {
                var rt = transform as RectTransform;
                var a = anchor as RectTransform;
                if (rt != null && a != null)
                {
                    rt.position = a.position;
                }
            }
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnOk()
        {
            int.TryParse(startInput.text, out startStep);
            int.TryParse(lengthInput.text, out lengthSteps);
            lengthSteps = Mathf.Max(1, lengthSteps);

            degree = (ScaleDegree)degreeDropdown.value;
            quality = (ChordQuality)qualityDropdown.value;
            velocity = Mathf.RoundToInt(velocitySlider.value);

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
    }
}