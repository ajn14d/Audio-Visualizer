using UnityEngine;
using UnityEngine.UI;

public class AudioVisualizerController : MonoBehaviour
{
    public AudioSource audioSource;
    public LineRenderer lineRenderer;
    public Slider waveHeightSlider; // UI Slider for adjusting waveform height
    public Slider waveDensitySlider; // UI Slider for adjusting wave density
    public int selectedDeviceIndex = 1; // Set in Inspector
    public int sampleRate = 44100;
    public int recordingLength = 1; // seconds
    public int numSamples = 1024; // Number of samples for visualization
    public float waveformHeight = 1f; // Default scale factor for waveform visualization
    public float waveDensity = 1f; // Default wave density (1 = normal, <1 = compressed, >1 = stretched)
    private float[] samples; // Buffer for audio samples

    void Start()
    {
        // Ensure AudioSource exists
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Ensure LineRenderer exists
        if (lineRenderer == null)
        {
            Debug.LogError("No LineRenderer found! Assign it in the Inspector.");
            return;
        }

        // Ensure WaveHeightSlider exists
        if (waveHeightSlider != null)
        {
            waveHeightSlider.minValue = 0.01f; // Prevents wave from being invisible
            waveHeightSlider.maxValue = 100f;  // Adjust as needed
            waveHeightSlider.value = waveformHeight; // Initialize with default value
            waveHeightSlider.onValueChanged.AddListener(UpdateWaveHeight);
        }
        else
        {
            Debug.LogWarning("No WaveHeightSlider assigned! Waveform height cannot be adjusted.");
        }

        // Ensure WaveDensitySlider exists
        if (waveDensitySlider != null)
        {
            waveDensitySlider.minValue = 0.1f; // Minimum density (compressed)
            waveDensitySlider.maxValue = 5f;  // Maximum density (stretched)
            waveDensitySlider.value = waveDensity; // Initialize with default value
            waveDensitySlider.onValueChanged.AddListener(UpdateWaveDensity);
        }
        else
        {
            Debug.LogWarning("No WaveDensitySlider assigned! Wave density cannot be adjusted.");
        }

        // Get available microphone devices
        string[] devices = Microphone.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No microphone devices found!");
            return;
        }

        Debug.Log("Available Microphone Devices:");
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log(i + ": " + devices[i]);
        }

        // Validate selected device index
        if (selectedDeviceIndex < 0 || selectedDeviceIndex >= devices.Length)
        {
            Debug.LogWarning("Selected device index is out of range. Defaulting to device 0.");
            selectedDeviceIndex = 0;
        }

        string selectedDevice = devices[selectedDeviceIndex];
        Debug.Log("Using microphone device: " + selectedDevice);

        // Start recording
        audioSource.clip = Microphone.Start(selectedDevice, true, recordingLength, sampleRate);
        while (Microphone.GetPosition(selectedDevice) <= 0) { } // Wait for recording to start
        audioSource.loop = true;
        audioSource.Play();

        // Initialize sample buffer
        samples = new float[numSamples];

        // Configure LineRenderer
        lineRenderer.positionCount = numSamples;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;

        // Allow the game to keep running in the background even if it's not the active window
        Application.runInBackground = true;
    }

    void Update()
    {
        // Ensure AudioClip is valid before trying to read data
        if (audioSource.clip == null || !audioSource.clip.isReadyToPlay)
        {
            Debug.LogWarning("AudioClip is not ready yet!");
            return;
        }

        // Get the audio sample data
        int startSample = Mathf.Max(0, audioSource.timeSamples);
        audioSource.clip.GetData(samples, startSample);

        // Update LineRenderer to display waveform
        for (int i = 0; i < numSamples; i++)
        {
            // Adjust x-axis to center the wave and apply density scaling
            float x = ((float)i / numSamples) * (10f * waveDensity) - (10f * waveDensity / 2f); // Center and scale by density
            float y = samples[i] * waveformHeight; // Scale waveform height
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    // Function to update waveform height when slider value changes
    public void UpdateWaveHeight(float newHeight)
    {
        waveformHeight = newHeight;
    }

    // Function to update wave density when slider value changes
    public void UpdateWaveDensity(float newDensity)
    {
        waveDensity = newDensity;
    }
}







