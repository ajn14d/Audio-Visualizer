using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class AudioVisualizerController : MonoBehaviour
{
    public Text startUpText;
    public Canvas canvas;
    public Camera camera;
    public AudioSource audioSource;
    public LineRenderer lineRenderer;
    public Slider waveHeightSlider;
    public Slider waveDensitySlider;
    public Slider waveWidthSlider;
    public Slider smoothingSlider;
    public Slider spectrumScaleSlider;
    public Slider spectrumExponentSlider;
    public Slider hueShiftSpeedSlider;
    public Dropdown deviceDropdown; 
    public int selectedDeviceIndex = 1;
    public int sampleRate = 44100;
    public int recordingLength = 1;
    public int numSamples = 1024;
    public float waveformHeight = 1f;
    public float waveDensity = 1f;
    private float[] samples;
    private float[] smoothedSamples;

    // variables for spectrum analyzer
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
    
    // variables for spectrum visualization enhancement
    public float spectrumMinimumThreshold = 0.00001f; // Minimum threshold to filter out noise
    public float spectrumExponent = 0.5f; // Power exponent for non-linear scaling (0.5 = square root)
    public float spectrumVerticalOffset = -3f; // Vertical position offset
    public float hueShiftSpeed = 0.02f; // Speed of hue shifting
    
    // Bar visualization variables
    public GameObject barPrefab; // Assign a cube or other primitive in inspector
    public float barWidth = 0.15f; // Width of each bar
    public float barSpacing = 0.05f; // Space between bars
    public float maxBarHeight = 5f; // Maximum height for bars
    public Color barStartColor = new Color(0f, 0.5f, 1f); // Color at the bottom of bars
    public Color barEndColor = new Color(0f, 1f, 1f); // Color at the top of bars
    private GameObject[] spectrumBars; // Array to hold bar GameObjects
    private bool barsCreated = false; // Flag to track if bars have been created
    public int colorSelect = 0; // varible to switch visualizer color
    public int backgroundSelect = 0; // varible to switch background color
    // Bar smoothing variables
    public int barSmoothingSamples = 2; // Number of bars to average together
    private Queue<float[]> barHistoryBuffer; // Buffer to store previous bar heights
    public float barTransitionSpeed = 5f; // Speed at which bars transition to new heights
    private float[] targetBarHeights; // Target heights for smooth transitions

    // bar material
    public Material spectrumBarMaterial;

    void Start()
    {
        // Initialization code

        StartUpText(); // Start the coroutine to disable startUpText after 10 seconds

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
        if (waveWidthSlider != null)
            {
                waveWidthSlider.minValue = 0.05f;
                waveWidthSlider.maxValue = 35.0f;
                waveWidthSlider.value = lineRenderer.widthMultiplier; // Set initial value

                // Update the LineRenderer width when the slider changes
                waveWidthSlider.onValueChanged.AddListener((value) =>
                {
                    if (lineRenderer != null)
                    {
                        lineRenderer.widthMultiplier = value;
                    }
                });
            }
        else
            {
                Debug.LogWarning("No WaveWidthSlider assigned! Wave width cannot be adjusted.");
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

        if (spectrumExponentSlider != null)
        {
            spectrumExponentSlider.minValue = 0.1f;
            spectrumExponentSlider.maxValue = 1.0f;
            spectrumExponentSlider.value = spectrumExponent;
            spectrumExponentSlider.onValueChanged.AddListener((value) => spectrumExponent = value);
        }
        else
        {
            Debug.LogWarning("No SpectrumExponentSlider assigned! Spectrum exponent cannot be adjusted.");
        }

        if (hueShiftSpeedSlider != null)
        {
            hueShiftSpeedSlider.minValue = 0.0f;
            hueShiftSpeedSlider.maxValue = 0.2f;
            hueShiftSpeedSlider.value = hueShiftSpeed;
            hueShiftSpeedSlider.onValueChanged.AddListener((value) => hueShiftSpeed = value);
        }
        else
        {
            Debug.LogWarning("No hueShiftSpeedSlider assigned! hue shift speed cannot be adjusted.");
        }

         // Get available microphone devices
        string[] devices = Microphone.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No microphone devices found!");
            return;
        }

        // Populate the dropdown with available devices
        List<string> deviceNames = new List<string>();
        for (int i = 0; i < devices.Length; i++)
        {
            deviceNames.Add(devices[i]);  // Add device names to the list
        }

        // Clear existing options and add the new ones
        deviceDropdown.ClearOptions();
        deviceDropdown.AddOptions(deviceNames);

        // Set the default selected index (usually 0 or the first device)
        deviceDropdown.value = selectedDeviceIndex;

        // Add listener to update selectedDeviceIndex when the user changes the selection
        deviceDropdown.onValueChanged.AddListener(OnDeviceSelectionChanged);

        // Start recording from the default device (or previously selected device)
        string selectedDevice = devices[selectedDeviceIndex];

        // Start recording
        ResetRecording();

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
        if (audioSource.clip == null || !Microphone.IsRecording(Microphone.devices[selectedDeviceIndex]))
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

            // Set colors based on barColorSelect
            if (colorSelect == 0)
            {
                // Shift the hue over time using Mathf.PingPong to create a "wave" effect
                float hue = Mathf.PingPong(Time.time * hueShiftSpeed + (i / (float)numSamples), 1f);

                // Apply the color based on the shifted hue (rainbow effect)
                Color rainbowColor = Color.HSVToRGB(hue, 1f, 1f);

                // Set the LineRenderer's color dynamically for each point in the line
                lineRenderer.startColor = rainbowColor;
                lineRenderer.endColor = rainbowColor;
            }
            else if (colorSelect == 1) lineRenderer.startColor = lineRenderer.endColor = Color.red;
            else if (colorSelect == 2) lineRenderer.startColor = lineRenderer.endColor = new Color(1f, 0.65f, 0f); // Orange
            else if (colorSelect == 3) lineRenderer.startColor = lineRenderer.endColor = Color.yellow;
            else if (colorSelect == 4) lineRenderer.startColor = lineRenderer.endColor = Color.green;
            else if (colorSelect == 5) lineRenderer.startColor = lineRenderer.endColor = new Color(0f, 0.5f, 0.5f); // Teal
            else if (colorSelect == 6) lineRenderer.startColor = lineRenderer.endColor = new Color(0.68f, 0.85f, 0.9f); // Light Blue
            else if (colorSelect == 7) lineRenderer.startColor = lineRenderer.endColor = Color.cyan;
            else if (colorSelect == 8) lineRenderer.startColor = lineRenderer.endColor = Color.blue;
            else if (colorSelect == 9) lineRenderer.startColor = lineRenderer.endColor = new Color(0.5f, 0f, 0.5f); // Purple
            else if (colorSelect == 10) lineRenderer.startColor = lineRenderer.endColor = Color.magenta;
            else if (colorSelect == 11) lineRenderer.startColor = lineRenderer.endColor = new Color(1f, 0.75f, 0.8f); // Pink
            else if (colorSelect == 12) lineRenderer.startColor = lineRenderer.endColor = new Color(0.6f, 0.3f, 0f); // Brown
            else if (colorSelect == 13) lineRenderer.startColor = lineRenderer.endColor = Color.white;
            else if (colorSelect == 14) lineRenderer.startColor = lineRenderer.endColor = new Color(0.75f, 0.75f, 0.75f); // Gray  
        }
        // Get spectrum data and update spectrum visualization
        UpdateSpectrumVisualization();
        
        // Check if the "H" key is pressed
        if (Input.GetKeyDown(KeyCode.H))
        {
            // Toggle the Canvas visibility
            canvas.enabled = !canvas.enabled;
        }

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

    // Update waveform height when slider value changes
    public void UpdateWaveHeight(float newHeight)
    {
        waveformHeight = newHeight;
    }

    // Update wave density when slider value changes
    public void UpdateWaveDensity(float newDensity)
    {
        waveDensity = newDensity;
    }

    // Update smoothing when slider value changes
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

    // Function for spectrum visualization
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

    // Fuction to create bar visualization for spectrum
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

            // Disable the first three bars and the last bar
            if (i == 0 || i == 1 || i == 2 || i == spectrumSamples - 1)
            {
                bar.SetActive(false);
            }
        }
        
        barsCreated = true;
        Debug.Log("Created " + spectrumSamples + " spectrum bars");
    }
    
    // Function to update bar heights based on spectrum data
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
        float scaleFactor = 50f;
        float logOffset = 1.05f;

        // Create array to hold current bar heights
        float[] currentHeights = new float[spectrumSamples];

        // Calculate initial heights
        for (int i = 0; i < spectrumSamples; i++)
        {
            // Get the spectrum value and apply threshold to filter out noise
            float value = smoothedSpectrumData[i];
            if (value < spectrumMinimumThreshold) value = 0;

            // Calculate frequency compensation based on bar index
            float frequencyCompensation;
            float normalizedIndex = i / (float)spectrumSamples;
            if (i == 3)
            {
                frequencyCompensation = 0.015f;
            }
            else if (i < 16) // First quarter (0-15) - very low frequencies
            {
                frequencyCompensation = Mathf.Lerp(0.01f, 0.1f, i / 16f);
            }
            else if (i < 32) // Second quarter (16-31) - low-mid frequencies
            {
                frequencyCompensation = Mathf.Lerp(0.1f, 0.3f, (i - 16) / 16f);
            }
            else if (i < 48) // Third quarter (32-47) - mid-high frequencies
            {
                frequencyCompensation = Mathf.Lerp(0.3f, 0.6f, (i - 32) / 16f);
            }
            else // Fourth quarter (48-63) - high frequencies
            {
                frequencyCompensation = Mathf.Lerp(0.5f, 1.0f, (i - 48) / 16f);
            }

            value *= frequencyCompensation;

            // Apply logarithmic scaling adjusted for small values
            float scaledValue = value * scaleFactor;
            float amplitude;

            if (scaledValue > 0)
            {
                amplitude = Mathf.Log(1 + scaledValue) * spectrumScale;
            }
            else
            {
                amplitude = 0;
            }

            // Apply non-linear scaling
            amplitude = Mathf.Pow(amplitude, spectrumExponent);
            
            // Store the height for this bar
            currentHeights[i] = Mathf.Clamp(amplitude, 0.01f, maxBarHeight);
        }

        // Apply temporal smoothing from your existing smoothing slider
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

            // Apply color to the bar
            if (i >= 3 && i <= 62)
            {
                Renderer renderer = spectrumBars[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (colorSelect == 0)
                    {
                        // Shift the hue over time using Mathf.PingPong to create a "wave" effect
                        float hue = Mathf.PingPong(Time.time * hueShiftSpeed + (i / (float)spectrumSamples), 1f);

                        // Apply the color based on the shifted hue
                        renderer.material.color = Color.HSVToRGB(hue, 1f, 1f);

                         // If backgroundSelect is 1, set the background to the complementary color
                        if (backgroundSelect == 1 && hueShiftSpeed > 0)
                            {
                                float oppositeHue = (hue + 0.5f) % 1f; // Shift hue by 180 degrees
                                float desaturatedSaturation = 0.8f; // Reduce saturation for a "washed out" effect
                                float dimmedBrightness = 0.6f; // Lower brightness so it's not too intense

                                Color backgroundColor = Color.HSVToRGB(oppositeHue, desaturatedSaturation, dimmedBrightness);
                                camera.backgroundColor = backgroundColor;
                            }
                    }
                    else if (colorSelect == 1) renderer.material.color = Color.red;
                    else if (colorSelect == 2) renderer.material.color = new Color(1f, 0.65f, 0f); // Orange
                    else if (colorSelect == 3) renderer.material.color = Color.yellow;
                    else if (colorSelect == 4) renderer.material.color = Color.green;
                    else if (colorSelect == 5) renderer.material.color = new Color(0f, 0.5f, 0.5f); // Teal
                    else if (colorSelect == 6) renderer.material.color = new Color(0.68f, 0.85f, 0.9f); // Light Blue
                    else if (colorSelect == 7) renderer.material.color = Color.cyan;
                    else if (colorSelect == 8) renderer.material.color = Color.blue;
                    else if (colorSelect == 9) renderer.material.color = new Color(0.5f, 0f, 0.5f); // Purple
                    else if (colorSelect == 10) renderer.material.color = Color.magenta;
                    else if (colorSelect == 11) renderer.material.color = new Color(1f, 0.75f, 0.8f); // Pink
                    else if (colorSelect == 12) renderer.material.color = new Color(0.6f, 0.3f, 0f); // Brown
                    else if (colorSelect == 13) renderer.material.color = Color.white;
                    else if (colorSelect == 14) renderer.material.color = new Color(0.75f, 0.75f, 0.75f); // Gray
                    else if (colorSelect == 15) colorSelect = 0; // Reset to rainbow mode
                }
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
        bool spectrumBarsActive = spectrumBars != null && spectrumBars[5].activeSelf; // Assuming spectrumBars is an array and bars[0] can be used to check visibility

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
            // Hide SpectrumScaleSlider/spectrumExponentSlider and show WaveHeightSlider/WaveDensitySlider
            if (spectrumScaleSlider != null)
            {
                spectrumScaleSlider.gameObject.SetActive(false); // Hide SpectrumScaleSlider
            }

            if (spectrumExponentSlider != null)
            {
                spectrumExponentSlider.gameObject.SetActive(false); // Hide SpectrumExponentSlider
            }

            if (waveHeightSlider != null)
            {
                waveHeightSlider.gameObject.SetActive(true); // Show WaveHeightSlider
            }

            if (waveDensitySlider != null)
            {
                waveDensitySlider.gameObject.SetActive(true); // Show WaveDensitySlider
            }

            if (waveWidthSlider != null)
            {
                waveWidthSlider.gameObject.SetActive(true); // Show WaveDensitySlider
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
                int index = System.Array.IndexOf(spectrumBars, bar); // Get the index of the current bar

                if (index == 0 || index == 1 || index == 2 || index == 63)
                {
                    bar.SetActive(false); // Disable specific bars
                }
                else
                {
                    bar.SetActive(true); // Enable all other spectrum bars
                }
            }

            // Show SpectrumScaleSlider/spectrumExponentSlider and hide WaveHeightSlider/WaveDensitySlider
            if (spectrumScaleSlider != null)
            {
                spectrumScaleSlider.gameObject.SetActive(true); // Show SpectrumScaleSlider
            }

            if (spectrumExponentSlider != null)
            {
                spectrumExponentSlider.gameObject.SetActive(true); // Show SpectrumExponentSlider
            }

            if (waveHeightSlider != null)
            {
                waveHeightSlider.gameObject.SetActive(false); // Hide WaveHeightSlider
            }

            if (waveDensitySlider != null)
            {
                waveDensitySlider.gameObject.SetActive(false); // Hide WaveDensitySlider
            }

            if (waveWidthSlider != null)
            {
                waveWidthSlider.gameObject.SetActive(false); // Show WaveDensitySlider
            }
        }
    }

    void OnDeviceSelectionChanged(int index)
    {
        // Update the selected device index
        selectedDeviceIndex = index;
        
        // Call ResetRecording to handle stopping and restarting cleanly
        ResetRecording();
    }
    
    public void ColorSwitch()
    {
        colorSelect += 1;
        Debug.Log(colorSelect);
    }
    public void BackgroundColor()
    {
        backgroundSelect += 1;
        Debug.Log(backgroundSelect);

        if (backgroundSelect == 0)
            {
                camera.backgroundColor = Color.black;
            }
        else if (backgroundSelect == 1)
        {
            Debug.Log("Background color changed");

            // Get the current color of the line or bars
            Color currentColor = lineRenderer.startColor; // Or use the bar's color

            // Convert the color to HSV
            Color.RGBToHSV(currentColor, out float h, out float s, out float v);

            // Adjust saturation and brightness to "wash out" the complementary color
            float desaturatedSaturation = 0.8f; // Reduce saturation for a "washed out" effect
            float dimmedBrightness = 0.6f; // Lower brightness so it's not too intense

            // Calculate the complementary hue (opposite side of color wheel)
            float complementaryHue = (h + 0.5f) % 1f; // Shift hue by 180 degrees

            // Convert back to RGB with adjusted saturation and brightness
            Color complementaryColor = Color.HSVToRGB(complementaryHue, desaturatedSaturation, dimmedBrightness);

            // Apply to the background (assuming a Camera background or UI Image)
            camera.backgroundColor = complementaryColor; // For Camera background
            // backgroundImage.color = complementaryColor; // If using a UI Image as background
        }

        else if (backgroundSelect == 2)
            {
                backgroundSelect = 0;
                camera.backgroundColor = Color.black;
            }

    }
    void StartUpText()
    {
        // Start the coroutine to disable startUpText after 10 seconds
        StartCoroutine(DisableTextAfterDelayCoroutine());
    }

    IEnumerator DisableTextAfterDelayCoroutine()
    {
        // Wait for 10 seconds
        yield return new WaitForSeconds(10f);

        // Disable the startUpText after the wait
        if (startUpText != null)
        {
            startUpText.gameObject.SetActive(false);
            Debug.Log("startUpText has been disabled after 10 seconds.");
        }
    }

    public void ResetRecording()
    {
        StartCoroutine(ResetMicrophone());
    }

    private IEnumerator ResetMicrophone()
    {
        if (Microphone.IsRecording(Microphone.devices[selectedDeviceIndex]))
        {
            Microphone.End(Microphone.devices[selectedDeviceIndex]);
        }

        audioSource.Stop();
        
        // Ensure the audio clip is null to remove any old data
        audioSource.clip = null;

        // Short delay to let Unity clear the buffer
        yield return new WaitForSeconds(0.1f);

        // Get the name of the selected device
        string selectedDevice = Microphone.devices[selectedDeviceIndex];
        Debug.Log("Restarting recording with microphone: " + selectedDevice);
        
        // Start recording with the newly selected device
        audioSource.clip = Microphone.Start(selectedDevice, true, recordingLength, sampleRate);

        // Wait for the microphone to start properly
        int timeout = 50;
        while (Microphone.GetPosition(selectedDevice) <= 0 && timeout > 0)
        {
            yield return null;
            timeout--;
        }

        if (timeout <= 0)
        {
            Debug.LogError("Microphone failed to start.");
            yield break;
        }

        // Start playback smoothly
        yield return new WaitForSeconds(0.1f); // Extra delay to avoid crackling
        audioSource.loop = true;
        audioSource.Play();

        // Fade in the volume to prevent sudden waveform artifacts
        StartCoroutine(FadeInAudio());
    }

    // Smoothly fade in audio after reset
    private IEnumerator FadeInAudio()
    {
        float duration = 0.5f; // Fade-in duration in seconds
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, 1f, elapsedTime / duration);
            yield return null;
        }
        audioSource.volume = 1f;
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
