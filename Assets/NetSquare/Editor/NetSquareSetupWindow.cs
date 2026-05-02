using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetSquare.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NetSquare.EditorTools
{
    /// <summary>
    /// Provides Unity editor setup, validation and connection tools for NetSquare.
    /// </summary>
    public class NetSquareSetupWindow : EditorWindow
    {
        #region Variables
        /// <summary>
        /// Stores the default settings asset path.
        /// </summary>
        private const string DefaultSettingsPath = "Assets/NetSquare/NetSquareSettings.asset";
        /// <summary>
        /// Stores the NetSquare plugin folder path.
        /// </summary>
        private const string PluginFolderPath = "Assets/NetSquare/Plugins";
        /// <summary>
        /// Stores required plugin dll names.
        /// </summary>
        private static readonly string[] RequiredDlls = { "NetSquareCore.dll", "NetSquareClient.dll" };
        /// <summary>
        /// Stores optional plugin dependency dll names.
        /// </summary>
        private static readonly string[] OptionalDependencyDlls = { "Utf8Json.dll", "System.ValueTuple.dll", "System.Threading.Tasks.Extensions.dll" };
        /// <summary>
        /// Stores the selected settings asset.
        /// </summary>
        private NetSquareSettings settings;
        /// <summary>
        /// Stores validation messages.
        /// </summary>
        private readonly List<ValidationIssue> validationIssues = new List<ValidationIssue>();
        /// <summary>
        /// Stores the scroll position.
        /// </summary>
        private Vector2 scroll;
        /// <summary>
        /// Stores the last connection test result.
        /// </summary>
        private string connectionResult = "Not tested";
        /// <summary>
        /// Stores whether a connection test is currently running.
        /// </summary>
        private bool connectionTestRunning;
        #endregion

        #region Menu
        /// <summary>
        /// Opens the NetSquare setup window.
        /// </summary>
        [MenuItem("Tools/NetSquare/Setup")]
        public static void Open()
        {
            GetWindow<NetSquareSetupWindow>("NetSquare Setup");
        }
        #endregion

        #region Unity Events
        /// <summary>
        /// Initializes the window state.
        /// </summary>
        private void OnEnable()
        {
            settings = FindSettingsAsset();
            RefreshValidation();
        }

        /// <summary>
        /// Draws the setup window UI.
        /// </summary>
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawHeader();
            DrawSettings();
            DrawActions();
            DrawConnectionTester();
            DrawValidation();
            EditorGUILayout.EndScrollView();
        }
        #endregion

        #region UI
        /// <summary>
        /// Draws the window header.
        /// </summary>
        private void DrawHeader()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("NetSquare Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates the standard Unity scene wiring, checks plugin DLLs, validates prefabs and tests a server endpoint without entering Play Mode.", MessageType.Info);
        }

        /// <summary>
        /// Draws settings selection.
        /// </summary>
        private void DrawSettings()
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
                settings = (NetSquareSettings)EditorGUILayout.ObjectField("NetSquare Settings", settings, typeof(NetSquareSettings), false);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Find"))
                    {
                        settings = FindSettingsAsset();
                        RefreshValidation();
                    }
                    if (GUILayout.Button("Create / Assign"))
                    {
                        settings = EnsureSettingsAsset();
                        SetupScene();
                        RefreshValidation();
                    }
                }
            }
        }

        /// <summary>
        /// Draws setup and validation actions.
        /// </summary>
        private void DrawActions()
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene Tools", EditorStyles.boldLabel);
                if (GUILayout.Button("Setup Current Scene"))
                {
                    settings = EnsureSettingsAsset();
                    SetupScene();
                    RefreshValidation();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Validate DLLs"))
                        RefreshValidation();
                    if (GUILayout.Button("Validate Prefabs"))
                        RefreshValidation();
                    if (GUILayout.Button("Select Settings") && settings != null)
                        Selection.activeObject = settings;
                }
            }
        }

        /// <summary>
        /// Draws the editor connection tester.
        /// </summary>
        private void DrawConnectionTester()
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Connection Tester", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Result", connectionTestRunning ? "Testing..." : connectionResult);
                using (new EditorGUI.DisabledScope(connectionTestRunning || settings == null))
                {
                    if (GUILayout.Button("Test IP / Port"))
                        TestConnection();
                }
            }
        }

        /// <summary>
        /// Draws validation results.
        /// </summary>
        private void DrawValidation()
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
                if (validationIssues.Count == 0)
                {
                    EditorGUILayout.HelpBox("Everything looks ready.", MessageType.Info);
                    return;
                }

                for (int i = 0; i < validationIssues.Count; i++)
                {
                    ValidationIssue issue = validationIssues[i];
                    EditorGUILayout.HelpBox(issue.Message, issue.Type);
                    if (issue.Target != null && GUILayout.Button("Select", GUILayout.Width(80)))
                        Selection.activeObject = issue.Target;
                }
            }
        }
        #endregion

        #region Setup
        /// <summary>
        /// Creates or loads the default settings asset.
        /// </summary>
        /// <returns>Settings asset.</returns>
        private static NetSquareSettings EnsureSettingsAsset()
        {
            NetSquareSettings asset = AssetDatabase.LoadAssetAtPath<NetSquareSettings>(DefaultSettingsPath);
            if (asset != null)
                return asset;

            EnsureFolder("Assets/NetSquare");
            asset = CreateInstance<NetSquareSettings>();
            AssetDatabase.CreateAsset(asset, DefaultSettingsPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// Finds the first NetSquare settings asset in the project.
        /// </summary>
        /// <returns>Settings asset, or null.</returns>
        private static NetSquareSettings FindSettingsAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:NetSquareSettings");
            if (guids == null || guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<NetSquareSettings>(path);
        }

        /// <summary>
        /// Ensures the current scene contains the standard NetSquare runtime objects.
        /// </summary>
        private void SetupScene()
        {
            NetSquareController controller = FindObjectOfTypeInScene<NetSquareController>();
            if (controller == null)
                controller = CreateSceneComponent<NetSquareController>("NetSquareController");

            AssignSettings(controller, settings);
            EnsureComponent<NetSquareDebugOverlay>(controller.gameObject);
            EnsureComponent<NetSquareWorldDebugView>(controller.gameObject);

            NetSquareTransformsManager manager = FindObjectOfTypeInScene<NetSquareTransformsManager>();
            if (manager == null)
                manager = CreateSceneComponent<NetSquareTransformsManager>("NetSquareTransformsManager");

            if (manager.PlayerPrefab != null && manager.PlayerPrefab.GetComponent<NetsquareOtherPlayerController>() == null)
                Debug.LogWarning("[NetSquare] PlayerPrefab should contain a NetsquareOtherPlayerController.");

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = controller.gameObject;
        }

        /// <summary>
        /// Assigns settings to the private controller field through Unity serialization.
        /// </summary>
        /// <param name="controller">Controller to configure.</param>
        /// <param name="asset">Settings asset to assign.</param>
        private static void AssignSettings(NetSquareController controller, NetSquareSettings asset)
        {
            if (controller == null || asset == null)
                return;

            SerializedObject serializedObject = new SerializedObject(controller);
            SerializedProperty property = serializedObject.FindProperty("settings");
            if (property == null)
                return;

            property.objectReferenceValue = asset;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        /// <summary>
        /// Ensures a component exists on a game object.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <param name="gameObject">Target game object.</param>
        /// <returns>Existing or added component.</returns>
        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component != null)
                return component;

            return Undo.AddComponent<T>(gameObject);
        }

        /// <summary>
        /// Creates a component on a new scene game object.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <param name="name">Game object name.</param>
        /// <returns>Created component.</returns>
        private static T CreateSceneComponent<T>(string name) where T : Component
        {
            GameObject gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            return gameObject.AddComponent<T>();
        }

        /// <summary>
        /// Finds one active or inactive object of type in the scene.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <returns>Found component, or null.</returns>
        private static T FindObjectOfTypeInScene<T>() where T : UnityEngine.Object
        {
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < objects.Length; i++)
            {
                UnityEngine.Object obj = objects[i];
                if (obj == null || EditorUtility.IsPersistent(obj))
                    continue;

                return objects[i];
            }

            return null;
        }

        /// <summary>
        /// Ensures an asset folder exists.
        /// </summary>
        /// <param name="folder">Folder path.</param>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
        #endregion

        #region Validation
        /// <summary>
        /// Refreshes all validation checks.
        /// </summary>
        private void RefreshValidation()
        {
            validationIssues.Clear();
            ValidateSettings();
            ValidateDlls();
            ValidateScene();
            ValidatePrefabs();
        }

        /// <summary>
        /// Validates settings availability.
        /// </summary>
        private void ValidateSettings()
        {
            if (settings == null)
                validationIssues.Add(new ValidationIssue("No NetSquareSettings asset assigned.", MessageType.Warning, null));
        }

        /// <summary>
        /// Validates plugin dll availability.
        /// </summary>
        private void ValidateDlls()
        {
            for (int i = 0; i < RequiredDlls.Length; i++)
            {
                string assetPath = PluginFolderPath + "/" + RequiredDlls[i];
                if (!File.Exists(ToFullPath(assetPath)))
                    validationIssues.Add(new ValidationIssue("Missing required DLL: " + assetPath, MessageType.Error, null));
            }

            for (int i = 0; i < OptionalDependencyDlls.Length; i++)
            {
                string assetPath = PluginFolderPath + "/" + OptionalDependencyDlls[i];
                if (!File.Exists(ToFullPath(assetPath)))
                    validationIssues.Add(new ValidationIssue("Missing optional dependency DLL: " + assetPath, MessageType.Warning, null));
            }
        }

        /// <summary>
        /// Validates scene objects.
        /// </summary>
        private void ValidateScene()
        {
            NetSquareController[] controllers = FindSceneObjects<NetSquareController>();
            if (controllers.Length == 0)
                validationIssues.Add(new ValidationIssue("Scene has no NetSquareController.", MessageType.Warning, null));
            if (controllers.Length > 1)
                validationIssues.Add(new ValidationIssue("Scene has multiple NetSquareController objects.", MessageType.Warning, controllers[0]));

            for (int i = 0; i < controllers.Length; i++)
                if (controllers[i].Settings == null)
                    validationIssues.Add(new ValidationIssue("NetSquareController is missing settings.", MessageType.Error, controllers[i]));

            NetSquareTransformsManager[] managers = FindSceneObjects<NetSquareTransformsManager>();
            if (managers.Length == 0)
                validationIssues.Add(new ValidationIssue("Scene has no NetSquareTransformsManager.", MessageType.Warning, null));
            for (int i = 0; i < managers.Length; i++)
                ValidatePlayerPrefab(managers[i].PlayerPrefab, managers[i]);
        }

        /// <summary>
        /// Validates NetSquare-related prefabs.
        /// </summary>
        private void ValidatePrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                NetSquareController controller = prefab.GetComponentInChildren<NetSquareController>(true);
                if (controller != null && controller.Settings == null)
                    validationIssues.Add(new ValidationIssue(path + ": NetSquareController is missing settings.", MessageType.Error, prefab));

                NetSquareTransformsManager manager = prefab.GetComponentInChildren<NetSquareTransformsManager>(true);
                if (manager != null)
                    ValidatePlayerPrefab(manager.PlayerPrefab, prefab);

                NetsquareOtherPlayerController otherPlayerController = prefab.GetComponentInChildren<NetsquareOtherPlayerController>(true);
                if (LooksLikeRemotePlayerPrefab(path, prefab) && otherPlayerController == null)
                    validationIssues.Add(new ValidationIssue(path + ": PlayerPrefab has no NetsquareOtherPlayerController.", MessageType.Error, prefab));
            }
        }

        /// <summary>
        /// Validates a player prefab reference.
        /// </summary>
        /// <param name="prefab">Prefab to validate.</param>
        /// <param name="target">Issue target.</param>
        private void ValidatePlayerPrefab(GameObject prefab, UnityEngine.Object target)
        {
            if (prefab == null)
            {
                validationIssues.Add(new ValidationIssue("NetSquareTransformsManager has no PlayerPrefab assigned.", MessageType.Warning, target));
                return;
            }

            if (prefab.GetComponent<NetsquareOtherPlayerController>() == null && prefab.GetComponentInChildren<NetsquareOtherPlayerController>(true) == null)
                validationIssues.Add(new ValidationIssue(prefab.name + " has no NetsquareOtherPlayerController.", MessageType.Error, prefab));
        }

        /// <summary>
        /// Finds all non-persistent scene objects of a type.
        /// </summary>
        /// <typeparam name="T">Component type.</typeparam>
        /// <returns>Scene objects.</returns>
        private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
        {
            List<T> results = new List<T>();
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < objects.Length; i++)
                if (objects[i] != null && !EditorUtility.IsPersistent(objects[i]))
                    results.Add(objects[i]);

            return results.ToArray();
        }

        /// <summary>
        /// Checks whether a prefab looks like a remote player prefab.
        /// </summary>
        /// <param name="path">Prefab path.</param>
        /// <param name="prefab">Prefab object.</param>
        /// <returns>True when the prefab should be validated as a remote player prefab.</returns>
        private static bool LooksLikeRemotePlayerPrefab(string path, GameObject prefab)
        {
            string name = prefab.name.ToLowerInvariant();
            string lowerPath = path.ToLowerInvariant();
            return name.Contains("other") || name.Contains("remote") || lowerPath.Contains("otherplayers") || lowerPath.Contains("playerprefab");
        }
        #endregion

        #region Connection
        /// <summary>
        /// Tests whether the configured TCP endpoint accepts a connection.
        /// </summary>
        private async void TestConnection()
        {
            if (settings == null)
                return;

            connectionTestRunning = true;
            connectionResult = "Testing " + settings.IPAddress + ":" + settings.Port + "...";
            Repaint();

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    Task connectTask = client.ConnectAsync(settings.IPAddress, settings.Port);
                    Task timeoutTask = Task.Delay(2500);
                    Task finishedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (finishedTask != connectTask)
                    {
                        connectionResult = "Timeout after 2500 ms.";
                    }
                    else
                    {
                        await connectTask;
                        connectionResult = "TCP endpoint reachable. Protocol setting: " + settings.ProtocoleType;
                    }
                }
            }
            catch (Exception ex)
            {
                connectionResult = "Failed: " + ex.Message;
            }
            finally
            {
                connectionTestRunning = false;
                Repaint();
            }
        }
        #endregion

        #region Paths
        /// <summary>
        /// Converts an asset path into a full file path.
        /// </summary>
        /// <param name="assetPath">Unity asset path.</param>
        /// <returns>Full file path.</returns>
        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
        #endregion

        #region Types
        /// <summary>
        /// Represents one validation issue.
        /// </summary>
        private sealed class ValidationIssue
        {
            /// <summary>
            /// Stores the issue message.
            /// </summary>
            public readonly string Message;
            /// <summary>
            /// Stores the issue type.
            /// </summary>
            public readonly MessageType Type;
            /// <summary>
            /// Stores the target object.
            /// </summary>
            public readonly UnityEngine.Object Target;

            /// <summary>
            /// Initializes a new instance of the validation issue class.
            /// </summary>
            /// <param name="message">Issue message.</param>
            /// <param name="type">Issue type.</param>
            /// <param name="target">Target object.</param>
            public ValidationIssue(string message, MessageType type, UnityEngine.Object target)
            {
                Message = message;
                Type = type;
                Target = target;
            }
        }
        #endregion
    }
}
