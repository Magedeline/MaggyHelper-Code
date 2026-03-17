#nullable enable

using MaggyHelper;
using FMOD.Studio;

namespace MaggyHelper.Cutscenes
{
    /// <summary>
    /// Vessel Creation Vignette - Interactive vessel creation cutscene 
    /// Based on Hollow Knight/Undertale inspired vessel creation sequence
    /// </summary>
    public class VesselCreationVignette : Scene
    {
        #region Constants
        private const float FADE_DURATION = 2f;
        private const float TEXT_FADE_SPEED = 3f;
        private const float CHOICE_EASE_SPEED = 4f;
        private const float VESSEL_DISPLAY_TIME = 3f;
        private const float WHITE_FADE_DURATION = 4f;
        
        // Graphics paths
        private const string VESSEL_GRAPHICS_PATH = "bgs/maggy/00/anotherhuman/";
        private const string GONERBODY_BASE  = "IMAGE_GONERBODY";   // 6 variants: 00-05
        private const string GONERHEAD_BASE  = "IMAGE_GONERHEAD";   // 8 variants: 00-07
        private const string GONERLEGS_BASE  = "IMAGE_GONERLEGS";   // 5 variants: 00-04
        private const string DEPTH_SPRITE = "IMAGE_DEPTH";
        private const string SOUL_BLUR_SPRITE = "IMAGE_SOUL_BLUR";
        private const string PINK_SOUL_BLUR_SPRITE = "IMAGE_PINK_SOUL_BLUR";
        
        // Audio events
        private const string CREATION_MUSIC_EVENT = "event:/desolozantas/music/lvl0/creation/create";
        private const string DRONE_MUSIC_EVENT = "event:/desolozantas/music/lvl0/creation/drone";
        private const string HEART_APPEAR_EVENT = "event:/desolozantas/music/lvl0/creation/heart_appear";
        private const string HEART_CHANGE_EVENT = "event:/desolozantas/music/lvl0/creation/heart_change";
        private const string CHOICE_APPEAR_EVENT = "event:/ui/game/chatoptions_appear";
        private const string CHOICE_SELECT_EVENT = "event:/ui/game/chatoptions_select";
        private const string CHOICE_MOVE_EVENT = "event:/ui/game/chatoptions_roll_down";
        #endregion

        #region Vessel Creation Data
        // Part labels are generated dynamically from loaded texture counts
        private string selectedLeg = "";
        private string selectedTorso = "";
        private string selectedHead = "";
        private int selectedLegIndex   = 0;
        private int selectedBodyIndex  = 0;
        private int selectedHeadIndex  = 0;
        private string vesselName = "";
        private string playerFeeling = "";
        private string creatorName = "";
        private bool isHonestAnswer = false;
        #endregion

        #region Fields
        private Session session;
        private string? areaMusic;
        private float backgroundFade = 1f;
        private float textAlpha = 0f;
        private float choiceEase = 0f;
        private bool exiting = false;
        private float pauseFade = 0f;
        private float fade = 0f;
        
        // UI Components
        private TextMenu? pauseMenu;
        private HudRenderer hud;
        private Coroutine? sequenceCoroutine;
        
        // Current state
        private CreationPhase currentPhase = CreationPhase.Introduction;
        private int currentChoiceIndex = 0;
        private List<string> currentChoices = new List<string>();
        
        // Graphics – indexed arrays, one entry per sprite variant
        private MTexture[] gonerBodyTextures = Array.Empty<MTexture>();
        private MTexture[] gonerHeadTextures = Array.Empty<MTexture>();
        private MTexture[] gonerLegTextures  = Array.Empty<MTexture>();
        private MTexture? depthTexture;
        private MTexture? soulBlurTexture;
        private MTexture? pinkSoulBlurTexture;
        private float vesselAlpha = 0f;
        private float soulBlurAlpha = 0f;
        private Vector2 vesselPosition;
        
        // Audio handle for creation music EventInstance
        private EventInstance? creationMusic;
        private EventInstance? droneMusic;
        
        public bool CanPause => pauseMenu == null;
        #endregion

        #region Enums
        private enum CreationPhase
        {
            Introduction,
            LegSelection,
            TorsoSelection, 
            HeadSelection,
            VesselNaming,
            HonestQuestion,
            FeelingsQuestion,
            CreatorNaming,
            VesselDisplay,
            VesselDiscard,
            Transition
        }
        #endregion

        #region Constructor
        public VesselCreationVignette(Session session) : base()
        {
            IngesteLogger.Info("[VesselCreation] Initializing VesselCreationVignette");
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            
            // Store and clear current music
            areaMusic = session.Audio.Music.Event;
            IngesteLogger.Debug($"[VesselCreation] Stored area music: {areaMusic}");
            session.Audio.Music.Event = null;
            session.Audio.Apply(forceSixteenthNoteHack: false);
            
            // Load vessel graphics
            loadVesselGraphics();
            
            // Initialize vessel position (center of screen)
            vesselPosition = new Vector2(Engine.Width / 2f, Engine.Height / 2f);
            IngesteLogger.Debug($"[VesselCreation] Vessel position initialized: {vesselPosition}");
            
            // Initialize UI
            Add(hud = new HudRenderer());
            RendererList.UpdateLists();
            
            // Start creation sequence
            sequenceCoroutine = new Coroutine(vesselCreationSequence());
            IngesteLogger.Info("[VesselCreation] Creation sequence coroutine started");
            
            // Add fade-in effect
            Add(new FadeWipe(this, true));
            IngesteLogger.Debug("[VesselCreation] Fade-in effect added");
        }
        #endregion

        #region Main Sequence
        private IEnumerator vesselCreationSequence()
        {
            IngesteLogger.Info("[VesselCreation] === Starting vessel creation sequence ===");
            
            // Phase 1: Introduction
            yield return introductionPhase();
            
            // Phase 2: Vessel Creation (Legs, Torso, Head)
            yield return legSelectionPhase();
            yield return torsoSelectionPhase();
            yield return headSelectionPhase();
            IngesteLogger.Info($"[VesselCreation] Vessel parts selected - Legs: {selectedLeg}, Torso: {selectedTorso}, Head: {selectedHead}");
            
            // Phase 3: Vessel Naming
            yield return vesselNamingPhase();
            
            // Phase 4: Truth Question
            yield return honestQuestionPhase();
            
            // Phase 5: Feelings Question
            yield return feelingsQuestionPhase();
            
            // Phase 6: Creator Naming
            yield return creatorNamingPhase();
            IngesteLogger.Info($"[VesselCreation] All choices collected - Vessel: '{vesselName}', Creator: '{creatorName}', Honest: {isHonestAnswer}");
            
            // Phase 7: Display Created Vessel
            yield return vesselDisplayPhase();
            
            // Phase 8: Vessel Discard
            yield return vesselDiscardPhase();
            
            // Phase 9: Transition to IntroVignette
            yield return transitionToIntroVignette();
        }
        #endregion

        #region Creation Phases
        private IEnumerator introductionPhase()
        {
            currentPhase = CreationPhase.Introduction;
            IngesteLogger.Info("[VesselCreation] Phase: Introduction");
            
            // Audio sequence: drone -> heart appear -> heart change -> creation music
            try { droneMusic = Audio.Play(DRONE_MUSIC_EVENT); IngesteLogger.Debug("[VesselCreation] Drone music started"); }
            catch (Exception ex) { IngesteLogger.Warn($"[VesselCreation] Failed to start drone music: {ex.Message}"); }
            
            yield return 1f;
            
            try { Audio.Play(HEART_APPEAR_EVENT); IngesteLogger.Debug("[VesselCreation] Heart appear sound played"); }
            catch (Exception ex) { IngesteLogger.Warn($"[VesselCreation] Failed to play heart appear: {ex.Message}"); }
            
            yield return 0.75f;
            
            try { Audio.Play(HEART_CHANGE_EVENT); IngesteLogger.Debug("[VesselCreation] Heart change sound played"); }
            catch (Exception ex) { IngesteLogger.Warn($"[VesselCreation] Failed to play heart change: {ex.Message}"); }
            
            yield return 0.75f;
            
            try { creationMusic = Audio.Play(CREATION_MUSIC_EVENT); IngesteLogger.Debug($"[VesselCreation] Creation music started: {CREATION_MUSIC_EVENT}"); }
            catch (Exception ex) { IngesteLogger.Warn($"[VesselCreation] Failed to start creation music: {ex.Message}"); }
            
            yield return 0.5f;
            
            // Display introduction text
            yield return showText("VESSEL_CREATION_INTRO");
            
            yield return 2f;
        }

        private IEnumerator legSelectionPhase()
        {
            currentPhase = CreationPhase.LegSelection;
            IngesteLogger.Info("[VesselCreation] Phase: Leg Selection");
            
            yield return showText("VESSEL_CREATION_LEG_CHOICE");
            
            // Play heart change sound for each selection phase
            Audio.Play(HEART_CHANGE_EVENT);
            
            yield return showChoiceMenu(buildIndexedLabels("Legs", gonerLegTextures.Length), (choice, idx) =>
            {
                selectedLeg = choice;
                selectedLegIndex = idx;
                IngesteLogger.Debug($"[VesselCreation] Legs selected: {selectedLeg} (index {selectedLegIndex})");
            });
            
            yield return 1f;
        }

        private IEnumerator torsoSelectionPhase()
        {
            currentPhase = CreationPhase.TorsoSelection;
            IngesteLogger.Info("[VesselCreation] Phase: Torso Selection");
            
            yield return showText("VESSEL_CREATION_TORSO_CHOICE");
            
            // Play heart change sound for each selection phase
            Audio.Play(HEART_CHANGE_EVENT);
            
            yield return showChoiceMenu(buildIndexedLabels("Body", gonerBodyTextures.Length), (choice, idx) =>
            {
                selectedTorso = choice;
                selectedBodyIndex = idx;
                IngesteLogger.Debug($"[VesselCreation] Body selected: {selectedTorso} (index {selectedBodyIndex})");
            });
            
            yield return 1f;
        }

        private IEnumerator headSelectionPhase()
        {
            currentPhase = CreationPhase.HeadSelection;
            IngesteLogger.Info("[VesselCreation] Phase: Head Selection");
            
            yield return showText("VESSEL_CREATION_HEAD_CHOICE");
            
            // Play heart change sound for each selection phase
            Audio.Play(HEART_CHANGE_EVENT);
            
            yield return showChoiceMenu(buildIndexedLabels("Head", gonerHeadTextures.Length), (choice, idx) =>
            {
                selectedHead = choice;
                selectedHeadIndex = idx;
                IngesteLogger.Debug($"[VesselCreation] Head selected: {selectedHead} (index {selectedHeadIndex})");
            });
            
            yield return 1f;
        }

        private IEnumerator vesselNamingPhase()
        {
            currentPhase = CreationPhase.VesselNaming;
            IngesteLogger.Info("[VesselCreation] Phase: Vessel Naming");
            
            yield return showText("VESSEL_CREATION_NAME_PROMPT");
            yield return showTextInput("Enter your vessel's name:", (name) => 
            {
                vesselName = name;
                IngesteLogger.Info($"[VesselCreation] Vessel named: '{vesselName}'");
            });
            
            yield return 1f;
        }

        private IEnumerator honestQuestionPhase()
        {
            currentPhase = CreationPhase.HonestQuestion;
            IngesteLogger.Info("[VesselCreation] Phase: Honest Question");
            
            yield return showText("VESSEL_CREATION_HONEST_QUESTION");
            
            string[] honestChoices = { "Yes, I was honest", "No, I wasn't completely honest" };
            yield return showChoiceMenu(honestChoices, (choice) => 
            {
                isHonestAnswer = choice.StartsWith("Yes");
                IngesteLogger.Debug($"[VesselCreation] Honesty choice: '{choice}' (isHonest: {isHonestAnswer})");
            });
            
            // Show response based on honesty
            string responseKey = isHonestAnswer ? "VESSEL_CREATION_HONEST_RESPONSE" : "VESSEL_CREATION_DISHONEST_RESPONSE";
            yield return showText(responseKey);
            
            yield return 2f;
        }

        private IEnumerator feelingsQuestionPhase()
        {
            currentPhase = CreationPhase.FeelingsQuestion;
            IngesteLogger.Info("[VesselCreation] Phase: Feelings Question");
            
            yield return showText("VESSEL_CREATION_FEELINGS_QUESTION");
            yield return showTextInput("How do you feel about this game's nature?", (feeling) => 
            {
                playerFeeling = feeling;
                IngesteLogger.Debug($"[VesselCreation] Player feeling: '{playerFeeling}'");
            });
            
            yield return 1f;
        }

        private IEnumerator creatorNamingPhase()
        {
            currentPhase = CreationPhase.CreatorNaming;
            IngesteLogger.Info("[VesselCreation] Phase: Creator Naming");
            
            yield return showText("VESSEL_CREATION_CREATOR_PROMPT");
            yield return showTextInput("Name yourself as the creator:", (name) => 
            {
                creatorName = name;
                IngesteLogger.Info($"[VesselCreation] Creator named: '{creatorName}'");
            });
            
            yield return 1f;
        }

        private IEnumerator vesselDisplayPhase()
        {
            currentPhase = CreationPhase.VesselDisplay;
            IngesteLogger.Info("[VesselCreation] Phase: Vessel Display");
            
            // Display the created vessel
            yield return showText("VESSEL_CREATION_DISPLAY");
            
            // Play heart appear sound when vessel is revealed
            Audio.Play(HEART_APPEAR_EVENT);
            
            // Fade in the vessel sprite
            float fadeTimer = 0f;
            while (fadeTimer < 2f)
            {
                fadeTimer += Engine.DeltaTime;
                vesselAlpha = Math.Min(1f, fadeTimer / 2f);
                soulBlurAlpha = Math.Min(0.7f, fadeTimer / 2f);
                yield return null;
            }
            
            // Show vessel details
            string vesselDetails = $"Vessel Name: {vesselName}\n" +
                                   $"Legs: {selectedLeg}\n" +
                                   $"Torso: {selectedTorso}\n" +
                                   $"Head: {selectedHead}\n" +
                                   $"Creator: {creatorName}";
            
            IngesteLogger.Info($"[VesselCreation] Displaying vessel details:\n{vesselDetails}");
            yield return showCustomText(vesselDetails);
            yield return VESSEL_DISPLAY_TIME;
        }

        private IEnumerator vesselDiscardPhase()
        {
            currentPhase = CreationPhase.VesselDiscard;
            IngesteLogger.Info("[VesselCreation] Phase: Vessel Discard");
            
            // Show discard message
            yield return showText("VESSEL_CREATION_DISCARD");
            
            // Fade out the vessel sprite
            float fadeTimer = 0f;
            while (fadeTimer < 2f)
            {
                fadeTimer += Engine.DeltaTime;
                vesselAlpha = Math.Max(0f, 1f - (fadeTimer / 2f));
                soulBlurAlpha = Math.Max(0f, 0.7f - (fadeTimer / 2f));
                yield return null;
            }
            
            yield return 0.5f;
            
            // Show choice for fate
            string[] fateChoices = { "Accept the vessel's fate", "Try to save the vessel", "Question the process" };
            string selectedFate = "";
            yield return showChoiceMenu(fateChoices, (choice) => 
            {
                selectedFate = choice;
                IngesteLogger.Info($"[VesselCreation] Fate choice selected: '{selectedFate}'");
            });
            
            // Show response based on choice
            string fateResponseKey = selectedFate switch
            {
                var s when s.Contains("Accept") => "VESSEL_CREATION_FATE_ACCEPT",
                var s when s.Contains("save") => "VESSEL_CREATION_FATE_SAVE",
                var s when s.Contains("Question") => "VESSEL_CREATION_FATE_QUESTION",
                _ => "VESSEL_CREATION_FATE_DEFAULT"
            };
            
            yield return showText(fateResponseKey);
            yield return 2f;
        }

        private IEnumerator transitionToIntroVignette()
        {
            currentPhase = CreationPhase.Transition;
            IngesteLogger.Info("[VesselCreation] Phase: Transition to IntroVignette");
            
            yield return showText("VESSEL_CREATION_TRANSITION");
            yield return 1f;
            
            // Stop creation music and restore area music
            try
            {
                creationMusic?.stop(STOP_MODE.ALLOWFADEOUT);
                droneMusic?.stop(STOP_MODE.ALLOWFADEOUT);
                IngesteLogger.Debug("[VesselCreation] Creation music stopped");
            }
            catch (Exception ex)
            {
                IngesteLogger.Warn($"[VesselCreation] Failed to stop music: {ex.Message}");
            }
            
            // Fade to white
            IngesteLogger.Info("[VesselCreation] Starting white fade transition");
            FadeWipe whiteFade = new FadeWipe(this, false, () =>
            {
                StopSfx();

                // Restore original music
                try
                {
                    if (!string.IsNullOrEmpty(areaMusic))
                    {
                        Audio.SetMusic(areaMusic);
                        IngesteLogger.Debug($"[VesselCreation] Restored area music: {areaMusic}");
                    }
                }
                catch (Exception ex)
                {
                    session.Audio.Music.Event = areaMusic;
                    IngesteLogger.Warn($"[VesselCreation] Failed to restore music via Audio.SetMusic: {ex.Message}");
                }
                
                // Transition to Chapter 0 IntroVignette
                IngesteLogger.Info("[VesselCreation] Transitioning to Cs00IntroVignette");

                // Mark the mod intro as seen so it never replays on subsequent launches.
                if (MaggyHelperModule.SaveData != null)
                {
                    MaggyHelperModule.SaveData.HasSeenModIntro = true;
                    UserIO.SaveHandler(file: true, settings: false);
                }

                Scene nextScene = new Cs00IntroVignette(session);
                logSceneSwitch("transition", nextScene);
                Engine.Scene = nextScene;
            });
            
            whiteFade.Duration = WHITE_FADE_DURATION;
            whiteFade.OnUpdate = (f) =>
            {
                textAlpha = Math.Min(textAlpha, 1f - f);
                backgroundFade = 1f - f; // Fade to white instead of black
            };
            
            exiting = true;
            yield return null;
        }
        #endregion

        #region UI Helper Methods
        private IEnumerator showText(string dialogKey)
        {
            var textbox = new Textbox(dialogKey);
            return showTextbox(textbox);
        }

        private IEnumerator showCustomText(string text)
        {
            var textbox = new Textbox("temp", Dialog.Languages["english"]);
            // This would need custom implementation to show arbitrary text
            // For now, we'll use a placeholder approach
            return showTextbox(textbox);
        }

        private IEnumerator showTextbox(Textbox textbox)
        {
            Engine.Scene.Add(textbox);
            while (textbox.Opened)
            {
                yield return true;
            }
        }

        // Convenience overload – callers that don't need the index
        private IEnumerator showChoiceMenu(string[] choices, Action<string> onSelect)
            => showChoiceMenu(choices, (choice, _) => onSelect(choice));

        private IEnumerator showChoiceMenu(string[] choices, Action<string, int> onSelect)
        {
            currentChoices = new List<string>(choices);
            currentChoiceIndex = 0;
            IngesteLogger.Debug($"[VesselCreation] Showing choice menu with {choices.Length} options: [{string.Join(", ", choices)}]");
            
            // Create choice menu
            Audio.Play(CHOICE_APPEAR_EVENT);
            
            // Animate choices appearing
            while ((choiceEase += Engine.DeltaTime * CHOICE_EASE_SPEED) < 1.0)
                yield return null;
            
            choiceEase = 1f;
            yield return 0.25f;
            
            // Handle input
            while (!Input.MenuConfirm.Pressed)
            {
                if (Input.MenuUp.Pressed && currentChoiceIndex > 0)
                {
                    Audio.Play(CHOICE_MOVE_EVENT);
                    currentChoiceIndex--;
                }
                else if (Input.MenuDown.Pressed && currentChoiceIndex < currentChoices.Count - 1)
                {
                    Audio.Play(CHOICE_MOVE_EVENT);
                    currentChoiceIndex++;
                }
                yield return null;
            }
            
            // Selection made
            Audio.Play(CHOICE_SELECT_EVENT);
            
            // Play heart change on final selection for vessel parts
            if (currentPhase == CreationPhase.LegSelection || 
                currentPhase == CreationPhase.TorsoSelection || 
                currentPhase == CreationPhase.HeadSelection)
            {
                Audio.Play(HEART_CHANGE_EVENT);
            }
            
            // Animate choices disappearing
            while ((choiceEase -= Engine.DeltaTime * CHOICE_EASE_SPEED) > 0.0)
                yield return null;
            
            string selectedChoice = currentChoices[currentChoiceIndex];
            int confirmedIndex = currentChoiceIndex;
            IngesteLogger.Debug($"[VesselCreation] Choice confirmed: '{selectedChoice}' (index: {confirmedIndex})");
            currentChoices.Clear();
            onSelect(selectedChoice, confirmedIndex);
        }

        private IEnumerator showTextInput(string prompt, Action<string> onComplete)
        {
            // This is a simplified text input system
            // In a real implementation, this would need a proper text input dialog
            yield return showCustomText(prompt);
            
            // For now, we'll simulate input with a basic string input system
            // This could be enhanced with a proper text input UI
            string input = "DefaultInput"; // Placeholder - could be enhanced with actual input
            onComplete(input);
            
            yield return 1f;
        }
        #endregion

        #region Graphics Loading
        private void loadVesselGraphics()
        {
            try
            {
                IngesteLogger.Debug($"[VesselCreation] Loading vessel graphics from: {VESSEL_GRAPHICS_PATH}");
                gonerBodyTextures = loadTextureArray(VESSEL_GRAPHICS_PATH + GONERBODY_BASE);
                gonerHeadTextures = loadTextureArray(VESSEL_GRAPHICS_PATH + GONERHEAD_BASE);
                gonerLegTextures  = loadTextureArray(VESSEL_GRAPHICS_PATH + GONERLEGS_BASE);
                depthTexture = GFX.Game.Has(VESSEL_GRAPHICS_PATH + DEPTH_SPRITE) ? GFX.Game[VESSEL_GRAPHICS_PATH + DEPTH_SPRITE] : null;
                soulBlurTexture = GFX.Game.Has(VESSEL_GRAPHICS_PATH + SOUL_BLUR_SPRITE) ? GFX.Game[VESSEL_GRAPHICS_PATH + SOUL_BLUR_SPRITE] : null;
                pinkSoulBlurTexture = GFX.Game.Has(VESSEL_GRAPHICS_PATH + PINK_SOUL_BLUR_SPRITE) ? GFX.Game[VESSEL_GRAPHICS_PATH + PINK_SOUL_BLUR_SPRITE] : null;
                
                IngesteLogger.Info($"[VesselCreation] Graphics loaded - Bodies: {gonerBodyTextures.Length}, Heads: {gonerHeadTextures.Length}, Legs: {gonerLegTextures.Length}, Depth: {depthTexture != null}, SoulBlur: {soulBlurTexture != null}, PinkSoulBlur: {pinkSoulBlurTexture != null}");
            }
            catch (Exception ex)
            {
                IngesteLogger.Error("[VesselCreation] Failed to load vessel graphics", ex);
            }
        }

        /// <summary>Loads all sequentially numbered variants of a sprite (00, 01, ...) until one is missing.</summary>
        private static MTexture[] loadTextureArray(string basePath)
        {
            var list = new List<MTexture>();
            for (int i = 0; i < 99; i++)
            {
                string key = basePath + i.ToString("D2");
                if (GFX.Game.Has(key))
                    list.Add(GFX.Game[key]);
                else
                    break;
            }
            return list.ToArray();
        }

        /// <summary>Builds choice labels like "Legs 0", "Legs 1", ... for the given count.</summary>
        private static string[] buildIndexedLabels(string prefix, int count)
        {
            if (count == 0) return new[] { $"{prefix} 0" };
            var labels = new string[count];
            for (int i = 0; i < count; i++)
                labels[i] = $"{prefix} {i}";
            return labels;
        }
        #endregion

        #region Scene Overrides
        public override void Update()
        {
            if (pauseMenu == null)
            {
                base.Update();
                if (!exiting)
                {
                    // Update the sequence coroutine directly
                    if (sequenceCoroutine != null && !sequenceCoroutine.Finished)
                    {
                        sequenceCoroutine.Update();
                    }
                    
                    if (Input.Pause.Pressed || Input.ESC.Pressed)
                    {
                        OpenPauseMenu();
                    }
                }
            }
            else if (!exiting)
            {
                pauseMenu.Update();
            }
            
            pauseFade = Calc.Approach(pauseFade, (pauseMenu != null) ? 1 : 0, Engine.DeltaTime * 8f);
            hud.BackgroundFade = Calc.Approach(hud.BackgroundFade, (pauseMenu != null) ? 0.6f : 0f, Engine.DeltaTime * 3f);
            fade = Calc.Approach(fade, 0f, Engine.DeltaTime);
        }
        
        public void OpenPauseMenu()
        {
            PauseSfx();
            Audio.Play("event:/ui/game/pause");
            Add(pauseMenu = new TextMenu());
            pauseMenu.Add(new TextMenu.Button(Dialog.Clean("intro_vignette_resume")).Pressed(ClosePauseMenu));
            pauseMenu.Add(new TextMenu.Button(Dialog.Clean("intro_vignette_skip")).Pressed(SkipVignette));
            pauseMenu.OnCancel = pauseMenu.OnESC = pauseMenu.OnPause = ClosePauseMenu;
        }
        
        private void ClosePauseMenu()
        {
            ResumeSfx();
            Audio.Play("event:/ui/game/unpause");
            if (pauseMenu != null)
            {
                pauseMenu.RemoveSelf();
            }
            pauseMenu = null;
        }
        
        private void SkipVignette()
        {
            StopSfx();
            sequenceCoroutine = null;
            session.Audio.Music.Event = areaMusic;
            if (pauseMenu != null)
            {
                pauseMenu.RemoveSelf();
                pauseMenu = null;
            }
            
            FadeWipe fadeWipe = new FadeWipe(this, false, delegate
            {
                // Skip directly to the next scene (Cs00IntroVignette or LevelLoader)
                IngesteLogger.Info("[VesselCreation] Skipping vignette - transitioning to Cs00IntroVignette");

                // Mark the mod intro as seen even when skipped.
                if (MaggyHelperModule.SaveData != null)
                {
                    MaggyHelperModule.SaveData.HasSeenModIntro = true;
                    UserIO.SaveHandler(file: true, settings: false);
                }

                Scene nextScene = new Cs00IntroVignette(session);
                logSceneSwitch("skip", nextScene);
                Engine.Scene = nextScene;
            })
            {
                OnUpdate = delegate(float f)
                {
                    textAlpha = Math.Min(textAlpha, 1f - f);
                }
            };
            exiting = true;
        }
        
        private void PauseSfx()
        {
            foreach (SoundSource sound in Tracker.GetComponents<SoundSource>())
            {
                sound.Pause();
            }
            creationMusic?.setPaused(true);
            droneMusic?.setPaused(true);
        }
        
        private void ResumeSfx()
        {
            foreach (SoundSource sound in Tracker.GetComponents<SoundSource>())
            {
                sound.Resume();
            }
            creationMusic?.setPaused(false);
            droneMusic?.setPaused(false);
        }
        
        private void StopSfx()
        {
            List<Component> components = new List<Component>();
            components.AddRange(Tracker.GetComponents<SoundSource>());
            foreach (SoundSource sound in components)
            {
                sound.RemoveSelf();
            }
            creationMusic?.stop(STOP_MODE.IMMEDIATE);
            droneMusic?.stop(STOP_MODE.IMMEDIATE);
        }

        private void logSceneSwitch(string source, Scene nextScene)
        {
            string currentScene = Engine.Scene?.GetType().Name ?? "null";
            string targetScene = nextScene.GetType().Name;
            IngesteLogger.Debug($"[VesselCreation] Scene switch [{source}] at {DateTime.UtcNow:O}: {currentScene} -> {targetScene}");
        }

        public override void Render()
        {
            // Draw background FIRST, before base.Render()
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RasterizerState.CullNone, null, Engine.ScreenMatrix);
            
            // Render solid background (black for most phases, white for transition)
            Color bgColor = currentPhase == CreationPhase.Transition ? Color.White : Color.Black;
            Draw.Rect(0f, 0f, Engine.ViewWidth, Engine.ViewHeight, bgColor);
            
            Draw.SpriteBatch.End();
            
            // Now call base render for HUD and other components
            base.Render();
            
            // Render vessel graphics during display phases
            if (vesselAlpha > 0f && (currentPhase == CreationPhase.VesselDisplay || currentPhase == CreationPhase.VesselDiscard))
            {
                renderVesselGraphics();
            }
            
            // Render choice menu if active
            if (currentChoices.Count > 0 && choiceEase > 0f)
            {
                renderChoiceMenu();
            }
            
            // Render fade overlay if transitioning
            if (backgroundFade < 1f && currentPhase == CreationPhase.Transition)
            {
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RasterizerState.CullNone, null, Engine.ScreenMatrix);
                Draw.Rect(0f, 0f, Engine.ViewWidth, Engine.ViewHeight, Color.White * (1f - backgroundFade));
                Draw.SpriteBatch.End();
            }
            
            // Render fade overlay for intro/outro fade
            if (fade > 0f || textAlpha > 0f)
            {
                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RasterizerState.CullNone, null, Engine.ScreenMatrix);
                if (fade > 0f)
                {
                    Draw.Rect(0f, 0f, Engine.ViewWidth, Engine.ViewHeight, Color.Black * fade);
                }
                Draw.SpriteBatch.End();
            }
        }

        private void renderVesselGraphics()
        {
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RasterizerState.CullNone, null, Engine.ScreenMatrix);
            
            // Render depth layer as full-screen background at full opacity
            if (depthTexture != null)
            {
                float depthScaleX = 1920f / depthTexture.Width;
                float depthScaleY = 1080f / depthTexture.Height;
                float depthScale = Math.Max(depthScaleX, depthScaleY);
                depthTexture.Draw(Vector2.Zero, Vector2.Zero, Color.White * vesselAlpha, depthScale);
            }
            
            // Render soul blur effects slightly enlarged (1.5x) but not screen-filling
            const float SOUL_BLUR_SCALE = 1.5f;
            if (soulBlurTexture != null)
            {
                Vector2 blurPos = vesselPosition - new Vector2(soulBlurTexture.Width * SOUL_BLUR_SCALE / 2f, soulBlurTexture.Height * SOUL_BLUR_SCALE / 2f);
                soulBlurTexture.Draw(blurPos, Vector2.Zero, Color.White * soulBlurAlpha, SOUL_BLUR_SCALE);
            }
            
            if (pinkSoulBlurTexture != null)
            {
                Vector2 pinkBlurPos = vesselPosition - new Vector2(pinkSoulBlurTexture.Width * SOUL_BLUR_SCALE / 2f, pinkSoulBlurTexture.Height * SOUL_BLUR_SCALE / 2f);
                pinkSoulBlurTexture.Draw(pinkBlurPos, Vector2.Zero, Color.White * (soulBlurAlpha * 0.6f), SOUL_BLUR_SCALE);
            }
            
            // Render vessel parts stacked: legs at bottom, body in center, head at top
            MTexture? bodyTex = (gonerBodyTextures.Length > 0) ? gonerBodyTextures[Math.Clamp(selectedBodyIndex, 0, gonerBodyTextures.Length - 1)] : null;
            MTexture? headTex = (gonerHeadTextures.Length > 0) ? gonerHeadTextures[Math.Clamp(selectedHeadIndex, 0, gonerHeadTextures.Length - 1)] : null;
            MTexture? legTex  = (gonerLegTextures.Length  > 0) ? gonerLegTextures [Math.Clamp(selectedLegIndex,  0, gonerLegTextures.Length  - 1)] : null;
            float bodyHalfH = bodyTex != null ? bodyTex.Height / 2f : 60f;
            
            // Legs (bottom)
            if (legTex != null)
            {
                Vector2 legPos = new Vector2(
                    vesselPosition.X - legTex.Width / 2f,
                    vesselPosition.Y + bodyHalfH);
                legTex.Draw(legPos, Vector2.Zero, Color.White * vesselAlpha);
            }
            
            // Body (center)
            if (bodyTex != null)
            {
                Vector2 bodyPos = vesselPosition - new Vector2(bodyTex.Width / 2f, bodyTex.Height / 2f);
                bodyTex.Draw(bodyPos, Vector2.Zero, Color.White * vesselAlpha);
            }
            
            // Head (top)
            if (headTex != null)
            {
                Vector2 headPos = new Vector2(
                    vesselPosition.X - headTex.Width / 2f,
                    vesselPosition.Y - bodyHalfH - headTex.Height);
                headTex.Draw(headPos, Vector2.Zero, Color.White * vesselAlpha);
            }
            
            Draw.SpriteBatch.End();
        }

        private void renderChoiceMenu()
        {
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, RasterizerState.CullNone, null, Engine.ScreenMatrix);
            
            Vector2 basePosition = new Vector2(Engine.Width / 2f, Engine.Height / 2f);
            float optionHeight = 60f;
            float totalHeight = currentChoices.Count * optionHeight;
            Vector2 startPosition = basePosition - new Vector2(0, totalHeight / 2f);
            
            for (int i = 0; i < currentChoices.Count; i++)
            {
                Vector2 position = startPosition + new Vector2(0, i * optionHeight);
                Color color = i == currentChoiceIndex ? Color.White : Color.Gray;
                color *= choiceEase;
                
                // Simple text rendering for choices
                ActiveFont.Draw(currentChoices[i], position, new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, color);
            }
            
            Draw.SpriteBatch.End();
        }
        #endregion

        #region Cleanup
        public override void End()
        {
            IngesteLogger.Info("[VesselCreation] VesselCreationVignette ending");
            StopSfx();
            // Ensure music is restored when vignette ends abruptly
            try
            {
                if (!string.IsNullOrEmpty(areaMusic))
                {
                    Audio.SetMusic(areaMusic);
                    IngesteLogger.Debug($"[VesselCreation] Restored music on end: {areaMusic}");
                }
            }
            catch (Exception ex)
            {
                IngesteLogger.Warn($"[VesselCreation] Failed to restore music on end: {ex.Message}");
            }
            base.End();
        }
        #endregion
    }
}



