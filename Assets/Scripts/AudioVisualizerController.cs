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
    public Slider spectrumScaleSlider;
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
    public bool useBarVisualization = true; // Toggle between line and bar visualization
    public int spectrumSamples = 64; // Number of frequency bands to display
    public float spectrumScale = 5000f; // Higher scale to amplify tiny values
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
    
    // Bar visualization variables
    public GameObject barPrefab; // Assign a cube or other primitive in inspector
    public float barWidth = 0.15f; // Width of each bar
    public float barSpacing = 0.05f; // Space between bars
    public float maxBarHeight = 5f; // Maximum height for bars
    public Color barStartColor = new Color(0f, 0.5f, 1f); // Color at the bottom of bars
    public Color barEndColor = new Color(0f, 1f, 1f); // Color at the top of bars
    private GameObject[] spectrumBars; // Array to hold bar GameObjects
    private bool barsCreated = false; // Flag to track if bars have been created

    // Bar smoothing variables
    public int barSmoothingSamples = 2; // Number of bars to average together
    private Queue<float[]> barHistoryBuffer; // Buffer to store previous bar heights
    public float barTransitionSpeed = 5f; // Speed at which bars transition to new heights
    private float[] targetBarHeights; // Target heights for smooth transitions

    // bar material
    public Material spectrumBarMaterial;

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

        if (spectrumScaleSlider != null)
        {
            spectrumScaleSlider.minValue = 100f;
            spectrumScaleSlider.maxValue = 2000f;
            spectrumScaleSlider.value = spectrumScale;
            spectrumScaleSlider.onValueChanged.AddListener((value) => spectrumScale = value);
        }
        else
        {
            Debug.LogWarning("No SpectrumScaleSlider assigned! Spectrum scale cannot be adjusted.");
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

        // Initialize bar history buffer and target heights
        barHistoryBuffer = new Queue<float[]>();
        targetBarHeights = new float[spectrumSamples];

        // Create spectrum bars if using bar visualization
        if (useBarVisualization && barPrefab != null)
        {
            CreateSpectrumBars();
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
                Debug.Log("Max spectrum value: " + maxSpectrumValue);
                debugTimer = 0f;
                maxSpectrumValue = 0f;
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
        // Get spectrum data
        audioSource.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);
        
        // Find max value for debugging
        for (int i = 0; i < spectrumSamples; i++)
        {
            if (spectrumData[i] > maxSpectrumValue)
            {
                maxSpectrumValue = spectrumData[i];
            }
        }

        // Apply smoothing to spectrum data
        ApplySpectrumSmoothing();

        if (useBarVisualization)
        {
            // Make sure bars exist before trying to update them
            if (!barsCreated && barPrefab != null)
            {
                CreateSpectrumBars();
            }
            
            // Update bar visualization if bars exist
            if (barsCreated)
            {
                UpdateSpectrumBars();
            }
            
            // Hide line renderer if using bars
            if (spectrumLineRenderer != null)
            {
                spectrumLineRenderer.enabled = false;
            }
        }
        else
        {
            // Show line renderer if using line visualization
            if (spectrumLineRenderer != null)
            {
                spectrumLineRenderer.enabled = true;
                
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

    // Smooth the bar heights by averaging multiple samples
    float[] SmoothBarHeights(float[] currentHeights)
    {
        if (barHistoryBuffer == null)
        {
            barHistoryBuffer = new Queue<float[]>();
            targetBarHeights = new float[spectrumSamples];
        }

        // Add current heights to history
        barHistoryBuffer.Enqueue((float[])currentHeights.Clone());

        // Keep only the specified number of samples
        while (barHistoryBuffer.Count > barSmoothingSamples)
        {
            barHistoryBuffer.Dequeue();
        }

        // Average all samples
        float[] smoothedHeights = new float[spectrumSamples];
        foreach (float[] heights in barHistoryBuffer)
        {
            for (int i = 0; i < spectrumSamples; i++)
            {
                smoothedHeights[i] += heights[i];
            }
        }

        // Calculate average
        for (int i = 0; i < spectrumSamples; i++)
        {
            smoothedHeights[i] /= barHistoryBuffer.Count;
            
            // Smooth transition to target height
            targetBarHeights[i] = Mathf.Lerp(targetBarHeights[i], smoothedHeights[i], Time.deltaTime * barTransitionSpeed);
            smoothedHeights[i] = targetBarHeights[i];
        }

        return smoothedHeights;
    }

    // Method to create bar visualization for spectrum
    void CreateSpectrumBars()
    {
        if (barPrefab == null)
        {
            Debug.LogError("Bar prefab is not assigned! Cannot create spectrum bars.");
            return;
        }
        
        // Create parent object for bars if it doesn't exist
        Transform barsParent = transform.Find("SpectrumBars");
        if (barsParent == null)
        {
            GameObject parentObj = new GameObject("SpectrumBars");
            parentObj.transform.SetParent(transform);
            parentObj.transform.localPosition = new Vector3(0, spectrumVerticalOffset, 0);
            parentObj.transform.localRotation = Quaternion.identity;
            barsParent = parentObj.transform;
        }
        
        // Initialize array to hold bar GameObjects
        spectrumBars = new GameObject[spectrumSamples];
        
        // Calculate total width of all bars and spacing
        float totalWidth = (spectrumSamples * barWidth) + ((spectrumSamples - 1) * barSpacing);
        float startX = -totalWidth / 2f; // Center the bars
        
        // Create bars
        for (int i = 0; i < spectrumSamples; i++)
        {
            // Calculate position for this bar
            float xPos = startX + (i * (barWidth + barSpacing));
            
            // Instantiate bar GameObject
            GameObject bar = Instantiate(barPrefab, barsParent);
            bar.name = "SpectrumBar_" + i;
            
            // Position the bar
            bar.transform.localPosition = new Vector3(xPos, 0, 0);
            
            // Scale the bar (initial height is 0)
            bar.transform.localScale = new Vector3(barWidth, 0.01f, barWidth); // Small initial height
            
            // Set bar color & ensure shader is included in build
            Renderer renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (spectrumBarMaterial != null)
                {
                    renderer.material = new Material(spectrumBarMaterial);  // Use preloaded material
                }
                else
                {
                    Debug.LogError("Spectrum Bar Material is not assigned! Assign it in the Inspector.");
                }

                // Set the color dynamically if needed
                renderer.material.color = barStartColor;
            }
            
            // Store reference to the bar
            spectrumBars[i] = bar;
        }
        
        barsCreated = true;
        Debug.Log("Created " + spectrumSamples + " spectrum bars");
    }
    
    // Method to update bar heights based on spectrum data
    void UpdateSpectrumBars()
    {
        if (spectrumBars == null || !barsCreated)
        {
            if (barPrefab != null)
            {
                CreateSpectrumBars();
            }
            return;
        }

        // Define logarithmic scaling parameters
        float scaleFactor = 50f;  // Boost small values before applying log
        float logOffset = 1.05f;     // Offset to ensure log(x) is meaningful

        // Create array to hold current bar heights
        float[] currentHeights = new float[spectrumSamples];

        // Calculate initial heights
        for (int i = 0; i < spectrumSamples; i++)
        {
            // Get the spectrum value and apply threshold to filter out noise
            float value = smoothedSpectrumData[i];
            if (value < spectrumMinimumThreshold) value = 0;

            // Apply targeted frequency compensation
            float frequencyCompensation = 1.0f;

            if (i < 5) // Specific targeting of first 5 bars
            {
                float t = i / 4f;
                frequencyCompensation = 0.01f + (0.03f * t);
            }
            else if (i < spectrumSamples * 0.3f)
            {
                float t = (i - 5) / (spectrumSamples * 0.3f - 5);
                frequencyCompensation = 0.1f + (0.8f * t);
            }

            value *= frequencyCompensation;

            // Apply logarithmic scaling adjusted for small values
            float scaledValue = value * scaleFactor;
            float amplitude;

            if (scaledValue > 0)
            {
                amplitude = Mathf.Log10(1 + scaledValue * 0.5f) * spectrumScale;
            }
            else
            {
                amplitude = 0;
            }

            amplitude = Mathf.Pow(amplitude, spectrumExponent);
            currentHeights[i] = Mathf.Clamp(amplitude, 0.01f, maxBarHeight);
        }

        // Apply smoothing across multiple frames
        float[] smoothedHeights = SmoothBarHeights(currentHeights);

        // Update bar visuals with smoothed heights
        for (int i = 0; i < spectrumSamples; i++)
        {
            if (spectrumBars[i] == null) continue;

            float barHeight = smoothedHeights[i];

            // Update bar scale
            Vector3 scale = spectrumBars[i].transform.localScale;
            scale.y = barHeight;
            spectrumBars[i].transform.localScale = scale;

            // Position the bar so it grows upward from the base
            Vector3 position = spectrumBars[i].transform.localPosition;
            position.y = barHeight / 2f;
            spectrumBars[i].transform.localPosition = position;

            // Apply rainbow color effect based on the index of the bar
            float hue = (float)i / spectrumSamples; // Spread across the hue spectrum (0 to 1)
            Color barColor = Color.HSVToRGB(hue, 1f, 1f); // Full saturation, full value

            // Update bar color
            Renderer renderer = spectrumBars[i].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = barColor;
            }
        }
    }
    
    // Toggle between line and bar visualization
    public void ToggleVisualizationType()
    {
        useBarVisualization = !useBarVisualization;
        
        if (useBarVisualization)
        {
            // When switching to bar visualization, make sure bars exist
            if (!barsCreated && barPrefab != null)
            {
                CreateSpectrumBars();
            }
            else
            {
                // If bars were previously created but hidden, show them
                if (spectrumBars != null)
                {
                    foreach (GameObject bar in spectrumBars)
                    {
                        if (bar != null)
                        {
                            bar.SetActive(true);
                        }
                    }
                }
            }
            
            // Hide line renderer
            if (spectrumLineRenderer != null)
            {
                spectrumLineRenderer.enabled = false;
            }
        }
        else
        {
            // When switching to line visualization, show line renderer and hide bars
            if (spectrumLineRenderer != null)
            {
                spectrumLineRenderer.enabled = true;
            }
            
            // Hide bars
            if (spectrumBars != null)
            {
                foreach (GameObject bar in spectrumBars)
                {
                    if (bar != null)
                    {
                        bar.SetActive(false);
                    }
                }
            }
        }
        
        Debug.Log("Visualization type toggled. Using bars: " + useBarVisualization);
    }

    public void ToggleDisplay()
    {
        // Toggle visibility between spectrum bars and waveform display

        // Check if spectrum bars are currently active
        bool spectrumBarsActive = spectrumBars != null && spectrumBars[0].activeSelf; // Assuming spectrumBars is an array and bars[0] can be used to check visibility

        // Switch to Spectrum and sliders
        if (spectrumBarsActive)
        {
            foreach (GameObject bar in spectrumBars)
            {
                bar.SetActive(false);
            }

            if (lineRenderer != null)
            {
                lineRenderer.enabled = true; // Enable waveform display (LineRenderer)
            }
            // Hide SpectrumScaleSlider and show WaveHeightSlider and WaveDensitySlider
            if (spectrumScaleSlider != null)
            {
                spectrumScaleSlider.gameObject.SetActive(false); // Hide SpectrumScaleSlider
            }

            if (waveHeightSlider != null)
            {
                waveHeightSlider.gameObject.SetActive(true); // Show WaveHeightSlider
            }

            if (waveDensitySlider != null)
            {
                waveDensitySlider.gameObject.SetActive(true); // Show WaveDensitySlider
            }
        }
        // If waveform display is active, hide it and show the spectrum bars
        else
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false; // Disable waveform display (LineRenderer)
            }

            foreach (GameObject bar in spectrumBars)
            {
                bar.SetActive(true); // Enable spectrum bars
            }
            // Show SpectrumScaleSlider and hide WaveHeightSlider and WaveDensitySlider
            if (spectrumScaleSlider != null)
            {
                spectrumScaleSlider.gameObject.SetActive(true); // Show SpectrumScaleSlider
            }

            if (waveHeightSlider != null)
            {
                waveHeightSlider.gameObject.SetActive(false); // Hide WaveHeightSlider
            }

            if (waveDensitySlider != null)
            {
                waveDensitySlider.gameObject.SetActive(false); // Hide WaveDensitySlider
            }
        }
    }

    // Clean up when the component is destroyed
    void OnDestroy()
    {
        // Clean up spectrum bars
        if (spectrumBars != null)
        {
            foreach (GameObject bar in spectrumBars)
            {
                if (bar != null)
                {
                    Destroy(bar);
                }
            }
        }
    }
}
