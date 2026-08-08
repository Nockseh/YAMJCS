using Microsoft.Xna.Framework.Input;

namespace YAMJCS;

public class RaptorHeadset {
    private const string ItemIdentifier = "raptorHeadset";
    //TODO: make configurable
    private static readonly Keys MenuKey = Keys.B;
    
    private const int Radius = 140;
    private static readonly Point ButtonSize = new Point(100, 40);
    
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
            new RectTransform(new Point(Radius * 2 + ButtonSize.X, Radius * 2 + ButtonSize.Y), GUI.Canvas, Anchor.Center),
            style: "GUIFrame") {
            Visible = false
        };

        new GUITextBlock(
            new RectTransform(new Vector2(1f, 0.14f), wheelFrame.RectTransform, Anchor.TopCenter),
            text: TextManager.Get("yamjUi.quickChat".ToIdentifier()).Value,
            textAlignment: Alignment.Center);

        for (int i = 0; i < MessageIds.Length; i++) {
            string text = TextManager.Get(MessageIds[i]).Value;
            float angle = -MathHelper.PiOver2 + i * (MathHelper.TwoPi / MessageIds.Length);
            Point offset = new Point(
                (int)MathF.Round(Radius * MathF.Cos(angle)),
                (int)MathF.Round(Radius * MathF.Sin(angle)));

            var button = new GUIButton(
                new RectTransform(ButtonSize, wheelFrame.RectTransform, Anchor.Center) { AbsoluteOffset = offset },
                text: text);

            button.OnClicked = (_, _) => {
                SendMessage(text);
                radialMenuOpen = false;
                return true;
            };
        }

        YAMJ.Log("Initialized raptor headset radial menu");
    }
    
    private static void SendMessage(string text) {
        ChatBox? chatBox = ChatBox.GetChatBox();
        if (chatBox is null) return;
        chatBox.InputBox.OnEnterPressed(chatBox.InputBox, text);
    }
}