using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;

using HarmonyLib;

using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using System.Collections;
using UnityEngine.SceneManagement;
using BepInEx.Unity.Mono.Bootstrap;

using TMPro;
using System;
using System.Threading.Tasks;

namespace SaveGameBackup;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class SaveGameBackup : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private Harmony harmony;
    private GameObject backupUIWindow; // Reference to the backup UI window
    private string selectedBackupPath; // Variable to hold the currently selected backup path
    private List<GameObject> backupButtons = new List<GameObject>(); // List to hold references to backup buttons
    private GameObject confirmationDialog; // New field for the confirmation dialog

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        if (FindObjectOfType<UnityMainThreadDispatcher>() == null)
            {
                var obj = new GameObject("UnityMainThreadDispatcher");
                obj.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(obj);
            }

        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }

    private void OnDestroy()
    {
        harmony.UnpatchSelf();
    }

    [HarmonyPatch(typeof(SceneManager), "Internal_SceneLoaded")]
    public class SceneLoadPatch
    {
        static void Postfix(ref Scene scene, ref LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                var pluginInfo = UnityChainloader.Instance.Plugins[MyPluginInfo.PLUGIN_GUID];
                SaveGameBackup instance = (SaveGameBackup)pluginInfo.Instance;
                instance.OnMainMenuLoaded();
            }
        }
    }

    private void OnMainMenuLoaded()
    {
        GameObject mainMenu = GameObject.Find("UI/Canvas/MainMenu/ButtonsContainer");

        if (mainMenu != null)
        {
            CreateBackupButton(mainMenu);
            Logger.LogInfo("Backup Save button added to the main menu.");
        }
        else
        {
            Logger.LogError("Failed to locate MainMenu/ButtonsContainer in the scene.");
        }
    }

    private async void BackupSaveFile()
    {
        try
        {
            // Show loading indicator or disable button here if desired
            await Task.Run(() => PerformBackup());
            
            NotificationSystem.Instance().ShowNotification("Current Save File Backed Up");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"Failed to backup save file: {ex.Message}");
        }
    }

    private void PerformBackup()
    {
        string sourcePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "..", "LocalLow", "Perfect Random", "Sulfur"
        );
        sourcePath = Path.GetFullPath(sourcePath);

        string backupBasePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "..", "LocalLow", "Perfect Random"
        );

        string dateTimeStamp = DateTime.Now.ToString("MM-dd-yyyy_HH-mm-ss");
        string backupPath = Path.Combine(backupBasePath, $"Sulfur_{dateTimeStamp}");

        // Create the backup directory
        Directory.CreateDirectory(backupPath);

        if (Directory.Exists(sourcePath))
        {
            // Copy directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                string newPath = dirPath.Replace(sourcePath, backupPath);
                Directory.CreateDirectory(newPath);
            }

            // Copy files
            foreach (string filePath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(filePath).Equals(".log", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string newPath = filePath.Replace(sourcePath, backupPath);
                File.Copy(filePath, newPath, true);
            }

            // Use UnityMainThreadDispatcher to update UI
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                PopulateBackupList(GameObject.Find("BackupContent").transform);
                Logger.LogInfo($"Save backed up successfully to {backupPath}");
            });
        }
    }
 
    private void CheckListeners(Button button)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            Logger.LogInfo($"Listener {i}: {button.onClick.GetPersistentMethodName(i)}");
        }
    }

    private void CreateBackupButton(GameObject buttonsContainer)
    {
        Transform continueButtonTransform = buttonsContainer.transform.Find("Continue");
        if (continueButtonTransform == null)
        {
            Logger.LogError("Failed to locate the Continue button in ButtonsContainer.");
            return;
        }

        GameObject backupButton = Instantiate(continueButtonTransform.gameObject, buttonsContainer.transform);
        backupButton.name = "BackupButton";
        backupButton.transform.SetSiblingIndex(1); // Place between "Continue" (0) and "Options" (2)
        backupButton.SetActive(true); 

        TMPro.TMP_Text textComponent = backupButton.GetComponentInChildren<TMPro.TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = "Manage Save Backups";
        }
        else
        {
            Logger.LogWarning("Failed to locate TMP_Text component on the cloned button.");
        }

        Button buttonComponent = backupButton.GetComponent<Button>();
        if (buttonComponent != null)
        {
            CheckListeners(buttonComponent);

            buttonComponent.onClick = new Button.ButtonClickedEvent();
            buttonComponent.onClick.AddListener(OpenBackupUIWindow);
            
            CheckListeners(buttonComponent);

        }
        else
        {
            Logger.LogWarning("Failed to locate Button component on the cloned button.");
        }
    }

    private void OpenBackupUIWindow()
    {
        GameObject mainMenuUI = GameObject.Find("UI/Canvas/MainMenu");
        if (mainMenuUI != null)
        {
            mainMenuUI.SetActive(false);
        }

        GameObject uiWindow = new GameObject("BackupUIWindow");
        uiWindow.transform.SetParent(GameObject.Find("UI/Canvas").transform, false);
        RectTransform uiRect = uiWindow.AddComponent<RectTransform>();
        uiRect.anchorMin = new Vector2(0.5f, 0.5f);
        uiRect.anchorMax = new Vector2(0.5f, 0.5f);
        uiRect.pivot = new Vector2(0.5f, 0.5f);
        uiRect.sizeDelta = new Vector2(800, 600);
        uiWindow.AddComponent<CanvasRenderer>();
        Image backgroundImage = uiWindow.AddComponent<Image>();
        backgroundImage.color = new Color(0, 0, 0, 0.8f);

        // Create button panel first to establish the bottom area
        GameObject buttonPanel = CreateButtonPanel(uiWindow);
        
        // Create scroll view with adjusted anchors to stop at button panel
        GameObject scrollView = new GameObject("BackupScrollView");
        scrollView.transform.SetParent(uiWindow.transform, false);
        RectTransform scrollRectTransform = scrollView.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0, 0.2f); // Start above button panel
        scrollRectTransform.anchorMax = new Vector2(1, 1); // Extend to top
        scrollRectTransform.offsetMin = new Vector2(10, 10);
        scrollRectTransform.offsetMax = new Vector2(-10, -10);

        // Add scroll view background
        Image scrollViewBg = scrollView.AddComponent<Image>();
        scrollViewBg.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Create viewport - this is what needs fixing
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollView.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        // Remove the Image component entirely and just use RectMask2D instead
        RectMask2D viewportMask = viewport.AddComponent<RectMask2D>();

        // Create content container
        GameObject content = new GameObject("BackupContent");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1); // Anchor to top
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        // Add layout group to content
        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.spacing = 5;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);

        // Add content size fitter
        ContentSizeFitter contentSizeFitter = content.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Setup scroll rect references
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        // Populate the content
        PopulateBackupList(contentRect);
    }

    

    // Adjust the CreateButtons method
    private void CreateButtons(GameObject uiWindow)
    {
        // Create a panel for the buttons
        GameObject buttonPanel = new GameObject("ButtonPanel");
        buttonPanel.transform.SetParent(uiWindow.transform, false);
        RectTransform buttonPanelRect = buttonPanel.AddComponent<RectTransform>();
        buttonPanelRect.anchorMin = new Vector2(0, 0);
        buttonPanelRect.anchorMax = new Vector2(1, 0);
        buttonPanelRect.offsetMin = new Vector2(0, 0); // 0 distance from bottom
        buttonPanelRect.offsetMax = new Vector2(0, 100); // Height of the button panel

        // Layout group for buttons
        HorizontalLayoutGroup buttonLayout = buttonPanel.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.spacing = 10;

        // Button to back up the current save
        GameObject backupButton = CreateButton("Backup Current Save", BackupSaveFile);
        backupButton.transform.SetParent(buttonPanel.transform, false);

        // Button to replace the current save with the selected one
        GameObject replaceButton = CreateButton("Replace Current Save", ReplaceCurrentSave);
        replaceButton.transform.SetParent(buttonPanel.transform, false);

        // Button to close the UI
        GameObject closeButton = CreateButton("Close", CloseBackupUIWindow);
        closeButton.transform.SetParent(buttonPanel.transform, false);
    }
    private GameObject CreateButtonPanel(GameObject uiWindow)
    {
        GameObject buttonPanel = new GameObject("ButtonPanel");
        buttonPanel.transform.SetParent(uiWindow.transform, false);
        RectTransform buttonPanelRect = buttonPanel.AddComponent<RectTransform>();
        buttonPanelRect.anchorMin = new Vector2(0, 0);
        buttonPanelRect.anchorMax = new Vector2(1, 0.2f); // Take up bottom 20%
        buttonPanelRect.offsetMin = new Vector2(10, 10);
        buttonPanelRect.offsetMax = new Vector2(-10, -10);

        // Add background to button panel
        Image buttonPanelBg = buttonPanel.AddComponent<Image>();
        buttonPanelBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        HorizontalLayoutGroup buttonLayout = buttonPanel.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.padding = new RectOffset(10, 10, 10, 10);
        buttonLayout.spacing = 10;

        GameObject backupButton = CreateButton("Backup Current Save", BackupSaveFile);
        backupButton.transform.SetParent(buttonPanel.transform, false);

        GameObject replaceButton = CreateButton("Replace Current Save", ReplaceCurrentSave);
        replaceButton.transform.SetParent(buttonPanel.transform, false);

        GameObject deleteButton = CreateButton("Delete Selected Backup", ShowDeleteConfirmation);
        deleteButton.transform.SetParent(buttonPanel.transform, false);

        GameObject closeButton = CreateButton("Close", CloseBackupUIWindow);
        closeButton.transform.SetParent(buttonPanel.transform, false);

        return buttonPanel;
    }

    private GameObject CreateButton(string buttonText, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(buttonText);
        
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        
        Button button = buttonObject.AddComponent<Button>();
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(0, 40); // Height only, width controlled by layout

        // Create a child object for the text to properly center it
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(buttonObject.transform, false);
        
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = textObject.AddComponent<TextMeshProUGUI>();
        btnText.text = buttonText;
        btnText.fontSize = 16;
        btnText.color = Color.white;
        btnText.alignment = TextAlignmentOptions.Center;

        // Setup button colors
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        colors.selectedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        button.colors = colors;

        button.onClick.AddListener(action);
        
        return buttonObject;
    }

    // Method to replace the current save with the selected backup
    private void ReplaceCurrentSave()
    {
        // Logic to replace the current save with the selected backup
        // Assuming you have a way to get the selected backup path from the UI
        string selectedBackupPath = GetSelectedBackupPath(); // Implement this method to return the selected backup path

        if (!string.IsNullOrEmpty(selectedBackupPath))
        {
            RestoreBackup(selectedBackupPath);
        }
        else
        {
            Logger.LogWarning("No backup selected to replace the current save.");
        }
    }

    // Method to close the backup UI window
    private void CloseBackupUIWindow()
    {
        // Find the Backup UI window and destroy it
        GameObject backupUIWindow = GameObject.Find("BackupUIWindow");
        if (backupUIWindow != null)
        {
            Destroy(backupUIWindow);
        }

        // Re-enable the main menu UI
        GameObject mainMenuUI = GameObject.Find("UI/Canvas/MainMenu");
        if (mainMenuUI != null)
        {
            mainMenuUI.SetActive(true);
        }

        Logger.LogInfo("Backup UI closed and Main Menu re-enabled.");
    }

    private string GetSelectedBackupPath()
    {
        // If a backup is selected, return the path
        if (!string.IsNullOrEmpty(selectedBackupPath))
        {
            return selectedBackupPath;
        }
        
        // No backup selected, log a warning or notify the user if needed
        Logger.LogWarning("No backup selected. Please select a backup from the list.");
        return null; // Return null to indicate no selection
    }



    private void PopulateBackupList(Transform content)
    {
        // Clear existing items
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
        backupButtons.Clear();

        string backupBasePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "..", "LocalLow", "Perfect Random"
        );

        string[] directories = Directory.GetDirectories(backupBasePath, "Sulfur_*");
        foreach (string directory in directories)
        {
            GameObject backupButton = CreateButton(Path.GetFileName(directory), () => SelectBackup(directory));
            backupButton.transform.SetParent(content, false);
            
            // Set proper layout properties
            LayoutElement layoutElement = backupButton.AddComponent<LayoutElement>();
            layoutElement.minHeight = 40;
            layoutElement.flexibleWidth = 1;
            
            // Update the RectTransform
            RectTransform buttonRect = backupButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0, 0);
            buttonRect.anchorMax = new Vector2(1, 0);

            // Set initial colors
            Image buttonImage = backupButton.GetComponent<Image>();
            buttonImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            
            backupButtons.Add(backupButton);
        }

        // If we had a previous selection, reselect it if it still exists
        if (!string.IsNullOrEmpty(selectedBackupPath) && Directory.Exists(selectedBackupPath))
        {
            SelectBackup(selectedBackupPath);
        }
    }

    // Method to handle selection of a backup
    private void SelectBackup(string backupPath)
    {
        selectedBackupPath = backupPath;

        // Update visual selection for all buttons
        foreach (GameObject button in backupButtons)
        {
            Image buttonImage = button.GetComponent<Image>();
            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            
            if (buttonText != null && Path.GetFileName(backupPath) == buttonText.text)
            {
                // Selected button
                buttonImage.color = new Color(0.2f, 0.4f, 0.8f, 1f); // Bright blue for selection
                buttonText.color = Color.white;
            }
            else
            {
                // Unselected buttons
                buttonImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                buttonText.color = Color.white;
            }
        }
    }

    private void ShowDeleteConfirmation()
    {
        if (string.IsNullOrEmpty(selectedBackupPath))
        {
            Logger.LogWarning("No backup selected for deletion.");
            return;
        }

        // Create confirmation dialog
        confirmationDialog = new GameObject("ConfirmationDialog");
        confirmationDialog.transform.SetParent(GameObject.Find("UI/Canvas").transform, false);
        
        RectTransform dialogRect = confirmationDialog.AddComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(400, 200);

        Image dialogBg = confirmationDialog.AddComponent<Image>();
        dialogBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        // Add message
        GameObject messageObj = new GameObject("Message");
        messageObj.transform.SetParent(confirmationDialog.transform, false);
        
        RectTransform messageRect = messageObj.AddComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0, 0.5f);
        messageRect.anchorMax = new Vector2(1, 1);
        messageRect.offsetMin = new Vector2(10, -10);
        messageRect.offsetMax = new Vector2(-10, -10);

        TextMeshProUGUI message = messageObj.AddComponent<TextMeshProUGUI>();
        message.text = "Are you sure you want to delete this backup?\nThis action cannot be undone.";
        message.fontSize = 18;
        message.alignment = TextAlignmentOptions.Center;
        message.color = Color.white;

        // Add buttons container
        GameObject buttonsContainer = new GameObject("ButtonsContainer");
        buttonsContainer.transform.SetParent(confirmationDialog.transform, false);
        
        RectTransform buttonsRect = buttonsContainer.AddComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0, 0);
        buttonsRect.anchorMax = new Vector2(1, 0.4f);
        buttonsRect.offsetMin = new Vector2(10, 10);
        buttonsRect.offsetMax = new Vector2(-10, -10);

        HorizontalLayoutGroup buttonsLayout = buttonsContainer.AddComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 10;
        buttonsLayout.childControlWidth = true;
        buttonsLayout.childForceExpandWidth = true;

        // Create Yes and No buttons
        GameObject yesButton = CreateButton("Yes", () => {
            DeleteSelectedBackup();
            Destroy(confirmationDialog);
        });
        yesButton.transform.SetParent(buttonsContainer.transform, false);

        GameObject noButton = CreateButton("No", () => {
            Destroy(confirmationDialog);
        });
        noButton.transform.SetParent(buttonsContainer.transform, false);
    }

    private void DeleteSelectedBackup()
    {
        if (string.IsNullOrEmpty(selectedBackupPath))
        {
            NotificationSystem.Instance().ShowNotification("No Backup selected for deletion");
            Logger.LogWarning("No backup selected for deletion.");
            return;
        }

        try
        {
            Directory.Delete(selectedBackupPath, true);
            Logger.LogInfo($"Successfully deleted backup at {selectedBackupPath}");
            NotificationSystem.Instance().ShowNotification("Backup deleted");
            selectedBackupPath = null;
            PopulateBackupList(GameObject.Find("BackupContent").transform);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to delete backup: {ex.Message}");
        }
    }

    private void RestoreBackup(string backupPath)
    {
        try
        {
            string sourcePath = backupPath; // Backup folder path
            string destinationPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "..", "LocalLow", "Perfect Random", "Sulfur"
            );

            // Ensure destination exists
            Directory.CreateDirectory(destinationPath);

            // Copy directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                string newDirPath = dirPath.Replace(sourcePath, destinationPath);
                Directory.CreateDirectory(newDirPath);
            }

            // Copy files, skipping .log files
            foreach (string filePath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(filePath).Equals(".log", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo($"Skipping log file: {filePath}");
                    continue; // Skip .log files
                }

                // Backup the file
                File.Copy(filePath, filePath.Replace(sourcePath, destinationPath), true);
            }
            NotificationSystem.Instance().ShowNotification("Current Save File Replaced");
            Logger.LogInfo($"Successfully restored backup from {backupPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to restore backup: {ex.Message}");
        }
    }

}
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher instance;
    private readonly Queue<Action> executionQueue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (!instance)
        {
            instance = FindObjectOfType<UnityMainThreadDispatcher>();
            if (!instance)
            {
                var obj = new GameObject("UnityMainThreadDispatcher");
                instance = obj.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(obj);
            }
        }
        return instance;
    }

    public void Update()
    {
        lock(executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue().Invoke();
            }
        }
    }

    public void Enqueue(Action action)
    {
        lock(executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }
}

public class NotificationSystem : MonoBehaviour
{
    private static NotificationSystem instance;
    private GameObject notificationPrefab;
    private const float NOTIFICATION_DURATION = 3f;
    private const float FADE_DURATION = 0.5f;
    internal static new ManualLogSource Logger;

    public static NotificationSystem Instance()
    {
        if (!instance)
        {
            instance = FindObjectOfType<NotificationSystem>();
            if (!instance)
            {
                var obj = new GameObject("NotificationSystem");
                instance = obj.AddComponent<NotificationSystem>();
                DontDestroyOnLoad(obj);
                instance.Initialize();
            }
        }
        return instance;
    }

    private void Initialize()
    {
        // Create notification prefab
        notificationPrefab = new GameObject("NotificationPrefab");
        notificationPrefab.SetActive(false);
        DontDestroyOnLoad(notificationPrefab);

        var rectTransform = notificationPrefab.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(300, 50);

        var canvasGroup = notificationPrefab.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0;

        var background = notificationPrefab.AddComponent<Image>();
        background.color = new Color(0, 0, 0, 0.8f);

        // Create text object
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(notificationPrefab.transform, false);
        
        var textRectTransform = textObj.AddComponent<RectTransform>();
        textRectTransform.anchorMin = Vector2.zero;
        textRectTransform.anchorMax = Vector2.one;
        textRectTransform.offsetMin = new Vector2(10, 5);
        textRectTransform.offsetMax = new Vector2(-10, -5);

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.fontSize = 16;
        tmp.color = Color.white;
    }

    public void ShowNotification(string message)
    {
        StartCoroutine(ShowNotificationCoroutine(message));
    }

    private IEnumerator ShowNotificationCoroutine(string message)
    {
        // Find or create canvas
        var canvas = GameObject.Find("UI/Canvas");
        if (!canvas)
        {
            Logger.LogError("Cannot find UI Canvas!");
            yield break;
        }

        // Create notification instance
        var notification = Instantiate(notificationPrefab, canvas.transform);
        notification.SetActive(true);

        // Set position (bottom left)
        var rectTransform = notification.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.anchoredPosition = new Vector2(20, 20);

        // Set text
        var tmp = notification.GetComponentInChildren<TextMeshProUGUI>();
        tmp.text = message;

        // Fade in
        var canvasGroup = notification.GetComponent<CanvasGroup>();
        float elapsed = 0;
        while (elapsed < FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / FADE_DURATION);
            yield return null;
        }

        // Wait
        yield return new WaitForSeconds(NOTIFICATION_DURATION);

        // Fade out
        elapsed = 0;
        while (elapsed < FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1, 0, elapsed / FADE_DURATION);
            yield return null;
        }

        // Destroy
        Destroy(notification);
    }
}