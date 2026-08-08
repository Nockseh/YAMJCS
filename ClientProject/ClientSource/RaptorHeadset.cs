using Microsoft.Xna.Framework.Input;

namespace YAMJCS;

public class RaptorHeadset {
    private const string ItemIdentifier = "raptorHeadset";
    
    private const int Radius = 210;
    private static readonly Point ButtonSize = new Point(170, 40);
    //has to clear the widest button or their outer corners poke out of the backdrop
    private static readonly int BackdropRadius = Radius + ButtonSize.X / 2 + 10;
    private static readonly Color BackdropColor = new Color(0, 0, 0, 150);
    
    private static readonly Identifier[] MessageIds = [
        "yamjRadial.msg1".ToIdentifier(),
        "yamjRadial.msg2".ToIdentifier(),
        "yamjRadial.msg3".ToIdentifier(),
        "yamjRadial.msg4".ToIdentifier(),
        "yamjRadial.msg5".ToIdentifier(),
        "yamjRadial.msg6".ToIdentifier(),
        "yamjRadial.msg7".ToIdentifier(),
        "yamjRadial.msg8".ToIdentifier()
    ];
    
    private static GUIButton? toggleButton;
    private static GUIFrame? wheelFrame;
    private static bool radialMenuOpen;

    public static bool IsEligible(Character? character) {
        return character is not null && !character.Removed && !character.IsDead && character.HasEquippedItem(ItemIdentifier);
    }

    public static void Initialize() {
        if (toggleButton is not null) return;

        toggleButton = new GUIButton(
            new RectTransform(new Vector2(0.07f, 0.04f), GUI.Canvas, Anchor.BottomLeft) { RelativeOffset = new Vector2(0.22f, 0f) },
            text: TextManager.Get("yamjUi.quickChat".ToIdentifier()).Value) {
            ToolTip = TextManager.Get("yamjUi.quickChatTooltip".ToIdentifier()).Value,
            Visible = false
        };
        toggleButton.OnClicked = (_, _) => {
            radialMenuOpen = !radialMenuOpen;
            return true;
        };

        wheelFrame = new GUIFrame(
            new RectTransform(new Point(BackdropRadius * 2, BackdropRadius * 2), GUI.Canvas, Anchor.Center),
            style: null) {
            Visible = false,
            CanBeFocused = false
        };

        //circle backdrop
        new GUICustomComponent(
            new RectTransform(Vector2.One, wheelFrame.RectTransform, Anchor.Center),
            onDraw: (spriteBatch, component) => GUI.DrawDonutSection( //MUST USE onDraw instead of Draw(), Draw() would be called too early
                spriteBatch,
                component.Rect.Center.ToVector2(),
                new Range<float>(0f, BackdropRadius),
                MathHelper.TwoPi,
                BackdropColor)
            )
            { CanBeFocused = false };
        
        //ui label
        new GUITextBlock(
            new RectTransform(ButtonSize, wheelFrame.RectTransform, Anchor.Center),
            text: TextManager.Get("yamjUi.quickChat".ToIdentifier()).Value,
            textAlignment: Alignment.Center,
            style: null
            )
            { CanBeFocused = false };
        

        for (int i = 0; i < MessageIds.Length; i++) {
            //arrange in a circle
            string text = TextManager.Get(MessageIds[i]).Value;
            float angle = -MathHelper.PiOver2 + i * (MathHelper.TwoPi / MessageIds.Length);
            Point offset = new Point(
                (int)MathF.Round(Radius * MathF.Cos(angle)),
                (int)MathF.Round(Radius * MathF.Sin(angle)));

            var button = new GUIButton(
                new RectTransform(ButtonSize, wheelFrame.RectTransform, Anchor.Center) { AbsoluteOffset = offset },
                text: text);
            button.TextBlock.AutoScaleHorizontal = true;

            button.OnClicked = (_, _) => {
                SendMessage(text);
                radialMenuOpen = false;
                return true;
            };
        }

        YAMJ.Log("Initialized raptor headset radial menu");
    }
    
    public static void Update() {
        if (toggleButton is null || wheelFrame is null) return;
        Character? controlled = Character.Controlled;
        bool eligible = IsEligible(controlled);

        if (!eligible) {
            radialMenuOpen = false;
        } else {
            if (YAMJ.QuickChatKey.IsHit()) radialMenuOpen = !radialMenuOpen;
            if (radialMenuOpen && PlayerInput.KeyHit(Keys.Escape)) radialMenuOpen = false;
        }

        toggleButton.Visible = eligible;
        wheelFrame.Visible = radialMenuOpen;

        if (eligible) toggleButton.AddToGUIUpdateList();
        if (radialMenuOpen) wheelFrame.AddToGUIUpdateList();
    }
    
    private static void SendMessage(string text) {
        ChatBox? chatBox = ChatBox.GetChatBox();
        if (chatBox is null) return;
        chatBox.InputBox.OnEnterPressed(chatBox.InputBox, text);
    }
}