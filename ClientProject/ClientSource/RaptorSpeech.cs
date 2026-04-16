namespace YAMJCS;

public class RaptorSpeech {
    private static GUIFrame root;
    private static GUIButton toggleButton;
    private static GUIFrame popupFrame;
    private static GUILayoutGroup wordList;

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

    public static void Initialize() {
        if (root != null) return;

        root = new GUIFrame(
            new RectTransform(new Vector2(0.12f, 0.18f), GUI.Canvas, Anchor.BottomCenter) {
                AbsoluteOffset = new Point(0, -110)
            },
            style: null);
            
        toggleButton = new GUIButton(
            new RectTransform(new Vector2(1.0f, 0.22f), root.RectTransform),
            text: "Words") {
            ToolTip = "Open known words"
        };
        
        toggleButton.OnClicked = (_, _) => {
            popupFrame.Visible = !popupFrame.Visible;
            if (popupFrame.Visible) {
                RebuildWordButtons();
            }
            return true;
        };

        popupFrame = new GUIFrame(
            new RectTransform(new Vector2(1.0f, 0.78f), root.RectTransform, Anchor.BottomCenter),
            style: "GUIFrame") {
            Visible = false
        };

        wordList = new GUILayoutGroup(
            new RectTransform(new Vector2(0.95f, 0.95f), popupFrame.RectTransform, Anchor.Center),
            isHorizontal: false,
            childAnchor: Anchor.TopCenter);
    }

    public static void RebuildWordButtons() {
        if (wordList == null) return;

        wordList.ClearChildren();

        foreach (Identifier tag in GetKnownWordTags()) {
            string text = TextManager.Get(tag).Value;

            var button = new GUIButton(
                new RectTransform(new Vector2(1.0f, 0.1f), wordList.RectTransform),
                text: text);

            button.UserData = tag;
            button.OnClicked = (btn, userData) => {
                Identifier wordTag = (Identifier)btn.UserData;
                
                // add word to string

                return true;
            };
        }
    }

    private static IEnumerable<Identifier> GetKnownWordTags() {
        Character character = Character.Controlled;
        if (character?.Info == null) {
            yield break;
        }

        HashSet<Identifier> talents = character.Info.UnlockedTalents;
        if (talents == null || talents.Count == 0) {
            yield break;
        }

        if (talents.Contains(BasicTalent)) {
            foreach (Identifier word in BasicWords) {
                yield return word;
            }
        }

        if (talents.Contains(AdvancedTalent)) {
            foreach (Identifier word in AdvancedWords) {
                yield return word;
            }
        }

        if (talents.Contains(CombatTalent)) {
            foreach (Identifier word in CombatWords) {
                yield return word;
            }
        }
    }
}