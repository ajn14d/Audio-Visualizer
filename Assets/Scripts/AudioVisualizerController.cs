using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class AudioVisualizerController : MonoBehaviour
{
    // Existing variables from your script
    public AudioSource audioSource;
    public LineRenderer lineRenderer;
    public Slider waveHeightSlider;
    public Slider waveDensitySlider;
    public Slider smoothingSlider;
    public int selectedDeviceIndex = 1;
    public int sampleRate = 44100;
    public int recordingLength = 1;
    public int numSamples = 1024;
    public float waveformHeight = 1f;
    public float waveDensity = 1f;
    private float[] samples;
    private float[] smoothedSamples;

    // New variables for spectrum analyzer
    public LineRenderer spectrumLineRenderer; // Assign in inspector
    public bool useBarVisualization = false; // Set to false to use line visualization
    public int spectrumSamples = 64; // Number of frequency bands to display
    public float spectrumScale = 5f; // INCREASED: Much higher scale to amplify tiny values
    private float[] spectrumData; // Buffer for frequency data
    private float[] smoothedSpectrumData; // Buffer for smoothed frequency data
    
    // Debug variables
    private float maxSpectrumValue = 0f;
    private bool debugOutput = true;
    private float debugTimer = 0f;
    
    // New variables for spectrum visualization enhancement
    public float spectrumMinimumThreshold = 0.00001f; // Minimum threshold to filter out noise
    public float spectrumExponent = 0.5f; // Power exponent for non-linear scaling (0.5 = square root)
    public float spectrumVerticalOffset = -3f; // Vertical position offset

    void Start()
    {
        // Existing initialization code
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (lineRenderer == null)
        {
            Debug.LogError("No LineRenderer found! Assign it in the Inspector.");
            return;
        }

        if (waveHeightSlider != null)
        {
            waveHeightSlider.minValue = 0.01f;
            waveHeightSlider.maxValue = 100f;
            waveHeightSlider.value = waveformHeight;
            waveHeightSlider.onValueChanged.AddListener(UpdateWaveHeight);
        }
        else
        {
            Debug.LogWarning("No WaveHeightSlider assigned! Waveform height cannot be adjusted.");
        }

        if (waveDensitySlider != null)
        {
            waveDensitySlider.minValue = 0.1f;
            waveDensitySlider.maxValue = 5f;
            waveDensitySlider.value = waveDensity;
            waveDensitySlider.onValueChanged.AddListener(UpdateWaveDensity);
        }
        else
        {
            Debug.LogWarning("No WaveDensitySlider assigned! Wave density cannot be adjusted.");
        }

        if (smoothingSlider != null)
        {
            smoothingSlider.minValue = 1f;
            smoothingSlider.maxValue = 50f;
            smoothingSlider.value = 1f;
            smoothingSlider.onValueChanged.AddListener(UpdateSmoothing);
        }
        else
        {
            Debug.LogWarning("No SmoothingSlider assigned! Waveform smoothing cannot be adjusted.");
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

        // Initialize sample buffers
        samples = new float[numSamples];
        smoothedSamples = new float[numSamples];

        // Initialize spectrum buffers
        spectrumData = new float[spectrumSamples];
        smoothedSpectrumData = new float[spectrumSamples];

        // Configure LineRenderer for waveform
        lineRenderer.positionCount = numSamples;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;

        // Configure LineRenderer for spectrum
        if (spectrumLineRenderer != null)
        {
            spectrumLineRenderer.positionCount = spectrumSamples;
            spectrumLineRenderer.startWidth = 0.05f;
            spectrumLineRenderer.endWidth = 0.05f;
            
            // Make sure the spectrum line renderer is enabled
            spectrumLineRenderer.enabled = true;
            
            Debug.Log("Spectrum LineRenderer configured with " + spectrumSamples + " points");
        }
        else
        {
            Debug.LogWarning("No Spectrum LineRenderer assigned! Spectrum visualization will not be displayed.");
        }

        // Allow the game to keep running in the background even if it's not the active window
        Application.runInBackground = true;

        // Set audio source volume to 0 to prevent feedback
        audioSource.volume = 1f;
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

        // Apply smoothing to the samples
        ApplySmoothing();

        // Update LineRenderer to display smoothed waveform
        for (int i = 0; i < numSamples; i++)
        {
            // Adjust x-axis to center the wave and apply density scaling
            float x = ((float)i / numSamples) * (10f * waveDensity) - (10f * waveDensity / 2f); // Center and scale by density
            float y = smoothedSamples[i] * waveformHeight; // Scale waveform height
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }

        // Get spectrum data and update spectrum visualization
        UpdateSpectrumVisualization();
        
        // Debug output
        if (debugOutput)
        {
            debugTimer += Time.deltaTime;
            if (debugTimer > 1f) // Output debug info every second
            {
                //Debug.Log("Max spectrum value: " + maxSpectrumValue);
                //debugTimer = 0f;
                //maxSpectrumValue = 0f;
            }
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

    // Function to update smoothing when slider value changes
    public void UpdateSmoothing(float newSmoothing)
    {
        // Smoothing value is now used in the ApplySmoothing method
    }

    // Apply moving average smoothing
    void ApplySmoothing()
    {
        int smoothingWindow = Mathf.RoundToInt(smoothingSlider.value); // Get the smoothing window size from slider value

        for (int i = 0; i < numSamples; i++)
        {
            float smoothedValue = 0f;
            int count = 0;

            // Apply the moving average by averaging the surrounding points within the smoothing window
            for (int j = i - smoothingWindow / 2; j <= i + smoothingWindow / 2; j++)
            {
                if (j >= 0 && j < numSamples) // Ensure within bounds
                {
                    smoothedValue += samples[j];
                    count++;
                }
            }

            smoothedSamples[i] = smoothedValue / count; // Store the average in the smoothed buffer
        }
    }

    // New method for spectrum visualization
    void UpdateSpectrumVisualization()
    {
        if (spectrumLineRenderer == null) return;

        // Get spectrum data
        audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

        // Apply smoothing to spectrum data
        ApplySpectrumSmoothing();

        if (useBarVisualization)
        {
            // Bar visualization (requires multiple line renderers or other visualization method)
            Debug.Log("Bar visualization not implemented in this example");
        }
        else
        {
            // Line visualization
            for (int i = 0; i < spectrumSamples; i++)
            {
                // Position the spectrum visualization below the waveform
                float x = ((float)i / spectrumSamples) * 10f - 5f; // Center on x-axis
                
                // Get the spectrum value and apply threshold to filter out noise
                float value = smoothedSpectrumData[i];
                if (value < spectrumMinimumThreshold) value = 0;
                
                // Apply non-linear scaling (power function) to emphasize smaller values
                // Using spectrumExponent = 0.5 gives square root scaling
                float amplitude = Mathf.Pow(value, spectrumExponent) * spectrumScale;
                
                // Position below waveform with offset
                float y = amplitude + spectrumVerticalOffset;
                
                spectrumLineRenderer.SetPosition(i, new Vector3(x, y, 0));
            }
        }
    }

    // Apply smoothing to spectrum data
    void ApplySpectrumSmoothing()
    {
        int smoothingWindow = Mathf.RoundToInt(smoothingSlider.value / 5f); // Use a smaller window for spectrum
        smoothingWindow = Mathf.Max(1, smoothingWindow); // Ensure at least 1

        for (int i = 0; i < spectrumSamples; i++)
        {
            float smoothedValue = 0f;
            int count = 0;

            // Apply the moving average
            for (int j = i - smoothingWindow / 2; j <= i + smoothingWindow / 2; j++)
            {
                if (j >= 0 && j < spectrumSamples)
                {
                    smoothedValue += spectrumData[j];
                    count++;
                }
            }

            smoothedSpectrumData[i] = smoothedValue / count;
        }
    }
}
