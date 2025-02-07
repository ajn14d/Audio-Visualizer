using UnityEngine;
using UnityEngine.UI;

public class AudioVisualizerController : MonoBehaviour
{
    public AudioSource audioSource;
    public LineRenderer lineRenderer;
    public Slider waveHeightSlider; // UI Slider for adjusting waveform height
    public int selectedDeviceIndex = 1; // Set in Inspector
    public int sampleRate = 44100;
    public int recordingLength = 30; // seconds
    public int numSamples = 4096; // Number of samples for visualization
    public float waveformHeight = 5f; // Default scale factor for waveform visualization
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
            waveHeightSlider.minValue = 0.5f; // Prevents wave from being invisible
            waveHeightSlider.maxValue = 10f;  // Adjust as needed
            waveHeightSlider.value = waveformHeight; // Initialize with default value
            waveHeightSlider.onValueChanged.AddListener(UpdateWaveHeight);
        }
        else
        {
            Debug.LogWarning("No WaveHeightSlider assigned! Waveform height cannot be adjusted.");
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
            float x = (float)i / numSamples * 10f - 5f; // Scale x-axis across screen
            float y = samples[i] * waveformHeight; // Scale waveform height
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    // Function to update waveform height when slider value changes
    public void UpdateWaveHeight(float newHeight)
    {
        waveformHeight = newHeight;
    }
}






