using Microsoft.Xna.Framework.Graphics;

namespace YAMJCS;

internal class YAMJClient {
    private static SpriteBatch? spriteBatch;
    
    internal static void ShowWarning(string text)
    {
        GUI.AddMessage(text, Color.Red, 5f);
    }
    
    internal static SpriteBatch? GetSpriteBatch() {
        if (spriteBatch is null) {
            var field = typeof(GameMain).GetField("spriteBatch", BindingFlags.NonPublic | BindingFlags.Static);
            spriteBatch = field?.GetValue(null) as SpriteBatch;
        }
        return spriteBatch;
    }
    
    
}