namespace YAMJCS;

public class RaptorSpeech {
    private static GUIFrame? root;
    private static GUIButton? toggleButton;
    private static GUIFrame? popupFrame;
    private static GUILayoutGroup? basicListLayout;
    private static GUILayoutGroup? advListLayout;
    private static GUILayoutGroup? combatListLayout;
    private static GUIDragHandle? dragHandle; //TODO add back in later

    private static readonly Identifier BasicTalent = "YAMJSpeechBasic".ToIdentifier();
    private static readonly Identifier AdvancedTalent = "YAMJSpeechAdvanced".ToIdentifier();
    private static readonly Identifier CombatTalent = "YAMJSpeechCombat".ToIdentifier();

    private static readonly Identifier[] BasicWords = [
        "raptorWord.me".ToIdentifier(),
        "raptorWord.you".ToIdentifier(),
        "raptorWord.thing".ToIdentifier(),
        "raptorWord.give".ToIdentifier(),
        "raptorWord.bite".ToIdentifier(),
        "raptorWord.eat".ToIdentifier(),
        "raptorWord.yes".ToIdentifier(),
        "raptorWord.no".ToIdentifier(),
        "raptorWord.help".ToIdentifier(),
        "raptorWord.questionMark".ToIdentifier()
    ];
    private static readonly Identifier[] AdvancedWords = [
        "raptorWord.i".ToIdentifier(),
        "raptorWord.has".ToIdentifier(),
        "raptorWord.that".ToIdentifier(),
        "raptorWord.this".ToIdentifier(),
        "raptorWord.water".ToIdentifier(),
        "raptorWord.inside".ToIdentifier(),
        "raptorWord.outside".ToIdentifier(),
        "raptorWord.here".ToIdentifier(),
        "raptorWord.boat".ToIdentifier(),
        "raptorWord.make".ToIdentifier(),
        "raptorWord.fix".ToIdentifier(),
        "raptorWord.move".ToIdentifier(),
        "raptorWord.need".ToIdentifier(),
        "raptorWord.bad".ToIdentifier(),
        "raptorWord.good".ToIdentifier()
    ];
    private static readonly Identifier[] CombatWords =  [
        "raptorWord.kill".ToIdentifier(),
        "raptorWord.shoot".ToIdentifier(),
        "raptorWord.enemy".ToIdentifier(),
        "raptorWord.dead".ToIdentifier(),
        "raptorWord.blood".ToIdentifier(),
        "raptorWord.gun".ToIdentifier(),
        "raptorWord.ammo".ToIdentifier(),
        "raptorWord.go".ToIdentifier(),
        "raptorWord.come".ToIdentifier(),
        "raptorWord.hurt".ToIdentifier(),
        "raptorWord.exclamation".ToIdentifier()
    ];
    
    private static bool built = false;
    private static string composedString = "";
    //private static IEnumerable? wordCache;

    public static void Initialize() {
        if (!YAMJ.IsPlayerRaptor(Character.Controlled)) return;
        if (root is not null) return;
        
        //Root
        root = new GUIFrame(
            new RectTransform(new Vector2(0.53f, 0.41f), GUI.Canvas, Anchor.BottomRight) { RelativeOffset = new Vector2(0.22f, 0f) },
            style: null) {
            CanBeFocused = false
        };
            
        //ToggleButton
        toggleButton = new GUIButton(
            new RectTransform(new Vector2(0.07f, 0.04f), root.RectTransform, Anchor.BottomRight),
            text: "Speak") {
            ToolTip = "show known words" //TODO localize
        };
        toggleButton.OnClicked = (_, _) => {
            popupFrame.Visible = !popupFrame.Visible;
            if (popupFrame.Visible) {
                RebuildWordButtons();
            }
            else {
                //send composed message
                ChatBox? chatBox = ChatBox.GetChatBox();
                if (chatBox is not null) {
                    chatBox.InputBox.OnEnterPressed(chatBox.InputBox, composedString);
                }
                composedString = "";
            }
            return true;
        };
        
        //Word tray
        popupFrame = new GUIFrame(
            new RectTransform(new Vector2(0.4554f, 0.8122f), root.RectTransform, Anchor.TopCenter),
            style: "GUIFrame") {
            Visible = false
        };
        basicListLayout = new GUILayoutGroup(
            new RectTransform(new Vector2(1f/3f, 1f), popupFrame.RectTransform, Anchor.BottomLeft),
            isHorizontal: false,
            childAnchor: Anchor.BottomCenter
        );
        advListLayout = new GUILayoutGroup(
            new RectTransform(new Vector2(1f/3f, 1f), popupFrame.RectTransform, Anchor.BottomCenter),
            isHorizontal: false,
            childAnchor: Anchor.BottomCenter
        );
        combatListLayout = new GUILayoutGroup(
            new RectTransform(new Vector2(1f/3f, 1f), popupFrame.RectTransform, Anchor.BottomRight),
            isHorizontal: false,
            childAnchor: Anchor.BottomCenter
        );

        YAMJ.Log("Intialized raptor speech hud");
    }

    public static void Update() {
        if (root is null) return;
        Character? controlled = Character.Controlled;
        bool enabled = controlled is not null && YAMJ.IsPlayerRaptor(controlled);

        root.Visible = enabled;

        if (enabled) {
            if (!built) {
                RebuildWordButtons();
                built = true;
            }
            root.AddToGUIUpdateList();
        }
        else {
            built = false;
        }
    }
    
    public static void RebuildWordButtons() {
        if (combatListLayout is null) return; //checking for initialized

        basicListLayout.ClearChildren();
        advListLayout.ClearChildren();
        combatListLayout.ClearChildren();
        
        foreach (Identifier[] wordTable in GetKnownWordTags()) {
            GUILayoutGroup targetLayout;

            if (ReferenceEquals(wordTable, BasicWords)) {
                targetLayout = basicListLayout;
            } else if (ReferenceEquals(wordTable, AdvancedWords)) {
                targetLayout = advListLayout;
            } else if (ReferenceEquals(wordTable, CombatWords)) {
                targetLayout = combatListLayout;
            } else { continue; }
            
            foreach (Identifier word in wordTable) {
                string text = TextManager.Get(word).Value;
                var button = new GUIButton(
                    new RectTransform(new Vector2(1f, 1f/(float)wordTable.Length), targetLayout.RectTransform),
                    text: text
                );
                
                button.UserData = text;
                button.OnClicked = (btn, userData) => {
                    composedString += text + " ";
                    YAMJ.Log(composedString);

                    return true;
                };
            }
        }
    }

    private static IEnumerable<Identifier[]> GetKnownWordTags() {
        Character character = Character.Controlled;
        if (character?.Info == null) {
            yield break;
        }

        HashSet<Identifier> talents = character.Info.UnlockedTalents;
        if (talents == null || talents.Count == 0) {
            yield break;
        }

        if (talents.Contains(BasicTalent)) {
            yield return BasicWords;
        }

        if (talents.Contains(AdvancedTalent)) {
            yield return AdvancedWords;
        }

        if (talents.Contains(CombatTalent)) {
            yield return CombatWords;
        }
    }
}