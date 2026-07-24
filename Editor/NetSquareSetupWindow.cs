using NetSquare.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NetSquare.Client.Editor
{
    /// <summary>
    /// Creates and validates the NetSquare Unity runtime setup.
    /// </summary>
    public sealed class NetSquareSetupWindow : EditorWindow
    {
        #region Constants
        private const string DefaultSettingsPath = "Assets/NetSquare/NetSquareSettings.asset";
        private const int HandshakeTimeoutMilliseconds = 10000;
        #endregion

        #region Fields
        private readonly List<string> validationErrors = new List<string>();
        private readonly List<string> validationWarnings = new List<string>();
        private NetSquareSettings settings;
        private Vector2 scrollPosition;
        private bool connectionTestRunning;
        private string connectionResult = "Not tested.";
        #endregion

        #region Window lifecycle
        /// <summary>
        /// Opens the NetSquare setup window.
        /// </summary>
        [MenuItem("Window/NetSquare/Setup")]
        public static void Open()
        {
            NetSquareSetupWindow window = GetWindow<NetSquareSetupWindow>("NetSquare Setup");
            window.minSize = new Vector2(520f, 480f);
            window.Show();
        }

        /// <summary>
        /// Loads the current settings asset and refreshes validation.
        /// </summary>
        private void OnEnable()
        {
            settings = FindSettingsAsset();
            RefreshValidation();
        }

        /// <summary>
        /// Draws package setup, connection testing and validation controls.
        /// </summary>
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("NetSquare " + NetSquarePackageInfo.PackageVersion, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates the runtime scene owner and validates the exact NetSquare assemblies. " +
                "Samples are imported separately from Package Manager.",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            settings = (NetSquareSettings)EditorGUILayout.ObjectField(
                "Settings",
                settings,
                typeof(NetSquareSettings),
                false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find"))
                    settings = FindSettingsAsset();
                if (GUILayout.Button("Create"))
                    settings = EnsureSettingsAsset();
                if (GUILayout.Button("Select") && settings != null)
                    Selection.activeObject = settings;
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Setup Current Scene"))
                SetupCurrentScene();

            using (new EditorGUI.DisabledScope(connectionTestRunning || settings == null))
            {
                if (GUILayout.Button(connectionTestRunning ? "Testing..." : "Test NetSquare Handshake"))
                    TestConnection();
            }
            EditorGUILayout.LabelField("Handshake", connectionResult);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Refresh Validation"))
                RefreshValidation();
            DrawValidation();
            EditorGUILayout.EndScrollView();
        }
        #endregion

        #region Setup
        /// <summary>
        /// Creates or returns the default project-local settings asset.
        /// </summary>
        /// <returns>Settings asset.</returns>
        private static NetSquareSettings EnsureSettingsAsset()
        {
            NetSquareSettings asset =
                AssetDatabase.LoadAssetAtPath<NetSquareSettings>(DefaultSettingsPath);
            if (asset != null)
                return asset;

            EnsureAssetFolder("Assets/NetSquare");
            asset = CreateInstance<NetSquareSettings>();
            AssetDatabase.CreateAsset(asset, DefaultSettingsPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// Finds the first project-local NetSquare settings asset.
        /// </summary>
        /// <returns>Found asset, or null.</returns>
        private static NetSquareSettings FindSettingsAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:NetSquareSettings");
            if (guids.Length == 0)
                return null;

            return AssetDatabase.LoadAssetAtPath<NetSquareSettings>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>
        /// Creates and configures the primary controller in the active scene.
        /// </summary>
        private void SetupCurrentScene()
        {
            if (settings == null)
                settings = EnsureSettingsAsset();

            NetSquareController[] controllers = FindSceneObjects<NetSquareController>();
            NetSquareController controller;
            if (controllers.Length == 0)
            {
                GameObject gameObject = new GameObject("NetSquareController");
                Undo.RegisterCreatedObjectUndo(gameObject, "Create NetSquareController");
                controller = gameObject.AddComponent<NetSquareController>();
            }
            else
            {
                controller = controllers[0];
            }

            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty property = serializedController.FindProperty("settings");
            property.objectReferenceValue = settings;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeObject = controller.gameObject;
            RefreshValidation();
        }

        /// <summary>
        /// Ensures a nested asset folder exists.
        /// </summary>
        /// <param name="folderPath">Unity asset folder path.</param>
        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
        #endregion

        #region Validation
        /// <summary>
        /// Validates package assemblies, settings and scene ownership.
        /// </summary>
        private void RefreshValidation()
        {
            validationErrors.Clear();
            validationWarnings.Clear();
            ValidateAssemblies();
            ValidateSettings();
            ValidateScene();
            Repaint();
        }

        /// <summary>
        /// Validates exact Client and Core assembly versions from the resolved package.
        /// </summary>
        private void ValidateAssemblies()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(NSClient).Assembly);
            if (packageInfo == null)
            {
                validationErrors.Add("Unity could not resolve the NetSquare package.");
                return;
            }

            ValidateAssemblyFile(
                Path.Combine(packageInfo.resolvedPath, "Runtime", "Plugins", "NetSquareCore.dll"),
                "NetSquareCore");
            ValidateAssemblyFile(
                Path.Combine(packageInfo.resolvedPath, "Runtime", "Plugins", "NetSquareClient.dll"),
                "NetSquareClient");
        }

        /// <summary>
        /// Validates one managed plugin file and its exact required version.
        /// </summary>
        /// <param name="path">Absolute assembly path.</param>
        /// <param name="expectedName">Expected assembly name.</param>
        private void ValidateAssemblyFile(string path, string expectedName)
        {
            if (!File.Exists(path))
            {
                validationErrors.Add("Missing required assembly: " + path);
                return;
            }

            try
            {
                AssemblyName assembly = AssemblyName.GetAssemblyName(path);
                if (assembly.Name != expectedName)
                    validationErrors.Add(path + " has unexpected assembly name " + assembly.Name + ".");
                if (assembly.Version.ToString() != NetSquarePackageInfo.RequiredAssemblyVersion)
                {
                    validationErrors.Add(
                        expectedName + " is " + assembly.Version +
                        " but this package requires " +
                        NetSquarePackageInfo.RequiredAssemblyVersion + ".");
                }
            }
            catch (Exception exception)
            {
                validationErrors.Add("Cannot inspect " + path + ": " + exception.Message);
            }
        }

        /// <summary>
        /// Validates the selected settings and reports security combinations.
        /// </summary>
        private void ValidateSettings()
        {
            if (settings == null)
            {
                validationWarnings.Add("No NetSquareSettings asset is selected.");
                return;
            }

            try
            {
                settings.Validate();
            }
            catch (Exception exception)
            {
                validationErrors.Add("Invalid settings: " + exception.Message);
            }

            if (settings.UseUdpAuthentication && !settings.UseTLS)
            {
                validationWarnings.Add(
                    "UDP authentication is enabled without TLS. MAC64 protects datagrams, " +
                    "but the UDP session key crosses the TCP handshake without encryption.");
            }
            if (settings.SynchronizationTransport == NetSquareSyncTransport.UnreliableUdp &&
                settings.ProtocoleType != NetSquareProtocoleType.TCP_AND_UDP)
            {
                validationErrors.Add("UDP synchronization requires TCP_AND_UDP.");
            }
        }

        /// <summary>
        /// Validates primary controller uniqueness and settings assignment.
        /// </summary>
        private void ValidateScene()
        {
            NetSquareController[] controllers = FindSceneObjects<NetSquareController>();
            if (controllers.Length == 0)
                validationWarnings.Add("The active scene has no NetSquareController.");
            if (controllers.Length > 1)
                validationErrors.Add("The active scene contains multiple NetSquareController objects.");
            if (controllers.Length == 1 && controllers[0].Settings == null)
                validationErrors.Add("The scene NetSquareController has no settings asset.");
        }

        /// <summary>
        /// Draws current validation errors and warnings.
        /// </summary>
        private void DrawValidation()
        {
            if (validationErrors.Count == 0 && validationWarnings.Count == 0)
            {
                EditorGUILayout.HelpBox("Package, settings and scene are valid.", MessageType.Info);
                return;
            }

            for (int i = 0; i < validationErrors.Count; i++)
                EditorGUILayout.HelpBox(validationErrors[i], MessageType.Error);
            for (int i = 0; i < validationWarnings.Count; i++)
                EditorGUILayout.HelpBox(validationWarnings[i], MessageType.Warning);
        }

        /// <summary>
        /// Finds all non-persistent objects of one type in loaded scenes.
        /// </summary>
        /// <typeparam name="T">Unity object type.</typeparam>
        /// <returns>Scene objects.</returns>
        private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
        {
            List<T> results = new List<T>();
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && !EditorUtility.IsPersistent(objects[i]))
                    results.Add(objects[i]);
            }

            return results.ToArray();
        }
        #endregion

        #region Handshake test
        /// <summary>
        /// Performs a complete typed NetSquare handshake with the configured Server.
        /// </summary>
        private async void TestConnection()
        {
            if (settings == null || connectionTestRunning)
                return;

            connectionTestRunning = true;
            connectionResult = "Connecting...";
            Repaint();

            NetSquareClient client = null;
            CancellationTokenSource cancellation = null;
            try
            {
                NetSquareClientConfiguration configuration = settings.CreateClientConfiguration();
                configuration.ConnectionTimeoutMilliseconds = Math.Min(
                    configuration.ConnectionTimeoutMilliseconds,
                    HandshakeTimeoutMilliseconds);
                client = new NetSquareClient(configuration, false);
                cancellation = new CancellationTokenSource(HandshakeTimeoutMilliseconds);
                ConnectionResult result = await client.ConnectAsync(cancellation.Token);
                connectionResult = FormatConnectionResult(result);
            }
            catch (Exception exception)
            {
                connectionResult = "Failed: " + exception.Message;
            }
            finally
            {
                client?.Disconnect();
                cancellation?.Dispose();
                connectionTestRunning = false;
                RefreshValidation();
            }
        }

        /// <summary>
        /// Formats one typed connection result for the Editor.
        /// </summary>
        /// <param name="result">Connection result.</param>
        /// <returns>Readable result.</returns>
        private static string FormatConnectionResult(ConnectionResult result)
        {
            if (result == null)
                return "Failed: no result.";
            if (result.IsConnected)
                return "Compatible. Connected as Client " + result.ClientID + ".";
            if (result.RejectionInfo != null)
            {
                return "Rejected: " + result.RejectionInfo.Reason +
                       (string.IsNullOrEmpty(result.RejectionInfo.Message)
                           ? string.Empty
                           : " / " + result.RejectionInfo.Message);
            }
            if (result.Exception != null)
                return result.Status + ": " + result.Exception.Message;
            return result.Status.ToString();
        }
        #endregion
    }
}
