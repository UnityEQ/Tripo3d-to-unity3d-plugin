using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tripo3D.Editor
{
    public class TripoStudioWindow : EditorWindow
    {
        enum Page
        {
            Image,
            Text,
            Multiview,
            Blender,
            Jobs,
            Settings
        }

        Page _page = Page.Image;
        string _imagePath;
        byte[] _imageBytes;
        Texture2D _imagePreview;
        Texture2D[] _multiviewTextures = new Texture2D[4];
        string[] _multiviewUrls = new string[4];
        string _balanceText = "Credits: —";
        string _prompt = string.Empty;

        VisualElement _root;
        Label _balanceLabel;
        Label _statusLabel;
        ProgressBar _progress;
        Image _imageView;
        Label _fileLabel;
        Label _warningLabel;
        Button _generateButton;
        VisualElement _imagePage;
        VisualElement _textPage;
        VisualElement _multiviewPage;
        VisualElement _blenderPage;
        VisualElement _jobsPage;
        VisualElement _settingsPage;
        Label _blenderStatus;
        Toggle _autoRigToggle;
        Toggle _ikToggle;
        TextField _blenderPathField;
        VisualElement _jobsList;
        Image[] _mvCells = new Image[4];
        Button[] _tabs;

        TextField _apiKeyField;
        PopupField<string> _modelField;
        IntegerField _faceLimitField;
        Toggle _textureToggle;
        Toggle _pbrToggle;
        PopupField<string> _qualityField;
        Toggle _autoSizeToggle;
        Toggle _autofixToggle;
        Toggle _placeToggle;
        Toggle _fbxToggle;
        TextField _outputField;
        TextField _promptField;
        TextField _grokPathField;
        TextField _codexPathField;
        PopupField<string> _aiProviderField;
        PopupField<string> _aiProviderFieldSettings;
        Label _aiStatusLabel;
        Label _blenderHint;
        Label _blenderExtra;
        Button _pickGlbButton;
        Button _lastGlbButton;
        VisualElement _propList;
        VisualElement _propCards;
        PopupField<string> _propDetailField;
        Toggle _propMergeToggle;
        Toggle _propBodyToggle;
        TextField _propAddField;
        Button _detectPropsButton;
        Button _extractPropsButton;
        Label _propCountLabel;
        readonly List<TripoPropPart> _props = new List<TripoPropPart>();

        [MenuItem("Tripo 3D/Studio %#t", false, 0)]
        [MenuItem("Window/Tripo 3D/Studio", false, 0)]
        public static void Open()
        {
            var window = GetWindow<TripoStudioWindow>();
            window.titleContent = new GUIContent("Tripo Studio");
            window.minSize = new Vector2(980, 640);
            window.Show();
        }

        void OnEnable()
        {
            TripoJobRunner.Changed += OnJobChanged;
            EditorApplication.delayCall += TripoJobRunner.ReconcileOpenJobs;
        }

        void OnDisable()
        {
            TripoJobRunner.Changed -= OnJobChanged;
        }

        void CreateGUI()
        {
            _root = rootVisualElement;
            _root.AddToClassList("studio");

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(TripoPaths.StyleSheet);
            if (uss != null)
                _root.styleSheets.Add(uss);

            BuildHeader();
            BuildTabs();
            BuildPages();
            ShowPage(Page.Image);
            RefreshJobUi();
            RefreshBalance();
        }

        void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("header");

            var title = new Label("Tripo Studio");
            title.AddToClassList("title");
            header.Add(title);

            var subtitle = new Label("Image to 3D for Unity");
            subtitle.AddToClassList("subtitle");
            header.Add(subtitle);

            var spacer = new VisualElement();
            spacer.AddToClassList("spacer");
            header.Add(spacer);

            var setup = new Button(TripoSetupWindow.Open) { text = "Setup" };
            setup.AddToClassList("ghost-btn");
            setup.tooltip = "Detect and install packages and tools this plugin needs.";
            header.Add(setup);

            _balanceLabel = new Label(_balanceText);
            _balanceLabel.AddToClassList("balance");
            header.Add(_balanceLabel);

            var refresh = new Button(RefreshBalance) { text = "Refresh credits" };
            refresh.AddToClassList("ghost-btn");
            header.Add(refresh);

            _root.Add(header);
        }

        void BuildTabs()
        {
            var tabs = new VisualElement();
            tabs.AddToClassList("tabs");
            _tabs = new[]
            {
                Tab(tabs, "Image to 3D", Page.Image),
                Tab(tabs, "Text to 3D", Page.Text),
                Tab(tabs, "Armor", Page.Multiview),
                Tab(tabs, "Blender Rig", Page.Blender),
                Tab(tabs, "Jobs", Page.Jobs),
                Tab(tabs, "Settings", Page.Settings)
            };
            _root.Add(tabs);
        }

        Button Tab(VisualElement parent, string label, Page page)
        {
            var button = new Button(() => ShowPage(page)) { text = label };
            button.AddToClassList("tab");
            parent.Add(button);
            return button;
        }

        void BuildPages()
        {
            _imagePage = BuildImagePage();
            _textPage = BuildTextPage();
            _multiviewPage = BuildMultiviewPage();
            _blenderPage = BuildBlenderPage();
            _jobsPage = BuildJobsPage();
            _settingsPage = BuildSettingsPage();
            _root.Add(_imagePage);
            _root.Add(_textPage);
            _root.Add(_multiviewPage);
            _root.Add(_blenderPage);
            _root.Add(_jobsPage);
            _root.Add(_settingsPage);
        }

        VisualElement BuildImagePage()
        {
            var page = new VisualElement();
            page.AddToClassList("page");
            var row = new VisualElement();
            row.AddToClassList("row");

            var upload = Card("Image Upload");
            upload.AddToClassList("image-col");
            var hint = new Label("Drop a PNG/JPEG/WebP, or click Add Image. Subject should be clearly visible.");
            hint.AddToClassList("hint");
            upload.Add(hint);

            var drop = new VisualElement();
            drop.AddToClassList("drop-zone");
            drop.RegisterCallback<ClickEvent>(_ => PickImage());
            drop.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            drop.RegisterCallback<DragPerformEvent>(OnDragPerform);

            _imageView = new Image();
            _imageView.scaleMode = ScaleMode.ScaleToFit;
            _imageView.AddToClassList("preview");
            drop.Add(_imageView);
            upload.Add(drop);

            _fileLabel = new Label("No image selected");
            _fileLabel.AddToClassList("file-name");
            upload.Add(_fileLabel);

            var fileBtn = new Button(PickImage) { text = "+ Add Image" };
            fileBtn.AddToClassList("secondary-btn");
            upload.Add(fileBtn);

            var generate = Card("Image to 3D");
            generate.AddToClassList("image-col");
            generate.AddToClassList("image-col--last");
            var generateBody = new ScrollView();
            generateBody.AddToClassList("image-col-scroll");
            generateBody.Add(BuildCommonOptions());

            _generateButton = new Button(GenerateFromImage) { text = "Generate" };
            _generateButton.AddToClassList("generate-btn");
            generateBody.Add(_generateButton);

            _progress = new ProgressBar { title = "Progress", value = 0 };
            _progress.AddToClassList("progress");
            _progress.style.display = DisplayStyle.None;
            generateBody.Add(_progress);

            _statusLabel = new Label(TripoJobRunner.StatusMessage);
            _statusLabel.AddToClassList("status");
            generateBody.Add(_statusLabel);

            _warningLabel = new Label();
            _warningLabel.AddToClassList("warning");
            generateBody.Add(_warningLabel);

            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var select = new Button(SelectLastAsset) { text = "Select Asset" };
            select.AddToClassList("secondary-btn");
            select.style.flexGrow = 1;
            select.style.marginRight = 6;
            var place = new Button(PlaceLastAsset) { text = "Add to Scene" };
            place.AddToClassList("secondary-btn");
            place.style.flexGrow = 1;
            var cancel = new Button(TripoJobRunner.Cancel) { text = "Cancel" };
            cancel.AddToClassList("secondary-btn");
            cancel.style.flexGrow = 1;
            cancel.style.marginLeft = 6;

            actions.Add(select);
            actions.Add(place);
            actions.Add(cancel);
            generateBody.Add(actions);
            generate.Add(generateBody);

            row.Add(upload);
            row.Add(generate);
            page.Add(row);
            return page;
        }

        VisualElement BuildTextPage()
        {
            var page = new VisualElement();
            page.AddToClassList("page");
            var card = Card("Text to 3D");
            var hint = new Label("Describe the object. Tripo returns a GLB into Assets/TripoModels.");
            hint.AddToClassList("hint");
            card.Add(hint);
            _promptField = new TextField("Prompt") { multiline = true, value = _prompt };
            _promptField.style.minHeight = 80;
            _promptField.RegisterValueChangedCallback(evt => _prompt = evt.newValue);
            card.Add(_promptField);
            var generate = new Button(GenerateFromText) { text = "Generate" };
            generate.AddToClassList("generate-btn");
            card.Add(generate);
            page.Add(card);
            return page;
        }

        VisualElement BuildMultiviewPage()
        {
            var page = new VisualElement();
            page.AddToClassList("page");
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            var card = Card("Character Sheet");
            var hint = new Label("Uses the image selected on Image to 3D. Generate the front / left / back / right sheet first, then extract props.");
            hint.AddToClassList("hint");
            card.Add(hint);

            var sheet = new Button(GenerateSheet) { text = "Generate Character Sheet" };
            sheet.AddToClassList("generate-btn");
            card.Add(sheet);

            var grid = new VisualElement();
            grid.AddToClassList("mv-grid");
            for (var i = 0; i < 4; i++)
            {
                _mvCells[i] = new Image();
                _mvCells[i].AddToClassList("mv-cell");
                grid.Add(_mvCells[i]);
            }

            card.Add(grid);
            var model = new Button(GenerateFromMultiview) { text = "Generate 3D from Views" };
            model.AddToClassList("generate-btn");
            card.Add(model);
            scroll.Add(card);

            var props = Card("Props Extraction");
            var propsHint = new Label(
                "After the character sheet (and 3D), detect independent props — helmet, armor plates, weapons — then generate watertight meshes for the ones you select. Semantic split uses the source image when available, then mesh completion fills holes and recenters each pivot.");
            propsHint.AddToClassList("hint");
            props.Add(propsHint);

            var detailChoices = new List<string> { "Minimal", "Standard", "Detailed" };
            _propDetailField = new PopupField<string>("Detail", detailChoices, 1);
            props.Add(_propDetailField);

            _propMergeToggle = new Toggle("Merge symmetrical parts") { value = true };
            _propMergeToggle.RegisterValueChangedCallback(_ => RebuildPropList());
            props.Add(_propMergeToggle);

            _propBodyToggle = new Toggle("Include head & body") { value = false };
            _propBodyToggle.RegisterValueChangedCallback(_ => RebuildPropList());
            props.Add(_propBodyToggle);

            _detectPropsButton = new Button(DetectProps) { text = "Detect props" };
            _detectPropsButton.AddToClassList("generate-btn");
            props.Add(_detectPropsButton);

            _propCountLabel = new Label("No props detected yet.");
            _propCountLabel.AddToClassList("status");
            props.Add(_propCountLabel);

            _propList = new VisualElement();
            props.Add(_propList);

            var addRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            _propAddField = new TextField("Add a part from the image") { value = string.Empty };
            _propAddField.style.flexGrow = 1;
            addRow.Add(_propAddField);
            var addBtn = new Button(AddCustomProp) { text = "Add" };
            addBtn.AddToClassList("secondary-btn");
            addRow.Add(addBtn);
            props.Add(addRow);

            _extractPropsButton = new Button(ExtractSelectedProps) { text = "Generate selected props" };
            _extractPropsButton.AddToClassList("generate-btn");
            props.Add(_extractPropsButton);

            _propCards = new VisualElement();
            _propCards.AddToClassList("prop-grid");
            props.Add(_propCards);

            scroll.Add(props);
            page.Add(scroll);
            return page;
        }

        VisualElement BuildBlenderPage()
        {
            var page = new VisualElement();
            page.AddToClassList("page");
            var card = Card("Headless Blender — biped_humanoid_v1");
            _aiProviderField = MakeProviderField();
            card.Add(_aiProviderField);

            _blenderHint = new Label(BlenderHintText());
            _blenderHint.AddToClassList("hint");
            card.Add(_blenderHint);

            _aiStatusLabel = new Label(TripoAiDispatcher.DescribeEnvironment());
            _aiStatusLabel.AddToClassList("status");
            card.Add(_aiStatusLabel);
            var refreshAi = new Button(ApplyProviderLabels) { text = "Refresh AI session status" };
            refreshAi.AddToClassList("ghost-btn");
            card.Add(refreshAi);

            _blenderStatus = new Label("Last GLB: " + (string.IsNullOrEmpty(TripoJobRunner.LastGlbPath) ? "(none)" : TripoJobRunner.LastGlbPath));
            _blenderStatus.AddToClassList("status");
            card.Add(_blenderStatus);

            _ikToggle = new Toggle("Build IK controls (FK default)") { value = TripoSettings.BlenderIk };
            _ikToggle.RegisterValueChangedCallback(evt => TripoSettings.BlenderIk = evt.newValue);
            card.Add(_ikToggle);

            var pickGlb = new Button(() =>
            {
                var path = EditorUtility.OpenFilePanel("Select GLB to rig", Application.dataPath, "glb");
                if (!string.IsNullOrEmpty(path))
                    SendGlbToRig(path);
            }) { text = "Send selected GLB to " + TripoSettings.AiProviderLabel + "..." };
            pickGlb.AddToClassList("generate-btn");
            _pickGlbButton = pickGlb;
            card.Add(pickGlb);

            _lastGlbButton = new Button(RigLastGlb) { text = "Send last generated GLB to " + TripoSettings.AiProviderLabel };
            _lastGlbButton.AddToClassList("generate-btn");
            card.Add(_lastGlbButton);

            var inventory = new Button(() =>
            {
                if (string.IsNullOrEmpty(TripoJobRunner.LastBlendPath))
                {
                    EditorUtility.DisplayDialog("Tripo Studio", "No rigged .blend yet. Run the rig first.", "OK");
                    return;
                }

                var blend = TripoJobRunner.ToDisk(TripoJobRunner.LastBlendPath);
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var log = TripoBlenderRunner.RunAgent(blend, "inventory", null, CancellationToken.None);
                        UnityEngine.Debug.Log("[Tripo3D] Blender inventory\n" + log);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("[Tripo3D] " + ex.Message);
                    }
                });
            }) { text = "Inventory last .blend (agent)" };
            inventory.AddToClassList("secondary-btn");
            card.Add(inventory);

            _blenderExtra = new Label(BlenderExtraText());
            _blenderExtra.AddToClassList("hint");
            card.Add(_blenderExtra);
            page.Add(card);
            return page;
        }

        VisualElement BuildJobsPage()
        {
            var page = new VisualElement();
            page.AddToClassList("page");
            var card = Card("Recent Jobs");
            var hint = new Label("Each job links to the imported Unity model (FBX if the rig exists, otherwise GLB). Status refreshes here: credit errors, AI rig queues, and stuck jobs get updated.");
            hint.AddToClassList("hint");
            card.Add(hint);
            var actions = new VisualElement();
            actions.AddToClassList("btn-row");
            var refreshJobs = new Button(() =>
            {
                TripoJobRunner.ReconcileOpenJobs();
                RebuildJobs();
            }) { text = "Refresh job status" };
            refreshJobs.AddToClassList("secondary-btn");
            var clearJobs = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Clear jobs", "Remove all jobs from this list? Imported models stay in the project.", "Clear", "Cancel"))
                    return;
                TripoJobRunner.ClearJobs();
                RebuildJobs();
            }) { text = "Clear jobs" };
            clearJobs.AddToClassList("secondary-btn");
            var spacer = new VisualElement();
            spacer.AddToClassList("btn-row-spacer");
            var cancelJobs = new Button(() =>
            {
                TripoJobRunner.Cancel();
                RebuildJobs();
            }) { text = "Cancel running jobs" };
            cancelJobs.AddToClassList("secondary-btn");
            actions.Add(refreshJobs);
            actions.Add(clearJobs);
            actions.Add(spacer);
            actions.Add(cancelJobs);
            card.Add(actions);
            _jobsList = new VisualElement();
            card.Add(_jobsList);
            page.Add(card);
            return page;
        }

        VisualElement BuildSettingsPage()
        {
            var page = new VisualElement();
            page.AddToClassList("page");
            var setupCard = Card("Project setup");
            var setupHint = new Label("If you copied this plugin into a new Unity project, scan and install missing registry packages (glTFast, URP) from here. Blender and API keys are detected, not downloaded.");
            setupHint.AddToClassList("hint");
            setupCard.Add(setupHint);
            var setupBtn = new Button(TripoSetupWindow.Open) { text = "Setup and install packages" };
            setupBtn.AddToClassList("generate-btn");
            setupCard.Add(setupBtn);
            page.Add(setupCard);

            var card = Card("Settings");

            _outputField = new TextField("Output folder") { value = TripoSettings.OutputFolder };
            _outputField.RegisterValueChangedCallback(evt => TripoSettings.OutputFolder = evt.newValue);
            card.Add(_outputField);

            _aiProviderFieldSettings = MakeProviderField();
            card.Add(_aiProviderFieldSettings);

            var aiHint = new Label("Grok uses the Grok Build CLI / open TUI session. ChatGPT uses Codex (ChatGPT desktop or CLI) and queues into your current open session when one is running.");
            aiHint.AddToClassList("hint");
            card.Add(aiHint);

            _grokPathField = new TextField("Grok.exe (optional)") { value = string.IsNullOrEmpty(TripoSettings.GrokPath) ? (TripoAiDispatcher.FindGrok() ?? string.Empty) : TripoSettings.GrokPath };
            _grokPathField.RegisterValueChangedCallback(evt => TripoSettings.GrokPath = evt.newValue);
            card.Add(_grokPathField);

            var browseGrok = new Button(() =>
            {
                var start = string.IsNullOrEmpty(TripoSettings.GrokPath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grok", "bin")
                    : Path.GetDirectoryName(TripoSettings.GrokPath);
                var path = EditorUtility.OpenFilePanel("Grok executable", start ?? "", "exe");
                if (!string.IsNullOrEmpty(path))
                {
                    TripoSettings.GrokPath = path;
                    _grokPathField.value = path;
                    ApplyProviderLabels();
                }
            }) { text = "Browse Grok.exe" };
            browseGrok.AddToClassList("secondary-btn");
            card.Add(browseGrok);

            _codexPathField = new TextField("Codex.exe (ChatGPT, optional)") { value = string.IsNullOrEmpty(TripoSettings.CodexPath) ? (TripoAiDispatcher.FindCodex() ?? string.Empty) : TripoSettings.CodexPath };
            _codexPathField.RegisterValueChangedCallback(evt => TripoSettings.CodexPath = evt.newValue);
            card.Add(_codexPathField);

            var browseCodex = new Button(() =>
            {
                var start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
                var path = EditorUtility.OpenFilePanel("Codex executable (ChatGPT)", Directory.Exists(start) ? start : "", "exe");
                if (!string.IsNullOrEmpty(path))
                {
                    TripoSettings.CodexPath = path;
                    _codexPathField.value = path;
                    ApplyProviderLabels();
                }
            }) { text = "Browse Codex.exe" };
            browseCodex.AddToClassList("secondary-btn");
            card.Add(browseCodex);

            _apiKeyField = new TextField("Tripo3D API Key") { isPasswordField = true, value = TripoSettings.ApiKey };
            card.Add(_apiKeyField);

            var keyPath = Path.GetFullPath(TripoPaths.SettingsFile);
            var keyHint = new Label("Save writes the key to this machine only (never under Assets/, gitignored).");
            keyHint.AddToClassList("hint");
            card.Add(keyHint);
            var prefsHint = new Label("Unity EditorPrefs key: Tripo3D.ApiKey");
            prefsHint.AddToClassList("hint");
            card.Add(prefsHint);
            var pathField = new TextField("Settings file") { value = keyPath, isReadOnly = true };
            card.Add(pathField);
            var envHint = new Label("You can also set the TRIPO_API_KEY environment variable.");
            envHint.AddToClassList("hint");
            card.Add(envHint);

            var keySaved = new Label();
            keySaved.AddToClassList("hint");

            var save = new Button(() =>
            {
                TripoSettings.ApiKey = (_apiKeyField.value ?? string.Empty).Trim();
                TripoSettings.SaveApiKeyToDisk();
                keySaved.text = "Saved to EditorPrefs (Tripo3D.ApiKey) and " + keyPath;
                RefreshBalance();
            }) { text = "Save Tripo3D API Key" };
            save.AddToClassList("generate-btn");
            card.Add(save);
            card.Add(keySaved);

            _blenderPathField = new TextField("Blender.exe") { value = string.IsNullOrEmpty(TripoSettings.BlenderPath) ? TripoBlenderRunner.FindBlender() : TripoSettings.BlenderPath };
            _blenderPathField.RegisterValueChangedCallback(evt => TripoSettings.BlenderPath = evt.newValue);
            card.Add(_blenderPathField);

            var browse = new Button(() =>
            {
                var path = EditorUtility.OpenFilePanel("Blender executable", "C:\\Program Files\\Blender Foundation", "exe");
                if (!string.IsNullOrEmpty(path))
                {
                    TripoSettings.BlenderPath = path;
                    _blenderPathField.value = path;
                }
            }) { text = "Browse Blender.exe" };
            browse.AddToClassList("secondary-btn");
            card.Add(browse);
            page.Add(card);
            return page;
        }

        VisualElement BuildCommonOptions()
        {
            var box = new VisualElement();
            var fold = new Foldout { text = "Parameters", value = true };
            var models = new System.Collections.Generic.List<string>(TripoSettings.Models);
            var modelIndex = models.IndexOf(TripoSettings.Model);
            if (modelIndex < 0)
                modelIndex = 0;
            _modelField = new PopupField<string>("Model", models, modelIndex);
            _modelField.RegisterValueChangedCallback(evt => TripoSettings.Model = evt.newValue);
            fold.Add(_modelField);

            _faceLimitField = new IntegerField("Face limit") { value = TripoSettings.FaceLimit };
            _faceLimitField.RegisterValueChangedCallback(evt => TripoSettings.FaceLimit = evt.newValue);
            fold.Add(_faceLimitField);

            var qualities = new System.Collections.Generic.List<string>(TripoSettings.TextureQualities);
            var qualityIndex = qualities.IndexOf(TripoSettings.TextureQuality);
            if (qualityIndex < 0)
                qualityIndex = 0;
            _qualityField = new PopupField<string>("Texture quality", qualities, qualityIndex);
            _qualityField.RegisterValueChangedCallback(evt => TripoSettings.TextureQuality = evt.newValue);
            fold.Add(_qualityField);

            _textureToggle = new Toggle("Texture") { value = TripoSettings.Texture };
            _textureToggle.RegisterValueChangedCallback(evt => TripoSettings.Texture = evt.newValue);
            fold.Add(_textureToggle);

            _pbrToggle = new Toggle("PBR maps") { value = TripoSettings.Pbr };
            _pbrToggle.RegisterValueChangedCallback(evt => TripoSettings.Pbr = evt.newValue);
            fold.Add(_pbrToggle);

            _autoSizeToggle = new Toggle("Real-world size (meters)") { value = TripoSettings.AutoSize };
            _autoSizeToggle.RegisterValueChangedCallback(evt => TripoSettings.AutoSize = evt.newValue);
            fold.Add(_autoSizeToggle);

            _autofixToggle = new Toggle("Auto-fix input image") { value = TripoSettings.EnableImageAutofix };
            _autofixToggle.RegisterValueChangedCallback(evt => TripoSettings.EnableImageAutofix = evt.newValue);
            fold.Add(_autofixToggle);

            _placeToggle = new Toggle("Place in scene when done") { value = TripoSettings.PlaceInScene };
            _placeToggle.RegisterValueChangedCallback(evt => TripoSettings.PlaceInScene = evt.newValue);
            fold.Add(_placeToggle);

            _fbxToggle = new Toggle("Also convert to FBX (Tripo API)") { value = TripoSettings.ConvertToFbx };
            _fbxToggle.RegisterValueChangedCallback(evt => TripoSettings.ConvertToFbx = evt.newValue);
            fold.Add(_fbxToggle);

            _autoRigToggle = new Toggle("Auto-rig in Blender after generate") { value = TripoSettings.RigInBlender };
            _autoRigToggle.RegisterValueChangedCallback(evt => TripoSettings.RigInBlender = evt.newValue);
            fold.Add(_autoRigToggle);

            box.Add(fold);
            return box;
        }

        PopupField<string> MakeProviderField()
        {
            var names = new System.Collections.Generic.List<string> { "Grok", "ChatGPT" };
            var current = TripoSettings.AiProviderLabel;
            var index = names.IndexOf(current);
            if (index < 0)
                index = 0;
            var field = new PopupField<string>("AI agent", names, index);
            field.RegisterValueChangedCallback(evt =>
            {
                TripoSettings.AiProvider = evt.newValue == "ChatGPT"
                    ? TripoAiProvider.ChatGPT
                    : TripoAiProvider.Grok;
                ApplyProviderLabels();
            });
            return field;
        }

        void ApplyProviderLabels()
        {
            var name = TripoSettings.AiProviderLabel;
            if (_aiProviderField != null && _aiProviderField.value != name)
                _aiProviderField.SetValueWithoutNotify(name);
            if (_aiProviderFieldSettings != null && _aiProviderFieldSettings.value != name)
                _aiProviderFieldSettings.SetValueWithoutNotify(name);
            if (_pickGlbButton != null)
                _pickGlbButton.text = "Send selected GLB to " + name + "...";
            if (_lastGlbButton != null)
                _lastGlbButton.text = "Send last generated GLB to " + name;
            if (_blenderHint != null)
                _blenderHint.text = BlenderHintText();
            if (_blenderExtra != null)
                _blenderExtra.text = BlenderExtraText();
            if (_aiStatusLabel != null)
                _aiStatusLabel.text = TripoAiDispatcher.DescribeEnvironment();
        }

        static string BlenderHintText()
        {
            if (TripoSettings.AiProvider == TripoAiProvider.ChatGPT)
            {
                return "Pick a GLB and Unity sends a job to ChatGPT (Codex). If Codex / ChatGPT desktop already has a session open, Unity queues the prompt into that session. Otherwise it starts Codex CLI. After skeleton and weights the agent must author Idle, Run, Jump, and a one-handed SwordSlash. Specs: Modular Biped Humanoid Rig Standard v1.md, Biped Humanoid Rig v1 - Reference.json, Lychee Model GLB - Blender FBX - Unity.md.";
            }

            return "Pick a GLB and Unity sends a job to Grok. If a Grok session is already open in this project, keep it open — it will see the job. Otherwise Unity continues the last Grok session (or starts a new one). After skeleton and weights the agent must author Idle, Run, Jump, and a one-handed SwordSlash. Specs: Modular Biped Humanoid Rig Standard v1.md, Biped Humanoid Rig v1 - Reference.json, Lychee Model GLB - Blender FBX - Unity.md.";
        }

        static string BlenderExtraText()
        {
            return "Jobs are written to Temp/tripo-ai-jobs/. " + TripoSettings.AiProviderLabel
                + " is told to open Modular Biped Humanoid Rig Standard v1.md and Lychee Model GLB - Blender FBX - Unity.md first, then rig, animate Idle / Run / Jump / SwordSlash, and drive Blender. The prompt is also copied to the clipboard.";
        }

        VisualElement Card(string title)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            var label = new Label(title);
            label.AddToClassList("card-title");
            card.Add(label);
            return card;
        }

        void ShowPage(Page page)
        {
            _page = page;
            _imagePage.style.display = page == Page.Image ? DisplayStyle.Flex : DisplayStyle.None;
            _textPage.style.display = page == Page.Text ? DisplayStyle.Flex : DisplayStyle.None;
            _multiviewPage.style.display = page == Page.Multiview ? DisplayStyle.Flex : DisplayStyle.None;
            _blenderPage.style.display = page == Page.Blender ? DisplayStyle.Flex : DisplayStyle.None;
            _jobsPage.style.display = page == Page.Jobs ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsPage.style.display = page == Page.Settings ? DisplayStyle.Flex : DisplayStyle.None;
            for (var i = 0; i < _tabs.Length; i++)
                _tabs[i].EnableInClassList("tab--active", i == (int)page);
            if (page == Page.Blender || page == Page.Settings)
                ApplyProviderLabels();
            if (page == Page.Jobs)
                TripoJobRunner.ReconcileOpenJobs();
        }

        void PickImage()
        {
            var path = EditorUtility.OpenFilePanel("Select image for Tripo", "", "png,jpg,jpeg,webp");
            if (!string.IsNullOrEmpty(path))
                LoadImageFromDisk(path);
        }

        void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }
        }

        void OnDragPerform(DragPerformEvent evt)
        {
            DragAndDrop.AcceptDrag();
            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                LoadImageFromDisk(DragAndDrop.paths[0]);
            evt.StopPropagation();
        }

        void LoadImageFromDisk(string path)
        {
            if (!File.Exists(path))
                return;
            _imagePath = path;
            _imageBytes = File.ReadAllBytes(path);
            ApplyPreview(_imageBytes, Path.GetFileName(path));
        }

        void ApplyPreview(byte[] bytes, string fileName)
        {
            if (_imagePreview != null)
                DestroyImmediate(_imagePreview);
            _imagePreview = new Texture2D(2, 2);
            _imagePreview.LoadImage(bytes);
            _imageView.image = _imagePreview;
            _fileLabel.text = fileName + "  " + _imagePreview.width + "x" + _imagePreview.height;
        }

        void GenerateFromImage()
        {
            if (_imageBytes == null || _imageBytes.Length == 0)
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Select an image first.", "OK");
                return;
            }

            var options = ReadOptions();
            options.OutputName = Path.GetFileNameWithoutExtension(_imagePath);
            TripoJobRunner.RunImageToModel(_imagePath, _imageBytes, options);
            RefreshJobUi();
        }

        void GenerateFromText()
        {
            var options = ReadOptions();
            options.Prompt = _promptField != null ? _promptField.value : _prompt;
            if (string.IsNullOrWhiteSpace(options.Prompt))
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Enter a prompt first.", "OK");
                return;
            }

            options.OutputName = options.Prompt;
            TripoJobRunner.RunTextToModel(options);
            RefreshJobUi();
        }

        void GenerateSheet()
        {
            if (_imageBytes == null)
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Select an image on the Image to 3D tab first.", "OK");
                return;
            }

            TripoJobRunner.RunImageToMultiview(_imagePath, _imageBytes, output =>
            {
                LoadMultiview(output);
            });
        }

        async void LoadMultiview(TripoOutput output)
        {
            if (output == null)
                return;
            _multiviewUrls[0] = output.front_view_url;
            _multiviewUrls[1] = output.left_view_url;
            _multiviewUrls[2] = output.back_view_url;
            _multiviewUrls[3] = output.right_view_url;
            for (var i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(_multiviewUrls[i]))
                    continue;
                try
                {
                    var bytes = await TripoApiClient.DownloadAsync(_multiviewUrls[i], CancellationToken.None);
                    var tex = new Texture2D(2, 2);
                    tex.LoadImage(bytes);
                    _multiviewTextures[i] = tex;
                    var index = i;
                    EditorApplication.delayCall += () =>
                    {
                        if (_mvCells[index] != null)
                            _mvCells[index].image = tex;
                    };
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Tripo3D] Could not load view " + i + ": " + ex.Message);
                }
            }
        }

        void GenerateFromMultiview()
        {
            if (string.IsNullOrEmpty(_multiviewUrls[0]))
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Generate a character sheet first.", "OK");
                return;
            }

            TripoJobRunner.RunMultiviewToModel(_multiviewUrls[0], _multiviewUrls[1], _multiviewUrls[2], _multiviewUrls[3], ReadOptions());
        }

        void DetectProps()
        {
            if (string.IsNullOrEmpty(_multiviewUrls[0]) && string.IsNullOrEmpty(TripoJobRunner.LastModelTaskId) && string.IsNullOrEmpty(TripoJobRunner.LastGlbPath))
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Generate a character sheet first (then 3D if you have not already).", "OK");
                return;
            }

            var granularity = PropGranularity();
            var split = _propMergeToggle == null || !_propMergeToggle.value;
            TripoJobRunner.RunDetectProps(_multiviewUrls, _imagePath, _imageBytes, granularity, split, (names, preview) =>
            {
                ApplyDetectedProps(names, preview);
            });
            RefreshJobUi();
        }

        void ApplyDetectedProps(string[] names, Texture2D preview)
        {
            _props.Clear();
            if (names != null)
            {
                for (var i = 0; i < names.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(names[i]))
                        continue;
                    _props.Add(new TripoPropPart
                    {
                        name = names[i],
                        sourceNames = new[] { names[i] },
                        selected = !IsBodyPart(names[i]),
                        preview = preview
                    });
                }
            }

            RebuildPropList();
            RebuildPropCards();
        }

        void AddCustomProp()
        {
            var name = _propAddField != null ? (_propAddField.value ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Type a part name (for example helmet or sword).", "OK");
                return;
            }

            _props.Add(new TripoPropPart
            {
                name = name,
                sourceNames = new[] { name },
                selected = true
            });
            if (_propAddField != null)
                _propAddField.value = string.Empty;
            RebuildPropList();
        }

        void ExtractSelectedProps()
        {
            var selected = VisibleSelectedPartNames();
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Detect props and select at least one.", "OK");
                return;
            }

            var slug = Path.GetFileNameWithoutExtension(_imagePath);
            if (string.IsNullOrEmpty(slug))
                slug = "props";
            TripoJobRunner.RunExtractProps(selected, slug, parts =>
            {
                if (parts == null)
                    return;
                for (var i = 0; i < parts.Length; i++)
                {
                    var got = parts[i];
                    for (var j = 0; j < _props.Count; j++)
                    {
                        if (string.Equals(_props[j].name, got.name, StringComparison.OrdinalIgnoreCase))
                        {
                            _props[j].assetPath = got.assetPath;
                            if (got.preview != null)
                                _props[j].preview = got.preview;
                        }
                    }

                    var exists = false;
                    for (var j = 0; j < _props.Count; j++)
                    {
                        if (string.Equals(_props[j].name, got.name, StringComparison.OrdinalIgnoreCase))
                            exists = true;
                    }

                    if (!exists)
                        _props.Add(got);
                }

                RebuildPropList();
                RebuildPropCards();
            });
            RefreshJobUi();
        }

        void RebuildPropList()
        {
            if (_propList == null)
                return;
            _propList.Clear();
            var visible = VisiblePropGroups();
            if (_propCountLabel != null)
            {
                _propCountLabel.text = visible.Count == 0
                    ? "No props detected yet."
                    : visible.Count + " part(s). Select the ones to extract.";
            }

            if (_extractPropsButton != null)
            {
                var n = 0;
                for (var i = 0; i < visible.Count; i++)
                {
                    if (visible[i].selected)
                        n++;
                }

                _extractPropsButton.text = n <= 0 ? "Generate selected props" : "Generate " + n + " selected prop" + (n == 1 ? "" : "s");
            }

            for (var i = 0; i < visible.Count; i++)
            {
                var part = visible[i];
                var row = new VisualElement();
                row.AddToClassList("prop-row");
                var toggle = new Toggle { value = part.selected, text = part.name };
                toggle.style.flexGrow = 1;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    part.selected = evt.newValue;
                    SetSourceSelection(part, evt.newValue);
                    RebuildPropList();
                });
                row.Add(toggle);
                _propList.Add(row);
            }
        }

        void RebuildPropCards()
        {
            if (_propCards == null)
                return;
            _propCards.Clear();
            for (var i = 0; i < _props.Count; i++)
            {
                var part = _props[i];
                if (string.IsNullOrEmpty(part.assetPath) && part.preview == null)
                    continue;
                var card = new VisualElement();
                card.AddToClassList("prop-card");
                var title = new Label(part.name);
                title.AddToClassList("card-title");
                card.Add(title);
                if (part.preview != null)
                {
                    var img = new Image { image = part.preview };
                    img.AddToClassList("prop-preview");
                    card.Add(img);
                }

                if (!string.IsNullOrEmpty(part.assetPath))
                {
                    var path = new Label(part.assetPath);
                    path.AddToClassList("job-path");
                    card.Add(path);
                    var pingPath = part.assetPath;
                    var ping = new Button(() => PingJobAsset(pingPath)) { text = "Select" };
                    ping.AddToClassList("ghost-btn");
                    card.Add(ping);
                }

                _propCards.Add(card);
            }
        }

        List<TripoPropPart> VisiblePropGroups()
        {
            var includeBody = _propBodyToggle != null && _propBodyToggle.value;
            var merge = _propMergeToggle != null && _propMergeToggle.value;
            var groups = new List<TripoPropPart>();
            for (var i = 0; i < _props.Count; i++)
            {
                var part = _props[i];
                if (!includeBody && IsBodyPart(part.name))
                    continue;
                if (!merge)
                {
                    groups.Add(part);
                    continue;
                }

                var key = CanonicalPropName(part.name);
                TripoPropPart group = null;
                for (var g = 0; g < groups.Count; g++)
                {
                    if (CanonicalPropName(groups[g].name) == key)
                    {
                        group = groups[g];
                        break;
                    }
                }

                if (group == null)
                {
                    groups.Add(new TripoPropPart
                    {
                        name = string.IsNullOrEmpty(key) ? part.name : key,
                        sourceNames = new[] { part.name },
                        selected = part.selected,
                        assetPath = part.assetPath,
                        preview = part.preview
                    });
                }
                else
                {
                    var merged = new List<string>(group.sourceNames ?? new[] { group.name });
                    var already = false;
                    for (var s = 0; s < merged.Count; s++)
                    {
                        if (string.Equals(merged[s], part.name, StringComparison.OrdinalIgnoreCase))
                            already = true;
                    }

                    if (!already)
                        merged.Add(part.name);
                    group.sourceNames = merged.ToArray();
                    if (part.selected)
                        group.selected = true;
                    if (string.IsNullOrEmpty(group.assetPath))
                        group.assetPath = part.assetPath;
                }
            }

            return groups;
        }

        string[] VisibleSelectedPartNames()
        {
            var names = new List<string>();
            var groups = VisiblePropGroups();
            for (var i = 0; i < groups.Count; i++)
            {
                if (!groups[i].selected)
                    continue;
                var sources = groups[i].sourceNames;
                if (sources == null || sources.Length == 0)
                    names.Add(groups[i].name);
                else
                {
                    for (var s = 0; s < sources.Length; s++)
                        names.Add(sources[s]);
                }
            }

            return names.ToArray();
        }

        void SetSourceSelection(TripoPropPart group, bool selected)
        {
            if (group == null)
                return;
            group.selected = selected;
            var sources = group.sourceNames;
            if (sources == null)
                return;
            for (var i = 0; i < _props.Count; i++)
            {
                for (var s = 0; s < sources.Length; s++)
                {
                    if (string.Equals(_props[i].name, sources[s], StringComparison.OrdinalIgnoreCase))
                        _props[i].selected = selected;
                }
            }
        }

        string PropGranularity()
        {
            var label = _propDetailField != null ? _propDetailField.value : "Standard";
            if (label == "Minimal")
                return "simple";
            if (label == "Detailed")
                return "detailed";
            return "balanced";
        }

        static bool IsBodyPart(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            var n = name.ToLowerInvariant();
            return n.Contains("head") || n.Contains("body") || n.Contains("torso") || n.Contains("neck")
                   || n.Contains("face") || n.Contains("skull") || n == "skin" || n == "character";
        }

        static string CanonicalPropName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            var n = name.ToLowerInvariant();
            n = n.Replace("left ", "").Replace("right ", "").Replace("left_", "").Replace("right_", "");
            n = n.Replace(".l", "").Replace(".r", "").Replace("_l", "").Replace("_r", "");
            return n.Trim();
        }

        TripoGenerateOptions ReadOptions()
        {
            var options = TripoSettings.CurrentOptions();
            if (_modelField != null) options.Model = _modelField.value;
            if (_faceLimitField != null) options.FaceLimit = _faceLimitField.value;
            if (_textureToggle != null) options.Texture = _textureToggle.value;
            if (_pbrToggle != null) options.Pbr = _pbrToggle.value;
            if (_qualityField != null) options.TextureQuality = _qualityField.value;
            if (_autoSizeToggle != null) options.AutoSize = _autoSizeToggle.value;
            if (_autofixToggle != null) options.EnableImageAutofix = _autofixToggle.value;
            if (_placeToggle != null) options.PlaceInScene = _placeToggle.value;
            if (_fbxToggle != null) options.ConvertToFbx = _fbxToggle.value;
            if (_autoRigToggle != null) options.RigInBlender = _autoRigToggle.value;
            // P2 quad output is FBX (glTF has no quads). Import writes .fbx from the file magic.
            options.Quad = options.Model == "P2-20260801";
            return options;
        }

        void RigLastGlb()
        {
            if (string.IsNullOrEmpty(TripoJobRunner.LastGlbPath))
            {
                EditorUtility.DisplayDialog("Tripo Studio", "Generate a model first, or pick a GLB on the Blender Rig tab.", "OK");
                return;
            }

            SendGlbToRig(TripoJobRunner.LastGlbPath);
        }

        void SendGlbToRig(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            TripoJobRunner.RunBlenderRig(path);
            ShowPage(Page.Jobs);
            RefreshJobUi();
        }

        void SelectLastAsset()
        {
            var path = TripoJobRunner.ResolveJobAssetPath(TripoJobRunner.LastAssetPath)
                       ?? TripoJobRunner.ResolveJobAssetPath(TripoJobRunner.LastGlbPath);
            if (string.IsNullOrEmpty(path))
                return;
            PingJobAsset(path);
        }

        void PlaceLastAsset()
        {
            var path = TripoJobRunner.ResolveJobAssetPath(TripoJobRunner.LastAssetPath)
                       ?? TripoJobRunner.ResolveJobAssetPath(TripoJobRunner.LastGlbPath);
            if (string.IsNullOrEmpty(path))
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return;
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = Instantiate(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Tripo Model");
            Selection.activeGameObject = instance;
        }

        void OnJobChanged()
        {
            RefreshJobUi();
        }

        void RefreshJobUi()
        {
            if (_statusLabel != null)
                _statusLabel.text = TripoJobRunner.StatusMessage;
            if (_progress != null)
            {
                _progress.value = TripoJobRunner.Progress;
                _progress.style.display = TripoJobRunner.IsBusy || TripoJobRunner.Progress > 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            if (_generateButton != null)
                _generateButton.SetEnabled(!TripoJobRunner.IsBusy);
            if (_blenderStatus != null)
            {
                _blenderStatus.text = "Last GLB: " + (string.IsNullOrEmpty(TripoJobRunner.LastGlbPath) ? "(none)" : TripoJobRunner.LastGlbPath)
                    + "\nLast FBX: " + (string.IsNullOrEmpty(TripoJobRunner.LastAssetPath) ? "(none)" : TripoJobRunner.LastAssetPath)
                    + "\nLast .blend: " + (string.IsNullOrEmpty(TripoJobRunner.LastBlendPath) ? "(none)" : TripoJobRunner.LastBlendPath);
            }
            RebuildJobs();
        }

        void RebuildJobs()
        {
            if (_jobsList == null)
                return;
            _jobsList.Clear();
            var jobs = TripoSession.instance.jobs;
            if (jobs == null || jobs.Count == 0)
            {
                var empty = new Label("No jobs yet.");
                empty.AddToClassList("hint");
                _jobsList.Add(empty);
                return;
            }

            var persisted = false;
            for (var i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                var resolved = TripoJobRunner.ResolveJobAssetPath(job.assetPath, job.name);
                if (!string.IsNullOrEmpty(resolved) && resolved != job.assetPath)
                {
                    job.assetPath = resolved;
                    persisted = true;
                }

                var row = new VisualElement();
                row.AddToClassList("job-row");

                var top = new VisualElement();
                top.AddToClassList("job-top");
                var name = new Label(job.name);
                name.AddToClassList("job-name");
                var metaBits = JobStatusLabel(job) + "  " + job.progress + "%  " + KindLabel(job.kind);
                if (!string.IsNullOrEmpty(job.model))
                    metaBits += "  " + job.model;
                var meta = new Label(metaBits);
                meta.AddToClassList("job-meta");
                top.Add(name);
                top.Add(meta);
                row.Add(top);
                if (!string.IsNullOrEmpty(job.message) && job.message != job.error)
                {
                    var msg = new Label(job.message);
                    msg.AddToClassList("status");
                    row.Add(msg);
                }

                var path = !string.IsNullOrEmpty(resolved) ? resolved : job.assetPath;
                var pathLabel = new Label(string.IsNullOrEmpty(path) ? "No model in project" : path);
                pathLabel.AddToClassList("job-path");
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                    pathLabel.AddToClassList("job-path--missing");
                row.Add(pathLabel);
                if (!string.IsNullOrEmpty(job.error))
                {
                    var err = new Label(job.error);
                    err.AddToClassList("warning");
                    row.Add(err);
                }

                var rowActions = new VisualElement();
                rowActions.AddToClassList("btn-row");
                if (!string.IsNullOrEmpty(path))
                {
                    var selectPath = path;
                    var ping = new Button(() => PingJobAsset(selectPath)) { text = "Select" };
                    ping.AddToClassList("ghost-btn");
                    rowActions.Add(ping);
                }

                if (job.status != "Success" && job.status != "Failed" && job.status != "Cancelled")
                {
                    var target = job;
                    var cancel = new Button(() =>
                    {
                        TripoJobRunner.CancelJob(target);
                        RebuildJobs();
                    }) { text = "Cancel" };
                    cancel.AddToClassList("secondary-btn");
                    rowActions.Add(cancel);
                }

                if (rowActions.childCount > 0)
                    row.Add(rowActions);

                _jobsList.Add(row);
            }

            if (persisted)
                TripoSession.instance.Persist();
        }

        static string JobStatusLabel(TripoJobRecord job)
        {
            if (job == null)
                return string.Empty;
            var status = job.status;
            if (job.kind == "BlenderRig")
            {
                if (status == "Failed")
                    return "Failed";
                if (status == "Success")
                    return "Success";
                if (status == "Running" || status == "Rigging")
                    return "Rigging";
                if (status == "Queued" || status == "Creating")
                    return "Queued";
            }

            return string.IsNullOrEmpty(status) ? "Unknown" : status;
        }

        static string KindLabel(string kind)
        {
            if (kind == "ImageToModel")
                return "Image to 3D";
            if (kind == "TextToModel")
                return "Text to 3D";
            if (kind == "ImageToMultiview")
                return "Character sheet";
            if (kind == "MultiviewToModel")
                return "Multiview to 3D";
            if (kind == "BlenderRig")
                return "Blender rig";
            if (kind == "ConvertFbx")
                return "FBX convert";
            if (kind == "PropDetect")
                return "Props detect";
            if (kind == "PropExtract")
                return "Props extract";
            return kind ?? string.Empty;
        }

        static void PingJobAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            var resolved = TripoJobRunner.ResolveJobAssetPath(path) ?? path;
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(resolved);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                return;
            }

            if (File.Exists(path))
                EditorUtility.RevealInFinder(path);
        }

        async void RefreshBalance()
        {
            if (!TripoSettings.HasApiKey)
            {
                _balanceText = "Credits: no API key";
                if (_balanceLabel != null)
                    _balanceLabel.text = _balanceText;
                if (_warningLabel != null)
                    _warningLabel.text = "Add a Tripo3D API key in Settings before generating.";
                return;
            }

            try
            {
                var data = await TripoApiClient.GetBalanceAsync(CancellationToken.None);
                _balanceText = "Credits: " + data.balance.ToString("0.##");
                if (data.frozen > 0)
                    _balanceText += "  (frozen " + data.frozen.ToString("0.##") + ")";
                if (_balanceLabel != null)
                    _balanceLabel.text = _balanceText;
                if (_warningLabel != null)
                {
                    _warningLabel.text = data.balance <= 0
                        ? "Account balance is 0. Top up at platform.tripo3d.ai before generating."
                        : string.Empty;
                }
            }
            catch (Exception ex)
            {
                _balanceText = "Credits: error";
                if (_balanceLabel != null)
                    _balanceLabel.text = _balanceText;
                if (_warningLabel != null)
                    _warningLabel.text = ex.Message;
            }
        }
    }
}
